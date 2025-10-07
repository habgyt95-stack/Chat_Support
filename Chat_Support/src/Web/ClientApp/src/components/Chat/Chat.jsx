import React, { useState, useEffect, useCallback } from "react";

import { useParams, useNavigate, useLocation } from "react-router-dom";

import { Container, Row, Col, Spinner, Alert, Button } from "react-bootstrap";

import { PeopleFill, Trash, ChatSquareText } from "react-bootstrap-icons";

import { useChat } from "../../hooks/useChat";
import { chatApi } from "../../services/chatApi";
import { getUserIdFromToken, parseJwt } from "../../Utils/jwt";

import { FaArrowRight } from "react-icons/fa6";

import ChatRoomList from "./ChatRoomList";

import MessageList from "./MessageList";

import MessageInput from "./MessageInput";

import TypingIndicatorComponent from "./TypingIndicatorComponent";

import ConnectionStatus from "./ConnectionStatus";

import ForwardModal from "./ForwardModal";

import NewRoomModal from "./NewRoomModal";

import GroupManagementModal from "./GroupManagementModal";

import "./Chat.css";

const Chat = () => {
  const { roomId } = useParams();

  const navigate = useNavigate();

  const location = useLocation();

  const {
    rooms,

    currentRoom,

    messages,

    typingUsers,

    isConnected,

    isLoading,

    error,

    clearError,

    currentLoggedInUserId,

    isForwardModalVisible,

    hideForwardModal,

    setCurrentRoom,

    loadMessages,

    loadRooms,

    joinRoom,

    leaveRoom,

    markAllMessagesAsReadInRoom,

    forwardingMessage,

    clearForwardingMessage,

    forwardMessage,
  } = useChat();

  const [showNewRoomModal, setShowNewRoomModal] = useState(false);

  const [showGroupManagement, setShowGroupManagement] = useState(false);
  const [groupManagementTab, setGroupManagementTab] = useState("members"); // default tab

  const [isMobile, setIsMobile] = useState(window.innerWidth <= 768);

  const [hasTriedLoadingRooms, setHasTriedLoadingRooms] = useState(false);

  const isSupportChat = currentRoom?.chatRoomType === 1;

  const urlParams = new URLSearchParams(location.search);

  const _isFromSupport = urlParams.get("support") === "true";
  const _ticketId = urlParams.get("ticketId");

  const selectedRoom = currentRoom;

  const handleRoomSelect = useCallback(
    async (room) => {
      if (currentRoom?.id === room.id) return;

      // Lock to prevent multiple simultaneous room changes
      if (isLoading) return;

      if (forwardingMessage) {
        try {
          await forwardMessage(forwardingMessage.id, room.id);
          clearForwardingMessage();
        } catch (err) {
          console.error("Failed to forward message on room select:", err);
          clearForwardingMessage();
          return;
        }
      }

      try {
        // First update the URL without triggering a new state update
        navigate(`/chat/${room.id}`, { replace: true });

        // Then handle the room change
        if (currentRoom?.id) {
          await leaveRoom(currentRoom.id);
        }

        setCurrentRoom(room);
        await joinRoom(room.id);
        await loadMessages(room.id, 1, 20, false);
        markAllMessagesAsReadInRoom(room.id);
      } catch (err) {
        console.error("Error selecting room:", err);
        // If there's an error, make sure we're in a consistent state
        navigate("/chat", { replace: true });
        setCurrentRoom(null);
      }
    },
    [
      currentRoom,
      forwardingMessage,
      navigate,
      forwardMessage,
      clearForwardingMessage,
      leaveRoom,
      setCurrentRoom,
      joinRoom,
      loadMessages,
      markAllMessagesAsReadInRoom,
      isLoading,
    ]
  );

  // Get current user from token (if needed)
  const [currentUser, setCurrentUser] = useState(null);
  const [isAgent, setIsAgent] = useState(false);
  useEffect(() => {
    const token = localStorage.getItem("token");
    if (token) {
      const userId = getUserIdFromToken(token);
      const payload = parseJwt(token);
      // roles can be in different claim keys; normalize to array
      let rawRoles =
        payload?.roles ??
        payload?.role ??
        payload?.[
          "http://schemas.microsoft.com/ws/2008/06/identity/claims/role"
        ];
      const roles = Array.isArray(rawRoles)
        ? rawRoles
        : rawRoles
        ? [rawRoles]
        : [];
      setCurrentUser({ id: userId, roles });
      const hasAgentRole = roles.some((r) => r === "Agent" || r === "Admin");
      setIsAgent(hasAgentRole);

      if (!hasAgentRole) {
        // fallback to server check (DB: SupportAgents)
        chatApi
          .getIsAgent()
          .then((res) => {
            if (res?.isAgent) setIsAgent(true);
          })
          .catch(() => {});
      }
    }
  }, []);

  const handleMobileBack = useCallback(() => {
    setCurrentRoom(null);

    loadRooms(); // لیست چت‌ها را دوباره بارگذاری کن

    navigate("/Chat", { replace: true });
  }, [navigate, setCurrentRoom, loadRooms]);

  useEffect(() => {
    const handleResize = () => setIsMobile(window.innerWidth <= 768);

    window.addEventListener("resize", handleResize);

    return () => window.removeEventListener("resize", handleResize);
  }, []);

  // Mobile keyboard/viewport fixes: keep header/footer visible and input above keyboard
  useEffect(() => {
    const root = document.documentElement;

    const applyViewportVars = () => {
      const vv = window.visualViewport;
      if (vv) {
        // Visual viewport height as CSS var
        root.style.setProperty("--vvh", `${vv.height}px`);
        // Keyboard offset: how much layout viewport bottom is covered
        const kb = Math.max(
          0,
          window.innerHeight - Math.round(vv.height + vv.offsetTop)
        );
        root.style.setProperty("--kb", `${kb}px`);
      } else {
        root.style.setProperty("--vvh", `${window.innerHeight}px`);
        root.style.setProperty("--kb", `0px`);
      }
    };

    applyViewportVars();
    if (window.visualViewport) {
      window.visualViewport.addEventListener("resize", applyViewportVars);
      window.visualViewport.addEventListener("scroll", applyViewportVars);
    }
    window.addEventListener("orientationchange", applyViewportVars);
    window.addEventListener("resize", applyViewportVars);

    return () => {
      if (window.visualViewport) {
        window.visualViewport.removeEventListener("resize", applyViewportVars);
        window.visualViewport.removeEventListener("scroll", applyViewportVars);
      }
      window.removeEventListener("orientationchange", applyViewportVars);
      window.removeEventListener("resize", applyViewportVars);
    };
  }, []);

  useEffect(() => {
    if (!rooms.length && !isLoading && !hasTriedLoadingRooms) {
      loadRooms();

      setHasTriedLoadingRooms(true);
    }
  }, [loadRooms, rooms.length, isLoading, hasTriedLoadingRooms]);

  useEffect(() => {
    // ✅ فقط زمانی اجرا شو که بارگذاری تمام شده باشد
    if (roomId && rooms.length > 0 && !isLoading) {
      const roomToSelect = rooms.find((r) => r.id === parseInt(roomId, 10));
      // چک می‌کنیم که چت مورد نظر پیدا شده باشد و هنوز به عنوان چت فعلی انتخاب نشده باشد
      if (roomToSelect && currentRoom?.id !== roomToSelect.id) {
        handleRoomSelect(roomToSelect);
      }
    }
  }, [roomId, rooms, currentRoom, handleRoomSelect, isLoading]); // ✅ isLoading را به وابستگی‌ها اضافه کن

  useEffect(() => {
    if (rooms.length > 0) {
      setHasTriedLoadingRooms(false);
    }
  }, [rooms.length]);

  const handleLoadMessages = async (
    roomId,
    page = 1,
    pageSize = 20,
    isLoadingMore = false
  ) => {
    try {
      await loadMessages(roomId, page, pageSize, isLoadingMore);
    } catch (err) {
      console.error("Error loading messages:", err);
    }
  };

  const handleNewRoomCreated = async (newRoom) => {
    setShowNewRoomModal(false);
    // پس از ایجاد چت جدید، لیست چت‌ها را دوباره بارگذاری کن
    await loadRooms(true);
    handleRoomSelect(newRoom);
  };

  // کد جدید (بعد از تغییر)
  const handleDeleteChat = async () => {
    if (!selectedRoom) return;

    if (selectedRoom.isGroup) {
      setGroupManagementTab("settings");
      setShowGroupManagement(true);
    } else {
      if (window.confirm("آیا از حذف این چت اطمینان دارید؟")) {
        try {
          await chatApi.softDeletePersonalChat(selectedRoom.id);
          // از تابع موجود برای پاک‌سازی استفاده می‌کنیم
          onRoomActionCompleted(selectedRoom.id);
        } catch (error) {
          console.error("Error deleting chat:", error);
        }
      }
    }
  };

  // کد جدید (بعد از تغییر)
  const onRoomActionCompleted = (roomId) => {
    loadRooms(true); // لیست چت‌ها را به روز کن

    // اگر چت فعلی همان چتی است که رویش عملیات انجام شده، کاربر را به صفحه اصلی هدایت کن
    if (currentRoom?.id === roomId) {
      setCurrentRoom(null);
      navigate("/chat", { replace: true });
    }
  };

  if (isLoading && !rooms.length) {
    return (
      <Container
        fluid
        className="d-flex h-100 align-items-center justify-content-center"
      >
        <Spinner animation="border" variant="primary" />
      </Container>
    );
  }

  if (error && !rooms.length) {
    return (
      <Container
        fluid
        className="h-100 d-flex align-items-center justify-content-center"
      >
        <Alert variant="danger" className="text-center">
          <h5>خطا در اتصال</h5>

          <p>{error}</p>

          <Button
            variant="outline-danger"
            onClick={() => window.location.reload()}
          >
            تلاش مجدد
          </Button>
        </Alert>
      </Container>
    );
  }

  const renderSidebar = () => (
    <ChatRoomList
      rooms={rooms}
      currentRoom={currentRoom}
      onRoomSelect={handleRoomSelect}
      onNewRoom={() => setShowNewRoomModal(true)}
      isLoading={isLoading && !rooms.length}
      isAgent={isAgent}
    />
  );

  const renderChatPanelHeader = () => (
    <div className="chat-panel-header">
      {isMobile && (
        <Button
          variant="link"
          className="text-secondary p-0 me-3"
          onClick={handleMobileBack}
        >
          <FaArrowRight size={22} />
        </Button>
      )}

      <div className="d-flex align-items-center ">
        {isSupportChat ? (
          <div className="avatar-placeholder bg-warning">
            <i className="bi bi-headset fs-5"></i>
          </div>
        ) : currentRoom?.avatar ? (
          <img
            src={currentRoom.avatar}
            alt={currentRoom.name}
            className="avatar"
          />
        ) : (
          <div
            className={`avatar-placeholder ${
              currentRoom?.isGroup ? "bg-success" : "bg-primary"
            }`}
          >
            {currentRoom?.isGroup ? (
              <i className="bi bi-people-fill fs-5"></i>
            ) : (
              currentRoom?.name?.charAt(0)?.toUpperCase()
            )}
          </div>
        )}
      </div>

      <div className="flex-grow-1 min-width-0">
        <h6 className="mb-0 text-truncate">
          {isSupportChat
            ? `پشتیبانی - ${currentRoom?.name}`
            : currentRoom?.name}
        </h6>

        {typingUsers[currentRoom?.id]?.filter(
          (u) => u.userId !== currentLoggedInUserId
        ).length > 0 ? (
          <TypingIndicatorComponent
            users={typingUsers[currentRoom?.id].filter(
              (u) => u.userId !== currentLoggedInUserId
            )}
          />
        ) : (
          currentRoom?.description && (
            <small className="text-muted text-truncate d-block">
              {currentRoom?.description}
            </small>
          )
        )}
      </div>

      <div className="d-flex align-items-center gap-2">
        {selectedRoom?.isGroup && (
          <Button
            variant="light"
            onClick={() => {
              setGroupManagementTab("members");
              setShowGroupManagement(true);
            }}
            className="rounded-circle p-2"
            title="مدیریت گروه"
          >
            <PeopleFill size={20} />
          </Button>
        )}

        <Button
          variant="link"
          onClick={handleDeleteChat}
          className="text-danger p-2"
          title={selectedRoom?.isGroup ? "حذف گروه" : "حذف چت"}
        >
          <Trash size={20} />
        </Button>
      </div>

      <div className="ms-auto">
        <ConnectionStatus isConnected={isConnected} />
      </div>
    </div>
  );

  const renderChatPanelContent = () => (
    <div
      className={`chat-panel-content ${
        isMobile && currentRoom ? "d-flex flex-column" : ""
      }`}
    >
      <MessageList
        key={currentRoom?.id}
        messages={messages[currentRoom.id]?.items || []}
        isLoading={isLoading}
        hasMoreMessages={messages[currentRoom.id]?.hasMore || false}
        onLoadMoreMessages={() => {
          if (messages[currentRoom.id]?.hasMore) {
            const nextPage = (messages[currentRoom.id]?.currentPage || 0) + 1;

            handleLoadMessages(currentRoom.id, nextPage, 20, true);
          }
        }}
        isGroupChat={currentRoom?.isGroup}
        roomId={currentRoom?.id}
      />

      <MessageInput roomId={currentRoom.id} />
    </div>
  );

  return (
    <div className="chat-app-container">
      {forwardingMessage && (
        <div className="forwarding-banner">
          <p>
            پیام در حال هدایت است. یک چت را برای ارسال انتخاب کنید.
            <Button
              variant="link"
              className="text-white p-1 ms-2"
              onClick={clearForwardingMessage}
            >
              لغو
            </Button>
          </p>
        </div>
      )}
      <div
        className={`chat-main-layout ${currentRoom ? "is-room-selected" : ""}`}
      >
        <main className="chat-panel">
          {currentRoom ? (
            <>
              {renderChatPanelHeader()}

              {renderChatPanelContent()}
            </>
          ) : (
            !isMobile && (
              <div className="chat-empty-state">
                <ChatSquareText size={64} className="mb-3" />

                <h4>چتی انتخاب نشده است</h4>

                <p>برای شروع گفتگو، یک چت از لیست انتخاب کنید.</p>
              </div>
            )
          )}
        </main>

        <aside
          className={`chat-sidebar ${
            !currentRoom || !isMobile ? "is-active" : ""
          }`}
        >
          {renderSidebar()}
        </aside>
      </div>
      <NewRoomModal
        show={showNewRoomModal}
        onHide={() => setShowNewRoomModal(false)}
        onRoomCreated={handleNewRoomCreated}
      />
      <ForwardModal
        isVisible={isForwardModalVisible}
        onClose={hideForwardModal}
        rooms={rooms}
      />
      {error && (
        <div
          className="position-fixed bottom-0 end-0 p-3"
          style={{ zIndex: 1050 }}
        >
          <Alert variant="danger" onClose={clearError} dismissible>
            {error}
          </Alert>
        </div>
      )}
      <GroupManagementModal
        show={showGroupManagement}
        onHide={() => setShowGroupManagement(false)}
        chatRoom={selectedRoom}
        currentUserId={currentUser?.id}
        defaultTab={groupManagementTab}
        onGroupUpdated={() => {
          onRoomActionCompleted(selectedRoom.id);
          setShowGroupManagement(false);
        }}
        onRoomDeleted={() => onRoomActionCompleted(selectedRoom.id)}
      />
    </div>
  );
};

export default Chat;
