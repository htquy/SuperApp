using StackExchange.Redis;
using System.Text.Json.Nodes;

var builder = WebApplication.CreateBuilder(args);

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("http://localhost:3000", "http://localhost:3001")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// Add HTTP Client
builder.Services.AddHttpClient();

// Add Redis
builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
{
    var configuration = sp.GetRequiredService<IConfiguration>();
    var connectionString = configuration.GetConnectionString("Redis") ?? "localhost:6379";
    return ConnectionMultiplexer.Connect(connectionString);
});

// Add YARP Reverse Proxy
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

var app = builder.Build();

app.UseCors();

// Intercept Login Route
app.MapPost("/api/auth/login", async (
    HttpContext context,
    IHttpClientFactory httpClientFactory,
    IConnectionMultiplexer redisMultiplexer,
    ILogger<Program> logger) =>
{
    var client = httpClientFactory.CreateClient();
    
    // Get backend destination address from config
    var backendUrl = app.Configuration["ReverseProxy:Clusters:auth-cluster:Destinations:destination1:Address"] ?? "http://localhost:6000";
    var loginUrl = $"{backendUrl.TrimEnd('/')}/api/auth/login";
    
    // Create downstream request message
    var requestMessage = new HttpRequestMessage(HttpMethod.Post, loginUrl);
    
    // Copy body
    if (context.Request.ContentLength > 0 || context.Request.Headers.ContainsKey("Transfer-Encoding"))
    {
        requestMessage.Content = new StreamContent(context.Request.Body);
        if (context.Request.ContentType != null)
        {
            requestMessage.Content.Headers.ContentType = System.Net.Http.Headers.MediaTypeHeaderValue.Parse(context.Request.ContentType);
        }
    }
    
    // Copy request headers
    foreach (var header in context.Request.Headers)
    {
        if (header.Key.Equals("Host", StringComparison.OrdinalIgnoreCase)) continue;
        requestMessage.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
    }
    
    // Send request to backend
    var responseMessage = await client.SendAsync(requestMessage);
    
    if (responseMessage.IsSuccessStatusCode)
    {
        var responseContent = await responseMessage.Content.ReadAsStringAsync();
        try
        {
            var jsonNode = JsonNode.Parse(responseContent);
            if (jsonNode != null)
            {
                var accessToken = jsonNode["access_token"]?.ToString() ?? jsonNode["accessToken"]?.ToString();
                var refreshToken = jsonNode["refresh_token"]?.ToString() ?? jsonNode["refreshToken"]?.ToString();
                
                if (!string.IsNullOrEmpty(accessToken))
                {
                    // Generate SessionId
                    var sessionId = Guid.NewGuid().ToString();
                    
                    // Save tokens to Redis DB 4
                    var db = redisMultiplexer.GetDatabase(4);
                    var sessionData = new JsonObject
                    {
                        ["access_token"] = accessToken,
                        ["refresh_token"] = refreshToken
                    };
                    await db.StringSetAsync($"bff:session:{sessionId}", sessionData.ToString());
                    
                    // Generate XSRF-TOKEN
                    var xsrfToken = Guid.NewGuid().ToString("N");
                    
                    // Cookie 1: __Host-bff (HttpOnly)
                    context.Response.Cookies.Append("__Host-bff", sessionId, new CookieOptions
                    {
                        HttpOnly = true,
                        SameSite = SameSiteMode.Strict,
                        Secure = true,
                        Path = "/"
                    });
                    
                    // Cookie 2: XSRF-TOKEN (HttpOnly = false)
                    context.Response.Cookies.Append("XSRF-TOKEN", xsrfToken, new CookieOptions
                    {
                        HttpOnly = false,
                        SameSite = SameSiteMode.Strict,
                        Secure = true,
                        Path = "/"
                    });
                    
                    // Required system console log output
                    Console.WriteLine($"[BFF Gateway] Đã intercept token. Lưu vào Redis với mã Session {sessionId}. Trả HttpOnly Cookie về trình duyệt.");
                    
                    // Strip the tokens from the JSON response before returning it to the client to prevent XSS
                    var strippedNode = JsonNode.Parse(responseContent) as JsonObject;
                    if (strippedNode != null)
                    {
                        strippedNode.Remove("access_token");
                        strippedNode.Remove("accessToken");
                        strippedNode.Remove("refresh_token");
                        strippedNode.Remove("refreshToken");
                        
                        context.Response.StatusCode = (int)responseMessage.StatusCode;
                        context.Response.ContentType = "application/json";
                        await context.Response.WriteAsync(strippedNode.ToString());
                        return;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing login tokens or saving to Redis.");
        }
    }
    
    // Default fallback: copy response status, headers, and body back to client
    context.Response.StatusCode = (int)responseMessage.StatusCode;
    foreach (var header in responseMessage.Headers)
    {
        context.Response.Headers[header.Key] = header.Value.ToArray();
    }
    foreach (var header in responseMessage.Content.Headers)
    {
        context.Response.Headers[header.Key] = header.Value.ToArray();
    }
    await responseMessage.Content.CopyToAsync(context.Response.Body);
});

// Custom Middleware for business APIs (/api/hrm/**)
app.Use(async (context, next) =>
{
    var path = context.Request.Path.Value ?? "";
    if (path.StartsWith("/api/hrm/", StringComparison.OrdinalIgnoreCase) || 
        path.Equals("/api/hrm", StringComparison.OrdinalIgnoreCase))
    {
        // 1. CSRF validation for data mutation methods
        var method = context.Request.Method;
        if (HttpMethods.IsPost(method) || HttpMethods.IsPut(method) || HttpMethods.IsDelete(method))
        {
            var csrfHeader = context.Request.Headers["X-XSRF-TOKEN"].ToString();
            var csrfCookie = context.Request.Cookies["XSRF-TOKEN"];
            
            if (string.IsNullOrEmpty(csrfHeader) || string.IsNullOrEmpty(csrfCookie) || csrfHeader != csrfCookie)
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                context.Response.ContentType = "text/plain";
                await context.Response.WriteAsync("CSRF validation failed: X-XSRF-TOKEN header must match XSRF-TOKEN cookie.");
                return;
            }
        }
        
        // 2. Read SessionId from __Host-bff Cookie
        if (!context.Request.Cookies.TryGetValue("__Host-bff", out var sessionId) || string.IsNullOrEmpty(sessionId))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = "text/plain";
            await context.Response.WriteAsync("Unauthorized: Session cookie is missing.");
            return;
        }
        
        // 3. Look up token in Redis DB 4
        var redisMultiplexer = context.RequestServices.GetRequiredService<IConnectionMultiplexer>();
        var db = redisMultiplexer.GetDatabase(4);
        var sessionDataStr = await db.StringGetAsync($"bff:session:{sessionId}");
        
        if (sessionDataStr.IsNullOrEmpty)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = "text/plain";
            await context.Response.WriteAsync("Unauthorized: Invalid session or session expired.");
            return;
        }
        
        try
        {
            var sessionData = JsonNode.Parse(sessionDataStr.ToString()) as JsonObject;
            var accessToken = sessionData?["access_token"]?.ToString() ?? sessionData?["accessToken"]?.ToString();
            
            if (string.IsNullOrEmpty(accessToken))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.ContentType = "text/plain";
                await context.Response.WriteAsync("Unauthorized: Access token not found in session.");
                return;
            }
            
            // 4. Inject Authorization: Bearer JWT
            context.Request.Headers["Authorization"] = $"Bearer {accessToken}";
            
            // 5. Required system console log output
            Console.WriteLine("[BFF Gateway] Nhận request nghiệp vụ. Đọc SessionId từ Cookie, map thành công sang Bearer JWT từ Redis và chuyển tiếp qua YARP.");
        }
        catch
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = "text/plain";
            await context.Response.WriteAsync("Unauthorized: Error reading session data.");
            return;
        }
    }
    
    await next();
});

app.MapReverseProxy();

app.Run();
