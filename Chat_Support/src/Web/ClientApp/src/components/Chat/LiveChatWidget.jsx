import React, { useState, useEffect, useRef } from "react";
import { useAuth } from "../../hooks/useAuth";
import { MessageCircle, X, Send, Paperclip } from "lucide-react";
import "./Chat.css";
import api from "../../api/apiClient";

const LiveChatWidget = () => {
  const { isAuthenticated, user } = useAuth();
  const [isOpen, setIsOpen] = useState(false);
  const [isMinimized, setIsMinimized] = useState(false);
  const [guestInfo, setGuestInfo] = useState({ name: "", phone: "" });
  const [isRegistered, setIsRegistered] = useState(false);
  const [messages, setMessages] = useState([]);
  const [inputMessage, setInputMessage] = useState("");
  const [agentInfo, setAgentInfo] = useState(null);
  const [chatRoomId, setChatRoomId] = useState(null);
  const [connectionStatus, setConnectionStatus] = useState("disconnected");

  const messagesEndRef = useRef(null);
  const fileInputRef = useRef(null);
  const lastPendingClientMessageRef = useRef(null);

  // Helper: figure current user id safely (camelCase or PascalCase)
  const getCurrentUserId = () => {
    if (!user) return null;
    const candidate =
      user.id || user.Id || user.userId || user.UserId || user.sub || user.nameIdentifier || null;
    return candidate != null ? candidate.toString() : null;
  };
  // NEW: helper to compute current user's full name (fallback for own-message detection)
  const getCurrentUserFullName = () => {
    if (!user) return "";
    const first = user.firstName || user.FirstName || user.name || "";
    const last = user.lastName || user.LastName || "";
    const full = `${first} ${last}`.trim();
    return full || (user.fullName || "");
  };

  // Helper: normalize server dto to widget model
  const normalizeMessage = (msg) => {
    const senderIdStr = msg.senderId ? msg.senderId.toString() : "";
    const currentUserId = getCurrentUserId();

    // If authenticated, own message when senderId equals current user's id.
    // If guest, own message when senderId is empty/null.
    let isOwn = isAuthenticated
      ? !!currentUserId && senderIdStr !== "" && senderIdStr === currentUserId
      : !senderIdStr || senderIdStr === "";

    // Fallback by full name in case ids don't align (only for authenticated users)
    if (isAuthenticated && !isOwn) {
      const myName = (getCurrentUserFullName() || "").trim();
      const senderName = (msg.senderFullName || "").toString().trim();
      if (myName && senderName && myName === senderName) {
        isOwn = true;
      }
    }

    return {
      id: msg.id,
      content: msg.content,
      senderName: msg.senderFullName,
      isOwn,
      createdAt: msg.timestamp || new Date().toISOString(),
      type: msg.type,
      attachmentUrl: msg.attachmentUrl,
    };
  };

  // دریافت یا ایجاد شناسه جلسه و تشخیص احراز هویت
  useEffect(() => {
    let sessionId = localStorage.getItem("chat_session_id");
    if (!sessionId) {
      sessionId = `guest_${Date.now()}_${Math.random().toString(36).substr(2, 9)}`;
      localStorage.setItem("chat_session_id", sessionId);
    }

    if (isAuthenticated && user) {
      setGuestInfo({
        name: user.name || user.fullName || user.firstName || "",
        phone: user.phone || "",
      });
      setIsRegistered(true);
    } else {
      const savedInfo = localStorage.getItem("chat_guest_info");
      if (savedInfo) {
        const info = JSON.parse(savedInfo);
        setGuestInfo({ name: info.name || "", phone: info.phone || "" });
        setIsRegistered(true);
      }
    }
  }, [isAuthenticated, user]);

  // اتصال SignalR: برای مهمان از GuestChatHub و برای کاربر احراز‌شده از ChatHub
  useEffect(() => {
    if (isOpen && isRegistered && !window.chatHubConnection) {
      if (isAuthenticated) {
        initializeAuthConnection();
      } else {
        initializeGuestConnection();
      }
    }

    return () => {
      // قبلاً اتصال را فقط برای حالت isRegistered=false می‌بست؛ همان رفتار را حفظ می‌کنیم
      if (window.chatHubConnection && !isRegistered) {
        window.chatHubConnection.stop();
        window.chatHubConnection = null;
      }
    };
  }, [isOpen, isRegistered, isAuthenticated]);

  const initializeAuthConnection = async () => {
    try {
      const { HubConnectionBuilder, LogLevel } = await import("@microsoft/signalr");

      const hubUrl =
        import.meta.env.MODE === "development"
          ? "https://localhost:5001/chathub"
          : window.location.origin + "/chathub";

      const connection = new HubConnectionBuilder()
        .withUrl(hubUrl, {
          accessTokenFactory: () => localStorage.getItem("token"),
          withCredentials: false,
        })
        .configureLogging(LogLevel.Information)
        .withAutomaticReconnect()
        .build();

      // پیام‌های دریافتی
      connection.on("ReceiveMessage", (serverMessage) => {
        const message = normalizeMessage(serverMessage);

        // جایگزینی پیام موقت کاربر احراز شده
        if (message.isOwn && lastPendingClientMessageRef.current) {
          const pending = lastPendingClientMessageRef.current;
          if (pending && pending.content === message.content && pending.type === message.type) {
            setMessages((prev) => {
              const idx = prev.findIndex((m) => m.id === pending.clientId);
              if (idx !== -1) {
                const updated = [...prev];
                updated[idx] = message;
                return updated;
              }
              return [...prev, message];
            });
            lastPendingClientMessageRef.current = null;
            scrollToBottom();
            return;
          }
        }

        setMessages((prev) => [...prev, message]);
        scrollToBottom();
      });

      // وضعیت تایپ برای هاب احراز‌شده
      connection.on("UserTyping", () => {
        // typing status not shown in widget UI
      });

      connection.onreconnecting(() => setConnectionStatus("در حال اتصال مجدد"));
      connection.onreconnected(() => setConnectionStatus("connected"));
      connection.onclose(() => setConnectionStatus("قطع شده"));

      await connection.start();
      setConnectionStatus("connected");
      window.chatHubConnection = connection;

      await startSupportChat();
    } catch (error) {
      console.error("اتصال احرازشده با شکست مواجه شد:", error);
      setConnectionStatus("خطا");
    }
  };

  const initializeGuestConnection = async () => {
    try {
      const { HubConnectionBuilder, LogLevel } = await import("@microsoft/signalr");

      const hubUrl =
        import.meta.env.MODE === "development"
          ? "https://localhost:5001/guestchathub"
          : window.location.origin + "/guestchathub";

      const connection = new HubConnectionBuilder()
        .withUrl(hubUrl, {
          accessTokenFactory: () => localStorage.getItem("chat_session_id"),
          withCredentials: false,
        })
        .configureLogging(LogLevel.Information)
        .withAutomaticReconnect()
        .build();

      // مدیر رویداد پیام دریافتی
      connection.on("ReceiveMessage", (serverMessage) => {
        const message = normalizeMessage(serverMessage);

        // اگر پیام از همین کاربر (مهمان/احرازشده) بود، پیام موقت را با پیام واقعی جایگزین کن
        if (message.isOwn && lastPendingClientMessageRef.current) {
          const pending = lastPendingClientMessageRef.current;
          if (pending && pending.content === message.content && pending.type === message.type) {
            setMessages((prev) => {
              const idx = prev.findIndex((m) => m.id === pending.clientId);
              if (idx !== -1) {
                const updated = [...prev];
                updated[idx] = message;
                return updated;
              }
              return [...prev, message];
            });
            lastPendingClientMessageRef.current = null;
            scrollToBottom();
            return;
          }
        }

        // اضافه کردن عادی
        setMessages((prev) => [...prev, message]);
        scrollToBottom();
      });

      connection.on("AgentTyping", () => {
        // typing status not shown in widget UI
      });

      connection.on("SupportChatUpdate", (update) => {
        if (update.type === "AgentAssigned") {
          setAgentInfo(update.agent);
        } else if (update.type === "ChatTransferred") {
          setAgentInfo(update.newAgent);
          addSystemMessage("چت شما به کارشناس دیگری منتقل شد.");
        }
      });

      connection.onreconnecting(() => setConnectionStatus("در حال اتصال مجدد"));
      connection.onreconnected(() => setConnectionStatus("connected"));
      connection.onclose(() => setConnectionStatus("قطع شده"));

      await connection.start();
      setConnectionStatus("connected");
      window.chatHubConnection = connection;

      await startSupportChat();
    } catch (error) {
      console.error("اتصال با شکست مواجه شد:", error);
      setConnectionStatus("خطا");
    }
  };

  const startSupportChat = async () => {
    try {
      // اگر از طریق iframe فراخوانی شده باشد، OriginUrl را هم بفرستیم
      const originUrl = (typeof window !== 'undefined' && window.__chat_origin) ? window.__chat_origin : undefined;
      const response = await api.post("/support/start", {
        guestSessionId: localStorage.getItem("chat_session_id"),
        guestName: guestInfo.name,
        guestPhone: guestInfo.phone,
        ipAddress: "",
        userAgent: navigator.userAgent,
        initialMessage: "کاربر مهمان چتی را شروع کرد",
        originUrl,
      });

      const result = response.data;
      setChatRoomId(result.chatRoomId);

      if (result.assignedAgentName) {
        setAgentInfo({ id: result.assignedAgentId, name: result.assignedAgentName });
      }

      // اگر پیام‌های قبلی وجود دارد، آن‌ها را بارگذاری کن
      if (Array.isArray(result.messages) && result.messages.length > 0) {
        const history = result.messages.map(normalizeMessage);
        setMessages(history);
        setTimeout(scrollToBottom, 50);
      }

      if (window.chatHubConnection) {
        // برای اطمینان از عضویت در گروه اتاق
        await window.chatHubConnection.invoke("JoinRoom", result.chatRoomId.toString());
      }
    } catch (error) {
      console.error("شروع چت با شکست مواجه شد:", error);
      addSystemMessage("اتصال به پشتیبانی با شکست مواجه شد. لطفاً دوباره تلاش کنید.");
    }
  };

  const handleRegistration = async (e) => {
    e.preventDefault();
    if (!guestInfo.name.trim() || !guestInfo.phone.trim()) {
      addSystemMessage("لطفاً نام و شماره تماس را وارد کنید.");
      return;
    }
    try {
      const response = await api.post("/support/guest/auth", { name: guestInfo.name, phone: guestInfo.phone });
      const result = response.data;
      localStorage.setItem("chat_session_id", result.sessionId);
      localStorage.setItem(
        "chat_guest_info",
        JSON.stringify({ name: result.name, phone: result.phone })
      );
      setIsRegistered(true);
    } catch (error) {
      let msg = "احراز هویت مهمان با خطا مواجه شد.";
      if (error.response && error.response.data) {
        msg = typeof error.response.data === "string" ? error.response.data : error.response.data.message || msg;
      }
      addSystemMessage(msg);
    }
  };

  const sendMessage = async () => {
    if (!inputMessage.trim() || !chatRoomId) return;

    const clientId = `temp_${Date.now()}`;
    const tempMessage = {
      id: clientId,
      content: inputMessage,
      senderName: "شما",
      isOwn: true,
      createdAt: new Date().toISOString(),
      status: "sending",
      type: 0,
    };

    lastPendingClientMessageRef.current = { clientId, content: inputMessage, type: 0 };

    setMessages((prev) => [...prev, tempMessage]);
    setInputMessage("");

    try {
      await api.post(
        `/chat/rooms/${chatRoomId}/messages`,
        { content: inputMessage, type: 0 },
        { headers: { "X-Session-Id": localStorage.getItem("chat_session_id") } }
      );
    } catch (error) {
      console.error("ارسال پیام با شکست مواجه شد:", error);
      setMessages((prev) => prev.map((m) => (m.id === clientId ? { ...m, status: "failed" } : m)));
      lastPendingClientMessageRef.current = null;
    }
  };

  const handleFileUpload = async (e) => {
    const file = e.target.files[0];
    if (!file || !chatRoomId) return;

    const formData = new FormData();
    formData.append("file", file);
    formData.append("type", file.type.startsWith("image/") ? "1" : "2");

    try {
      const response = await api.post("/chat/upload", formData, {
        headers: { "X-Session-Id": localStorage.getItem("chat_session_id") },
      });
      const result = response.data;

      await api.post(
        `/chat/rooms/${chatRoomId}/messages`,
        { content: file.name, type: file.type.startsWith("image/") ? 1 : 2, attachmentUrl: result.fileUrl },
        { headers: { "X-Session-Id": localStorage.getItem("chat_session_id") } }
      );
    } catch (error) {
      console.error("آپلود فایل با شکست مواجه شد:", error);
      addSystemMessage("آپلود فایل با شکست مواجه شد. لطفاً دوباره تلاش کنید.");
    }
  };

  const addSystemMessage = (content) => {
    setMessages((prev) => [
      ...prev,
      { id: Date.now(), content, type: 5, createdAt: new Date().toISOString() },
    ]);
  };

  const scrollToBottom = () => {
    messagesEndRef.current?.scrollIntoView({ behavior: "smooth" });
  };

  useEffect(() => {
    scrollToBottom();
  }, [messages]);

  const styles = {
    widgetButton: {
      position: "fixed",
      bottom: "24px",
      right: "24px",
      backgroundColor: "#0d6efd",
      color: "white",
      borderRadius: "50%",
      padding: "16px",
      border: "none",
      boxShadow: "0 10px 25px rgba(0,0,0,0.2)",
      cursor: "pointer",
      transition: "transform 0.2s",
      zIndex: 50,
    },
    chatWindow: {
      position: "fixed",
      bottom: "24px",
      right: "24px",
      backgroundColor: "white",
      borderRadius: "8px",
      boxShadow: "0 20px 25px -5px rgba(0, 0, 0, 0.1)",
      zIndex: 50,
      display: "flex",
      flexDirection: "column",
      transition: "all 0.3s",
    },
    chatHeader: {
      backgroundColor: "#0d6efd",
      color: "white",
      padding: "16px",
      borderRadius: "8px 8px 0 0",
      display: "flex",
      alignItems: "center",
      justifyContent: "space-between",
      cursor: "pointer",
    },
    messageContainer: {
      flex: 1,
      overflowY: "auto",
      padding: "16px",
      display: "flex",
      flexDirection: "column",
      gap: "12px",
    },
    inputContainer: {
      borderTop: "1px solid #dee2e6",
      padding: "16px",
      display: "flex",
      alignItems: "flex-end",
      gap: "8px",
    },
  };

  if (!isOpen) {
    return (
      <button
        onClick={() => setIsOpen(true)}
        style={styles.widgetButton}
        className="position-fixed"
        onMouseEnter={(e) => (e.target.style.transform = "scale(1.1)")}
        onMouseLeave={(e) => (e.target.style.transform = "scale(1)")}
      >
        <MessageCircle size={24} />
        <span className="position-absolute top-0 start-100 translate-middle badge rounded-pill bg-danger">1</span>
      </button>
    );
  }

  return (
    <div
      style={{
        ...styles.chatWindow,
        width: isMinimized ? "320px" : "384px",
        height: isMinimized ? "64px" : "600px",
        maxHeight: "80vh",
      }}
    >
      <div style={styles.chatHeader} onClick={() => setIsMinimized(!isMinimized)} className="d-flex align-items-center justify-content-between">
        <div className="d-flex align-items-center gap-3">
          <div className="bg-white bg-opacity-25 rounded-circle p-2">
            <MessageCircle size={24} />
          </div>
          <div>
            <h6 className="mb-0 fw-semibold">گفتگوی پشتیبانی</h6>
            {agentInfo && <small className="opacity-75">{agentInfo.name}</small>}
          </div>
        </div>
        <div className="d-flex gap-2">
          <button
            onClick={(e) => {
              e.stopPropagation();
              setIsMinimized(!isMinimized);
            }}
            className="btn btn-sm text-white p-1"
            style={{ background: "transparent", border: "none" }}
          >
            {isMinimized ? "□" : "−"}
          </button>
          <button
            onClick={(e) => {
              e.stopPropagation();
              setIsOpen(false);
            }}
            className="btn btn-sm text-white p-1"
            style={{ background: "transparent", border: "none" }}
          >
            <X size={20} />
          </button>
        </div>
      </div>

      {!isMinimized && (
        <>
          {!isRegistered && !isAuthenticated ? (
            <div className="p-4 d-flex flex-column justify-content-center h-100">
              <h5 className="mb-3">شروع گفتگو</h5>
              <p className="text-muted mb-4">لطفاً اطلاعات خود را وارد کنید تا گفتگوی شما با پشتیبانی آغاز شود.</p>
              <div className="d-flex flex-column gap-3">
                <input type="text" placeholder="نام شما *" value={guestInfo.name} onChange={(e) => setGuestInfo({ ...guestInfo, name: e.target.value })} className="form-control" required />
                <input type="tel" placeholder="شماره تماس *" value={guestInfo.phone} onChange={(e) => setGuestInfo({ ...guestInfo, phone: e.target.value })} className="form-control" required />
                <button onClick={handleRegistration} className="btn btn-primary" disabled={!guestInfo.name.trim() || !guestInfo.phone.trim()}>
                  شروع چت
                </button>
              </div>
            </div>
          ) : (
            <>
              {connectionStatus !== "connected" && (
                <div className="alert alert-warning mb-0 rounded-0 py-2">
                  <small>{connectionStatus === "reconnecting" ? "در حال اتصال مجدد..." : "ارتباط قطع شد"}</small>
                </div>
              )}

              <div style={styles.messageContainer}>
                {messages.map((message) => (
                  <div key={message.id} className={`d-flex ${message.isOwn ? "justify-content-end" : "justify-content-start"}`}>
                    <div className={`rounded p-3 ${message.type === 5 ? "w-100 text-center bg-light" : message.isOwn ? "bg-primary text-white" : "bg-light"}`} style={{ maxWidth: "70%" }}>
                      {message.type === 5 ? (
                        <small className="text-muted fst-italic">{message.content}</small>
                      ) : (
                        <>
                          {!message.isOwn && (
                            <small className="d-block fw-semibold opacity-75 mb-1">{message.senderName || agentInfo?.name || "پشتیبانی"}</small>
                          )}

                          {message.type === 0 && <p className="mb-1 small">{message.content}</p>}
                          {message.type === 1 && <img src={message.attachmentUrl} alt={message.content} className="img-fluid rounded" style={{ maxWidth: "100%" }} />}
                          {message.type === 2 && (
                            <a href={message.attachmentUrl} target="_blank" rel="noopener noreferrer" className="d-flex align-items-center gap-2 text-decoration-underline">
                              <Paperclip size={16} />
                              {message.content}
                            </a>
                          )}
                          {message.type === 3 && (
                            <audio controls className="w-100" style={{ maxWidth: "250px" }}>
                              <source src={message.attachmentUrl} type="audio/webm" />
                            </audio>
                          )}

                          <small className="d-block opacity-75 mt-1">
                            {new Date(message.createdAt).toLocaleTimeString()}
                            {message.status === "sending" && " • در حال ارسال..."}
                            {message.status === "failed" && " • ناموفق"}
                          </small>
                        </>
                      )}
                    </div>
                  </div>
                ))}

                <div ref={messagesEndRef} />
              </div>

              <div style={styles.inputContainer}>
                <button onClick={() => fileInputRef.current?.click()} className="btn btn-light btn-sm" disabled={!chatRoomId}>
                  <Paperclip size={20} />
                </button>

                <input ref={fileInputRef} type="file" onChange={handleFileUpload} className="d-none" accept="image/*,.pdf,.doc,.docx,.xls,.xlsx,.txt,.zip,.rar" />

                <input
                  type="text"
                  value={inputMessage}
                  onChange={(e) => setInputMessage(e.target.value)}
                  onKeyPress={(e) => e.key === "Enter" && !e.shiftKey && sendMessage()}
                  placeholder={chatRoomId ? "پیام خود را بنویسید..." : "در حال اتصال..."}
                  className="form-control form-control-sm"
                  disabled={!chatRoomId}
                />

                <button onClick={sendMessage} disabled={!inputMessage.trim() || !chatRoomId} className="btn btn-primary btn-sm">
                  <Send size={20} />
                </button>
              </div>
            </>
          )}
        </>
      )}
    </div>
  );
};

export default LiveChatWidget;
