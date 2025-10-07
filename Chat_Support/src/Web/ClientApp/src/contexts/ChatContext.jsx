import React, { useReducer, useEffect, useCallback } from "react";
import { chatApi } from "../services/chatApi";
import signalRService from "../services/signalRService";
import { MessageType, ReadStatus } from "../types/chat";
import { getUserIdFromToken } from "../utils/jwt";
import {
  ChatContext,
  ActionTypes,
  initialState,
  MessageDeliveryStatus,
} from "./ChatContextCore";

// Reducer
function chatReducer(state, action) {
  const sortedRooms =
    action.payload && Array.isArray(action.payload)
      ? [...action.payload].sort((a, b) => {
          const timeA = new Date(a.lastMessageTime || a.createdAt).getTime();
          const timeB = new Date(b.lastMessageTime || b.createdAt).getTime();
          return timeB - timeA;
        })
      : [];

  switch (action.type) {
    case ActionTypes.SET_CURRENT_USER:
      return { ...state, currentLoggedInUserId: action.payload };
    case ActionTypes.SET_LOADING:
      return { ...state, isLoading: action.payload };
    case ActionTypes.SET_LOADING_MESSAGES:
      return { ...state, isLoadingMessages: action.payload };
    case ActionTypes.SET_ERROR:
      return {
        ...state,
        error: action.payload,
        isLoading: false,
        isLoadingMessages: false,
      };
    case ActionTypes.SET_CONNECTION_STATUS:
      return { ...state, isConnected: action.payload };

    case ActionTypes.SET_ROOMS:
      return { ...state, rooms: sortedRooms };

    case ActionTypes.UPSERT_CHAT_ROOM: {
      const updatedRoomData = action.payload;
      const roomExists = state.rooms.some((room) => room.id === updatedRoomData.id);
      let newRoomsArray;
      if (roomExists) {
        newRoomsArray = state.rooms.map((room) =>
          room.id === updatedRoomData.id ? { ...room, ...updatedRoomData } : room
        );
      } else {
        newRoomsArray = [...state.rooms, updatedRoomData];
      }
      newRoomsArray.sort((a, b) => {
        const timeA = new Date(a.lastMessageTime || a.createdAt).getTime();
        const timeB = new Date(b.lastMessageTime || b.createdAt).getTime();
        return timeB - timeA;
      });
      return { ...state, rooms: newRoomsArray };
    }

    case ActionTypes.SET_CURRENT_ROOM:
      return { ...state, currentRoom: action.payload };

    case ActionTypes.SET_MESSAGES: {
      const { roomId, messages } = action.payload;
      return {
        ...state,
        messages: {
          ...state.messages,
          [roomId]: { items: messages },
        },
      };
    }
    case ActionTypes.PREPEND_MESSAGES: {
      const { roomId, messages } = action.payload;
      const existingRoomMessages = state.messages[roomId]?.items || [];
      return {
        ...state,
        messages: {
          ...state.messages,
          [roomId]: {
            items: [...messages, ...existingRoomMessages],
          },
        },
      };
    }

    case ActionTypes.UPDATE_MESSAGE_READ_STATUS: {
      const { messageId, readByUserId, roomIdToUpdate } = action.payload;
      if (!roomIdToUpdate || !state.messages[roomIdToUpdate]) return state;

      const updatedMessagesForRoom = state.messages[roomIdToUpdate].items.map((msg) =>
        msg.id === messageId
          ? { ...msg, readStatus: ReadStatus.Read, readBy: [...(msg.readBy || []), readByUserId] }
          : msg
      );
      return {
        ...state,
        messages: {
          ...state.messages,
          [roomIdToUpdate]: { ...state.messages[roomIdToUpdate], items: updatedMessagesForRoom },
        },
      };
    }

    case ActionTypes.SET_ONLINE_USERS:
      return {
        ...state,
        onlineUsers: action.payload
          .filter((user) => user.id !== state.currentLoggedInUserId)
          .filter((user, index, self) => index === self.findIndex((u) => u.id === user.id)),
        isLoading: false,
      };

    case ActionTypes.UPDATE_USER_ONLINE_STATUS: {
      const { userId, isOnline, user } = action.payload;
      if (userId === state.currentLoggedInUserId) return state;

      if (isOnline) {
        const userExists = state.onlineUsers.some((u) => u.id === userId);
        if (!userExists && user) {
          return {
            ...state,
            onlineUsers: [...state.onlineUsers, user].filter(
              (u, index, self) => index === self.findIndex((i) => i.id === u.id)
            ),
          };
        }
      } else {
        return { ...state, onlineUsers: state.onlineUsers.filter((u) => u.id !== userId) };
      }
      return state;
    }

    case ActionTypes.UPDATE_TYPING_STATUS: {
      const { chatRoomId, userId: typingUserId, userFullName, isTyping } = action.payload;
      const currentTypingInRoom = state.typingUsers[chatRoomId] || [];
      let newTypingUsersInRoom;

      if (isTyping) {
        if (!currentTypingInRoom.some((u) => u.userId === typingUserId)) {
          newTypingUsersInRoom = [...currentTypingInRoom, { userId: typingUserId, userFullName, isTyping: true }];
        } else {
          newTypingUsersInRoom = currentTypingInRoom;
        }
      } else {
        newTypingUsersInRoom = currentTypingInRoom.filter((u) => u.userId !== typingUserId);
      }
      return { ...state, typingUsers: { ...state.typingUsers, [chatRoomId]: newTypingUsersInRoom } };
    }

    case ActionTypes.ADD_MESSAGE: {
      const newMessage = action.payload;
      const roomId = newMessage.chatRoomId;
      const currentRoomMessages = state.messages[roomId]?.items || [];
      const currentRoomInfo = state.messages[roomId] || { items: [] };

      // De-duplicate by id
      const alreadyExists = newMessage?.id && currentRoomMessages.some((m) => m.id === newMessage.id);
      if (alreadyExists) {
        // Only update preview
        return {
          ...state,
          rooms: state.rooms
            .map((r) =>
              r.id === roomId
                ? {
                    ...r,
                    lastMessageContent: newMessage.content,
                    lastMessageTime: newMessage.created || new Date().toISOString(),
                  }
                : r
            )
            .sort((a, b) => new Date(b.lastMessageTime || b.createdAt) - new Date(a.lastMessageTime || a.createdAt)),
        };
      }

      const messageWithInitialReadState = {
        ...newMessage,
        deliveryStatus: MessageDeliveryStatus.Sent,
        isReadByMe: Number(newMessage.senderId) === Number(state.currentLoggedInUserId),
      };

      let nextState = {
        ...state,
        messages: {
          ...state.messages,
          [roomId]: { ...currentRoomInfo, items: [...currentRoomMessages, messageWithInitialReadState] },
        },
      };

      const isIncoming = Number(newMessage.senderId) !== Number(state.currentLoggedInUserId);
      const isActiveRoom = state.currentRoom?.id === roomId;

      nextState.rooms = state.rooms
        .map((r) => {
          if (r.id !== roomId) return r;
          const unread = isIncoming && !isActiveRoom ? (r.unreadCount || 0) + 1 : r.unreadCount || 0;
          return {
            ...r,
            lastMessageContent: newMessage.content,
            lastMessageTime: newMessage.created || new Date().toISOString(),
            unreadCount: unread,
          };
        })
        .sort((a, b) => {
          const timeA = new Date(a.lastMessageTime || a.createdAt).getTime();
          const timeB = new Date(b.lastMessageTime || b.createdAt).getTime();
          return timeB - timeA;
        });

      return nextState;
    }

    case ActionTypes.UPDATE_MESSAGE_READ_STATUS_FOR_SENDER: {
      const payload = action.payload;
      const chatRoomId = payload.chatRoomId ?? payload.ChatRoomId;
      const messageId = payload.messageId ?? payload.MessageId;
      if (!chatRoomId || !state.messages[chatRoomId]) return state;

      const updatedMessagesForRoom = state.messages[chatRoomId].items.map((msg) =>
        msg.id === messageId && Number(msg.senderId) === Number(state.currentLoggedInUserId) ? { ...msg, deliveryStatus: MessageDeliveryStatus.Read } : msg
      );
      return {
        ...state,
        messages: {
          ...state.messages,
          [chatRoomId]: { ...state.messages[chatRoomId], items: updatedMessagesForRoom },
        },
      };
    }

    case ActionTypes.UPDATE_MESSAGE_DELIVERED_STATUS_FOR_SENDER: {
      const payload = action.payload;
      const chatRoomId = payload.chatRoomId ?? payload.ChatRoomId;
      const messageId = payload.messageId ?? payload.MessageId;
      if (!chatRoomId || !state.messages[chatRoomId]) return state;
      const updatedMessagesForRoom = state.messages[chatRoomId].items.map((msg) => {
        if (msg.id === messageId && Number(msg.senderId) === Number(state.currentLoggedInUserId)) {
          const current = msg.deliveryStatus ?? MessageDeliveryStatus.Sent;
          // Only upgrade Sent -> Delivered. Do not downgrade if it's already Delivered or Read.
          const next = current === MessageDeliveryStatus.Sent ? MessageDeliveryStatus.Delivered : current;
          return { ...msg, deliveryStatus: next };
        }
        return msg;
      });
      return {
        ...state,
        messages: {
          ...state.messages,
          [chatRoomId]: { ...state.messages[chatRoomId], items: updatedMessagesForRoom },
        },
      };
    }

    case ActionTypes.UPDATE_MESSAGE_AS_READ_FOR_RECEIVER: {
      const { messageId, chatRoomId } = action.payload;
      if (!chatRoomId || !state.messages[chatRoomId]) return state;

      const updatedMessagesForRoom = state.messages[chatRoomId].items.map((msg) =>
        msg.id === messageId && Number(msg.senderId) !== Number(state.currentLoggedInUserId) ? { ...msg, isReadByMe: true } : msg
      );
      return {
        ...state,
        messages: {
          ...state.messages,
          [chatRoomId]: { ...state.messages[chatRoomId], items: updatedMessagesForRoom },
        },
      };
    }

    case ActionTypes.MARK_ALL_MESSAGES_AS_READ_IN_ROOM: {
      const { roomId, userId } = action.payload;
      if (!roomId || !state.messages[roomId]) return state;

      let unreadCountChanged = false;
      const updatedMessages = state.messages[roomId].items.map((msg) => {
        if (Number(msg.senderId) !== Number(userId) && !msg.isReadByMe) {
          unreadCountChanged = true;
          return { ...msg, isReadByMe: true };
        }
        return msg;
      });

      const updatedRooms = unreadCountChanged
        ? state.rooms
            .map((r) => (r.id === roomId ? { ...r, unreadCount: 0 } : r))
            .sort((a, b) => new Date(b.lastMessageTime || b.createdAt) - new Date(a.lastMessageTime || a.createdAt))
        : state.rooms;

      return {
        ...state,
        messages: { ...state.messages, [roomId]: { ...state.messages[roomId], items: updatedMessages } },
        rooms: updatedRooms,
      };
    }

    case ActionTypes.EDIT_MESSAGE_SUCCESS: {
      const updatedMessage = action.payload;
      const roomId = updatedMessage.chatRoomId;
      if (!state.messages[roomId]) return state;

      return {
        ...state,
        messages: {
          ...state.messages,
          [roomId]: {
            ...state.messages[roomId],
            items: state.messages[roomId].items.map((msg) => (msg.id === updatedMessage.id ? updatedMessage : msg)),
          },
        },
      };
    }

    case ActionTypes.DELETE_MESSAGE_SUCCESS: {
      const { messageId, roomId } = action.payload;
      if (!state.messages[roomId]) return state;

      const updatedMessages = state.messages[roomId].items.map((msg) =>
        msg.id === messageId ? { ...msg, content: "[پیام حذف شد]", isDeleted: true, attachmentUrl: null } : msg
      );

      return {
        ...state,
        messages: { ...state.messages, [roomId]: { ...state.messages[roomId], items: updatedMessages } },
      };
    }

    case ActionTypes.UPDATE_UNREAD_COUNT: {
      const { roomId, unreadCount } = action.payload;
      return { ...state, rooms: state.rooms.map((room) => (room.id === roomId ? { ...room, unreadCount } : room)) };
    }
    case ActionTypes.SET_REPLYING_TO_MESSAGE:
      return { ...state, replyingToMessage: action.payload };
    case ActionTypes.CLEAR_REPLYING_TO_MESSAGE:
      return { ...state, replyingToMessage: null };

    case ActionTypes.SET_EDITING_MESSAGE:
      return { ...state, editingMessage: action.payload };
    case ActionTypes.CLEAR_EDITING_MESSAGE:
      return { ...state, editingMessage: null };

    case ActionTypes.SET_FORWARDING_MESSAGE:
      return { ...state, forwardingMessage: action.payload };
    case ActionTypes.CLEAR_FORWARDING_MESSAGE:
      return { ...state, forwardingMessage: null };

    case ActionTypes.MESSAGE_REACTION_SUCCESS: {
      const { messageId, userId, userName, emoji, chatRoomId, isRemoved } = action.payload;
      if (!state.messages[chatRoomId]) return state;

      return {
        ...state,
        messages: {
          ...state.messages,
          [chatRoomId]: {
            ...state.messages[chatRoomId],
            items: state.messages[chatRoomId].items.map((msg) => {
              if (msg.id === messageId) {
                let newReactionsList = [...(msg.reactions || [])];
                const reactionIndex = newReactionsList.findIndex((r) => r.userId === userId && r.emoji === emoji);

                if (isRemoved) {
                  if (reactionIndex > -1) newReactionsList.splice(reactionIndex, 1);
                } else {
                  newReactionsList = newReactionsList.filter((r) => r.userId !== userId);
                  newReactionsList.push({ emoji, userId, userName });
                }
                return { ...msg, reactions: newReactionsList };
              }
              return msg;
            }),
          },
        },
      };
    }

    case ActionTypes.SHOW_FORWARD_MODAL:
      return { ...state, isForwardModalVisible: true, messageIdToForward: action.payload };

    case ActionTypes.HIDE_FORWARD_MODAL:
      return { ...state, isForwardModalVisible: false, messageIdToForward: null, error: null };

    default:
      return state;
  }
}

