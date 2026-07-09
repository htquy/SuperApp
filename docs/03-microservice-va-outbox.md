# BƯỚC 3: XÂY DỰNG NGHIỆP VỤ LÕI VÀ TRANSACTIONAL OUTBOX PATTERN

Phần này triển khai cấu trúc nghiệp vụ của phân hệ Nhân sự (HRM) nằm trong dự án `sh-backend-core` (Port 6000), áp dụng mô hình kiến trúc sạch, tách biệt luồng Đọc/Ghi (CQRS) và đồng bộ dữ liệu an toàn qua Outbox Pattern.

## 1. Cơ cấu Kiến trúc Dữ liệu (CQRS)
- **Đường Ghi (Command)**: Sử dụng Entity Framework Core để quản lý các thay đổi trạng thái dữ liệu phức tạp đảm bảo tính toàn vẹn.
- **Đường Đọc (Query)**: Sử dụng thư viện **Dapper** kết hợp với Raw SQL nhằm truy vấn trực tiếp dữ liệu từ Database lên giao diện mà không đi qua các tầng bọc nặng nề, tối ưu CPU và tốc độ I/O tối đa.

## 2. Đảm bảo dữ liệu với Outbox Pattern
Để tránh lỗi hệ thống (Ví dụ: thông tin nhân viên đã lưu vào DB thành công nhưng mạng sập khiến sự kiện gửi sang RabbitMQ bị mất), hệ thống áp dụng Transactional Outbox Pattern: Dữ liệu nghiệp vụ nhân viên và dữ liệu sự kiện Event phải được ghi vào DB trong cùng một Database Transaction duy nhất.

## Yêu cầu triển khai (Prompt dành cho AI):
Hãy viết code cho phân hệ HRM nằm trong dự án `sh-backend-core`:
1. **Thiết lập Database Contex (EF Core)**:
   - Bảng `Employees` (Id, Name, Email, Department).
   - Bảng `OutboxMessages` (Id, EventType, Content, CreatedAt, ProcessedAt).
2. **Endpoint `GET /api/hrm/employees` (Đường Đọc)**:
   - Sử dụng **Dapper** thực hiện câu lệnh `SELECT * FROM Employees WHERE Active = 1...` để trả dữ liệu nhanh về cho BFF.
3. **Endpoint `POST /api/hrm/employees` (Đường Ghi + Outbox)**:
   - Mở một `IDbContextTransaction`.
   - Lưu thực thể nhân viên mới vào bảng `Employees`.
   - Tạo một sự kiện `EmployeeCreatedEvent` (chứa thông tin nhân viên) định dạng thành chuỗi JSON, lưu vào bảng `OutboxMessages` với trạng thái `ProcessedAt = NULL`.
   - Thực hiện `dbContext.SaveChanges()` và `transaction.Commit()`.
4. **Outbox Background Worker (Sử dụng MassTransit)**:
   - Viết một `BackgroundService` chạy ngầm, cứ mỗi 5 giây quét bảng `OutboxMessages` tìm các dòng có `ProcessedAt IS NULL`.
   - Sử dụng `IPublishEndpoint` của MassTransit để bắn sự kiện này vào **RabbitMQ Broker**.
   - Bắn tin thành công thì cập nhật ngày giờ vào cột `ProcessedAt` để không bắn lại.
5. **Log Hệ Thống**:
   - `[Backend-HRM] Nhận request lấy dữ liệu. Header Authorization hiện tại là: Bearer [Token]. Đang xử lý truy vấn bằng Dapper...`
   - `[Outbox Worker] Phát hiện tin nhắn Outbox chưa xử lý. Tiến hành đẩy sự kiện lên RabbitMQ và đóng gói Transaction.`