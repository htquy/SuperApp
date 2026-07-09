# BƯỚC 2: XÂY DỰNG TRẠM BẢO MẬT BFF GATEWAY (`sh-bff-gateway`)

Dự án này là trái tim của hệ thống bảo mật, đóng vai trò là một **Token Handler Pattern / Cookie-to-Bearer Proxy** dựa trên nền tảng YARP (Yet Another Reverse Proxy) và ASP.NET Core 8.

## Quy tắc bảo mật nghiêm ngặt SAD v3.0:
1. Frontend tuyệt đối không lưu trữ hay quản lý bất kỳ chuỗi JWT Token nào để tránh lỗ hổng XSS.
2. BFF sẽ giữ nhiệm vụ thu giữ JWT từ Auth Service, đưa vào lưu tại Redis Cache, và chỉ nhả một `__Host-bff` HttpOnly Cookie về trình duyệt.
3. BFF quản lý cơ chế phòng chống tấn công chéo CSRF bằng Double Submit Cookie Pattern thông qua custom header `X-XSRF-TOKEN`.

## Yêu cầu triển khai (Prompt dành cho AI):
Hãy viết mã nguồn hoàn chỉnh cho dự án `sh-bff-gateway` (Port 5000) đáp ứng các logic sau:

1. **Cấu hình YARP Định tuyến (`appsettings.json`)**:
   - Route `/api/auth/**` ➡️ Chuyển tiếp tới Phân hệ Auth (`http://localhost:6000`)
   - Route `/api/hrm/**` ➡️ Chuyển tiếp tới Phân hệ HRM (`http://localhost:6000`)

2. **Xử lý Luồng Đăng Nhập (Login Transform)**:
   - Khi request `/api/auth/login` đi qua BFF và nhận kết quả thành công từ Backend Core, BFF sẽ bắt lấy cục JSON (chứa `access_token`, `refresh_token`).
   - Sinh một chuỗi ngẫu nhiên `SessionId = Guid.NewGuid().ToString()`.
   - Lưu thông tin cặp token trên vào **Redis Cache** với Key: `bff:session:{SessionId}`.
   - Gửi về cho Frontend trình duyệt 2 cái Cookie:
     - Cookie 1: `__Host-bff` chứa giá trị `SessionId`, cấu hình `HttpOnly = true`, `SameSite = Strict`, `Secure = true`, `Path = /`. (Frontend JavaScript không thể đọc cái này).
     - Cookie 2: `XSRF-TOKEN` chứa một mã bảo mật ngẫu nhiên, cấu hình `HttpOnly = false`. (Frontend đọc cái này để gửi kèm header chống CSRF).

3. **Xử lý Luồng Gọi API Nghiệp Vụ (Cookie-to-Bearer Proxy Transform)**:
   - Khi nhận bất kỳ request nào gọi xuống `/api/hrm/**`, BFF kiểm tra xem nếu là phương thức biến đổi dữ liệu (POST/PUT/DELETE) thì bắt buộc phải có Header `X-XSRF-TOKEN` khớp với giá trị Cookie `XSRF-TOKEN` gửi lên để chặn đứng CSRF.
   - BFF đọc Cookie `__Host-bff` lấy ra `SessionId`.
   - Tra cứu vào Redis lấy ra chuỗi `access_token` thật của session đó.
   - Tiến hành inject tiêu đề `Authorization: Bearer [JWT]` vào Request Header trước khi YARP chuyển tiếp dữ liệu xuống Backend Core.

4. **Log Hệ Thống**: In ra màn hình Terminal:
   - Khi Login: `[BFF Gateway] Đã intercept token. Lưu vào Redis với mã Session {id}. Trả HttpOnly Cookie về trình duyệt.`
   - Khi Gọi API: `[BFF Gateway] Nhận request nghiệp vụ. Đọc SessionId từ Cookie, map thành công sang Bearer JWT từ Redis và chuyển tiếp qua YARP.`