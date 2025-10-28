import React, { useState, useEffect, useCallback } from "react";
import {
  Container,
  Row,
  Col,
  Card,
  Button,
  Form,
  Badge,
  Spinner,
  Alert,
  ListGroup,
  InputGroup,
} from "react-bootstrap";
import {
  Search,
  Filter,
  PersonCircle,
  ChatDots,
  People,
  Activity,
  Download,
  GraphUp,
} from "react-bootstrap-icons";
import { adminChatApi } from "../../services/adminChatApi";
import AdminChatFilters from "./AdminChatFilters";
import AdminChatStatsCards from "./AdminChatStatsCards";
import AdminMessageList from "./AdminMessageList";
import "./AdminChatDashboard.css";

// تعریف برچسب‌های نوع چت
const ChatRoomTypeLabels = {
  0: { text: "خصوصی", variant: "primary" },
  1: { text: "پشتیبانی", variant: "warning" },
  2: { text: "گروهی", variant: "success" },
};

const AdminChatDashboard = () => {
  // States اصلی
  const [chats, setChats] = useState([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState(null);
  const [stats, setStats] = useState(null);
  const [showFilters, setShowFilters] = useState(false);

  // States برای لایه 3 پنل
  const [selectedUser, setSelectedUser] = useState(null);
  const [selectedChat, setSelectedChat] = useState(null);
  const [chatMessages, setChatMessages] = useState([]);
  const [messagesLoading, setMessagesLoading] = useState(false);
  const [messagesHasMore, setMessagesHasMore] = useState(false);
  const [messagesPage, setMessagesPage] = useState(1);
  const [messagesIsDeletedFilter, setMessagesIsDeletedFilter] = useState(null); // null=حذف‌نشده (پیش‌فرض سرور)، true=فقط حذف‌شده، false=فقط حذف‌نشده

  // States فیلتر و جستجو
  const [searchTerm, setSearchTerm] = useState("");
  const [filters, setFilters] = useState({
    chatRoomType: null,
    regionId: null,
    createdFrom: null,
    createdTo: null,
    isDeleted: null,
    isGroup: null,
    minMembersCount: null,
    maxMembersCount: null,
    minMessagesCount: null,
    maxMessagesCount: null,
    lastActivityFrom: null,
    lastActivityTo: null,
    sortBy: "LastActivity",
    isDescending: true,
  });

  // States صفحه‌بندی
  const [currentPage, setCurrentPage] = useState(1);
  const [pageSize] = useState(100); // تعداد بیشتر برای گروه‌بندی بهتر
  const [totalPages, setTotalPages] = useState(0);
  const [totalCount, setTotalCount] = useState(0);

  // بارگذاری چت‌ها
  const loadChats = useCallback(async () => {
    setLoading(true);
    setError(null);

    try {
      const response = await adminChatApi.getAllChats({
        ...filters,
        searchTerm,
        pageNumber: currentPage,
        pageSize,
      });

      setChats(response.items || []);
      setTotalPages(response.totalPages || 0);
      setTotalCount(response.totalCount || 0);
    } catch (err) {
      console.error("Error loading chats:", err);
      setError(err.response?.data?.message || "خطا در بارگذاری چت‌ها");
    } finally {
      setLoading(false);
    }
  }, [filters, searchTerm, currentPage, pageSize]);

  // بارگذاری آمار
  const loadStats = useCallback(async () => {
    try {
      const response = await adminChatApi.getChatStats();
      setStats(response);
    } catch (err) {
      console.error("Error loading stats:", err);
    }
  }, []);

  // بارگذاری اولیه
  useEffect(() => {
    loadChats();
    loadStats();
  }, [loadChats, loadStats]);

  // گروه‌بندی چت‌ها بر اساس کاربران
  const groupChatsByUsers = useCallback(() => {
    const userChatsMap = new Map();

    chats.forEach((chat) => {
      if (chat.members && chat.members.length > 0) {
        chat.members.forEach((member) => {
          if (!userChatsMap.has(member.userId)) {
            userChatsMap.set(member.userId, {
              userId: member.userId,
              fullName: member.fullName || "کاربر ناشناس",
              phoneNumber: member.phoneNumber,
              avatar: member.avatar,
              chats: [],
            });
          }
          userChatsMap.get(member.userId).chats.push(chat);
        });
      }
    });

    // تبدیل Map به آرایه و مرتب‌سازی بر اساس تعداد چت‌ها
    return Array.from(userChatsMap.values()).sort(
      (a, b) => b.chats.length - a.chats.length
    );
  }, [chats]);

  const users = groupChatsByUsers();

  // انتخاب کاربر
  const handleUserSelect = (user) => {
    setSelectedUser(user);
    setSelectedChat(null);
    setChatMessages([]);
    setMessagesPage(1);
  };

  // انتخاب چت و بارگذاری پیام‌ها
  const handleChatSelect = async (chat) => {
    setSelectedChat(chat);
    setMessagesLoading(true);
    setMessagesPage(1);

    try {
      const response = await adminChatApi.getChatMessages(chat.id, {
        pageNumber: 1,
        pageSize: 50,
        isDeleted: messagesIsDeletedFilter,
      });

      // اضافه کردن اطلاعات فرستنده به هر پیام
      const messagesWithSenderInfo = (response.items || []).map((msg) => {
        // پیدا کردن عضو با senderId
        const sender = chat.members?.find((m) => m.userId === msg.senderId);
        return {
          ...msg,
          senderFullName: sender?.fullName || msg.senderFullName || "کاربر ناشناس",
        };
      });

      setChatMessages(messagesWithSenderInfo);
      setMessagesHasMore(response.currentPage < response.totalPages);
    } catch (err) {
      console.error("Error loading messages:", err);
      setError("خطا در بارگذاری پیام‌ها");
    } finally {
      setMessagesLoading(false);
    }
  };

  // بارگذاری پیام‌های بیشتر (اسکرول بالا)
  const handleLoadMoreMessages = async () => {
    if (!selectedChat || messagesLoading || !messagesHasMore) return;

    setMessagesLoading(true);
    const nextPage = messagesPage + 1;

    try {
      const response = await adminChatApi.getChatMessages(selectedChat.id, {
        pageNumber: nextPage,
        pageSize: 50,
        isDeleted: messagesIsDeletedFilter,
      });

      // اضافه کردن اطلاعات فرستنده به هر پیام
      const messagesWithSenderInfo = (response.items || []).map((msg) => {
        const sender = selectedChat.members?.find((m) => m.userId === msg.senderId);
        return {
          ...msg,
          senderFullName: sender?.fullName || msg.senderFullName || "کاربر ناشناس",
        };
      });

      // اضافه کردن پیام‌های جدید به ابتدای لیست (پیام‌های قدیمی‌تر)
      setChatMessages((prev) => [...messagesWithSenderInfo, ...prev]);
      setMessagesHasMore(response.currentPage < response.totalPages);
      setMessagesPage(nextPage);
    } catch (err) {
      console.error("Error loading more messages:", err);
    } finally {
      setMessagesLoading(false);
    }
  };

  // تغییر فیلتر وضعیت حذف برای پیام‌ها و بارگذاری مجدد صفحه ۱
  const handleMessagesDeletedFilterChange = async (value) => {
    // value: 'all' | 'not-deleted' | 'deleted'
    let filter = null;
    if (value === 'deleted') filter = true;
    if (value === 'not-deleted') filter = false;
    setMessagesIsDeletedFilter(filter);

    if (!selectedChat) return;

    setMessagesLoading(true);
    setMessagesPage(1);
    try {
      const response = await adminChatApi.getChatMessages(selectedChat.id, {
        pageNumber: 1,
        pageSize: 50,
        isDeleted: filter,
      });

      const messagesWithSenderInfo = (response.items || []).map((msg) => {
        const sender = selectedChat.members?.find((m) => m.userId === msg.senderId);
        return {
          ...msg,
          senderFullName: sender?.fullName || msg.senderFullName || "کاربر ناشناس",
        };
      });

      setChatMessages(messagesWithSenderInfo);
      setMessagesHasMore(response.currentPage < response.totalPages);
    } catch (err) {
      console.error("Error loading messages with filter:", err);
    } finally {
      setMessagesLoading(false);
    }
  };

  // اعمال فیلترها
  const handleApplyFilters = (newFilters) => {
    setFilters(newFilters);
    setCurrentPage(1);
    setShowFilters(false);
  };

  // ریست فیلترها
  const handleResetFilters = () => {
    setFilters({
      chatRoomType: null,
      regionId: null,
      createdFrom: null,
      createdTo: null,
      isDeleted: null,
      isGroup: null,
      minMembersCount: null,
      maxMembersCount: null,
      minMessagesCount: null,
      maxMessagesCount: null,
      lastActivityFrom: null,
      lastActivityTo: null,
      sortBy: "LastActivity",
      isDescending: true,
    });
    setSearchTerm("");
    setCurrentPage(1);
  };

  // جستجو با debounce
  useEffect(() => {
    const timer = setTimeout(() => {
      if (searchTerm !== undefined) {
        setCurrentPage(1);
        loadChats();
      }
    }, 500);

    return () => clearTimeout(timer);
  }, [searchTerm, loadChats]);

  // دانلود گزارش
  const handleDownloadReport = () => {
    const csvContent = [
      [
        "نام چت",
        "نوع",
        "تعداد اعضا",
        "تعداد پیام‌ها",
        "تاریخ ایجاد",
        "آخرین فعالیت",
      ],
      ...chats.map((chat) => [
        chat.name,
        ChatRoomTypeLabels[chat.chatRoomType]?.text || "نامشخص",
        chat.membersCount,
        chat.messagesCount,
        new Date(chat.createdAt).toLocaleDateString("fa-IR"),
        chat.lastActivityAt
          ? new Date(chat.lastActivityAt).toLocaleDateString("fa-IR")
          : "-",
      ]),
    ]
      .map((row) => row.join(","))
      .join("\n");

    const blob = new Blob(["\ufeff" + csvContent], {
      type: "text/csv;charset=utf-8;",
    });
    const link = document.createElement("a");
    link.href = URL.createObjectURL(blob);
    link.download = `chats-report-${new Date().toISOString()}.csv`;
    link.click();
  };

  // رندر پنل کاربران (سمت چپ)
  const renderUsersPanel = () => (
    <Card className="h-100 border-0 shadow-sm">
      <Card.Header className="bg-white border-bottom">
        <div className="d-flex justify-content-between align-items-center">
          <h6 className="mb-0">
            <People className="me-2" />
            کاربران ({users.length})
          </h6>
          <Badge bg="info">{totalCount} چت</Badge>
        </div>
      </Card.Header>
      <Card.Body className="p-0">
        {loading ? (
          <div className="text-center p-4">
            <Spinner animation="border" />
          </div>
        ) : users.length === 0 ? (
          <div className="text-center text-muted p-4">
            <PersonCircle size={48} className="mb-2" />
            <p>کاربری یافت نشد</p>
          </div>
        ) : (
          <ListGroup variant="flush" className="users-list">
            {users.map((user) => (
              <ListGroup.Item
                key={user.userId}
                action
                active={selectedUser?.userId === user.userId}
                onClick={() => handleUserSelect(user)}
                className="border-0 user-list-item"
              >
                <div className="d-flex align-items-center">
                  {user.avatar ? (
                    <img
                      src={user.avatar}
                      alt={user.fullName}
                      className="rounded-circle me-3"
                      width="45"
                      height="45"
                    />
                  ) : (
                    <div className="avatar-placeholder bg-primary rounded-circle me-3 d-flex align-items-center justify-content-center">
                      <PersonCircle size={24} color="white" />
                    </div>
                  )}
                  <div className="flex-grow-1 min-width-0">
                    <div className="fw-bold text-truncate">
                      {user.fullName}
                    </div>
                    {user.phoneNumber && (
                      <small className="text-muted">{user.phoneNumber}</small>
                    )}
                    <div>
                      <Badge bg="secondary" className="mt-1">
                        {user.chats.length} چت
                      </Badge>
                    </div>
                  </div>
                </div>
              </ListGroup.Item>
            ))}
          </ListGroup>
        )}
      </Card.Body>
    </Card>
  );

  // رندر پنل چت‌های کاربر (وسط)
  const renderUserChatsPanel = () => {
    if (!selectedUser) {
      return (
        <Card className="h-100 border-0 shadow-sm">
          <Card.Body className="d-flex flex-column align-items-center justify-content-center text-muted">
            <ChatDots size={64} className="mb-3" />
            <h5>یک کاربر را انتخاب کنید</h5>
            <p>برای مشاهده چت‌های او، یک کاربر از لیست انتخاب کنید</p>
          </Card.Body>
        </Card>
      );
    }

    // تابع برای نمایش صحیح نام چت
    const getChatDisplayName = (chat) => {
      // اگر چت خصوصی است (UserToUser) و دو عضو دارد
      if (chat.chatRoomType === 0 && chat.members?.length === 2) {
        // پیدا کردن عضو دیگر (غیر از کاربر انتخاب شده)
        const otherMember = chat.members.find(m => m.userId !== selectedUser.userId);
        return otherMember?.fullName || chat.name;
      }
      // برای گروه‌ها و سایر انواع، نام اصلی چت را برمی‌گردانیم
      return chat.name;
    };

    return (
      <Card className="h-100 border-0 shadow-sm">
        <Card.Header className="bg-white border-bottom">
          <div className="d-flex justify-content-between align-items-center">
            <h6 className="mb-0">
              <ChatDots className="me-2" />
              چت‌های {selectedUser.fullName}
            </h6>
            <Badge bg="primary">{selectedUser.chats.length} چت</Badge>
          </div>
        </Card.Header>
        <Card.Body className="p-0">
          <ListGroup variant="flush" className="chats-list">
            {selectedUser.chats.map((chat) => (
              <ListGroup.Item
                key={chat.id}
                action
                active={selectedChat?.id === chat.id}
                onClick={() => handleChatSelect(chat)}
                className="border-0 chat-list-item"
              >
                <div className="d-flex align-items-start">
                  <div className="flex-grow-1 min-width-0">
                    <div className="d-flex justify-content-between align-items-center mb-1">
                      <span className="fw-bold text-truncate">
                        {getChatDisplayName(chat)}
                      </span>
                      <Badge
                        bg={ChatRoomTypeLabels[chat.chatRoomType]?.variant}
                        className="ms-2"
                      >
                        {ChatRoomTypeLabels[chat.chatRoomType]?.text}
                      </Badge>
                    </div>
                    {chat.description && (
                      <small className="text-muted d-block text-truncate mb-1">
                        {chat.description}
                      </small>
                    )}
                    <div className="d-flex justify-content-between align-items-center">
                      <small className="text-muted">
                        <People size={14} className="me-1" />
                        {chat.membersCount} عضو • {chat.messagesCount} پیام
                      </small>
                      {chat.lastActivityAt && (
                        <small className="text-muted">
                          {new Date(chat.lastActivityAt).toLocaleDateString(
                            "fa-IR",
                            {
                              month: "short",
                              day: "numeric",
                              hour: "2-digit",
                              minute: "2-digit",
                            }
                          )}
                        </small>
                      )}
                    </div>
                  </div>
                </div>
              </ListGroup.Item>
            ))}
          </ListGroup>
        </Card.Body>
      </Card>
    );
  };

  // رندر پنل پیام‌ها (سمت راست)
  const renderMessagesPanel = () => {
    if (!selectedChat) {
      return (
        <Card className="h-100 border-0 shadow-sm">
          <Card.Body className="d-flex flex-column align-items-center justify-content-center text-muted">
            <Activity size={64} className="mb-3" />
            <h5>یک چت را انتخاب کنید</h5>
            <p>برای مشاهده پیام‌های آن، یک چت از لیست انتخاب کنید</p>
          </Card.Body>
        </Card>
      );
    }

    return (
      <Card className="h-100 border-0 shadow-sm">
        <Card.Header className="bg-white border-bottom">
          <div className="d-flex justify-content-between align-items-center">
            <div>
              <h6 className="mb-0">{selectedChat.name}</h6>
              <small className="text-muted">
                {chatMessages.length} پیام
                {messagesHasMore && " (بیشتر موجود است)"}
              </small>
            </div>
            <div className="d-flex align-items-center">
              <Form.Select
                size="sm"
                className="me-2"
                style={{ width: 180 }}
                value={messagesIsDeletedFilter === null ? 'not-deleted' : (messagesIsDeletedFilter ? 'deleted' : 'not-deleted')}
                onChange={(e) => handleMessagesDeletedFilterChange(e.target.value)}
                title="فیلتر وضعیت حذف"
              >
                <option value="not-deleted">فقط حذف‌نشده</option>
                <option value="deleted">فقط حذف‌شده</option>
                <option value="all">همه پیام‌ها</option>
              </Form.Select>
              <Badge bg={ChatRoomTypeLabels[selectedChat.chatRoomType]?.variant}>
                {ChatRoomTypeLabels[selectedChat.chatRoomType]?.text}
              </Badge>
            </div>
          </div>
        </Card.Header>
        <Card.Body className="p-0 position-relative messages-panel-body">
          {messagesLoading && chatMessages.length === 0 ? (
            <div className="text-center p-4">
              <Spinner animation="border" />
            </div>
          ) : chatMessages.length === 0 ? (
            <div className="text-center text-muted p-4">
              <p>پیامی در این چت وجود ندارد</p>
            </div>
          ) : (
            <AdminMessageList
              messages={chatMessages}
              isLoading={messagesLoading}
              hasMoreMessages={messagesHasMore}
              onLoadMoreMessages={handleLoadMoreMessages}
            />
          )}
        </Card.Body>
      </Card>
    );
  };

  return (
    <Container fluid className="admin-chat-dashboard p-4">
      {/* هدر */}
      <div className="dashboard-header mb-4">
        <Row className="align-items-center mb-3">
          <Col>
            <h3 className="mb-0">
              <ChatDots className="me-2" />
              مدیریت چت‌ها
            </h3>
          </Col>
          <Col xs="auto">
            <Button
              variant="outline-primary"
              size="sm"
              className="me-2"
              onClick={handleDownloadReport}
              disabled={chats.length === 0}
            >
              <Download className="me-2" />
              دانلود گزارش
            </Button>
            <Button
              variant={showFilters ? "primary" : "outline-primary"}
              size="sm"
              onClick={() => setShowFilters(!showFilters)}
            >
              <Filter className="me-2" />
              فیلترها
            </Button>
          </Col>
        </Row>

        {/* آمار */}
        {stats && <AdminChatStatsCards stats={stats} />}

        {/* جستجو و فیلترها */}
        <Row className="mt-3">
          <Col md={6}>
            <InputGroup>
              <InputGroup.Text>
                <Search />
              </InputGroup.Text>
              <Form.Control
                type="text"
                placeholder="جستجو در چت‌ها..."
                value={searchTerm}
                onChange={(e) => setSearchTerm(e.target.value)}
              />
            </InputGroup>
          </Col>
          <Col md={6} className="text-start">
            <Button
              variant="outline-secondary"
              size="sm"
              onClick={handleResetFilters}
            >
              ریست فیلترها
            </Button>
          </Col>
        </Row>

        {/* پنل فیلترها */}
        {showFilters && (
          <div className="mt-3">
            <AdminChatFilters
              filters={filters}
              onApply={handleApplyFilters}
              onClose={() => setShowFilters(false)}
            />
          </div>
        )}
      </div>

      {/* خطاها */}
      {error && (
        <Alert
          variant="danger"
          dismissible
          onClose={() => setError(null)}
          className="mb-3"
        >
          {error}
        </Alert>
      )}

      {/* لایه 3 پنل */}
      <Row className="three-panel-layout g-3" style={{ minHeight: "600px" }}>
        <Col md={3} className="panel-col">
          {renderUsersPanel()}
        </Col>
        <Col md={4} className="panel-col">
          {renderUserChatsPanel()}
        </Col>
        <Col md={5} className="panel-col">
          {renderMessagesPanel()}
        </Col>
      </Row>

      {/* صفحه‌بندی */}
      {totalPages > 1 && (
        <div className="d-flex justify-content-center align-items-center mt-4">
          <Button
            variant="outline-primary"
            size="sm"
            disabled={currentPage === 1}
            onClick={() => setCurrentPage((prev) => prev - 1)}
            className="me-2"
          >
            قبلی
          </Button>
          <span className="mx-3">
            صفحه {currentPage} از {totalPages}
          </span>
          <Button
            variant="outline-primary"
            size="sm"
            disabled={currentPage === totalPages}
            onClick={() => setCurrentPage((prev) => prev + 1)}
          >
            بعدی
          </Button>
        </div>
      )}
    </Container>
  );
};

export default AdminChatDashboard;
