// src/Utils/accounts.js
// Helpers for managing multiple authenticated accounts (tokens) in localStorage

import { parseJwt } from "./jwt";

const LS_KEYS = {
  ACCOUNTS: "chat_accounts", // array of accounts
  ACTIVE_ID: "activeAccountId", // current selected account userId (string)
  ACCESS: "token", // mirrored for compatibility
  REFRESH: "refreshToken", // mirrored for compatibility
};

function readJson(key, fallback) {
  try {
    const raw = localStorage.getItem(key);
    return raw ? JSON.parse(raw) : fallback;
  } catch {
    return fallback;
  }
}

function writeJson(key, value) {
  localStorage.setItem(key, JSON.stringify(value));
}

export function getAccounts() {
  return readJson(LS_KEYS.ACCOUNTS, []);
}

export function getActiveAccountId() {
  return localStorage.getItem(LS_KEYS.ACTIVE_ID) || null;
}

export function getActiveAccount() {
  const id = getActiveAccountId();
  const accounts = getAccounts();
  if (!id) return null;
  return accounts.find((a) => String(a.id) === String(id)) || null;
}

export function setActiveAccount(userId) {
  const accounts = getAccounts();
  const acc = accounts.find((a) => String(a.id) === String(userId));
  if (!acc) return false;

  localStorage.setItem(LS_KEYS.ACTIVE_ID, String(acc.id));
  // Mirror tokens for existing code (apiClient interceptors, ChatContext, etc.)
  localStorage.setItem(LS_KEYS.ACCESS, acc.token || "");
  localStorage.setItem(LS_KEYS.REFRESH, acc.refreshToken || "");
  return true;
}

export function addOrUpdateAccount(accessToken, refreshToken) {
  if (!accessToken || !refreshToken) return null;
  const payload = parseJwt(accessToken) || {};
  const id = payload?.sub ? String(payload.sub) : null;
  if (!id) return null;

  const userName = payload?.unique_name || payload?.name || payload?.UserName || payload?.username || "";
  const firstName = payload?.firstname || payload?.firstName || "";
  const lastName = payload?.lastname || payload?.lastName || "";
  const fullName = `${firstName} ${lastName}`.trim() || userName || `User ${id}`;

  const now = new Date().toISOString();
  const accounts = getAccounts();
  const existingIndex = accounts.findIndex((a) => String(a.id) === String(id));
  const account = {
    id,
    userName,
    fullName,
    token: accessToken,
    refreshToken,
    updatedAt: now,
    addedAt: existingIndex >= 0 ? accounts[existingIndex].addedAt : now,
  };

  if (existingIndex >= 0) {
    accounts[existingIndex] = account;
  } else {
    accounts.push(account);
  }
  writeJson(LS_KEYS.ACCOUNTS, accounts);

  // Set as active and mirror tokens
  setActiveAccount(id);
  return account;
}

export function updateActiveAccountTokens(newAccessToken, newRefreshToken) {
  const id = getActiveAccountId();
  if (!id) return;
  const accounts = getAccounts();
  const idx = accounts.findIndex((a) => String(a.id) === String(id));
  if (idx === -1) return;
  const now = new Date().toISOString();
  accounts[idx] = {
    ...accounts[idx],
    token: newAccessToken || accounts[idx].token,
    refreshToken: newRefreshToken || accounts[idx].refreshToken,
    updatedAt: now,
  };
  writeJson(LS_KEYS.ACCOUNTS, accounts);
  // mirror
  if (newAccessToken) localStorage.setItem(LS_KEYS.ACCESS, newAccessToken);
  if (newRefreshToken) localStorage.setItem(LS_KEYS.REFRESH, newRefreshToken);
}

export function removeAccount(userId) {
  const accounts = getAccounts();
  const next = accounts.filter((a) => String(a.id) !== String(userId));
  writeJson(LS_KEYS.ACCOUNTS, next);

  const activeId = getActiveAccountId();
  if (String(activeId) === String(userId)) {
    // Choose first remaining or clear tokens
    if (next.length > 0) {
      setActiveAccount(next[0].id);
    } else {
      localStorage.removeItem(LS_KEYS.ACTIVE_ID);
      localStorage.removeItem(LS_KEYS.ACCESS);
      localStorage.removeItem(LS_KEYS.REFRESH);
    }
  }
}

export function clearAllAccounts() {
  localStorage.removeItem(LS_KEYS.ACCOUNTS);
  localStorage.removeItem(LS_KEYS.ACTIVE_ID);
  localStorage.removeItem(LS_KEYS.ACCESS);
  localStorage.removeItem(LS_KEYS.REFRESH);
}
