# Chi Tiết Bước 4: Log Hệ Thống (System Logging)

Tài liệu này giải thích chi tiết hoạt động ghi nhật ký hệ thống (System Logging) tại **BFF Gateway**, đảm bảo tính minh bạch, dễ theo dõi (observability) và đáp ứng chính xác đặc tả yêu cầu trong tài liệu thiết kế.

---

## 1. Mục tiêu
Thiết lập cơ chế log ra màn hình console (Terminal) theo chuẩn chuỗi định dạng khi xảy ra 2 sự kiện cốt lõi:
1. **Sự kiện Đăng nhập thành công (Login Intercept):**
   - Khi BFF đánh chặn token thành công, lưu vào Redis và trả Cookie về cho trình duyệt.
   - Định dạng Log bắt buộc: `[BFF Gateway] Đã intercept token. Lưu vào Redis với mã Session {id}. Trả HttpOnly Cookie về trình duyệt.`
2. **Sự kiện Gọi API nghiệp vụ (API Proxying):**
   - Khi nhận yêu cầu nghiệp vụ, phân tích session thành công từ cookie và chuyển tiếp kèm token Authorization Bearer.
   - Định dạng Log bắt buộc: `[BFF Gateway] Nhận request nghiệp vụ. Đọc SessionId từ Cookie, map thành công sang Bearer JWT từ Redis và chuyển tiếp qua YARP.`

---

## 2. Chi tiết mã nguồn đã triển khai trong [Program.cs](file:///home/nobugnolife/Source/SuperApp/BE/sh-bff-gateway/Program.cs)

### 2.1. Log khi Đăng nhập (POST `/api/auth/login`)
Trong khối xử lý sau khi lấy được access token và lưu vào Redis:
```csharp
// Required system console log output
Console.WriteLine($"[BFF Gateway] Đã intercept token. Lưu vào Redis với mã Session {sessionId}. Trả HttpOnly Cookie về trình duyệt.");
```
* **Giải thích:** Dùng để thông báo cho người quản trị hệ thống biết rằng một phiên làm việc mới (Session) đã được khởi tạo và lưu trữ an toàn trong Redis Cache, đồng thời các thiết lập cookie bảo mật đã được đưa về client.

### 2.2. Log khi gọi API nghiệp vụ (Middleware của `/api/hrm/**`)
Trong khối middleware xử lý khôi phục token và chuyển tiếp:
```csharp
// Required system console log output
Console.WriteLine("[BFF Gateway] Nhận request nghiệp vụ. Đọc SessionId từ Cookie, map thành công sang Bearer JWT từ Redis và chuyển tiếp qua YARP.");
```
* **Giải thích:** Dùng để theo dõi mỗi khi client gửi request nghiệp vụ. Dòng log này minh chứng cho cơ chế "Cookie-to-Bearer" hoạt động chính xác: BFF tìm thấy SessionId từ cookie, phân giải sang JWT token thực tế của phiên, và truyền xuống backend một cách hoàn toàn trong suốt với frontend.
