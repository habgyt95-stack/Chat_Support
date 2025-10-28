// src/main.jsx

import { StrictMode } from 'react';
import 'bootstrap/dist/css/bootstrap.min.css'; // Import Bootstrap CSS globally
import { createRoot } from 'react-dom/client';
import { BrowserRouter } from 'react-router-dom'; // این خط را اضافه کنید
import App from './App.jsx';
import { AuthProvider } from './contexts/AuthContext.jsx';
import './index.css';
import { register as registerServiceWorker } from './serviceWorkerRegistration.js';

const root = createRoot(document.getElementById('root'));

root.render(
  <StrictMode>
    <BrowserRouter> {/* باید در اینجا باشد */}
      <AuthProvider>
        <App />
      </AuthProvider>
    </BrowserRouter>
  </StrictMode>,
);

// Register service worker in production builds to enable PWA
if (import.meta.env.PROD && 'serviceWorker' in navigator) {
  registerServiceWorker();
}