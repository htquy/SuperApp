# Chi Tiết Bước 3: Xử lý Luồng Gọi API Nghiệp Vụ (Cookie-to-Bearer Proxy Transform)

Tài liệu này giải thích chi tiết các thay đổi cấu hình và logic cần thiết để thiết lập luồng **Cookie-to-Bearer Proxy Transform** cho dự án `sh-bff-gateway`, đáp ứng quy tắc bảo mật chống tấn công CSRF và tự động khôi phục JWT để gửi xuống Backend Core.

---

## 1. Mục tiêu
Thiết lập một Middleware toàn cục trong BFF Gateway để chặn và xử lý toàn bộ các request gửi tới `/api/hrm/**`:
1. **Chống tấn công chéo CSRF (Cross-Site Request Forgery):**
   - Chỉ áp dụng đối với các phương thức biến đổi dữ liệu (POST, PUT, DELETE).
   - Kiểm tra xem Request Header `X-XSRF-TOKEN` gửi lên có khớp hoàn toàn với giá trị lưu trong Cookie `XSRF-TOKEN` hay không.
   - Nếu không khớp hoặc thiếu, trả về ngay lập tức mã lỗi `403 Forbidden` để ngăn chặn hành vi giả mạo request.
2. **Khôi phục token gốc từ Redis (Cookie-to-Bearer):**
   - Đọc Cookie `__Host-bff` để lấy ra `SessionId`. Nếu không có, trả về `401 Unauthorized`.
   - Sử dụng `SessionId` để truy vấn vào Redis Cache (DB 4) nhằm lấy ra chuỗi JSON chứa các token của phiên đó.
   - Nếu phiên không hợp lệ hoặc đã hết hạn, trả về `401 Unauthorized`.
   - Trích xuất `access_token` từ dữ liệu Redis.
3. **Tiêm Header Authorization (Bearer Injection):**
   - Chèn tiêu đề HTTP `Authorization: Bearer [JWT]` vào Request Header hiện tại.
   - Ghi nhận nhật ký (Log) ra terminal theo định dạng yêu cầu.
   - Gọi `await next()` để bàn giao cho YARP tiếp tục chuyển tiếp request an toàn xuống Backend Core (`http://localhost:6000`).

---

## 2. Chi tiết các tệp thay đổi

### 2.1. Tệp [Program.cs](file:///home/nobugnolife/Source/SuperApp/BE/sh-bff-gateway/Program.cs)
Chúng ta đã thêm Middleware đăng ký bằng phương thức `app.Use(...)` đặt trước middleware YARP `app.MapReverseProxy()` để tiền xử lý mọi request nghiệp vụ:

```csharp
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
            // Ép kiểu tường minh .ToString() để tránh lỗi nhập nhằng (ambiguous compile error) của RedisValue
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
```

* **Giải thích chi tiết:**
  * Việc đặt middleware này trước `app.MapReverseProxy()` đảm bảo rằng các chỉnh sửa lên `context.Request.Headers` (cụ thể là `Authorization`) sẽ được YARP đọc trực tiếp khi tạo request chuyển tiếp xuống backend.
  * Vì `RedisValue` của StackExchange.Redis có thể ngầm định đổi sang cả `string` lẫn `byte[]`, chúng ta sử dụng `sessionDataStr.ToString()` trước khi truyền vào `JsonNode.Parse` để tránh lỗi biên dịch phân vân overload (CS0121).
  * Log hệ thống được viết ra bằng lệnh `Console.WriteLine` đúng định dạng chuỗi yêu cầu để hiển thị ra màn hình Terminal khi proxy chuyển tiếp thành công.
