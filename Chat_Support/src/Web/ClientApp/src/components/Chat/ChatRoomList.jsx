import React, { useState, useRef, useMemo } from "react";
import {
  ListGroup,
  Button,
  Form,
  Spinner,
  Badge,
  Tabs,
  Tab,
  Offcanvas,
  Collapse,
} from "react-bootstrap";
import {
  Search,
  Headset,
  List,
  ArrowRight,
  PencilFill,
  ChevronDown,
  PeopleFill,
  Check,
} from "react-bootstrap-icons";
import { useChat } from "../../hooks/useChat";
import { chatApi } from "../../services/chatApi";
import { useQueryClient } from "@tanstack/react-query";
import { useAuth } from "../../hooks/useAuth";

const ChatRoomList = ({
  rooms = [],
  currentRoom,
  onRoomSelect,
  onNewRoom,
  isLoading,
  isAgent = false,
}) => {
  const queryClient = useQueryClient();
  const [searchTerm, setSearchTerm] = useState("");

  const { createChatRoom, currentLoggedInUserId } = useChat();
  const { accounts = [], activeAccountId, switchAccount, logout } = useAuth();

  const activeAccount = useMemo(
    () =>
      accounts.find((a) => String(a.id) === String(activeAccountId)) || null,
    [accounts, activeAccountId]
  );
  const activePhone =
    activeAccount?.userName ||
    activeAccount?.fullName ||
    (activeAccount ? `User ${activeAccount.id}` : "بدون حساب");

  const [usersWithoutActiveChat, setUsersWithoutActiveChat] = useState([]);
  const [showUserSearchResults, setShowUserSearchResults] = useState(false);
  const searchInputRef = useRef(null);
  const [activeTab, setActiveTab] = useState("all"); // all | support
  const [isSearchActive, setIsSearchActive] = useState(false);
  const [showSettingsDrawer, setShowSettingsDrawer] = useState(false);
  const [accountsOpen, setAccountsOpen] = useState(true);

  const filterUsersWithoutChat = (users, activeRooms) => {
    const usersWithChat = new Set();
    activeRooms.forEach((room) => {
      if (!room.isGroup && room.members) {
        room.members.forEach((member) => {
          if (member.userId !== currentLoggedInUserId) {
            usersWithChat.add(member.userId);
          }
        });
      }
    });
    return users.filter(
      (user) => !usersWithChat.has(user.id) && user.id !== currentLoggedInUserId
    );
  };

  const updateUsersList = async (searchValue = "") => {
    try {
      const users = await chatApi.searchUsers(searchValue);
      const filteredUsers = filterUsersWithoutChat(users, rooms);
      setUsersWithoutActiveChat(filteredUsers);
    } catch (error) {
      console.error("Error updating users list:", error);
      setUsersWithoutActiveChat([]);
    }
  };

  const handleSearchFocus = async () => {
    setShowUserSearchResults(true);
    await updateUsersList(searchTerm);
  };

  const handleSearchBlur = () => {
    setTimeout(() => {
      setShowUserSearchResults(false);
      if (!currentRoom) {
        setSearchTerm("");
      }
    }, 200);
  };

  const handleSearchChange = async (e) => {
    const value = e.target.value;
    setSearchTerm(value);
    if (showUserSearchResults) {
      await updateUsersList(value);
    }
  };

  const handleUserSelectForChat = async (selectedUser) => {
    if (!selectedUser?.id) return;

    try {
      const newRoom = await createChatRoom({
        name: selectedUser.fullName,
        isGroup: false,
        memberIds: [selectedUser.id],
      });

      if (newRoom) {
        queryClient.invalidateQueries(["chatRooms"]);
        onRoomSelect(newRoom);
      }

      setSearchTerm("");
      setShowUserSearchResults(false);
      setUsersWithoutActiveChat([]);
    } catch (error) {
      console.error("Error creating private chat:", error);
    }
  };

  const formatDate = (dateString) => {
    if (!dateString) return "";
    const date = new Date(dateString);
    const today = new Date();
    if (date.toDateString() === today.toDateString()) {
      return date.toLocaleTimeString("fa-IR", {
        hour: "2-digit",
        minute: "2-digit",
      });
    }
    return date.toLocaleDateString("fa-IR", {
      day: "2-digit",
      month: "2-digit",
    });
  };

  // Tabs
  const supportRooms = rooms.filter((room) => room.chatRoomType === 1);
  const normalRooms = rooms.filter((room) => room.chatRoomType !== 1);
  const normalUnreadTotal = normalRooms.reduce(
    (sum, r) => sum + (r.unreadCount || 0),
    0
  );
  const supportUnreadTotal = supportRooms.reduce(
    (sum, r) => sum + (r.unreadCount || 0),
    0
  );
  const listToShow =
    isAgent && activeTab === "support" ? supportRooms : normalRooms;
  const filteredRooms = listToShow.filter((room) =>
    room.name?.toLowerCase().includes(searchTerm.toLowerCase())
  );

  const activateSearch = () => {
    setIsSearchActive(true);
    setTimeout(() => {
      searchInputRef.current?.focus();
    }, 0);
    if (!(isAgent && activeTab === "support")) handleSearchFocus();
  };
  const deactivateSearch = () => {
    setIsSearchActive(false);
    setShowUserSearchResults(false);
    setSearchTerm("");
  };

  const renderSearchInline = () => (
    <div className="d-flex align-items-center w-100 gap-2 telegram-search-inline">
      <Button
        variant="light"
        size="sm"
        className="icon-btn"
        onClick={deactivateSearch}
        title="بازگشت"
      >
        <ArrowRight size={18} />
      </Button>
      <div className="position-relative flex-grow-1">
        <Search className="search-inline-icon" size={16} />
        <Form.Control
          type="text"
          placeholder={
            isAgent && activeTab === "support"
              ? "جستجو در چت‌های پشتیبانی..."
              : "جستجو..."
          }
          value={searchTerm}
          ref={searchInputRef}
          onBlur={
            isAgent && activeTab === "support" ? undefined : handleSearchBlur
          }
          onChange={handleSearchChange}
          className="search-input telegram"
          autoComplete="off"
        />
      </div>
    </div>
  );

  const renderRoomList = () => {
    if (isLoading) {
      return (
        <div className="d-flex justify-content-center p-4">
          <Spinner animation="border" size="sm" />
        </div>
      );
    }

    if (showUserSearchResults && !(isAgent && activeTab === "support")) {
      return usersWithoutActiveChat.length > 0 ? (
        <>
          <div className="user-search-list-separator">کاربران جدید</div>
          {usersWithoutActiveChat.map((user) => (
            <ListGroup.Item
              key={user.id}
              className="user-search-list-item"
              action
              onMouseDown={() => handleUserSelectForChat(user)}
            >
              <span className="user-search-avatar bg-secondary text-light me-2">
                {user.fullName?.charAt(0)}
              </span>
              <span className="fw-bold">{user.fullName}</span>
              <span className="text-muted ms-2">{user.userName}</span>
            </ListGroup.Item>
          ))}
        </>
      ) : (
        <div className="text-center p-4 text-muted">
          {searchTerm
            ? "کاربری یافت نشد"
            : "برای شروع چت، کاربری را جستجو کنید"}
        </div>
      );
    }

    if (filteredRooms.length === 0) {
      const noSearch = !searchTerm;
      if (isAgent && activeTab === "support")
        return (
          <div className="text-center p-4 text-muted">
            {noSearch
              ? "در حال حاضر چت پشتیبانی ندارید"
              : "چت پشتیبانی یافت نشد"}
          </div>
        );
      return (
        <div className="text-center p-4 text-muted">
          {noSearch ? "هنوز چتی ندارید" : "چتی یافت نشد"}
        </div>
      );
    }

    return filteredRooms.map((room) => (
      <ListGroup.Item
        key={room.id}
        action
        active={currentRoom?.id === room.id}
        onClick={() => onRoomSelect(room)}
        className="chatroom-list-item"
      >
        <div className="position-relative">
          {room.avatar ? (
            <img src={room.avatar} alt={room.name} className="avatar" />
          ) : (
            <div
              className={`avatar-placeholder ${
                room.isGroup
                  ? "bg-success"
                  : room.chatRoomType === 1
                  ? "bg-warning"
                  : "bg-primary"
              }`}
            >
              {room.isGroup ? (
                <PeopleFill className="fs-5" />
              ) : room.chatRoomType === 1 ? (
                <Headset className="fs-5" />
              ) : (
                room.name.charAt(0).toUpperCase()
              )}
            </div>
          )}
        </div>
        <div className="chatroom-info">
          <div className="d-flex justify-content-between">
            <h6 className="mb-0 text-truncate fw-bold">{room.name}</h6>
            <small className="text-muted flex-shrink-0">
              {room.lastMessageTime && formatDate(room.lastMessageTime)}
            </small>
          </div>
          <div className="d-flex justify-content-between align-items-center">
            <small className="text-muted text-truncate">
              {room.lastMessageContent || room.description || ""}
            </small>
            {room.unreadCount > 0 && (
              <Badge className="unread-badge" pill>
                {room.unreadCount > 9 ? "9+" : room.unreadCount}
              </Badge>
            )}
          </div>
        </div>
      </ListGroup.Item>
    ));
  };

  const renderTabTitle = (label, count, icon) => (
    <span className="d-inline-flex align-items-center gap-1 position-relative">
      {icon}
      <span>{label}</span>
      {count > 0 && (
        <Badge className="tab-unread-badge" pill>
          {count > 99 ? "99+" : count}
        </Badge>
      )}
    </span>
  );

  const renderAccounts = () => (
    <div className="mb-3">
      <div
        className="d-flex align-items-center justify-content-between"
        role="button"
        onClick={() => setAccountsOpen((s) => !s)}
        aria-expanded={accountsOpen}
        aria-controls="accounts-collapse"
      >
        <div className="small fw-semibold">{activePhone}</div>
        <ChevronDown
          size={18}
          style={{
            transform: accountsOpen ? "rotate(180deg)" : "rotate(0deg)",
            transition: "transform 360ms cubic-bezier(0.2, 0.8, 0.2, 1)",
          }}
          aria-hidden
        />
      </div>
      <Collapse in={accountsOpen}>
        <div id="accounts-collapse">
          <ListGroup variant="flush">
            {accounts.length === 0 && (
              <ListGroup.Item className="text-muted small">
                هیچ حسابی موجود نیست
              </ListGroup.Item>
            )}
            {accounts.map((acc) => {
              const isActive = String(activeAccountId) === String(acc.id);
              return (
                <ListGroup.Item
                  key={acc.id}
                  action
                  onClick={() => switchAccount(acc.id)}
                  className="d-flex align-items-center justify-content-between"
                >
                  <div className="d-flex align-items-center gap-2">
                    <div
                      className="position-relative"
                      style={{ width: 28, height: 28 }}
                    >
                      <div
                        className="avatar-placeholder bg-secondary text-white"
                        style={{
                          width: 28,
                          height: 28,
                          borderRadius: "50%",
                          display: "inline-flex",
                          alignItems: "center",
                          justifyContent: "center",
                        }}
                      >
                        {acc.fullName?.charAt(0) ||
                          acc.userName?.charAt(0) ||
                          "U"}
                      </div>
                      {isActive && (
                        <span
                          className="bg-success rounded-circle"
                          style={{
                            position: "absolute",
                            bottom: -2,
                            right: -2,
                            width: 12,
                            height: 12,
                            display: "inline-flex",
                            alignItems: "center",
                            justifyContent: "center",
                            border: "2px solid #fff",
                          }}
                          title="حساب فعال"
                        >
                          <Check
                            size={10}
                            style={{ lineHeight: 1, color: "#fff" }}
                          />
                        </span>
                      )}
                    </div>
                    <div className="d-flex flex-column">
                      <span className="small fw-semibold">
                        {acc.fullName || acc.userName || `User ${acc.id}`}
                      </span>
                      <span
                        className="text-muted"
                        style={{ fontSize: "0.75rem" }}
                      >
                        {acc.userName || acc.id}
                      </span>
                    </div>
                  </div>
                </ListGroup.Item>
              );
            })}
            <ListGroup.Item
              action
              className="text-primary"
              onClick={() => {
                window.location.assign("/login?add=1&returnUrl=/chat");
              }}
            >
              + افزودن حساب جدید
            </ListGroup.Item>
          </ListGroup>
        </div>
      </Collapse>
    </div>
  );

  const renderBottomToggle = () => (
    <div
      style={{
        position: "sticky",
        bottom: 0,
        background: "#fff",
        borderTop: "1px solid rgba(0,0,0,0.08)",
      }}
      className="p-2 d-flex align-items-center justify-content-between"
    >
      <div
        className="text-truncate"
        title={activePhone}
        style={{ maxWidth: "70%" }}
      >
        <small className="text-muted">حساب فعال:</small>{" "}
        <span className="fw-semibold">{activePhone}</span>
      </div>
      <Button
        variant="light"
        size="sm"
        className="d-flex align-items-center"
        onClick={() => {
          setShowSettingsDrawer(true);
          setAccountsOpen((s) => !s);
        }}
        aria-label="باز/بسته حساب‌ها"
        aria-expanded={accountsOpen}
        aria-controls="accounts-collapse"
      >
        <ChevronDown
          size={18}
          style={{
            transform: accountsOpen ? "rotate(180deg)" : "rotate(0deg)",
            transition: "transform 360ms cubic-bezier(0.2, 0.8, 0.2, 1)",
          }}
        />
      </Button>
    </div>
  );

  return (
    <div className="d-flex flex-column h-100">
      <div className="sidebar-header p-0">
        <div
          className={`chat-top-bar d-flex align-items-center p-1 ${
            isSearchActive ? "is-search-active" : ""
          }`}
        >
          {!isSearchActive && (
            <>
              <div className="ms-auto d-flex align-items-center gap-2 actions-bar">
                <Button
                  variant="light"
                  className="icon-btn p-1"
                  title="جستجو"
                  onClick={activateSearch}
                >
                  <Search size={20} />
                </Button>
                {isAgent && activeTab === "support" ? (
                  <Button
                    variant="outline-warning"
                    className="rounded-circle p-1 d-flex"
                    title="چت‌های پشتیبانی"
                  >
                    <Headset size={18} />
                  </Button>
                ) : null}
              </div>
              <div className="chat-logo-wrapper me-2">
                <img src="/images/logo.png" alt="لوگو" className="chat-logo" />
              </div>
              <Button
                variant="light"
                className="icon-btn p-0"
                title="منو"
                onClick={() => setShowSettingsDrawer(true)}
              >
                <List size={20} />
              </Button>
            </>
          )}
          {isSearchActive && renderSearchInline()}
        </div>
        {isAgent && (
          <div className="chat-telegram-tabs">
            <Tabs
              id="chat-tabs"
              activeKey={activeTab}
              onSelect={(k) => {
                setActiveTab(k);
                setShowUserSearchResults(false);
                deactivateSearch();
              }}
              justify
              className="mt-0"
            >
              <Tab
                eventKey="all"
                title={renderTabTitle("چت", normalUnreadTotal, null)}
              />
              <Tab
                eventKey="support"
                title={renderTabTitle(
                  "پشتیبانی",
                  supportUnreadTotal,
                  null
                )}
              />
            </Tabs>
          </div>
        )}
      </div>

      <ListGroup variant="flush" className="chatroom-list hide-scrollbar">
        {renderRoomList()}
      </ListGroup>

      {/* {renderBottomToggle()} */}

      {/* Floating compose button (Telegram-like) */}
      {!(isAgent && activeTab === "support") && (
        <Button
          variant="primary"
          onClick={onNewRoom}
          className="floating-compose-btn shadow"
          aria-label="چت جدید"
          title="چت جدید"
        >
          <PencilFill size={20} />
        </Button>
      )}

      <Offcanvas
        show={showSettingsDrawer}
        onHide={() => setShowSettingsDrawer(false)}
        placement="start"
        className="settings-drawer"
        backdrop
      >
        <Offcanvas.Header closeButton>
          <Offcanvas.Title>تنظیمات</Offcanvas.Title>
        </Offcanvas.Header>
        <Offcanvas.Body>
          {renderAccounts()}
          <ListGroup variant="flush" className="small">
            <ListGroup.Item action onClick={() => alert("به زودی")}>
              تنظیمات (به زودی)
            </ListGroup.Item>
            <ListGroup.Item action className="text-danger" onClick={logout}>
              خروج از حساب فعلی
            </ListGroup.Item>
          </ListGroup>
        </Offcanvas.Body>
      </Offcanvas>
    </div>
  );
};

export default ChatRoomList;
