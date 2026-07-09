using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using Dapper;
using MassTransit;
using System.Text.Json;
using sh_backend_core;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddOpenApi();

// Register SQL Server HrmDbContext
builder.Services.AddDbContext<HrmDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
                           ?? "Server=localhost,1433;Database=SuperAppDb;User Id=sa;Password=Dev@SuperApp123!;TrustServerCertificate=True";
    options.UseSqlServer(connectionString);
});

// Register MassTransit with RabbitMQ
builder.Services.AddMassTransit(x =>
{
    x.UsingRabbitMq((context, cfg) =>
    {
        var rabbitHost = builder.Configuration.GetConnectionString("RabbitMQ") ?? "localhost";
        cfg.Host(rabbitHost);
    });
});

// Register the Outbox Background Worker
builder.Services.AddHostedService<OutboxBackgroundWorker>();

var app = builder.Build();

// Ensure Database is created on startup (EF context auto-initialization)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<HrmDbContext>();
    db.Database.EnsureCreated();
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

// GET /api/hrm/employees (Read Path via Dapper)
app.MapGet("/api/hrm/employees", async (HttpContext context, IConfiguration configuration) =>
{
    var authHeader = context.Request.Headers["Authorization"].ToString();
    if (string.IsNullOrEmpty(authHeader))
    {
        authHeader = "[None]";
    }

    // Required console log output
    Console.WriteLine($"[Backend-HRM] Nhận request lấy dữ liệu. Header Authorization hiện tại là: {authHeader}. Đang xử lý truy vấn bằng Dapper...");

    var connectionString = configuration.GetConnectionString("DefaultConnection") 
                           ?? "Server=localhost,1433;Database=SuperAppDb;User Id=sa;Password=Dev@SuperApp123!;TrustServerCertificate=True";

    using var connection = new SqlConnection(connectionString);
    var employees = await connection.QueryAsync<Employee>("SELECT * FROM Employees WHERE Active = 1");
    
    return Results.Ok(employees);
});

// POST /api/hrm/employees (Write Path via EF Core + Transactional Outbox)
app.MapPost("/api/hrm/employees", async (HrmDbContext dbContext, EmployeeDto employeeDto) =>
{
    if (string.IsNullOrEmpty(employeeDto.Name) || string.IsNullOrEmpty(employeeDto.Email))
    {
        return Results.BadRequest("Name and Email are required.");
    }

    // Open transaction
    using var transaction = await dbContext.Database.BeginTransactionAsync();

    try
    {
        // 1. Save new employee in the Employees table
        var employee = new Employee
        {
            Name = employeeDto.Name,
            Email = employeeDto.Email,
            Department = employeeDto.Department ?? string.Empty,
            Active = true
        };
        dbContext.Employees.Add(employee);
        await dbContext.SaveChangesAsync();

        // 2. Create Event and serialize to JSON
        var eventData = new EmployeeCreatedEvent(employee.Id, employee.Name, employee.Email, employee.Department);
        var eventJson = JsonSerializer.Serialize(eventData);

        // 3. Save event to OutboxMessages table
        var outboxMessage = new OutboxMessage
        {
            EventType = nameof(EmployeeCreatedEvent),
            Content = eventJson,
            CreatedAt = DateTime.UtcNow,
            ProcessedAt = null
        };
        dbContext.OutboxMessages.Add(outboxMessage);
        await dbContext.SaveChangesAsync();

        // Commit transaction
        await transaction.CommitAsync();

        return Results.Created($"/api/hrm/employees/{employee.Id}", employee);
    }
    catch (Exception ex)
    {
        await transaction.RollbackAsync();
        return Results.Problem($"An error occurred while saving the employee: {ex.Message}");
    }
});

app.Run();

// DTO for incoming employee data
public record EmployeeDto(string Name, string Email, string? Department);
