// src/services/adminChatApi.js
// API service for admin chat dashboard functionality

import apiClient from "../api/apiClient";

const ADMIN_CHAT_BASE_URL =
  import.meta.env.MODE === "development"
    ? "https://localhost:5001/api/admin/chats"
    : window.location.origin + "/api/admin/chats";

export const adminChatApi = {
  /**
   * دریافت لیست تمام چت‌ها با فیلترهای قوی
   * @param {Object} filters - فیلترهای جستجو
   * @param {string} filters.searchTerm - عبارت جستجو
   * @param {number} filters.chatRoomType - نوع چت (0: UserToUser, 1: Support, 2: Group)
   * @param {number} filters.regionId - شناسه ناحیه
   * @param {Date} filters.createdFrom - تاریخ شروع ایجاد
   * @param {Date} filters.createdTo - تاریخ پایان ایجاد
   * @param {boolean} filters.isDeleted - آیا حذف شده است؟
   * @param {boolean} filters.isGroup - آیا گروهی است؟
   * @param {number} filters.minMembersCount - حداقل تعداد اعضا
   * @param {number} filters.maxMembersCount - حداکثر تعداد اعضا
   * @param {number} filters.minMessagesCount - حداقل تعداد پیام‌ها
   * @param {number} filters.maxMessagesCount - حداکثر تعداد پیام‌ها
   * @param {Date} filters.lastActivityFrom - تاریخ شروع آخرین فعالیت
   * @param {Date} filters.lastActivityTo - تاریخ پایان آخرین فعالیت
   * @param {string} filters.sortBy - مرتب‌سازی بر اساس ("CreatedAt", "LastActivity", "MessagesCount", "MembersCount", "Name")
   * @param {boolean} filters.isDescending - نزولی بودن مرتب‌سازی
   * @param {number} filters.pageNumber - شماره صفحه
   * @param {number} filters.pageSize - تعداد آیتم در هر صفحه
   * @returns {Promise} لیست چت‌ها به همراه اطلاعات صفحه‌بندی
   */
  getAllChats: async (filters = {}) => {
    const params = new URLSearchParams();

    // افزودن فیلترها به query string - فقط اگر مقدار معتبر داشته باشند
    if (filters.searchTerm) params.append("searchTerm", filters.searchTerm);
    if (filters.chatRoomType !== undefined && filters.chatRoomType !== null)
      params.append("chatRoomType", filters.chatRoomType);
    if (filters.regionId) params.append("regionId", filters.regionId);
    if (filters.createdFrom)
      params.append("createdFrom", filters.createdFrom.toISOString());
    if (filters.createdTo)
      params.append("createdTo", filters.createdTo.toISOString());
    // فقط اگر boolean واقعی باشد (نه null)
    if (filters.isDeleted !== undefined && filters.isDeleted !== null)
      params.append("isDeleted", filters.isDeleted);
    if (filters.isGroup !== undefined && filters.isGroup !== null)
      params.append("isGroup", filters.isGroup);
    if (filters.minMembersCount)
      params.append("minMembersCount", filters.minMembersCount);
    if (filters.maxMembersCount)
      params.append("maxMembersCount", filters.maxMembersCount);
    if (filters.minMessagesCount)
      params.append("minMessagesCount", filters.minMessagesCount);
    if (filters.maxMessagesCount)
      params.append("maxMessagesCount", filters.maxMessagesCount);
    if (filters.lastActivityFrom)
      params.append("lastActivityFrom", filters.lastActivityFrom.toISOString());
    if (filters.lastActivityTo)
      params.append("lastActivityTo", filters.lastActivityTo.toISOString());
    if (filters.sortBy) params.append("sortBy", filters.sortBy);
    if (filters.isDescending !== undefined)
      params.append("isDescending", filters.isDescending);

    params.append("pageNumber", filters.pageNumber || 1);
    params.append("pageSize", filters.pageSize || 20);

    const response = await apiClient.get(
      `${ADMIN_CHAT_BASE_URL}?${params.toString()}`
    );
    return response.data;
  },

  /**
   * دریافت آمار کلی چت‌ها
   * @returns {Promise} آمار کلی شامل تعداد چت‌ها، پیام‌ها، کاربران و...
   */
  getChatStats: async () => {
    const response = await apiClient.get(`${ADMIN_CHAT_BASE_URL}/stats`);
    return response.data;
  },

  /**
   * دریافت پیام‌های یک چت خاص
   * @param {number} chatRoomId - شناسه چت
   * @param {Object} filters - فیلترهای جستجو در پیام‌ها
   * @param {number} filters.pageNumber - شماره صفحه
   * @param {number} filters.pageSize - تعداد پیام در هر صفحه
   * @param {string} filters.searchTerm - جستجو در محتوای پیام
   * @param {number} filters.senderId - شناسه فرستنده
   * @param {Date} filters.fromDate - از تاریخ
   * @param {Date} filters.toDate - تا تاریخ
   * @returns {Promise} لیست پیام‌ها به همراه اطلاعات صفحه‌بندی
   */
  getChatMessages: async (chatRoomId, filters = {}) => {
    const params = new URLSearchParams();

    params.append("pageNumber", filters.pageNumber || 1);
    params.append("pageSize", filters.pageSize || 50);

    if (filters.searchTerm) params.append("searchTerm", filters.searchTerm);
    if (filters.senderId) params.append("senderId", filters.senderId);
    if (filters.fromDate)
      params.append("fromDate", filters.fromDate.toISOString());
    if (filters.toDate) params.append("toDate", filters.toDate.toISOString());
    if (filters.isDeleted !== undefined && filters.isDeleted !== null)
      params.append("isDeleted", filters.isDeleted);

    const response = await apiClient.get(
      `${ADMIN_CHAT_BASE_URL}/${chatRoomId}/messages?${params.toString()}`
    );
    return response.data;
  },
};
