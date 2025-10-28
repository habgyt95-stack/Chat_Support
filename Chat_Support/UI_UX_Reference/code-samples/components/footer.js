class CustomFooter extends HTMLElement {
  connectedCallback() {
    this.attachShadow({ mode: 'open' });
    this.shadowRoot.innerHTML = `
      <style>
        @import url('https://cdn.jsdelivr.net/gh/rastikerdar/vazir-font@v30.1.0/dist/font-face.css');
        
        footer {
          background: #1f2937;
          color: #d1d5db;
          padding: 1.5rem 2rem;
          text-align: center;
          border-top: 1px solid #374151;
          font-family: 'Vazir', Tahoma, sans-serif;
        }
        
        .footer-content {
          display: flex;
          justify-content: space-between;
          align-items: center;
          max-width: 1200px;
          margin: 0 auto;
        }
        
        .footer-links {
          display: flex;
          gap: 1.5rem;
        }
        
        .footer-link {
          color: #d1d5db;
          text-decoration: none;
          transition: color 0.3s ease;
          font-size: 0.875rem;
        }
        
        .footer-link:hover {
          color: #3B82F6;
        }
        
        @media (max-width: 768px) {
          .footer-content {
            flex-direction: column;
            gap: 1rem;
          }
        }
      </style>
      <footer>
        <div class="footer-content">
          <p>&copy; ۱۴۰۲ ChatFlow Dashboard. تمامی حقوق محفوظ است.</p>
          <div class="footer-links">
            <a href="#" class="footer-link">قوانین و مقررات</a>
          <a href="#" class="footer-link">حریم خصوصی</a>
          <a href="#" class="footer-link">پشتیبانی</a>
          <a href="#" class="footer-link">درباره ما</a>
          </div>
        </div>
      </footer>
    `;
  }
}

customElements.define('custom-footer', CustomFooter);