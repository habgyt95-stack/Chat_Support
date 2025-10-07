//src/Session/sessionManager.js
import {clearAuthTokens, getAuthTokens} from '../contexts/AuthContext';

let inactivityTimeout;
const INACTIVITY_TIME = 24 * 60 * 60 * 1000; // 24 ساعت به میلی‌ثانیه

// تابع بررسی فعال بودن نشست
export const checkSession = () => {
  const {token, expiryTime} = getAuthTokens();
  if (!token) {
    return false;
  }
  if (expiryTime && new Date() > new Date(expiryTime)) {
    clearAuthTokens();
    return false;
  }

  return true;
};

// تابع ریست کردن تایمر غیرفعالی
export const resetInactivityTimer = () => {
  if (inactivityTimeout) {
    clearTimeout(inactivityTimeout);
  }

  inactivityTimeout = setTimeout(() => {
    const {token} = getAuthTokens();
    if (token) {
      clearAuthTokens();
      const currentPath = window.location.pathname;
      window.location.href = `/login?returnUrl=${encodeURIComponent(currentPath)}`;
    }
  }, INACTIVITY_TIME);
};

// تابع شروع مانیتورینگ فعالیت کاربر
export const startActivityMonitoring = () => {
  // رویدادهایی که نشان‌دهنده فعالیت کاربر هستند
  const activityEvents = ['mousedown', 'mousemove', 'keypress', 'scroll', 'touchstart', 'click'];

  // افزودن لیسنرها برای رویدادهای فعالیت
  activityEvents.forEach((event) => {
    document.addEventListener(event, resetInactivityTimer);
  });

  // ریست اولیه تایمر
  resetInactivityTimer();
};

// تابع توقف مانیتورینگ فعالیت کاربر
export const stopActivityMonitoring = () => {
  const activityEvents = ['mousedown', 'mousemove', 'keypress', 'scroll', 'touchstart', 'click'];

  activityEvents.forEach((event) => {
    document.removeEventListener(event, resetInactivityTimer);
  });

  if (inactivityTimeout) {
    clearTimeout(inactivityTimeout);
  }
};
