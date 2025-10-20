// src/services/signalRService.js
// SignalR service for real-time chat functionality

import {HubConnectionBuilder, LogLevel} from '@microsoft/signalr';
import apiClient from '../api/apiClient';

function resolveHubUrl() {
  try {
    const base = apiClient?.defaults?.baseURL || (import.meta.env.BASE_URL ?? '/api');
    // base is like https://localhost:5001/api or https://domain/api
    const hubBase = String(base).replace(/\/?api\/?$/, '');
    return `${hubBase}/chathub`;
  } catch {
    // fallback to same-origin
    return `${window.location.origin}/chathub`;
  }
}

class SignalRService {
  constructor() {
    this.connection = null;
    this.isConnected = false;
    this.listeners = new Map();
  }

  // Initialize connection
  async startConnection(token) {
    try {
      const hubUrl = resolveHubUrl();
      this.connection = new HubConnectionBuilder()
        .withUrl(hubUrl, {
          accessTokenFactory: () => token,
        })
        .withAutomaticReconnect([0, 2000, 5000, 10000])
        .configureLogging(LogLevel.Information)
        .build();

      // Set up event handlers
      this.setupEventHandlers();

      await this.connection.start();
      this.isConnected = true;

      // Notify listeners about connection status
      this.notifyListeners('connectionStatusChanged', true);

      return true;
    } catch (error) {
      console.error('[SignalR] Connection Error:', error);
      this.isConnected = false;
      this.notifyListeners('connectionStatusChanged', false);
      return false;
    }
  }

  // Stop connection
  async stopConnection() {
    if (this.connection) {
      await this.connection.stop();
      this.isConnected = false;
      this.notifyListeners('connectionStatusChanged', false);
    }
  }

  // Setup event handlers
  setupEventHandlers() {
    if (!this.connection) return;

    // Listen for room list updates
    this.connection.on('ReceiveChatRoomUpdate', (roomData) => {
      this.notifyListeners('receiveChatRoomUpdate', roomData);
    });

    // Handle connection closed
    this.connection.onclose((error) => {
      console.warn('[SignalR] Connection closed', error);
      this.isConnected = false;
      this.notifyListeners('connectionStatusChanged', false);

      // Attempt to reconnect after 5 seconds
      setTimeout(() => this.reconnect(), 5000);
    });

    // Handle reconnecting
    this.connection.onreconnecting((error) => {
      console.warn('[SignalR] Reconnecting...', error);
      this.notifyListeners('connectionStatusChanged', false);
    });

    // Handle reconnected
    this.connection.onreconnected(() => {
      this.isConnected = true;
      this.notifyListeners('connectionStatusChanged', true);
    });

    // Listen for new messages
    this.connection.on('ReceiveMessage', (message) => {
      this.notifyListeners('messageReceived', message);
    });

    // Listen for typing indicators
    this.connection.on('UserTyping', (typingData) => {
      this.notifyListeners('userTyping', typingData);
    });

    // Listen for message read status
    this.connection.on('MessageRead', (readData) => {
      this.notifyListeners('messageRead', readData);
    });

    // Delivered ack from server
    this.connection.on('MessageDelivered', (deliveredData) => {
      this.notifyListeners('messageDelivered', deliveredData);
    });

    // UserOnlineStatus از بک‌اند می‌آید
    this.connection.on('UserOnlineStatus', (userData) => {
      if (userData.IsOnline) {
        this.notifyListeners('userOnline', {id: userData.UserId, userName: userData.UserName, avatar: userData.Avatar, connectedAt: new Date().toISOString()});
      } else {
        this.notifyListeners('userOffline', {id: userData.UserId});
      }
    });

    this.connection.on('MessageEdited', (messageDto) => {
      this.notifyListeners('MessageEdited', messageDto);
    });

    this.connection.on('UnreadCountUpdate', (data) => {
      this.notifyListeners('unreadCountUpdate', data);
    });

    this.connection.on('MessageDeleted', (payload) => {
      this.notifyListeners('MessageDeleted', payload);
    });

    this.connection.on('MessageReacted', (reactionDto) => {
      this.notifyListeners('MessageReacted', reactionDto);
    });
  }

  // Join a chat room
  async joinRoom(roomId) {
    if (this.connection && this.isConnected) {
      await this.connection.invoke('JoinRoom', roomId.toString());
    }
  }

  // Leave a chat room
  async leaveRoom(roomId) {
    if (this.connection && this.isConnected) {
      await this.connection.invoke('LeaveRoom', roomId.toString());
    }
  }

  // Start typing indicator
  async startTyping(roomId) {
    if (this.connection && this.isConnected) {
      await this.connection.invoke('StartTyping', roomId.toString());
    }
  }

  // Stop typing indicator
  async stopTyping(roomId = null) {
    if (this.connection && this.isConnected) {
      await this.connection.invoke('StopTyping', roomId?.toString());
    }
  }

  // Mark message as read
  async markMessageAsRead(messageId, roomId) {
    if (this.connection && this.isConnected) {
      try {
        await this.connection.invoke('MarkMessageAsRead', messageId.toString(), roomId.toString());
      } catch (error) {
        console.error('Error marking message as read:', error);
      }
    }
  }

  // Acknowledge a delivered message (when it appears in UI)
  async acknowledgeDelivered(messageId, roomId) {
    if (this.connection && this.isConnected) {
      try {
        await this.connection.invoke('AcknowledgeDelivered', messageId.toString(), roomId.toString());
      } catch (error) {
        console.error('Error acknowledging delivered:', error);
      }
    }
  }

  // Mark entire room as read (to latest incoming message)
  async markRoomAsRead(roomId) {
    if (this.connection && this.isConnected) {
      try {
        await this.connection.invoke('MarkRoomAsRead', roomId.toString());
      } catch (error) {
        console.error('Error marking room as read:', error);
      }
    }
  }

  // Add event listener
  addEventListener(event, callback) {
    if (!this.listeners.has(event)) {
      this.listeners.set(event, []);
    }
    this.listeners.get(event).push(callback);
  }

  // Remove event listener
  removeEventListener(event, callback) {
    if (this.listeners.has(event)) {
      const callbacks = this.listeners.get(event);
      const index = callbacks.indexOf(callback);
      if (index > -1) {
        callbacks.splice(index, 1);
      }
    }
  }

  // Notify listeners
  notifyListeners(event, data) {
    if (this.listeners.has(event)) {
      this.listeners.get(event).forEach((callback) => callback(data));
    }
  }

  // Reconnect
  async reconnect() {
    if (!this.isConnected && this.connection) {
      try {
        await this.connection.start();
        this.isConnected = true;
        this.notifyListeners('connectionStatusChanged', true);
      } catch (error) {
        console.error('[SignalR] Reconnection failed:', error);
        setTimeout(() => this.reconnect(), 5000);
      }
    }
  }

  // Get connection status
  getConnectionStatus() {
    return this.isConnected;
  }
}

// Create singleton instance
const signalRService = new SignalRService();
export default signalRService;
