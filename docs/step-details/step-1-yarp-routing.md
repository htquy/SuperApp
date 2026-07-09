# Chi Tiết Bước 1: Cấu hình YARP Định tuyến (sh-bff-gateway)

Tài liệu này giải thích chi tiết các thay đổi cấu hình cần thiết để thiết lập hệ thống định tuyến (Reverse Proxy) bằng **YARP** cho dự án `sh-bff-gateway`.

---

## 1. Mục tiêu
Thiết lập dự án `sh-bff-gateway` chạy ở cổng **5000** đóng vai trò là một Reverse Proxy nhận mọi request từ client gửi lên và phân phối chính xác:
- Các request `/api/auth/**` ➡️ Chuyển tiếp tới Phân hệ Auth (`http://localhost:6000`).
- Các request `/api/hrm/**` ➡️ Chuyển tiếp tới Phân hệ HRM (`http://localhost:6000`).

---

## 2. Chi tiết các tệp thay đổi

### 2.1. Tệp [sh-bff-gateway.csproj](file:///home/nobugnolife/Source/SuperApp/BE/sh-bff-gateway/sh-bff-gateway.csproj)
Chúng ta đã thêm package `Yarp.ReverseProxy` vào dự án:
```xml
<PackageReference Include="Yarp.ReverseProxy" Version="2.3.0" />
```
* **Giải thích:** YARP (Yet Another Reverse Proxy) là một thư viện mã nguồn mở của Microsoft, được thiết kế trên nền tảng .NET để xây dựng các máy chủ proxy ngược có hiệu năng cao và khả năng tùy biến mạnh mẽ.

### 2.2. Tệp [Properties/launchSettings.json](file:///home/nobugnolife/Source/SuperApp/BE/sh-bff-gateway/Properties/launchSettings.json)
Thay đổi cấu hình cổng chạy mặc định của ứng dụng khi chạy local:
```json
"applicationUrl": "https://localhost:5001;http://localhost:5000"
```
* **Giải thích:** BFF Gateway được thiết kế để lắng nghe các request từ client ở port `5000` (đối với HTTP) và `5001` (đối với HTTPS). Việc cập nhật này đảm bảo khi khởi chạy bằng lệnh `dotnet run`, ứng dụng sẽ tự động bind vào đúng các port này thay vì port ngẫu nhiên.

### 2.3. Tệp [appsettings.json](file:///home/nobugnolife/Source/SuperApp/BE/sh-bff-gateway/appsettings.json)
Khai báo cấu hình định tuyến cho YARP:
```json
"ReverseProxy": {
  "Routes": {
    "auth-route": {
      "ClusterId": "auth-cluster",
      "Match": {
        "Path": "/api/auth/{**catch-all}"
      }
    },
    "hrm-route": {
      "ClusterId": "hrm-cluster",
      "Match": {
        "Path": "/api/hrm/{**catch-all}"
      }
    }
  },
  "Clusters": {
    "auth-cluster": {
      "Destinations": {
        "destination1": {
          "Address": "http://localhost:6000"
        }
      }
    },
    "hrm-cluster": {
      "Destinations": {
        "destination1": {
          "Address": "http://localhost:6000"
        }
      }
    }
  }
}
```
* **Giải thích:**
  * **Routes (Định tuyến):** Định nghĩa cách map các request gửi đến gateway.
    * `Match.Path`: Biểu thức để so khớp đường dẫn. Ở đây sử dụng `{**catch-all}` để bắt lấy tất cả các tài nguyên con phía sau `/api/auth/` hoặc `/api/hrm/`.
    * `ClusterId`: Chỉ định nhóm server đích (cluster) sẽ xử lý request khớp với route này.
  * **Clusters (Cụm server đích):** Định nghĩa danh sách các server backend thực tế.
    * `Address`: Địa chỉ gốc của backend. Cả `auth-cluster` và `hrm-cluster` đều được cấu hình trỏ về cổng `6000` (`http://localhost:6000`).

### 2.4. Tệp [Program.cs](file:///home/nobugnolife/Source/SuperApp/BE/sh-bff-gateway/Program.cs)
Cấu hình tích hợp YARP vào middleware pipeline của ASP.NET Core:
```csharp
// Đăng ký dịch vụ YARP và nạp cấu hình từ mục "ReverseProxy" trong appsettings.json
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

// Kích hoạt middleware YARP để bắt các request phù hợp và chuyển hướng chúng
app.MapReverseProxy();
```
* **Giải thích:**
  * `AddReverseProxy()` đăng ký các thành phần cần thiết của proxy ngược (như router, cluster manager, HTTP client factory, v.v.).
  * `LoadFromConfig()` trích xuất các cấu hình cụ thể từ file `appsettings.json`.
  * `MapReverseProxy()` định nghĩa endpoint cuối cùng trong pipeline để chuyển hướng request sang backend đích.
