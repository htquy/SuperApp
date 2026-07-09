# BƯỚC 1: KHỞI TẠO HẠ TẦNG DOCKER & AUTH PHÂN HỆ LÕI

Theo đúng tài liệu thiết kế  SAD v3.0, hệ thống sử dụng một Identity Provider để quản lý tài khoản tập trung và cấp phát mã JWT token chứa các thông tin định danh và phân quyền (Claims).

## 1. Cấu hình Hạ tầng (Docker Compose)
Tạo file `docker-compose.yml` tại thư mục gốc để giả lập môi trường Production:
- **Redis**: Đóng vai trò là `RedisTicketStore` để BFF lưu trữ và map thông tin Session của người dùng một cách an toàn.
- **RabbitMQ**: Message Broker để xử lý đồng bộ dữ liệu bất đồng bộ qua mô hình Event-Driven.
- **SQL Server**: Lưu trữ Database thực tế cho cả phân hệ xác thực lẫn nghiệp vụ.

```yaml
version: '3.9'
services:
  sqlserver:
    image: [mcr.microsoft.com/mssql/server:2022-latest](https://mcr.microsoft.com/mssql/server:2022-latest)
    environment:
      ACCEPT_EULA: "Y"
      SA_PASSWORD: "Dev@SuperApp123!"
    ports:
      - "1433:1433"

  redis:
    image: redis:7-alpine
    ports:
      - "6379:6379"

  rabbitmq:
    image: rabbitmq:3-management-alpine
    ports:
      - "5672:5672"
      - "15672:15672"
    environment:
      RABBITMQ_DEFAULT_USER: admin
      RABBITMQ_DEFAULT_PASS: admin