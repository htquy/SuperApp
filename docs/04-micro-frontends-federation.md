# BƯỚC 4: LẮP RÁP GIAO DIỆN CHẠY RUNTIME BẰNG MODULE FEDERATION

Triển khai 2 dự án Frontend React độc lập sử dụng công cụ build `Vite` và plugin `@originjs/vite-plugin-federation` để hiện thực hóa kiến trúc Micro-frontends (MFE) như trong SAD v3.0.

## 1. Dự án `sh-hrm-fe` (Remote Micro-frontend - Chạy Port 3001)
Dự án này là phân hệ quản lý nhân sự độc lập, thuộc quyền quản lý của một team riêng biệt.

### Yêu cầu triển khai (Prompt dành cho AI):
1. Thiết kế component `EmployeeList.jsx`: Thực hiện gọi API `GET /api/hrm/employees` lên địa chỉ của BFF Gateway (`http://localhost:5000`) bằng cơ chế `withCredentials: true` để trình duyệt tự động đính kèm HttpOnly Cookie một cách an toàn. Hiển thị danh sách nhân viên ra table.
2. Cấu hình file `vite.config.js`: Sử dụng Module Federation để xuất khẩu (expose) component này ra bên ngoài:
   ```javascript
   federation({
     name: 'sh_hrm_fe',
     filename: 'remoteEntry.js',
     exposes: {
       './EmployeeList': './src/components/EmployeeList.jsx',
     },
     shared: ['react', 'react-dom']
   })