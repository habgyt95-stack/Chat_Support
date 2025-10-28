class CustomNavbar extends HTMLElement {
  connectedCallback() {
    this.attachShadow({ mode: 'open' });
    this.shadowRoot.innerHTML = `
      <style>
        @import url('https://cdn.jsdelivr.net/gh/rastikerdar/vazir-font@v30.1.0/dist/font-face.css');
        
        nav {
          background: linear-gradient(135deg, #3B82F6 0%, #8B5CF6 100%);
          padding: 1rem 2rem;
          display: flex;
          justify-content: space-between;
          align-items: center;
          box-shadow: 0 2px 10px rgba(0, 0, 0, 0.1);
          position: relative;
          z-index: 40;
        }
        
        .logo-container {
          display: flex;
          align-items: center;
          gap: 0.75rem;
        }
        
        .logo {
          color: white;
          font-weight: bold;
          font-size: 1.5rem;
          font-family: 'Vazir', Tahoma, sans-serif;
        }
        
        .nav-actions {
          display: flex;
          align-items: center;
          gap: 1.5rem;
        }
        
        .nav-link {
          color: white;
          text-decoration: none;
          padding: 0.5rem 1rem;
          border-radius: 0.5rem;
          transition: all 0.3s ease;
          display: flex;
          align-items: center;
          gap: 0.5rem;
          font-family: 'Vazir', Tahoma, sans-serif;
        }
        
        .nav-link:hover {
          background-color: rgba(255, 255, 255, 0.1);
          transform: translateY(-1px);
        }
        
        .badge {
          background-color: #EF4444;
          color: white;
          border-radius: 50%;
          width: 20px;
          height: 20px;
          display: flex;
          align-items: center;
          justify-content: center;
          font-size: 0.75rem;
          font-weight: bold;
        }
        
        @media (max-width: 768px) {
          nav {
            padding: 1rem;
          }
          
          .logo {
            font-size: 1.25rem;
          }
        }
      </style>
      <nav>
        <div class="logo-container">
          <i data-feather="message-circle"></i>
          <div class="logo">ChatFlow Dashboard</div>
        </div>
        
        <div class="nav-actions">
          <a href="#" class="nav-link">
            <i data-feather="bell"></i>
            اعلان‌ها
            <span class="badge">3</span>
          </a>
          <a href="#" class="nav-link">
            <i data-feather="settings"></i>
            تنظیمات
          </a>
          <a href="#" class="nav-link">
            <i data-feather="help-circle"></i>
            راهنما
          </a>
        </div>
      </nav>
    `;
  }
}

customElements.define('custom-navbar', CustomNavbar);