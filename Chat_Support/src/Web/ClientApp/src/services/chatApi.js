// src/services/chatApi.js
// API service for chat functionality

import apiClient from "../api/apiClient";

const CHAT_BASE_URL =
  import.meta.env.MODE === "development"
    ? "https://localhost:5001/api/chat"
    : window.location.origin + "/api/chat";
const SUPPORT_BASE_URL =
  import.meta.env.MODE === "development"
    ? "https://localhost:5001/api/support"
    : window.location.origin + "/api/support";

export const MessageType = {
  Text: 0,
  Image: 1,
  File: 2,
  Audio: 3,
  Video: 4,
  System: 5,
  Voice: 6,
};

export const chatApi = {
  // Get user's chat rooms
  getChatRooms: async () => {
    const response = await apiClient.get(`${CHAT_BASE_URL}/rooms`);
    return response.data;
  },

  // Get messages for a chat room
  getChatMessages: async (roomId, page = 1, pageSize = 20) => {
    const response = await apiClient.get(
      `${CHAT_BASE_URL}/rooms/${roomId}/messages`,
      {
        params: { page, pageSize },
      }
    );
    return response.data;
  },

  // Create new chat room
  createChatRoom: async (roomData) => {
    const response = await apiClient.post(`${CHAT_BASE_URL}/rooms`, roomData);
    return response.data;
  },

  // Send message
  sendMessage: async (roomId, messageData) => {
    const response = await apiClient.post(
      `${CHAT_BASE_URL}/rooms/${roomId}/messages`,
      messageData
    );
    return response.data;
  },

  // Upload file with progress tracking
  uploadFile: async (file, chatRoomId, type, onProgress) => {
    const formData = new FormData();
    formData.append("file", file);
    formData.append("chatRoomId", chatRoomId);
    formData.append("type", type);

    const response = await apiClient.post(`${CHAT_BASE_URL}/upload`, formData, {
      headers: {
        "Content-Type": "multipart/form-data",
      },
      onUploadProgress: (progressEvent) => {
        if (onProgress && progressEvent.total) {
          const percentCompleted = Math.round(
            (progressEvent.loaded * 100) / progressEvent.total
          );
          onProgress(percentCompleted);
        }
      },
    });

    return response.data;
  },

  // Get file download URL
  getFileUrl: (fileUrl) => {
    // If fileUrl is already absolute, return as is
    if (fileUrl.startsWith("http")) return fileUrl;
    // Otherwise, prepend base URL
    return `${apiClient.defaults.baseURL}${fileUrl}`;
  },

  // Join chat room
  joinChatRoom: async (roomId) => {
    const response = await apiClient.post(
      `${CHAT_BASE_URL}/rooms/${roomId}/join`
    );
    return response.data;
  },

  // Leave chat room
  leaveChatRoom: async (roomId) => {
    const response = await apiClient.delete(
      `${CHAT_BASE_URL}/rooms/${roomId}/leave`
    );
    return response.data;
  },

  // Get online users
  getOnlineUsers: async () => {
    const response = await apiClient.get(`${CHAT_BASE_URL}/users/online`);
    return response.data;
  },

  // Search users
  searchUsers: async (query) => {
    const response = await apiClient.get(`${CHAT_BASE_URL}/users/search`, {
      params: { query },
    });
    return response.data;
  },

  // Get chat room members
  getChatRoomMembers: async (roomId) => {
    const response = await apiClient.get(
      `${CHAT_BASE_URL}/rooms/${roomId}/members`
    );
    return response.data;
  },

  editMessage: async (messageId, newContent) => {
    const response = await apiClient.put(
      `${CHAT_BASE_URL}/messages/${messageId}`,
      { newContent }
    );
    return response.data;
  },

  deleteMessage: async (messageId) => {
    const response = await apiClient.delete(
      `${CHAT_BASE_URL}/messages/${messageId}`
    );
    return response.data;
  },

  reactToMessage: async (messageId, reactionData) => {
    // reactionData: { emoji: string }
    const response = await apiClient.post(
      `${CHAT_BASE_URL}/messages/${messageId}/react`,
      reactionData
    );
    return response.data; // This should be MessageReactionDto
  },

  forwardMessage: async (originalMessageId, targetChatRoomId) => {
    const response = await apiClient.post(`${CHAT_BASE_URL}/messages/forward`, {
      originalMessageId,
      targetChatRoomId,
    });
    return response.data; // Should be ChatMessageDto of the new forwarded message
  },

  // Group management
  addGroupMembers: async (roomId, userIds) => {
    const response = await apiClient.post(
      `${CHAT_BASE_URL}/rooms/${roomId}/members/add`,
      { userIds }
    );
    return response.data;
  },

  removeGroupMember: async (roomId, userId) => {
    const response = await apiClient.delete(
      `${CHAT_BASE_URL}/rooms/${roomId}/members/${userId}`
    );
    return response.data;
  },

  deleteChatRoom: async (roomId) => {
    const response = await apiClient.delete(`${CHAT_BASE_URL}/rooms/${roomId}`);
    return response.data;
  },

  softDeletePersonalChat: async (roomId) => {
    const response = await apiClient.put(
      `${CHAT_BASE_URL}/rooms/${roomId}/soft-delete`
    );
    return response.data;
  },

  // Support API
  getSupportTickets: async () => {
    const response = await apiClient.get(`${SUPPORT_BASE_URL}/tickets`);
    return response.data;
  },
  getAvailableAgents: async (regionId) => {
    const response = await apiClient.get(
      `${SUPPORT_BASE_URL}/agents/available`,
      { params: { regionId } }
    );
    return response.data;
  },
  transferTicket: async (ticketId, newAgentId, reason) => {
    const response = await apiClient.post(
      `${SUPPORT_BASE_URL}/tickets/${ticketId}/transfer`,
      { newAgentId, reason }
    );
    return response.data;
  },
  closeTicket: async (ticketId, reason) => {
    const response = await apiClient.post(
      `${SUPPORT_BASE_URL}/tickets/${ticketId}/close`,
      { reason }
    );
    return response.data;
  },
  getIsAgent: async () => {
    const response = await apiClient.get(`${SUPPORT_BASE_URL}/is-agent`);
    return response.data; // { isAgent: boolean }
  },
};