// Provider component
export const ChatProvider = ({ children }) => {
  const [state, dispatch] = useReducer(chatReducer, {
    ...initialState,
    currentLoggedInUserId: getUserIdFromToken(localStorage.getItem("token")),
  });

  // React to token/account changes and ensure connection is under the right account
  useEffect(() => {
    const token = localStorage.getItem("token");
    const uid = getUserIdFromToken(token);
    if (uid && uid !== state.currentLoggedInUserId) {
      dispatch({ type: ActionTypes.SET_CURRENT_USER, payload: uid });
    }
    if (token) {
      if (!signalRService.getConnectionStatus()) {
        signalRService.startConnection(token).then((connected) => {
          dispatch({ type: ActionTypes.SET_CONNECTION_STATUS, payload: connected });
        });
      }
    } else {
      // no token, stop connection if any
      if (signalRService.getConnectionStatus()) {
        signalRService.stopConnection();
      }
    }
  }, [state.currentLoggedInUserId]);

  // --- Auto reconnect logic ---
  React.useEffect(() => {
    let reconnectTimer = null;
    let tryCount = 0;
    const isLoginRoute = (() => {
      try {
        const p = window.location.pathname.toLowerCase();
        return p.startsWith("/login") || p.startsWith("/home/login");
      } catch {
        return false;
      }
    })();

    if (!state.isConnected && !signalRService.getConnectionStatus()) {
      reconnectTimer = setInterval(() => {
        const token = localStorage.getItem("token");
        if (token && !signalRService.getConnectionStatus()) {
          signalRService.startConnection(token);
        }
        tryCount++;
        if (tryCount >= 3) {
          // اگر توکن نداریم یا در صفحه لاگین هستیم، ریفرش نکن
          if (!token || isLoginRoute) {
            clearInterval(reconnectTimer);
            reconnectTimer = null;
            return;
          }
          window.location.reload();
        }
      }, 3000);
    }
    return () => {
      if (reconnectTimer) clearInterval(reconnectTimer);
    };
  }, [state.isConnected]);

  // Initialize SignalR connection and event handlers
  useEffect(() => {
    const token = localStorage.getItem("token");
    if (token && !signalRService.getConnectionStatus()) {
      const userId = getUserIdFromToken(token);
      if (userId !== state.currentLoggedInUserId) {
        dispatch({ type: ActionTypes.SET_CURRENT_USER, payload: userId });
      }

      signalRService.startConnection(token).then((connected) => {
        dispatch({ type: ActionTypes.SET_CONNECTION_STATUS, payload: connected });
      });
    }

    const handleConnectionStatusChanged = (isConnected) => {
      dispatch({ type: ActionTypes.SET_CONNECTION_STATUS, payload: isConnected });
    };

    const handleMessageReceived = (message) => {
      // Immediately acknowledge delivered for incoming messages
      if (signalRService.getConnectionStatus() && message?.id && message?.chatRoomId) {
        signalRService.acknowledgeDelivered(message.id, message.chatRoomId);
      }

      dispatch({ type: ActionTypes.ADD_MESSAGE, payload: message });

      const isIncoming = Number(message.senderId) !== Number(state.currentLoggedInUserId);
      const isActiveRoom = state.currentRoom?.id === message.chatRoomId;
      if (isIncoming && isActiveRoom) {
        dispatch({ type: ActionTypes.MARK_ALL_MESSAGES_AS_READ_IN_ROOM, payload: { roomId: message.chatRoomId, userId: state.currentLoggedInUserId } });
        if (signalRService.getConnectionStatus()) {
          // Only call MarkRoomAsRead to let server mark ALL unread messages atomically
          signalRService.markRoomAsRead(message.chatRoomId.toString());
        }
      }
    };

    const handleUserTyping = (typingData) => {
      const selfId = state.currentLoggedInUserId;
      if (!typingData || typingData.userId == null) return;
      if (Number(typingData.userId) === Number(selfId)) return;
      dispatch({ type: ActionTypes.UPDATE_TYPING_STATUS, payload: typingData });
    };

    const handleReceiveChatRoomUpdate = (roomData) => {
      if (!roomData || typeof roomData.name !== "string") return;
      dispatch({ type: ActionTypes.UPSERT_CHAT_ROOM, payload: { ...roomData, name: String(roomData.name || "") } });
    };

    const handleMessageRead = (readData) => {
      const normalized = {
        messageId: readData.MessageId ?? readData.messageId,
        chatRoomId: readData.ChatRoomId ?? readData.chatRoomId,
      };
      dispatch({ type: ActionTypes.UPDATE_MESSAGE_READ_STATUS_FOR_SENDER, payload: normalized });
    };

    const handleMessageDelivered = (data) => {
      const normalized = {
        messageId: data.MessageId ?? data.messageId,
        chatRoomId: data.ChatRoomId ?? data.chatRoomId,
      };
      dispatch({ type: ActionTypes.UPDATE_MESSAGE_DELIVERED_STATUS_FOR_SENDER, payload: normalized });
    };

    const handleMessageReadReceipt = (receiptData) => {
      dispatch({ type: ActionTypes.UPDATE_MESSAGE_AS_READ_FOR_RECEIVER, payload: receiptData });
    };

    const handleMessageEdited = (messageDto) => {
      dispatch({ type: ActionTypes.EDIT_MESSAGE_SUCCESS, payload: messageDto });
    };

    const handleMessageDeleted = (payload) => {
      const normalized = {
        messageId: payload?.MessageId ?? payload?.messageId,
        roomId: payload?.ChatRoomId ?? payload?.chatRoomId,
      };
      if (normalized.messageId != null && normalized.roomId != null) {
        dispatch({ type: ActionTypes.DELETE_MESSAGE_SUCCESS, payload: normalized });
      }
    };

    const handleMessageReacted = (reactionData) => {
      dispatch({ type: ActionTypes.MESSAGE_REACTION_SUCCESS, payload: reactionData });
    };

    const handleUnreadCountUpdate = (data) => {
      const roomId = data.RoomId ?? data.roomId;
      const unreadCount = data.UnreadCount ?? data.unreadCount;
      if (roomId == null || unreadCount == null) return;
      dispatch({ type: ActionTypes.UPDATE_UNREAD_COUNT, payload: { roomId, unreadCount } });
    };

    signalRService.addEventListener("MessageReacted", handleMessageReacted);
    signalRService.addEventListener("connectionStatusChanged", handleConnectionStatusChanged);
    signalRService.addEventListener("messageReceived", handleMessageReceived);
    signalRService.addEventListener("userTyping", handleUserTyping);
    signalRService.addEventListener("receiveChatRoomUpdate", handleReceiveChatRoomUpdate);
    signalRService.addEventListener("messageRead", handleMessageRead);
    signalRService.addEventListener("messageDelivered", handleMessageDelivered);
    signalRService.addEventListener("messageReadReceipt", handleMessageReadReceipt);
    signalRService.addEventListener("MessageEdited", handleMessageEdited);
    signalRService.addEventListener("MessageDeleted", handleMessageDeleted);
    signalRService.addEventListener("unreadCountUpdate", handleUnreadCountUpdate);

    return () => {
      signalRService.removeEventListener("MessageReacted", handleMessageReacted);
      signalRService.removeEventListener("connectionStatusChanged", handleConnectionStatusChanged);
      signalRService.removeEventListener("messageReceived", handleMessageReceived);
      signalRService.removeEventListener("userTyping", handleUserTyping);
      signalRService.removeEventListener("receiveChatRoomUpdate", handleReceiveChatRoomUpdate);
      signalRService.removeEventListener("messageRead", handleMessageRead);
      signalRService.removeEventListener("messageDelivered", handleMessageDelivered);
      signalRService.removeEventListener("messageReadReceipt", handleMessageReadReceipt);
      signalRService.removeEventListener("MessageEdited", handleMessageEdited);
      signalRService.removeEventListener("MessageDeleted", handleMessageDeleted);
      signalRService.removeEventListener("unreadCountUpdate", handleUnreadCountUpdate);
    };
  }, [state.currentLoggedInUserId, state.currentRoom?.id]);

  // Actions
  const markAllMessagesAsReadInRoom = useCallback(
    (roomId) => {
      if (!roomId || !state.messages[roomId]?.items) return;

      if (signalRService.getConnectionStatus()) {
        // Only MarkRoomAsRead to avoid advancing LastReadMessageId prematurely
        signalRService.markRoomAsRead(roomId.toString());
      }
      dispatch({ type: ActionTypes.MARK_ALL_MESSAGES_AS_READ_IN_ROOM, payload: { roomId, userId: state.currentLoggedInUserId } });
    },
    [state.messages, state.currentLoggedInUserId]
  );

  const loadRooms = useCallback(async () => {
    // از اجرای همزمان جلوگیری می‌کنیم
    if (state.isLoading) return;

    dispatch({ type: ActionTypes.SET_LOADING, payload: true });
    try {
      const rooms = await chatApi.getChatRooms();
      if (rooms) {
        dispatch({ type: ActionTypes.SET_ROOMS, payload: rooms });
      }
    } catch {
      dispatch({ type: ActionTypes.SET_ERROR, payload: "خطا در بارگذاری چت‌ها" });
    } finally {
      dispatch({ type: ActionTypes.SET_LOADING, payload: false });
    }
  }, [state.isLoading, dispatch]);

  const loadChatRooms = useCallback(async () => {
    try {
      dispatch({ type: ActionTypes.SET_LOADING, payload: true });
      const rooms = await chatApi.getChatRooms(); // این API باید ChatRoomDto تکمیل شده را برگرداند
      dispatch({ type: ActionTypes.SET_ROOMS, payload: rooms });
      return rooms; // بازگرداندن روم‌ها برای استفاده مستقیم در صورت نیاز
    } catch (error) {
      dispatch({ type: ActionTypes.SET_ERROR, payload: error.message });
      return []; // بازگرداندن آرایه خالی در صورت خطا
    } finally {
      dispatch({ type: ActionTypes.SET_LOADING, payload: false });
    }
  }, []);

  const loadMessages = useCallback(
    async (roomId, page = 1, pageSize = 20, isLoadingMore = false) => {
      if (state.isLoadingMessages && !isLoadingMore) return;

      dispatch({ type: ActionTypes.SET_LOADING_MESSAGES, payload: true });

      try {
        const response = await chatApi.getChatMessages(roomId, page, pageSize);

        if (isLoadingMore) {
          dispatch({ type: ActionTypes.PREPEND_MESSAGES, payload: { roomId, messages: response } });
        } else {
          dispatch({ type: ActionTypes.SET_MESSAGES, payload: { roomId, messages: response } });
        }
      } catch {
        dispatch({ type: ActionTypes.SET_ERROR, payload: "خطا در بارگذاری پیام‌ها" });
      } finally {
        dispatch({ type: ActionTypes.SET_LOADING_MESSAGES, payload: false });
      }
    },
    [state.isLoadingMessages]
  );

  const setCurrentRoom = useCallback((room) => {
    dispatch({ type: ActionTypes.SET_CURRENT_ROOM, payload: room });
  }, []);

  const createChatRoom = useCallback(
    async (roomData) => {
      try {
        dispatch({ type: ActionTypes.SET_LOADING, payload: true });
        const newRoom = await chatApi.createChatRoom(roomData);
        await loadRooms(true);
        return newRoom;
      } catch (error) {
        dispatch({ type: ActionTypes.SET_ERROR, payload: error.message });
        throw error;
      } finally {
        dispatch({ type: ActionTypes.SET_LOADING, payload: false });
      }
    },
    [loadRooms]
  );

  const joinRoom = useCallback(async (roomId) => {
    try {
      if (signalRService.getConnectionStatus()) {
        await signalRService.joinRoom(roomId);
      }
    } catch (error) {
      console.error("Error joining room:", error);
    }
  }, []);

  const leaveRoom = useCallback(async (roomId) => {
    try {
      if (signalRService.getConnectionStatus()) {
        await signalRService.leaveRoom(roomId);
      }
    } catch (error) {
      console.error("Error leaving room:", error);
    }
  }, []);

  const sendMessage = useCallback(async (roomId, messageData) => chatApi.sendMessage(roomId, messageData), []);

  const markMessageAsRead = useCallback((roomId, messageId) => {
    if (signalRService.getConnectionStatus() && roomId && messageId) {
      signalRService.markMessageAsRead(messageId.toString(), roomId.toString());
    }
  }, []);

  const editMessage = useCallback(
    async (messageId, newContent) => {
      try {
        const updatedMessageDto = await chatApi.editMessage(messageId, newContent);
        return updatedMessageDto;
      } catch (error) {
        dispatch({ type: ActionTypes.SET_ERROR, payload: error.message });
        throw error;
      }
    },
    [dispatch]
  );

  const deleteMessage = useCallback(
    async (messageId) => {
      try {
        await chatApi.deleteMessage(messageId);
        // Optimistic local update: mark message as deleted immediately
        const roomIdStr = Object.keys(state.messages || {}).find((rid) =>
          (state.messages[rid]?.items || []).some((m) => m.id === messageId)
        );
        const roomId = roomIdStr ? (isNaN(+roomIdStr) ? roomIdStr : +roomIdStr) : null;
        if (roomId != null) {
          dispatch({ type: ActionTypes.DELETE_MESSAGE_SUCCESS, payload: { messageId, roomId } });
        }
      } catch (error) {
        dispatch({ type: ActionTypes.SET_ERROR, payload: error.message });
        throw error;
      }
    },
    [dispatch, state.messages]
  );

  const setReplyingToMessage = useCallback((messageData) => {
    dispatch({ type: ActionTypes.SET_REPLYING_TO_MESSAGE, payload: messageData });
  }, []);

  const clearReplyingToMessage = useCallback(() => {
    dispatch({ type: ActionTypes.CLEAR_REPLYING_TO_MESSAGE });
  }, []);

  const setEditingMessage = useCallback((messageData) => {
    dispatch({ type: ActionTypes.SET_EDITING_MESSAGE, payload: messageData });
  }, []);

  const clearEditingMessage = useCallback(() => {
    dispatch({ type: ActionTypes.CLEAR_EDITING_MESSAGE });
  }, []);

  const setForwardingMessage = useCallback((messageData) => {
    dispatch({ type: ActionTypes.SET_FORWARDING_MESSAGE, payload: messageData });
  }, []);

  const clearForwardingMessage = useCallback(() => {
    dispatch({ type: ActionTypes.CLEAR_FORWARDING_MESSAGE });
  }, []);

  const sendReaction = useCallback(
    async (messageId, roomId, emoji) => {
      try {
        await chatApi.reactToMessage(messageId, { emoji });
      } catch (error) {
        dispatch({ type: ActionTypes.SET_ERROR, payload: error.message });
      }
    },
    [dispatch]
  );

  const showForwardModal = useCallback((messageId) => {
    dispatch({ type: ActionTypes.SHOW_FORWARD_MODAL, payload: messageId });
  }, []);

  const hideForwardModal = useCallback(() => {
    dispatch({ type: ActionTypes.HIDE_FORWARD_MODAL });
  }, []);

  const forwardMessage = useCallback(
    async (originalMessageId, targetChatRoomId) => {
      try {
        const forwardedMessageDto = await chatApi.forwardMessage(originalMessageId, targetChatRoomId);
        return forwardedMessageDto;
      } catch (error) {
        dispatch({ type: ActionTypes.SET_ERROR, payload: error.message });
        throw error;
      }
    },
    [dispatch]
  );

  const value = {
    // State values
    ...state,
    currentRoom: state.currentRoom,
    rooms: state.rooms,
    messages: state.messages,
    onlineUsers: state.onlineUsers,
    typingUsers: state.typingUsers,
    isConnected: state.isConnected,
    isLoading: state.isLoading,
    isLoadingMessages: state.isLoadingMessages,
    error: state.error,
    currentLoggedInUserId: state.currentLoggedInUserId,
    replyingToMessage: state.replyingToMessage,
    isForwardModalVisible: state.isForwardModalVisible,
    messageIdToForward: state.messageIdToForward,
    editingMessage: state.editingMessage,
    forwardingMessage: state.forwardingMessage,

    // Actions
    loadRooms,
    loadChatRooms,
    loadMessages,
    setCurrentRoom,
    sendMessage,
    joinRoom,
    leaveRoom,
    editMessage,
    deleteMessage,
    markAllMessagesAsReadInRoom,
    setReplyingToMessage,
    clearReplyingToMessage,
    setEditingMessage,
    clearEditingMessage,
    setForwardingMessage,
    clearForwardingMessage,
    sendReaction,
    showForwardModal,
    hideForwardModal,
    forwardMessage,
    startTyping: (roomId) => signalRService.startTyping(roomId.toString()),
    stopTyping: (roomId) => signalRService.stopTyping(roomId ? roomId.toString() : null),
    markMessageAsRead,
    loadOnlineUsers: useCallback(async () => {
      try {
        const users = await chatApi.getOnlineUsers();
        dispatch({ type: ActionTypes.SET_ONLINE_USERS, payload: users });
      } catch (error) {
        dispatch({ type: ActionTypes.SET_ERROR, payload: error.message });
      }
    }, []),
    clearError: () => dispatch({ type: ActionTypes.SET_ERROR, payload: null }),
    createChatRoom,
  };

  return <ChatContext.Provider value={value}>{children}</ChatContext.Provider>;
};
