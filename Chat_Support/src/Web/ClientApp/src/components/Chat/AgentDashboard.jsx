import React, { useState, useEffect, useCallback, useMemo } from 'react';
import { Container, Row, Col, Card, Button, Form, Badge, ListGroup, Spinner, Alert, ButtonGroup, Modal, InputGroup } from 'react-bootstrap';
import { MessageSquare, Clock, CheckCircle, ArrowRight, RefreshCw, Mail, Circle, Phone, Search } from 'lucide-react';
import { chatApi } from '../../services/chatApi';
import { ChatProvider } from '../../contexts/ChatContext';
import { useChat } from '../../hooks/useChat';
import MessageList from './MessageList';
import MessageInput from './MessageInput';
import ConnectionStatus from './ConnectionStatus';
import signalRService from '../../services/signalRService';
import './Chat.css';

const STATUS_MAP = {
  all: null,
  open: 0,
  investigating: 1,
  resolved: 2,
  closed: 3,
};

const AgentDashboardInner = () => {
  const [tickets, setTickets] = useState([]);
  const [selectedTicket, setSelectedTicket] = useState(null);
  const [agentStatus, setAgentStatus] = useState('Available');
  const [statusInfo, setStatusInfo] = useState(null); // اطلاعات کامل وضعیت
  const [stats, setStats] = useState({ activeChats: 0, resolvedToday: 0, avgResponseTime: '2m 15s' });
  const [isLoading, setIsLoading] = useState(true);
  const [agentsModal, setAgentsModal] = useState({ show: false, agents: [], selected: null, reason: '' });
  const [search] = useState('');
  const [statusFilter] = useState('all');
  const [isMobile, setIsMobile] = useState(typeof window !== 'undefined' ? window.innerWidth <= 768 : false);

  // Chat context (real-time + SignalR)
  const {
    rooms,
    currentRoom,
    messages,
    isConnected,
    isLoading: isChatLoading,
    setCurrentRoom,
    loadRooms,
    loadMessages,
    joinRoom,
    markAllMessagesAsReadInRoom,
  } = useChat();

  const loadTickets = useCallback(async () => {
    try {
      const list = await chatApi.getSupportTickets();
      setTickets(list);
      setStats((prev) => ({ ...prev, activeChats: list.filter((t) => t.status < 2).length }));
    } catch (error) {
      console.error('Failed to load tickets:', error);
    } finally {
      setIsLoading(false);
    }
  }, []);

  const loadStatusInfo = useCallback(async () => {
    try {
      const info = await chatApi.getAgentStatusInfo();
      console.log('Status info received:', info);
      setStatusInfo(info);
      // تبدیل مقدار عددی یا string به نام وضعیت برای select
      const statusNames = ['Available', 'Busy', 'Away', 'Offline'];
      let statusName = info.currentStatus;
      
      // اگر عدد است، تبدیل به نام کن
      if (typeof info.currentStatus === 'number') {
        statusName = statusNames[info.currentStatus] || 'Available';
      } else if (typeof info.currentStatus === 'string') {
        // اگر string است، بررسی کن که یکی از مقادیر معتبر باشد
        statusName = statusNames.includes(info.currentStatus) ? info.currentStatus : 'Available';
      }
      
      setAgentStatus(statusName);
      console.log('Agent status set to:', statusName);
    } catch (error) {
      console.error('Failed to load status info:', error);
    }
  }, []);

  useEffect(() => {
    loadTickets();
    loadRooms(); // load chat rooms for agent once
    loadStatusInfo(); // بارگذاری اطلاعات وضعیت
    const interval = setInterval(() => {
      loadTickets();
      loadStatusInfo(); // بروزرسانی وضعیت هر 30 ثانیه
    }, 30000);
    return () => clearInterval(interval);
  }, [loadRooms, loadStatusInfo, loadTickets]);

  // window resize -> mobile toggle
  useEffect(() => {
    const onResize = () => setIsMobile(window.innerWidth <= 768);
    window.addEventListener('resize', onResize);
    return () => window.removeEventListener('resize', onResize);
  }, []);

  // Real-time handlers for transfer and close
  useEffect(() => {
    const handleNewSupportChat = (data) => {
      loadRooms();
      loadTickets();
      if (selectedTicket && selectedTicket.chatRoomId === data.ChatRoomId) {
        joinRoom(data.ChatRoomId);
      }
    };
    const handleChatTransferred = () => {
      loadRooms();
      loadTickets();
    };
    const handleTicketClosed = (payload) => {
      loadTickets();
      if (selectedTicket && payload.TicketId === selectedTicket.id) {
        setSelectedTicket({ ...selectedTicket, status: 3 });
      }
    };

    signalRService.addEventListener('NewSupportChat', handleNewSupportChat);
    signalRService.addEventListener('ChatTransferred', handleChatTransferred);
    signalRService.addEventListener('TicketClosed', handleTicketClosed);

    return () => {
      signalRService.removeEventListener('NewSupportChat', handleNewSupportChat);
      signalRService.removeEventListener('ChatTransferred', handleChatTransferred);
      signalRService.removeEventListener('TicketClosed', handleTicketClosed);
    };
  }, [selectedTicket, joinRoom, loadRooms, loadTickets]);

  // Derived filtered tickets
  const filteredTickets = useMemo(() => {
    const q = search.trim().toLowerCase();
    const st = STATUS_MAP[statusFilter];
    return tickets.filter((t) => {
      const matchesQuery = !q || [t.requesterName, t.requesterEmail, t.requesterPhone, `#${t.id}`]
        .filter(Boolean)
        .some((v) => String(v).toLowerCase().includes(q));
      const matchesStatus = st === null || t.status === st;
      return matchesQuery && matchesStatus;
    });
  }, [tickets, search, statusFilter]);

  // Sync selected ticket to current chat room (live in-page chat)
  const selectTicketAndOpenChat = useCallback(async (ticket) => {
    setSelectedTicket(ticket);
    let room = rooms.find((r) => r.id === ticket.chatRoomId);
    if (!room) {
      await loadRooms();
      room = rooms.find((r) => r.id === ticket.chatRoomId) || { id: ticket.chatRoomId, name: ticket.requesterName, isGroup: false, chatRoomType: 1 };
    }
    setCurrentRoom(room);
    try { await joinRoom(ticket.chatRoomId); } catch (error) { console.error('Join room failed:', error); }
    await loadMessages(ticket.chatRoomId, 1, 20, false);
    markAllMessagesAsReadInRoom(ticket.chatRoomId);
  }, [rooms, loadRooms, setCurrentRoom, joinRoom, loadMessages, markAllMessagesAsReadInRoom]);

  const handleAgentStatusChange = async (value) => {
    console.log('Changing status to:', value);
    setAgentStatus(value);
    
    // تبدیل مقدار string به عدد مطابق enum
    const statusMap = { Available: 0, Busy: 1, Away: 2, Offline: 3 };
    const statusValue = statusMap[value] ?? 0;
    
    console.log('Sending status value to backend:', statusValue);
    
    try {
      const result = await chatApi.updateAgentStatus(statusValue);
      console.log('Status update response:', result);
      
      // بارگذاری مجدد اطلاعات وضعیت برای نمایش زمان انقضا
      await loadStatusInfo();
    } catch (error) {
      console.error('Failed to update status:', error);
      console.error('Error details:', error.response?.data);
    }
  };

  const handleOpenTransfer = async (ticket) => {
    setSelectedTicket(ticket);
    try {
      const agents = await chatApi.getAvailableAgents(ticket.regionId);
      setAgentsModal({ show: true, agents, selected: null, reason: '' });
    } catch (error) {
      console.error('Failed to load agents:', error);
      setAgentsModal({ show: true, agents: [], selected: null, reason: '' });
    }
  };

  const handleTransfer = async () => {
    if (!selectedTicket || !agentsModal.selected) return;
    try {
      await chatApi.transferTicket(selectedTicket.id, agentsModal.selected, agentsModal.reason || undefined);
      setAgentsModal({ show: false, agents: [], selected: null, reason: '' });
      await loadTickets();
      await loadRooms();
      await selectTicketAndOpenChat({ ...selectedTicket });
    } catch (error) {
      console.error('Transfer failed:', error);
    }
  };

  const handleClose = async (ticket) => {
    try {
      await chatApi.closeTicket(ticket.id, 'Closed by agent');
      await loadTickets();
      if (selectedTicket && selectedTicket.id === ticket.id) setSelectedTicket({ ...selectedTicket, status: 3 });
    } catch (error) {
      console.error('Close ticket failed:', error);
    }
  };

  const handleMobileBack = useCallback(() => {
    setSelectedTicket(null);
    setCurrentRoom(null);
    loadRooms();
  }, [setCurrentRoom, loadRooms]);

  const getStatusVariant = (status) => ({ 0: 'primary', 1: 'warning', 2: 'success', 3: 'secondary' }[status] || 'secondary');
  const getStatusText = (status) => ({ 0: 'باز', 1: 'در حال بررسی', 2: 'حل شده', 3: 'بسته شده' }[status] || 'نامشخص');

  const renderStatusBadge = () => {
    if (!statusInfo) return null;

    const timeRemainingMinutes = statusInfo.timeRemainingMinutes || 0;
    const hours = Math.floor(timeRemainingMinutes / 60);
    const minutes = Math.floor(timeRemainingMinutes % 60);
    const timeText = hours > 0 ? `${hours}س ${minutes}د` : `${minutes}د`;

    return (
      <div className="d-flex align-items-center gap-2" style={{ fontSize: '0.85rem' }}>
        {statusInfo.isManuallySet && (
          <Badge bg="info" className="d-flex align-items-center gap-1">
            <Clock size={14} />
            <span>دستی: {timeText} باقیمانده</span>
          </Badge>
        )}
        {!statusInfo.isManuallySet && (
          <Badge bg="secondary" className="d-flex align-items-center gap-1">
            <Circle size={10} style={{ fill: 'currentColor' }} />
            <span>خودکار</span>
          </Badge>
        )}
      </div>
    );
  };

  const renderTopbar = () => (
    <div className="dashboard-topbar px-3 py-2 d-flex align-items-center justify-content-between">
      <div className="d-flex align-items-center gap-2 min-width-0">
        <h5 className="mb-0 text-truncate">پنل پشتیبان</h5>
        <Badge bg="light" text="dark">در حال کار</Badge>
        {renderStatusBadge()}
      </div>
      <div className="d-flex align-items-center gap-2 agent-controls">
        <ConnectionStatus isConnected={isConnected} />
        <Form.Select 
          size="sm" 
          value={agentStatus} 
          onChange={(e) => handleAgentStatusChange(e.target.value)} 
          style={{ minWidth: 140 }}
          title={statusInfo?.isManuallySet ? `وضعیت دستی تا ${new Date(statusInfo.expiresAt).toLocaleTimeString('fa-IR')} معتبر است` : 'وضعیت خودکار توسط سیستم'}
        >
          <option value="Available">در دسترس</option>
          <option value="Busy">مشغول</option>
          <option value="Away">غایب</option>
          <option value="Offline">آفلاین</option>
        </Form.Select>
        <Button variant="outline-secondary" size="sm" onClick={() => { loadTickets(); loadStatusInfo(); }} className="d-inline-flex align-items-center">
          <RefreshCw size={16} className="me-1" /> بروزرسانی
        </Button>
      </div>
    </div>
  );

  const renderSidebar = () => (
    <div className="dashboard-sidebar">
      <div className="dashboard-sidebar-header p-3">
        <div className="d-flex justify-content-between align-items-center">
          <h6 className="mb-0">تیکت‌ها</h6>
          <Badge bg="secondary" pill>{tickets.length}</Badge>
        </div>
      </div>
      <div className="p-3 pt-2">
        <div className="ticket-list">
          {isLoading ? (
            <div className="text-center py-5"><Spinner /></div>
          ) : filteredTickets.length === 0 ? (
            <Alert variant="light" className="text-center">نتیجه‌ای یافت نشد</Alert>
          ) : (
            <ListGroup variant="flush">
              {filteredTickets.map((ticket) => (
                <ListGroup.Item key={ticket.id} action active={selectedTicket?.id === ticket.id} onClick={() => selectTicketAndOpenChat(ticket)} className="ticket-item mb-2 border rounded">
                  <div className="d-flex align-items-start gap-2">
                    <div className="avatar-placeholder bg-primary-subtle text-primary fw-bold">{(ticket.requesterName || '?').charAt(0)}</div>
                    <div className="flex-grow-1 min-width-0">
                      <div className="d-flex justify-content-between align-items-center mb-1">
                        <strong className="ticket-title text-truncate">{ticket.requesterName}</strong>
                        <div className="d-flex align-items-center gap-2">
                          {typeof ticket.unreadCount === 'number' && ticket.unreadCount > 0 && (
                            <span className="unread-badge">{ticket.unreadCount}</span>
                          )}
                          <Badge bg={getStatusVariant(ticket.status)}>{getStatusText(ticket.status)}</Badge>
                        </div>
                      </div>
                      <div className="small text-muted text-truncate">
                        {ticket.lastMessage?.content || '—'}
                      </div>
                      <div className="d-flex justify-content-between align-items-center mt-1">
                        <small className="ticket-time text-muted">{new Date(ticket.created).toLocaleTimeString()}</small>
                        <div className="d-flex align-items-center gap-2">
                          {ticket.regionId && <Badge bg="info">{ticket.regionTitle ? ticket.regionTitle : `Region #${ticket.regionId}`}</Badge>}
                          <small className="text-muted">#{ticket.id}</small>
                        </div>
                      </div>
                    </div>
                  </div>
                </ListGroup.Item>
              ))}
            </ListGroup>
          )}
        </div>
      </div>
    </div>
  );

  const renderMainContent = () => (
    <div className="dashboard-main p-0">
      {selectedTicket ? (
        <>
          <div className="chat-panel-header px-3 py-2 d-flex align-items-center gap-2">
            {isMobile && (
              <Button variant="link" className="text-secondary p-0 ms-2 icon_flip" onClick={handleMobileBack} aria-label="بازگشت">
                <ArrowRight size={22} />
              </Button>
            )}
            <div className="avatar-placeholder bg-warning"><i className="bi bi-headset fs-6" /></div>
            <div className="min-width-0 flex-grow-1">
              <h6 className="mb-0 text-truncate">{selectedTicket.requesterName}</h6>
              <div className="d-flex flex-wrap gap-2 text-muted small">
                {selectedTicket.requesterEmail && (<span><Mail size={14} className="me-1" />{selectedTicket.requesterEmail}</span>)}
                {selectedTicket.requesterPhone && (<span><Phone size={14} className="me-1" />{selectedTicket.requesterPhone}</span>)}
                {selectedTicket.regionId && (<Badge bg="info">{selectedTicket.regionTitle ? selectedTicket.regionTitle : `Region #${selectedTicket.regionId}`}</Badge>)}
              </div>
            </div>
            <div className="d-flex align-items-center gap-2 flex-wrap">
              <Badge bg={getStatusVariant(selectedTicket.status)}>{getStatusText(selectedTicket.status)}</Badge>
              <ButtonGroup>
                <Button variant="outline-secondary" size="sm" onClick={() => handleOpenTransfer(selectedTicket)}>
                  <ArrowRight size={16} className="me-1" /> انتقال
                </Button>
                <Button variant="outline-success" size="sm" onClick={() => handleClose(selectedTicket)}>
                  <CheckCircle size={16} className="me-1" /> بستن
                </Button>
              </ButtonGroup>
            </div>
          </div>

          <div className="chat-panel-content">
            <div className="message-list-container">
              {isChatLoading && !messages[selectedTicket.chatRoomId]?.items ? (
                <div className="text-center py-4"><Spinner /></div>
              ) : (
                <MessageList
                  key={currentRoom?.id || selectedTicket.chatRoomId}
                  messages={messages[selectedTicket.chatRoomId]?.items || []}
                  isLoading={isChatLoading}
                  hasMoreMessages={messages[selectedTicket.chatRoomId]?.hasMore || false}
                  onLoadMoreMessages={() => {
                    if (messages[selectedTicket.chatRoomId]?.hasMore) {
                      const nextPage = (messages[selectedTicket.chatRoomId]?.currentPage || 0) + 1;
                      loadMessages(selectedTicket.chatRoomId, nextPage, 20, true);
                    }
                  }}
                  isGroupChat={false}
                  roomId={selectedTicket.chatRoomId}
                />
              )}
            </div>
            {currentRoom && (
              <div className="message-input-container p-2">
                <MessageInput roomId={selectedTicket.chatRoomId} />
              </div>
            )}
          </div>
        </>
      ) : (
        <div className="chat-empty-state">
          <MessageSquare size={64} className="mb-3" />
          <h4>تیکتی انتخاب نشده است</h4>
          <p>برای مشاهده و پاسخ، یک تیکت را از لیست انتخاب کنید.</p>
        </div>
      )}

      <Modal show={agentsModal.show} onHide={() => setAgentsModal({ show: false, agents: [], selected: null, reason: '' })} centered>
        <Modal.Header closeButton>
          <Modal.Title>انتقال تیکت</Modal.Title>
        </Modal.Header>
        <Modal.Body>
          <Form.Group className="mb-3">
            <Form.Label>انتخاب کارشناس</Form.Label>
            <Form.Select value={agentsModal.selected || ''} onChange={(e) => setAgentsModal((s) => ({ ...s, selected: parseInt(e.target.value) || null }))}>
              <option value="">— انتخاب —</option>
              {agentsModal.agents.map(a => (
                <option key={a.userId} value={a.userId}>{a.name} — {a.currentActiveChats}/{a.maxConcurrentChats}</option>
              ))}
            </Form.Select>
          </Form.Group>
          <Form.Group>
            <Form.Label>دلیل (اختیاری)</Form.Label>
            <Form.Control value={agentsModal.reason} onChange={(e) => setAgentsModal((s) => ({ ...s, reason: e.target.value }))} />
          </Form.Group>
        </Modal.Body>
        <Modal.Footer>
          <Button variant="secondary" onClick={() => setAgentsModal({ show: false, agents: [], selected: null, reason: '' })}>انصراف</Button>
          <Button variant="primary" onClick={handleTransfer} disabled={!agentsModal.selected}>انتقال</Button>
        </Modal.Footer>
      </Modal>
    </div>
  );

  return (
    <div className="chat-app-container agent-dashboard">
      {renderTopbar()}
      <div className="chat-main-layout">
        <aside className={`chat-sidebar ${!selectedTicket || !isMobile ? 'is-active' : ''}`}>
          {renderSidebar()}
        </aside>
        <main className={`chat-panel ${selectedTicket ? 'is-room-selected' : ''}`}>
          {renderMainContent()}
        </main>
      </div>
    </div>
  );
};

const AgentDashboard = () => (
  <ChatProvider>
    <AgentDashboardInner />
  </ChatProvider>
);

export default AgentDashboard;