// Helper functions for file handling
export const fileHelpers = {
  // Get message type based on file
  getMessageTypeFromFile: (file) => {
    const mimeType = file.type.toLowerCase();

    if (mimeType.startsWith("image/")) return MessageType.Image;
    if (mimeType.startsWith("video/")) return MessageType.Video;
    if (mimeType.startsWith("audio/")) return MessageType.Audio;

    return MessageType.File;
  },

  // Validate file
  validateFile: (file, maxSizeMB = 20) => {
    const maxSize = maxSizeMB * 1024 * 1024;

    if (file.size > maxSize) {
      throw new Error(`حجم فایل نباید بیشتر از ${maxSizeMB} مگابایت باشد`);
    }

    // Check for dangerous extensions
    const dangerousExtensions = [
      ".exe",
      ".bat",
      ".cmd",
      ".com",
      ".scr",
      ".vbs",
      ".js",
    ];
    const fileName = file.name.toLowerCase();

    if (dangerousExtensions.some((ext) => fileName.endsWith(ext))) {
      throw new Error("نوع فایل مجاز نیست");
    }

    return true;
  },

  // Format file size
  formatFileSize: (bytes) => {
    if (bytes === 0) return "0 Bytes";

    const k = 1024;
    const sizes = ["Bytes", "KB", "MB", "GB"];
    const i = Math.floor(Math.log(bytes) / Math.log(k));

    return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + " " + sizes[i];
  },

  // Get file icon based on type
  getFileIcon: (fileName, fileType) => {
    const extension = fileName.split(".").pop()?.toLowerCase();
    const mimeType = fileType?.toLowerCase();

    // By MIME type
    if (mimeType) {
      if (mimeType.startsWith("image/")) return "image";
      if (mimeType.startsWith("video/")) return "video";
      if (mimeType.startsWith("audio/")) return "audio";
      if (mimeType.includes("pdf")) return "pdf";
      if (mimeType.includes("word") || mimeType.includes("document"))
        return "word";
      if (mimeType.includes("excel") || mimeType.includes("spreadsheet"))
        return "excel";
      if (mimeType.includes("powerpoint") || mimeType.includes("presentation"))
        return "powerpoint";
      if (mimeType.includes("zip") || mimeType.includes("compressed"))
        return "archive";
    }

    // By extension
    switch (extension) {
      case "jpg":
      case "jpeg":
      case "png":
      case "gif":
      case "webp":
      case "svg":
        return "image";

      case "mp4":
      case "avi":
      case "mov":
      case "wmv":
      case "flv":
      case "webm":
        return "video";

      case "mp3":
      case "wav":
      case "ogg":
      case "m4a":
      case "aac":
        return "audio";

      case "pdf":
        return "pdf";

      case "doc":
      case "docx":
        return "word";

      case "xls":
      case "xlsx":
        return "excel";

      case "ppt":
      case "pptx":
        return "powerpoint";

      case "zip":
      case "rar":
      case "7z":
      case "tar":
      case "gz":
        return "archive";

      default:
        return "file";
    }
  },
};
