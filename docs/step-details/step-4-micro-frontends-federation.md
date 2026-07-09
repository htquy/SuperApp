# Chi Tiết Bước 4: Lắp Ráp Giao Diện Chạy Runtime Bằng Module Federation (FE)

Tài liệu này giải thích chi tiết cấu trúc triển khai **Micro-frontends (MFE)** bằng React, Vite và plugin `@originjs/vite-plugin-federation` cho hai dự án độc lập là `sh-hrm-fe` (Remote) và `sh-shell-app` (Host).

---

## 1. Mục tiêu
1. **Thiết lập CORS ở BFF:** Cấu hình chính sách chia sẻ tài nguyên CORS trên BFF Gateway (`Program.cs`) để cho phép các nguồn gốc từ frontend (`http://localhost:3000` và `http://localhost:3001`) gửi yêu cầu có kèm thông tin xác thực (`credentials: 'include'`).
2. **Triển khai Remote MFE (`sh-hrm-fe`):**
   - Thiết kế component `EmployeeList.jsx` để gọi API lấy danh sách nhân viên từ BFF Gateway (`http://localhost:5000/api/hrm/employees`) với option `credentials: 'include'` nhằm đính kèm HttpOnly Cookie bảo mật tự động. Renders danh sách theo giao diện Glassmorphism.
   - Cấu hình Module Federation trong `vite.config.js` để đóng gói (expose) component `EmployeeList` ra bên ngoài tại cổng **3001**.
3. **Triển khai Host MFE (`sh-shell-app`):**
   - Cấu hình Module Federation nhận đầu vào từ remote entry ở port **3001**.
   - Cấu hình ứng dụng chạy trên cổng **3000**.
   - Tích hợp Lazy Loading và `Suspense` để nạp động component quản lý nhân viên từ remote ở runtime. Thiết kế Shell Layout hoàn chỉnh gồm Sidebar điều hướng và thống kê hệ thống.

---

## 2. Chi tiết các tệp thay đổi và thêm mới

### 2.1. Thiết lập CORS tại BFF [BE/sh-bff-gateway/Program.cs](file:///home/nobugnolife/Source/SuperApp/BE/sh-bff-gateway/Program.cs)
```csharp
// Đăng ký dịch vụ CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("http://localhost:3000", "http://localhost:3001")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials(); // Bắt buộc để nhận Cookie (__Host-bff) từ Browser
    });
});

// Sử dụng middleware CORS trước các định tuyến
app.UseCors();
```

### 2.2. Triển khai dự án Remote MFE (`sh-hrm-fe`)

#### A. Cài đặt plugin và cấu hình [vite.config.js](file:///home/nobugnolife/Source/SuperApp/FE/sh-hrm-fe/vite.config.js):
```javascript
import federation from '@originjs/vite-plugin-federation'

export default defineConfig({
  plugins: [
    react(),
    federation({
      name: 'sh_hrm_fe',
      filename: 'remoteEntry.js',
      exposes: {
        './EmployeeList': './src/components/EmployeeList.jsx',
      },
      shared: ['react', 'react-dom']
    })
  ],
  server: {
    port: 3001
  },
  build: {
    target: 'esnext' // Yêu cầu từ Module Federation để tận dụng ES Modules gốc
  }
})
```

#### B. Component danh sách nhân sự [EmployeeList.jsx](file:///home/nobugnolife/Source/SuperApp/FE/sh-hrm-fe/src/components/EmployeeList.jsx):
- Gọi API bằng fetch:
  ```javascript
  fetch('http://localhost:5000/api/hrm/employees', {
    method: 'GET',
    credentials: 'include', // Rất quan trọng: cho phép đính kèm cookie HttpOnly
  })
  ```
- Định dạng giao diện theo xu hướng hiện đại (Glassmorphism, Dark-mode, có loading/error state và responsive table).

---

### 2.3. Triển khai dự án Host MFE (`sh-shell-app`)

#### A. Cài đặt plugin và cấu hình [vite.config.js](file:///home/nobugnolife/Source/SuperApp/FE/sh-shell-app/vite.config.js):
```javascript
import federation from '@originjs/vite-plugin-federation'

export default defineConfig({
  plugins: [
    react(),
    federation({
      name: 'sh_shell_app',
      remotes: {
        sh_hrm_fe: 'http://localhost:3001/assets/remoteEntry.js'
      },
      shared: ['react', 'react-dom']
    })
  ],
  server: {
    port: 3000
  },
  build: {
    target: 'esnext'
  }
})
```

#### B. Lắp ráp giao diện chính [App.jsx](file:///home/nobugnolife/Source/SuperApp/FE/sh-shell-app/src/App.jsx):
- Nạp động component thông qua React Lazy:
  ```javascript
  const RemoteEmployeeList = React.lazy(() => import('sh_hrm_fe/EmployeeList'));
  ```
- Sử dụng `<Suspense fallback={...}>` để hiển thị màn hình chờ tải trong lúc fetch chunk `remoteEntry.js` từ port 3001.
- Layout Shell Dashboard cao cấp (Sidebar, Header, Main view, và các Card hiển thị trạng thái hệ thống).

#### C. Thiết kế Layout CSS [App.css](file:///home/nobugnolife/Source/SuperApp/FE/sh-shell-app/src/App.css):
- Triển khai lưới (CSS Grid) và flexbox phân chia Header, Sidebar và Content.
- Sử dụng bảng phối màu HSL chuyên nghiệp cho chế độ Sleek Dark-mode, tối ưu hóa trải nghiệm người dùng với các hiệu ứng hover mượt mà và bo góc hiện đại.
