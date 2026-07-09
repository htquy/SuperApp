# Chi Tiết Bước 3: Nghiệp vụ Lõi (HRM) và Transactional Outbox Pattern (sh-backend-core)

Tài liệu này giải thích chi tiết thiết lập kiến trúc **CQRS** (phân tách Đọc/Ghi) và mô hình đồng bộ sự kiện tin cậy **Transactional Outbox Pattern** trong dự án backend lõi `sh-backend-core`.

---

## 1. Mục tiêu
1. **Thay đổi cổng ứng dụng:** Đổi cổng dịch vụ `sh-backend-core` sang port **6000** để đồng bộ với cấu hình chuyển tiếp của BFF Gateway.
2. **Cơ cấu dữ liệu CQRS:**
   - **Đường Đọc (Read Path):** Sử dụng **Dapper** truy vấn nhanh qua SQL thô để lấy danh sách nhân viên từ bảng `Employees` có trạng thái `Active = 1`.
   - **Đường Ghi (Write Path):** Sử dụng **Entity Framework Core** để mở một transaction, thêm mới nhân viên vào bảng `Employees`, đồng thời sinh ra sự kiện `EmployeeCreatedEvent` dạng JSON lưu vào bảng `OutboxMessages` (ProcessedAt = NULL) trong cùng một transaction.
3. **Outbox Worker (Background Service):** Thiết lập một dịch vụ nền chạy ngầm mỗi 5 giây quét các tin nhắn outbox chưa xử lý, đẩy sự kiện lên **RabbitMQ Broker** qua MassTransit, và cập nhật ngày giờ xử lý (`ProcessedAt`) khi đẩy tin thành công.
4. **Log Hệ thống:** Log chi tiết ra terminal khi đọc dữ liệu nghiệp vụ và khi worker quét đẩy tin nhắn lên RabbitMQ.

---

## 2. Chi tiết các tệp thay đổi và thêm mới

### 2.1. Tệp [sh-backend-core.csproj](file:///home/nobugnolife/Source/SuperApp/BE/sh-backend-core/sh-backend-core.csproj)
Cài đặt các thư viện cần thiết:
```xml
<PackageReference Include="Microsoft.EntityFrameworkCore.SqlServer" Version="10.0.0-preview.1.25080.9" />
<PackageReference Include="Microsoft.EntityFrameworkCore.Design" Version="10.0.0-preview.1.25080.9" />
<PackageReference Include="Dapper" Version="2.1.35" />
<PackageReference Include="MassTransit.RabbitMQ" Version="9.1.2" />
```
* **Giải thích:** Cung cấp đầy đủ tính năng kết nối SQL Server (EF Core), truy vấn nhanh hiệu năng cao (Dapper) và truyền thông điệp bất đồng bộ (MassTransit qua RabbitMQ).

### 2.2. Tệp [Properties/launchSettings.json](file:///home/nobugnolife/Source/SuperApp/BE/sh-backend-core/Properties/launchSettings.json)
Thay đổi cấu hình cổng chạy mặc định của ứng dụng sang `6000` (HTTP) và `6001` (HTTPS).

### 2.3. Tệp [appsettings.json](file:///home/nobugnolife/Source/SuperApp/BE/sh-backend-core/appsettings.json)
Bổ sung cấu hình kết nối CSDL SQL Server và Broker RabbitMQ:
```json
"ConnectionStrings": {
  "DefaultConnection": "Server=localhost,1433;Database=SuperAppDb;User Id=sa;Password=Dev@SuperApp123!;TrustServerCertificate=True",
  "RabbitMQ": "localhost"
}
```

### 2.4. Tệp mới [Entities.cs](file:///home/nobugnolife/Source/SuperApp/BE/sh-backend-core/Entities.cs)
Định nghĩa cấu trúc các bảng dữ liệu:
- `Employee`: Chứa thông tin nhân viên (`Id`, `Name`, `Email`, `Department`, `Active`).
- `OutboxMessage`: Lưu trữ thông tin sự kiện chờ xử lý (`Id`, `EventType`, `Content` lưu JSON, `CreatedAt`, `ProcessedAt` đánh dấu ngày giờ xử lý).
- `EmployeeCreatedEvent`: Record mô tả cấu trúc dữ liệu sự kiện gửi đi.

### 2.5. Tệp mới [HrmDbContext.cs](file:///home/nobugnolife/Source/SuperApp/BE/sh-backend-core/HrmDbContext.cs)
Lớp Context ánh xạ các Entity EF Core sang các bảng tương ứng trong CSDL và thiết lập thuộc tính ràng buộc cho các cột.

### 2.6. Tệp mới [OutboxBackgroundWorker.cs](file:///home/nobugnolife/Source/SuperApp/BE/sh-backend-core/OutboxBackgroundWorker.cs)
Dịch vụ chạy ngầm kế thừa từ `BackgroundService`:
- Cứ sau mỗi 5 giây quét bảng `OutboxMessages` tìm các tin nhắn có `ProcessedAt == null`.
- Ghi log ra Terminal: `[Outbox Worker] Phát hiện tin nhắn Outbox chưa xử lý. Tiến hành đẩy sự kiện lên RabbitMQ và đóng gói Transaction.`
- Deserialize nội dung JSON và sử dụng `IPublishEndpoint` của MassTransit để publish sự kiện lên RabbitMQ.
- Cập nhật trường `ProcessedAt = DateTime.UtcNow` khi thành công và lưu lại vào database.

### 2.7. Tệp [Program.cs](file:///home/nobugnolife/Source/SuperApp/BE/sh-backend-core/Program.cs)
Cấu hình tích hợp hệ thống và xây dựng các API Endpoints:

#### A. Cấu hình dịch vụ & Đảm bảo khởi tạo database:
```csharp
// Đăng ký SQL Server & DbContext
builder.Services.AddDbContext<HrmDbContext>(...);

// Đăng ký MassTransit + RabbitMQ
builder.Services.AddMassTransit(...);

// Đăng ký Hosted Service cho Background Worker
builder.Services.AddHostedService<OutboxBackgroundWorker>();

// Tự động kiểm tra và tạo database cùng các bảng nếu chưa tồn tại ở startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<HrmDbContext>();
    db.Database.EnsureCreated();
}
```

#### B. API Lấy danh sách nhân viên (`GET /api/hrm/employees`):
- Sử dụng **Dapper** kết nối qua `SqlConnection` để truy vấn SQL thô: `SELECT * FROM Employees WHERE Active = 1`.
- Đọc header `Authorization` truyền từ BFF Gateway và log ra Terminal:
  ```text
  [Backend-HRM] Nhận request lấy dữ liệu. Header Authorization hiện tại là: Bearer [Token]. Đang xử lý truy vấn bằng Dapper...
  ```

#### C. API Thêm mới nhân viên và Ghi Outbox (`POST /api/hrm/employees`):
- Mở một Transaction bằng EF Core (`BeginTransactionAsync`).
- Lưu thực thể nhân viên mới vào bảng `Employees`.
- Tạo một sự kiện `EmployeeCreatedEvent`, serialize sang chuỗi JSON và lưu vào bảng `OutboxMessages` với `ProcessedAt = null`.
- Commit Transaction bảo toàn tính toàn vẹn (cả nhân viên và outbox message cùng được ghi thành công, hoặc rollback nếu một trong hai thất bại).
