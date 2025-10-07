// src/context/AuthContext.jsx

import React, { useState, useEffect, useMemo } from "react";
import apiClient from "../api/apiClient";
import { AuthContext } from "./AuthContextCore";
import { getActiveAccount, getAccounts, setActiveAccount, removeAccount } from "../Utils/accounts";

export const AuthProvider = ({ children }) => {
  const [user, setUser] = useState(null);
  const [isAuthenticated, setIsAuthenticated] = useState(false);
  const [isLoading, setIsLoading] = useState(true);

  useEffect(() => {
    const initializeAuth = async () => {
      // If there is an active account, mirror its tokens
      const active = getActiveAccount();
      if (active) {
        localStorage.setItem("token", active.token || "");
        localStorage.setItem("refreshToken", active.refreshToken || "");
      }

      const urlParams = new URLSearchParams(window.location.search);
      const tokenFromUrl = urlParams.get("token");
      let token = tokenFromUrl || localStorage.getItem("token");

      if (token) {
        if (tokenFromUrl) {
          localStorage.setItem("token", tokenFromUrl);
          // آدرس را تمیز می‌کنیم تا توکن در URL باقی نماند
          window.history.replaceState(
            {},
            document.title,
            window.location.pathname
          );
        }

        try {
          const response = await apiClient.get("/auth/profile");
          setUser(response.data);
          setIsAuthenticated(true);
        } catch (error) {
          console.error("Authentication failed:", error);
          localStorage.removeItem("token");
          setIsAuthenticated(false);
          setUser(null);
        }
      }
      setIsLoading(false);
    };
    initializeAuth();
  }, []);

  const logout = () => {
    // حذف حساب فعال از لیست و سوییچ به بعدی (در صورت وجود)
    const active = getActiveAccount();
    if (active) {
      removeAccount(active.id);
      const left = getAccounts();
      if (left && left.length > 0) {
        // setActiveAccount happens in removeAccount when choosing next
        // Mirror tokens already updated, فقط رفرش برای همگام‌سازی کامل
        setUser(null);
        setIsAuthenticated(false);
        window.location.replace("/chat");
        return;
      }
    }
    // اگر هیچ حسابی نماند
    localStorage.removeItem("token");
    localStorage.removeItem("refreshToken");
    setUser(null);
    setIsAuthenticated(false);
    window.location.replace("/login");
  };

  const switchAccount = async (userId) => {
    const ok = setActiveAccount(userId);
    if (ok) {
      // Reload to re-init contexts and SignalR under new identity cleanly
      window.location.reload();
    }
  };

  const accounts = useMemo(() => getAccounts(), [isAuthenticated]);
  const activeAccountId = useMemo(() => getActiveAccount()?.id ?? null, [isAuthenticated]);

  return (
    <AuthContext.Provider value={{ isAuthenticated, user, isLoading, logout, accounts, activeAccountId, switchAccount }}>
      {children}
    </AuthContext.Provider>
  );
};
