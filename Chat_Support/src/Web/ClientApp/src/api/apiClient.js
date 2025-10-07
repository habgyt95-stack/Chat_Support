// src/apiClient.js
import axios from "axios";
import { updateActiveAccountTokens } from "../Utils/accounts";

// این آدرس را با آدرس بک‌اند پروژه چت خودتان جایگزین کنید
const API_BASE_URL =
  import.meta.env.MODE === "development"
    ? "https://localhost:5001/api"
    : window.location.origin + "/api";
const apiClient = axios.create({
  baseURL: API_BASE_URL,
});

// Interceptor برای اضافه کردن توکن به هدر همه درخواست‌ها
apiClient.interceptors.request.use(
  (config) => {
    const token = localStorage.getItem("token");
    if (token) {
      config.headers.Authorization = `Bearer ${token}`;
    }
    // اگر ویجت در iframe است، OriginUrl را به‌صورت هدر پاس بده
    try {
      if (window && window.__chat_origin) {
        config.headers["X-Origin-Url"] = window.__chat_origin;
      }
    } catch {
      // ignore when window is not accessible
    }
    return config;
  },
  (error) => {
    return Promise.reject(error);
  }
);

function redirectToLoginIfNeeded() {
  try {
    const current = `${window.location.pathname}${window.location.search}${window.location.hash}`;
    const isOnLogin = window.location.pathname.toLowerCase().startsWith("/login");
    if (!isOnLogin) {
      const url = `/login?returnUrl=${encodeURIComponent(current)}`;
      // replace to avoid back-button loop
      window.location.replace(url);
    }
  } catch {
    // noop if window is not accessible
  }
}

// Interceptor برای مدیریت خطای 401 (توکن منقضی شده) و تلاش برای رفرش کردن آن
apiClient.interceptors.response.use(
  (response) => response,
  async (error) => {
    const originalRequest = error.config || {};
    if (error.response && error.response.status === 401 && !originalRequest._retry) {
      originalRequest._retry = true;
      try {
        const refreshToken = localStorage.getItem('refreshToken');
        const accessToken = localStorage.getItem('token');
        // اگر رفرش توکن وجود نداشت، به لاگین هدایت شود (اما اگر همین حالا روی لاگین هستیم ریدایرکت نکن)
        if (!refreshToken || !accessToken) {
          localStorage.removeItem('token');
          localStorage.removeItem('refreshToken');
          redirectToLoginIfNeeded();
          return Promise.reject(error);
        }
        const rs = await axios.post(`${API_BASE_URL.replace(/\/api$/, '')}/auth/refresh-token`, {
          accessToken,
          refreshToken,
        });
        const data = rs?.data || {};
        const newAccessToken = data.accessToken || data.AccessToken;
        const newRefreshToken = data.refreshToken || data.RefreshToken;
        if (!newAccessToken || !newRefreshToken) {
          throw new Error('Invalid refresh response');
        }
        localStorage.setItem('token', newAccessToken);
        localStorage.setItem('refreshToken', newRefreshToken);
        // به‌روزرسانی اکانت فعال
        try { updateActiveAccountTokens(newAccessToken, newRefreshToken); } catch (e) { console.warn('account update failed', e); }
        apiClient.defaults.headers.common['Authorization'] = `Bearer ${newAccessToken}`;
        originalRequest.headers = originalRequest.headers || {};
        originalRequest.headers['Authorization'] = `Bearer ${newAccessToken}`;
        return apiClient(originalRequest);
      } catch (_error) {
        localStorage.removeItem('token');
        localStorage.removeItem('refreshToken');
        redirectToLoginIfNeeded();
        return Promise.reject(_error);
      }
    }
    return Promise.reject(error);
  }
);

export default apiClient;

// Optional helper for setting tokens programmatically
export function setAuthTokens(accessToken, refreshToken) {
  localStorage.setItem('token', accessToken || '');
  if (refreshToken) localStorage.setItem('refreshToken', refreshToken);
  if (accessToken) apiClient.defaults.headers.common['Authorization'] = `Bearer ${accessToken}`;
}
