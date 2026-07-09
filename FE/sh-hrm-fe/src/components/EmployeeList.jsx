import React, { useEffect, useState } from 'react';

export default function EmployeeList() {
  const [employees, setEmployees] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);

  useEffect(() => {
    fetch('http://localhost:5000/api/hrm/employees', {
      method: 'GET',
      headers: {
        'Accept': 'application/json',
      },
      credentials: 'include', 
    })
      .then((res) => {
        if (!res.ok) {
          throw new Error(`HTTP error! status: ${res.status}`);
        }
        return res.json();
      })
      .then((data) => {
        setEmployees(data);
        setLoading(false);
      })
      .catch((err) => {
        setError(err.message);
        setLoading(false);
      });
  }, []);

  return (
    <div className="employee-list-container">
      <style>{`
        .employee-list-container {
          font-family: 'Outfit', 'Inter', sans-serif;
          background: rgba(30, 41, 59, 0.4);
          backdrop-filter: blur(16px);
          -webkit-backdrop-filter: blur(16px);
          border: 1px solid rgba(255, 255, 255, 0.08);
          border-radius: 16px;
          padding: 24px;
          box-shadow: 0 10px 40px 0 rgba(0, 0, 0, 0.3);
          color: #f1f5f9;
          max-width: 900px;
          margin: 20px auto;
          transition: all 0.3s ease;
        }
        .employee-list-container:hover {
          border-color: rgba(99, 102, 241, 0.3);
          box-shadow: 0 12px 48px 0 rgba(99, 102, 241, 0.15);
        }
        .employee-title {
          font-size: 1.5rem;
          font-weight: 600;
          margin-bottom: 20px;
          background: linear-gradient(135deg, #818cf8, #c084fc);
          -webkit-background-clip: text;
          -webkit-text-fill-color: transparent;
          display: flex;
          align-items: center;
          justify-content: space-between;
          gap: 10px;
        }
        .status-badge {
          background: rgba(99, 102, 241, 0.15);
          color: #818cf8;
          font-size: 0.75rem;
          font-weight: 500;
          padding: 6px 14px;
          border-radius: 9999px;
          border: 1px solid rgba(99, 102, 241, 0.25);
          text-transform: uppercase;
          letter-spacing: 0.05em;
        }
        .employee-table {
          width: 100%;
          border-collapse: collapse;
          text-align: left;
        }
        .employee-table th {
          font-weight: 600;
          text-transform: uppercase;
          font-size: 0.75rem;
          letter-spacing: 0.08em;
          color: #94a3b8;
          padding: 14px 18px;
          border-bottom: 1px solid rgba(255, 255, 255, 0.1);
        }
        .employee-table td {
          padding: 16px 18px;
          border-bottom: 1px solid rgba(255, 255, 255, 0.05);
          font-size: 0.95rem;
        }
        .employee-row {
          transition: background-color 0.2s ease;
        }
        .employee-row:hover {
          background: rgba(255, 255, 255, 0.02);
        }
        .avatar-circle {
          width: 36px;
          height: 36px;
          border-radius: 50%;
          background: linear-gradient(135deg, #6366f1, #a855f7);
          color: white;
          display: flex;
          align-items: center;
          justify-content: center;
          font-weight: 600;
          font-size: 0.9rem;
          box-shadow: 0 4px 12px rgba(99, 102, 241, 0.3);
        }
        .employee-info-cell {
          display: flex;
          align-items: center;
          gap: 14px;
        }
        .employee-name {
          font-weight: 500;
          color: #f8fafc;
        }
        .employee-dept {
          background: rgba(255, 255, 255, 0.06);
          padding: 4px 10px;
          border-radius: 8px;
          font-size: 0.85rem;
          color: #cbd5e1;
          border: 1px solid rgba(255, 255, 255, 0.05);
        }
        .loading-text, .error-text, .no-data {
          padding: 40px;
          text-align: center;
          font-size: 1rem;
          color: #94a3b8;
        }
        .error-text {
          color: #f87171;
          background: rgba(248, 113, 113, 0.1);
          border-radius: 12px;
          border: 1px solid rgba(248, 113, 113, 0.2);
          line-height: 1.6;
        }
      `}</style>
      
      <div className="employee-title">
        <span>Nhân sự Sunhouse</span>
        <span className="status-badge">Module HRM (Port 3001)</span>
      </div>

      {loading && <div className="loading-text">🔄 Đang tải danh sách nhân viên từ BFF Gateway...</div>}
      
      {error && (
        <div className="error-text">
          ⚠️ <strong>Lỗi kết nối:</strong> {error}<br />
          <span style={{ fontSize: '0.85rem', color: '#cbd5e1' }}>
            Vui lòng kiểm tra xem BFF Gateway (Port 5000) và Backend Core đã chạy, và bạn đã đăng nhập thành công.
          </span>
        </div>
      )}

      {!loading && !error && employees.length === 0 && (
        <div className="no-data">📭 Danh sách nhân viên đang trống.</div>
      )}

      {!loading && !error && employees.length > 0 && (
        <table className="employee-table">
          <thead>
            <tr>
              <th>Nhân viên</th>
              <th>Email</th>
              <th>Phòng ban</th>
            </tr>
          </thead>
          <tbody>
            {employees.map((emp) => {
              const initial = emp.name ? emp.name.charAt(0).toUpperCase() : '?';
              return (
                <tr key={emp.id || emp.email} className="employee-row">
                  <td>
                    <div className="employee-info-cell">
                      <div className="avatar-circle">{initial}</div>
                      <div className="employee-name">{emp.name}</div>
                    </div>
                  </td>
                  <td>{emp.email}</td>
                  <td>
                    <span className="employee-dept">{emp.department || 'Chưa xếp'}</span>
                  </td>
                </tr>
              );
            })}
          </tbody>
        </table>
      )}
    </div>
  );
}
