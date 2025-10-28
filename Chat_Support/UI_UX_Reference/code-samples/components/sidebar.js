class CustomSidebar extends HTMLElement {
  connectedCallback() {
    this.attachShadow({ mode: 'open' });
    this.shadowRoot.innerHTML = `
      <style>
        @import url('https://cdn.jsdelivr.net/gh/rastikerdar/vazir-font@v30.1.0/dist/font-face.css');
        
        :host {
          display: block;
          width: 280px;
          background: white;
          border-left: 1px solid #e5e7eb;
          height: 100%;
        }
        
        .sidebar {
          height: 100%;
          padding: 1.5rem 1rem;
          display: flex;
          flex-direction: column;
        }
        
        .sidebar-section {
          margin-bottom: 2rem;
        }
        
        .sidebar-title {
          color: #6b7280;
          font-size: 0.875rem;
          font-weight: 600;
          margin-bottom: 1rem;
          padding-right: 0.75rem;
          font-family: 'Vazir', Tahoma, sans-serif;
        }
        
        .sidebar-item {
          display: flex;
          align-items: center;
          gap: 0.75rem;
          padding: 0.75rem 1rem;
          border-radius: 0.5rem;
          color: #374151;
          text-decoration: none;
          transition: all 0.3s ease;
          margin-bottom: 0.25rem;
          font-family: 'Vazir', Tahoma, sans-serif;
          cursor: pointer;
        }
        
        .sidebar-item:hover {
          background-color: #f3f4f6;
          color: #3B82F6;
        }
        
        .sidebar-item.active {
          background-color: #eff6ff;
          color: #3B82F6;
          font-weight: 600;
        }
        
        .sidebar-item i {
          width: 18px;
          height: 18px;
        }
        
        .stats-card {
          background: linear-gradient(135deg, #3B82F6 0%, #8B5CF6 100%);
          color: white;
          padding: 1rem;
          border-radius: 0.75rem;
          margin-bottom: 1rem;
        }
        
        .stat-number {
          font-size: 1.5rem;
          font-weight: bold;
          margin-bottom: 0.25rem;
        }
        
        .stat-label {
          font-size: 0.875rem;
          opacity: 0.9;
        }
        
        @media (max-width: 1024px) {
          :host {
            width: 240px;
          }
        }
        
        @media (max-width: 768px) {
          :host {
            display: none;
          }
        }
      </style>
      <div class="sidebar">
        <!-- Quick Stats -->
        <div class="sidebar-section">
          <div class="stats-card">
            <div class="stat-number">۱۲۴</div>
            <div class="stat-label">کاربر فعال</div>
          </div>
          
          <div class="stats-card" style="background: linear-gradient(135deg, #10B981 0%, #059669 100%);">
            <div class="stat-number">۸۹</div>
            <div class="stat-label">گفتگوی امروز</div>
          </div>
        </div>
        
        <!-- Navigation -->
        <div class="sidebar-section">
          <h3 class="sidebar-title">مدیریت اصلی</h3>
          <a href="#" class="sidebar-item active">
            <i data-feather="home"></i>
            <span>داشبورد</span>
          </a>
          <a href="#" class="sidebar-item">
            <i data-feather="users"></i>
            <span>مدیریت کاربران</span>
          </a>
          <a href="#" class="sidebar-item">
            <i data-feather="message-square"></i>
            <span>همه گفتگوها</span>
          </a>
        </div>
        
        <!-- Reports -->
        <div class="sidebar-section">
          <h3 class="sidebar-title">گزارش‌گیری</h3>
          <a href="#" class="sidebar-item">
            <i data-feather="bar-chart-2"></i>
            <span>آمار و گزارش‌ها</span>
          </a>
          <a href="#" class="sidebar-item">
            <i data-feather="file-text"></i>
            <span>گزارش‌های روزانه</span>
          </a>
          <a href="#" class="sidebar-item">
            <i data-feather="pie-chart"></i>
            <span>تحلیل عملکرد</span>
          </a>
        </div>
        
        <!-- Settings -->
        <div class="sidebar-section">
          <h3 class="sidebar-title">تنظیمات</h3>
          <a href="#" class="sidebar-item">
            <i data-feather="shield"></i>
            <span>امنیت</span>
          </a>
          <a href="#" class="sidebar-item">
            <i data-feather="sliders"></i>
            <span>تنظیمات سیستم</span>
          </a>
        </div>
      </div>
    `;
  }
}

customElements.define('custom-sidebar', CustomSidebar);