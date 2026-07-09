# Chi Tiết Bước 2: Xử lý Luồng Đăng Nhập (Login Transform)

Tài liệu này giải thích chi tiết các thay đổi cấu hình và lập trình cần thiết để thiết lập luồng **Login Transform** cho dự án `sh-bff-gateway`, đáp ứng quy tắc bảo mật không lưu trữ JWT ở Frontend (Token Handler Pattern).

---

## 1. Mục tiêu
Thiết lập một endpoint chặn `/api/auth/login` (POST) trên BFF Gateway:
1. Nhận yêu cầu đăng nhập từ Client.
2. Chuyển tiếp (proxy) yêu cầu đăng nhập này xuống backend thực tế (`http://localhost:6000/api/auth/login`).
3. Nếu backend trả về kết quả đăng nhập thành công (200 OK) có kèm `access_token` và `refresh_token`:
   - Sinh mã định danh phiên ngẫu nhiên `SessionId` (Guid).
   - Lưu trữ cặp token này vào **Redis Cache** (mặc định trỏ tới DB 4) với Key dạng `bff:session:{SessionId}`.
   - Thiết lập và trả về 2 cookie cho Client:
     - `__Host-bff`: Chứa `SessionId` (HttpOnly = true, SameSite = Strict, Secure = true, Path = /).
     - `XSRF-TOKEN`: Mã bảo mật chống tấn công CSRF (HttpOnly = false, SameSite = Strict, Secure = true, Path = /).
   - Loại bỏ hoàn toàn `access_token` và `refresh_token` ra khỏi dữ liệu phản hồi trả về cho Client nhằm triệt tiêu nguy cơ XSS.
   - Ghi nhận nhật ký (Log) ra terminal theo định dạng yêu cầu.

---

## 2. Chi tiết các tệp thay đổi

### 2.1. Tệp [sh-bff-gateway.csproj](file:///home/nobugnolife/Source/SuperApp/BE/sh-bff-gateway/sh-bff-gateway.csproj)
Chúng ta đã cài đặt thư viện kết nối Redis:
```xml
<PackageReference Include="StackExchange.Redis" Version="3.0.11" />
```
* **Giải thích:** `StackExchange.Redis` là thư viện kết nối và thao tác với máy chủ Redis hiệu năng cao, phổ biến nhất trong hệ sinh thái .NET.

### 2.2. Tệp [appsettings.json](file:///home/nobugnolife/Source/SuperApp/BE/sh-bff-gateway/appsettings.json)
Khai báo chuỗi kết nối Redis:
```json
"ConnectionStrings": {
  "Redis": "localhost:6379"
}
```
* **Giải thích:** Giúp ứng dụng cấu hình linh hoạt địa chỉ IP và Port của Redis server khi chạy ở các môi trường khác nhau (Local, Docker, Production).

### 2.3. Tệp [Program.cs](file:///home/nobugnolife/Source/SuperApp/BE/sh-bff-gateway/Program.cs)
Bổ sung các thiết lập đăng ký DI và endpoint xử lý đăng nhập:

#### A. Đăng ký dịch vụ DI:
```csharp
// Đăng ký HTTP Client
builder.Services.AddHttpClient();

// Đăng ký Singleton IConnectionMultiplexer cho Redis
builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
{
    var configuration = sp.GetRequiredService<IConfiguration>();
    var connectionString = configuration.GetConnectionString("Redis") ?? "localhost:6379";
    return ConnectionMultiplexer.Connect(connectionString);
});
```

#### B. Định nghĩa Endpoint `/api/auth/login`:
Endpoint này nhận request `POST /api/auth/login` và thực hiện các nhiệm vụ:
1. **Chuyển tiếp yêu cầu (Request Proxying):** Tạo một `HttpRequestMessage` gửi tới `http://localhost:6000/api/auth/login`, sao chép toàn bộ request header từ client sang.
2. **Xử lý phản hồi thành công (Response Interception):** 
   - Đọc JSON trả về từ backend, kiểm tra xem có chứa `access_token` hay không.
   - Sinh ngẫu nhiên một chuỗi `SessionId = Guid.NewGuid().ToString()`.
   - Kết nối tới Database số `4` của Redis và lưu chuỗi JSON `{ "access_token": "...", "refresh_token": "..." }` với Key là `bff:session:{SessionId}`.
   - Tạo Cookie bảo mật:
     - `__Host-bff` được cấu hình với thuộc tính `HttpOnly = true` (Frontend JS không đọc được) để lưu `SessionId`.
     - `XSRF-TOKEN` cấu hình `HttpOnly = false` (Frontend JS đọc được để gửi kèm trong custom header `X-XSRF-TOKEN` chống tấn công CSRF).
3. **Log Hệ thống:** In thông báo ra console:
   ```text
   [BFF Gateway] Đã intercept token. Lưu vào Redis với mã Session {SessionId}. Trả HttpOnly Cookie về trình duyệt.
   ```
4. **Bảo mật dữ liệu (Strip Tokens):** Sử dụng `System.Text.Json.Nodes.JsonObject` để remove thuộc tính `access_token` và `refresh_token` ra khỏi response body rồi mới gửi về cho trình duyệt.
