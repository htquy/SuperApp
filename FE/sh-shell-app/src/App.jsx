import React, { useState, Suspense } from 'react'
import './App.css'

// Dynamically import the remote MFE component
const RemoteEmployeeList = React.lazy(() => import('sh_hrm_fe/EmployeeList'));

function App() {
  const [activeTab, setActiveTab] = useState('overview');

  return (
    <div className="app-shell">
      <header className="app-header">
        <div className="logo-section">
          <div className="app-logo">SH</div>
          <h1>Sunhouse Super App Ecosystem <span className="version-badge">v3.0</span></h1>
        </div>
        <div className="user-profile">
          <div className="user-avatar">AD</div>
          <span>Administrator</span>
        </div>
      </header>

      <div className="app-body">
        <aside className="app-sidebar">
          <nav className="nav-menu">
            <button 
              className={`nav-item ${activeTab === 'overview' ? 'active' : ''}`}
              onClick={() => setActiveTab('overview')}
            >
              📊 Tổng quan hệ thống
            </button>
            <button 
              className={`nav-item ${activeTab === 'hrm' ? 'active' : ''}`}
              onClick={() => setActiveTab('hrm')}
            >
              👥 Quản lý Nhân sự (MFE)
            </button>
          </nav>
          <div className="sidebar-footer">
            <div className="status-indicator">
              <span className="dot online"></span>
              <span>BFF Port: 5000</span>
            </div>
          </div>
        </aside>

        <main className="app-content">
          {activeTab === 'overview' && (
            <div className="overview-container">
              <div className="welcome-card">
                <h2>Chào mừng quay trở lại!</h2>
                <p>Đây là giao diện điều khiển trung tâm (Host Shell) của hệ sinh thái Sunhouse Super App. Phân hệ Nhân sự bên dưới được tải trực tiếp tại runtime từ dự án Remote MFE.</p>
              </div>
              <div className="stats-grid">
                <div className="stat-card">
                  <h3>Host App Status</h3>
                  <div className="stat-value text-indigo">Running</div>
                  <div className="stat-desc">Port: 3000 (React 19 + Vite 8)</div>
                </div>
                <div className="stat-card">
                  <h3>Remote HRM Status</h3>
                  <div className="stat-value text-purple">Connected</div>
                  <div className="stat-desc">Port: 3001 (Module Federation)</div>
                </div>
                <div className="stat-card">
                  <h3>Security Protocol</h3>
                  <div className="stat-value text-green">BFF Cookie</div>
                  <div className="stat-desc">Token Handler Pattern active</div>
                </div>
              </div>
            </div>
          )}

          {activeTab === 'hrm' && (
            <div className="hrm-container">
              <Suspense fallback={<div className="loading-mfe">⏳ Đang nạp phân hệ Nhân sự (sh-hrm-fe) từ Port 3001...</div>}>
                <RemoteEmployeeList />
              </Suspense>
            </div>
          )}
        </main>
      </div>
    </div>
  )
}

export default App
