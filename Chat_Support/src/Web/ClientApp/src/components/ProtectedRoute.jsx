// src/components/ProtectedRoute.jsx
import React from "react";
import { useLocation } from "react-router-dom";
import { useAuth } from "../hooks/useAuth";

const ProtectedRoute = ({ children }) => {
  const { isAuthenticated, isLoading } = useAuth();
  const location = useLocation();

  if (isLoading) {
    return <div>Loading...</div>;
  }

  if (!isAuthenticated) {
    // کاربر را به صفحه لاگین هدایت کن و مسیر فعلی را به عنوان returnUrl ارسال کن تا بعد از لاگین به همان‌جا برگردد
    const fullPath = `${location.pathname}${location.search}${location.hash}`;
    const loginUrl = `/login?returnUrl=${encodeURIComponent(fullPath)}`;
    window.location.href = loginUrl;
    return null; // در حین ریدایرکت چیزی رندر نمی‌شود
  }

  return children;
};

export default ProtectedRoute;
