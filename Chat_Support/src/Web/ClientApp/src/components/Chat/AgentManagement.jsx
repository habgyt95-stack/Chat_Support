import React, { useState, useEffect } from 'react';
import {
  Container,
  Row,
  Col,
  Card,
  Button,
  Table,
  Modal,
  Form,
  Badge,
  Spinner,
  Alert,
  Tabs,
  Tab,
  ListGroup,
  InputGroup,
} from 'react-bootstrap';
import { Plus, Edit2, Trash2, RefreshCw, User, Circle, Eye, MessageSquare, XCircle, Repeat } from 'lucide-react';
import { chatApi, MessageType } from '../../services/chatApi';

const AgentManagement = () => {
  const [agents, setAgents] = useState([]);
  const [users, setUsers] = useState([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState(null);
  const [showModal, setShowModal] = useState(false);
  const [modalMode, setModalMode] = useState('create'); // 'create' or 'edit'
  const [selectedAgent, setSelectedAgent] = useState(null);
  const [formData, setFormData] = useState({
    userId: '',
    maxConcurrentChats: 5,
    isActive: true,
  });

  // Admin viewer state
  const [activeTab, setActiveTab] = useState('agents');
  const [selectedAgentForView, setSelectedAgentForView] = useState(null); // { agent, tickets: [] }
  const [agentTicketsLoading, setAgentTicketsLoading] = useState(false);
  const [selectedTicket, setSelectedTicket] = useState(null); // ticket details
  const [ticketMessages, setTicketMessages] = useState([]);
  const [ticketMessagesLoading, setTicketMessagesLoading] = useState(false);
  const [newMessage, setNewMessage] = useState('');

  // Transfer state
  const [availableAgents, setAvailableAgents] = useState([]);
  const [loadingAvailableAgents, setLoadingAvailableAgents] = useState(false);
  const [transferAgentId, setTransferAgentId] = useState('');
  const [transferReason, setTransferReason] = useState('');

  useEffect(() => {
    loadAgents();
    loadUsers();
  }, []);

  useEffect(() => {
    // Load available agents when a ticket is selected
    const fetchAvailable = async () => {
      if (!selectedTicket || !selectedTicket.regionId) {
        setAvailableAgents([]);
        setTransferAgentId('');
        return;
      }
      try {
        setLoadingAvailableAgents(true);
        const list = await chatApi.getAvailableAgents(selectedTicket.regionId);
        setAvailableAgents(list);
        setTransferAgentId(list[0]?.userId?.toString() || '');
      } catch (e) {
        // ignore
      } finally {
        setLoadingAvailableAgents(false);
      }
    };
    fetchAvailable();
  }, [selectedTicket]);

  const loadAgents = async () => {
    try {
      setIsLoading(true);
      setError(null);
      const data = await chatApi.getAllAgents();
      setAgents(data);
    } catch (err) {
      setError('خطا در بارگذاری لیست پشتیبان‌ها: ' + (err.message || 'Unknown error'));
      console.error('Failed to load agents:', err);
    } finally {
      setIsLoading(false);
    }
  };

  const loadUsers = async () => {
    try {
      // Search for all users (empty query returns all)
      const data = await chatApi.searchUsers('');
      setUsers(data);
    } catch (err) {
      console.error('Failed to load users:', err);
    }
  };

  // فیلتر کاربرانی که هنوز پشتیبان نیستند
  const availableUsers = users.filter(user => 
    !agents.some(agent => agent.userId === user.id)
  );

  const handleOpenCreateModal = () => {
    setModalMode('create');
    setSelectedAgent(null);
    setFormData({
      userId: '',
      maxConcurrentChats: 5,
      isActive: true,
    });
    setShowModal(true);
  };

  const handleOpenEditModal = (agent) => {
    setModalMode('edit');
    setSelectedAgent(agent);
    setFormData({
      userId: agent.userId,
      maxConcurrentChats: agent.maxConcurrentChats,
      isActive: agent.isActive,
    });
    setShowModal(true);
  };

  const handleCloseModal = () => {
    setShowModal(false);
    setSelectedAgent(null);
  };

  const handleSubmit = async (e) => {
    e.preventDefault();
    
    try {
      setError(null);
      
      if (modalMode === 'create') {
        // بررسی تکراری بودن
        const isDuplicate = agents.some(agent => agent.userId === parseInt(formData.userId));
        if (isDuplicate) {
          setError('این کاربر قبلاً به عنوان پشتیبان اضافه شده است.');
          return;
        }
        
        await chatApi.createAgent(parseInt(formData.userId), formData.maxConcurrentChats);
      } else {
        // Use SupportAgent.Id for update
        await chatApi.updateAgent(selectedAgent.id, {
          isActive: formData.isActive,
          maxConcurrentChats: formData.maxConcurrentChats,
        });
      }
      handleCloseModal();
      await loadAgents();
    } catch (err) {
      setError('خطا در ذخیره اطلاعات: ' + (err.message || 'Unknown error'));
      console.error('Failed to save agent:', err);
    }
  };

  const handleDelete = async (agentEntityId) => {
    const agent = agents.find(a => a.id === agentEntityId);
    const confirmMessage = agent
      ? `آیا از حذف پشتیبان "${agent.fullName || 'نامشخص'}" اطمینان دارید؟\n\n` +
        `تیکت‌های فعال (${agent.currentActiveChats || 0}) به پشتیبان دیگری منتقل خواهد شد.`
      : 'آیا از حذف این پشتیبان اطمینان دارید؟';

    if (!window.confirm(confirmMessage)) {
      return;
    }

    try {
      setIsLoading(true);
      // Use SupportAgent.Id for delete
      await chatApi.deleteAgent(agentEntityId);
      await loadAgents();
      setError(null); // پاک کردن خطاهای قبلی
    } catch (err) {
      setError('خطا در حذف پشتیبان: ' + (err.message || 'Unknown error'));
      console.error('Failed to delete agent:', err);
    } finally {
      setIsLoading(false);
    }
  };

  const getStatusColor = (status) => {
    const statusMap = {
      'Offline': 'secondary',
      'Available': 'success',
      'Busy': 'warning',
      'Away': 'danger',
    };
    return statusMap[status] || 'secondary';
  };

  const getStatusText = (status) => {
    const statusMap = {
      'Offline': 'آفلاین',
      'Available': 'در دسترس',
      'Busy': 'مشغول',
      'Away': 'غایب',
    };
    return statusMap[status] || status || 'نامشخص';
  };

  // Admin panel helpers
  const viewAgentTickets = async (agent) => {
    try {
      setActiveTab('inspector');
      setSelectedAgentForView({ agent, tickets: [] });
      setAgentTicketsLoading(true);
      const data = await chatApi.getAgentTicketsByAgentId(agent.id);
      setSelectedAgentForView({ agent: data.agent, tickets: data.tickets });
      setSelectedTicket(null);
      setTicketMessages([]);
    } catch (err) {
      setError('خطا در بارگذاری تیکت‌های پشتیبان');
    } finally {
      setAgentTicketsLoading(false);
    }
  };

  const openTicket = async (ticket) => {
    try {
      setSelectedTicket(ticket);
      setTicketMessagesLoading(true);
      // Load messages for ticket's chatroom
      const messages = await chatApi.getChatMessages(ticket.chatRoomId, 1, 100);
      setTicketMessages(messages);
    } catch (err) {
      setError('خطا در بارگذاری پیام‌های تیکت');
    } finally {
      setTicketMessagesLoading(false);
    }
  };

  const sendMessageToTicket = async () => {
    if (!selectedTicket || !newMessage.trim()) return;
    try {
      const msg = await chatApi.sendMessage(selectedTicket.chatRoomId, {
        content: newMessage,
        type: MessageType.Text,
      });
      setTicketMessages((prev) => [...prev, msg]);
      setNewMessage('');
    } catch (err) {
      setError('ارسال پیام ناموفق بود');
    }
  };

  const closeSelectedTicket = async (ticket) => {
    if (!ticket) return;
    if (!window.confirm('این تیکت بسته شود؟')) return;
    try {
      await chatApi.closeTicket(ticket.id, 'Closed by admin');
      // refresh list
      if (selectedAgentForView?.agent?.id) await viewAgentTickets(selectedAgentForView.agent);
      setSelectedTicket({ ...ticket, status: 2 });
    } catch (err) {
      setError('بستن تیکت ناموفق بود');
    }
  };

  const transferSelectedTicket = async () => {
    if (!selectedTicket || !transferAgentId) return;
    try {
      await chatApi.transferTicket(selectedTicket.id, parseInt(transferAgentId), transferReason || undefined);
      // refresh
      if (selectedAgentForView?.agent?.id) await viewAgentTickets(selectedAgentForView.agent);
      // reload messages to include system message
      const messages = await chatApi.getChatMessages(selectedTicket.chatRoomId, 1, 100);
      setTicketMessages(messages);
      setTransferReason('');
    } catch (err) {
      setError('انتقال تیکت ناموفق بود');
    }
  };

  return (
    <Container fluid className="py-4">
      <Row className="mb-4">
        <Col>
          <div className="d-flex justify-content-between align-items-center">
            <h2>
              <User size={32} className="me-2" />
              مدیریت پشتیبان‌ها
            </h2>
            <div className="d-flex gap-2">
              <Button variant="outline-primary" onClick={loadAgents}>
                <RefreshCw size={18} className="me-1" />
                بازخوانی
              </Button>
              <Button variant="primary" onClick={handleOpenCreateModal}>
                <Plus size={18} className="me-1" />
                افزودن پشتیبان جدید
              </Button>
            </div>
          </div>
        </Col>
      </Row>

      {error && (
        <Alert variant="danger" dismissible onClose={() => setError(null)}>
          {error}
        </Alert>
      )}

      <Tabs activeKey={activeTab} onSelect={(k) => setActiveTab(k || 'agents')} className="mb-3">
        <Tab eventKey="agents" title="پشتیبان‌ها">
          <Row>
            <Col>
              <Card>
                <Card.Body>
                  {isLoading ? (
                    <div className="text-center py-5">
                      <Spinner animation="border" />
                      <p className="mt-3">در حال بارگذاری...</p>
                    </div>
                  ) : agents.length === 0 ? (
                    <Alert variant="info">
                      هیچ پشتیبانی یافت نشد. برای شروع، یک پشتیبان جدید اضافه کنید.
                    </Alert>
                  ) : (
                    <Table responsive hover>
                      <thead>
                        <tr>
                          <th>شناسه کاربر</th>
                          <th>نام کامل</th>
                          <th>وضعیت</th>
                          <th>چت‌های فعال</th>
                          <th>حداکثر ظرفیت</th>
                          <th>بار کاری</th>
                          <th>فعال</th>
                          <th>عملیات</th>
                        </tr>
                      </thead>
                      <tbody>
                        {agents.map((agent) => {
                          const workloadPercent = agent.maxConcurrentChats > 0
                            ? Math.round((agent.currentActiveChats / agent.maxConcurrentChats) * 100)
                            : 0;

                          return (
                            <tr key={agent.id}>
                              <td>{agent.userId}</td>
                              <td>{agent.fullName || 'نامشخص'}</td>
                              <td>
                                <Badge bg={getStatusColor(agent.agentStatus)} className="d-flex align-items-center gap-1" style={{ width: 'fit-content' }}>
                                  <Circle size={10} fill="currentColor" />
                                  {getStatusText(agent.agentStatus)}
                                </Badge>
                              </td>
                              <td>{agent.currentActiveChats}</td>
                              <td>{agent.maxConcurrentChats}</td>
                              <td>
                                <div className="progress" style={{ height: '20px' }}>
                                  <div
                                    className={`progress-bar ${
                                      workloadPercent >= 80
                                        ? 'bg-danger'
                                        : workloadPercent >= 50
                                        ? 'bg-warning'
                                        : 'bg-success'
                                    }`}
                                    role="progressbar"
                                    style={{ width: `${workloadPercent}%` }}
                                    aria-valuenow={workloadPercent}
                                    aria-valuemin="0"
                                    aria-valuemax="100"
                                  >
                                    {workloadPercent}%
                                  </div>
                                </div>
                              </td>
                              <td>
                                <Badge bg={agent.isActive ? 'success' : 'secondary'}>
                                  {agent.isActive ? 'فعال' : 'غیرفعال'}
                                </Badge>
                              </td>
                              <td>
                                <div className="d-flex gap-2">
                                  <Button
                                    variant="outline-primary"
                                    size="sm"
                                    onClick={() => handleOpenEditModal(agent)}
                                    title="ویرایش"
                                  >
                                    <Edit2 size={14} />
                                  </Button>
                                  <Button
                                    variant="outline-secondary"
                                    size="sm"
                                    onClick={() => viewAgentTickets(agent)}
                                    title="نمایش تیکت‌ها"
                                  >
                                    <Eye size={14} />
                                  </Button>
                                  <Button
                                    variant="outline-danger"
                                    size="sm"
                                    onClick={() => handleDelete(agent.id)}
                                  >
                                    <Trash2 size={14} />
                                  </Button>
                                </div>
                              </td>
                            </tr>
                          );
                        })}
                      </tbody>
                    </Table>
                  )}
                </Card.Body>
              </Card>
            </Col>
          </Row>
        </Tab>

        <Tab eventKey="inspector" title="بازبین پشتیبان/تیکت‌ها">
          <Row>
            <Col md={4}>
              <Card className="mb-3">
                <Card.Header>پشتیبان انتخاب‌شده</Card.Header>
                <Card.Body>
                  {!selectedAgentForView ? (
                    <Alert variant="secondary">ابتدا یک پشتیبان را از تب پشتیبان‌ها انتخاب کنید.</Alert>
                  ) : (
                    <div>
                      <div className="d-flex align-items-center justify-content-between">
                        <div>
                          <div className="fw-bold">{selectedAgentForView.agent?.name || selectedAgentForView.agent?.fullName}</div>
                          <div className="small text-muted">UserId: {selectedAgentForView.agent?.userId}</div>
                        </div>
                        {selectedAgentForView.agent && (
                          <Badge bg={getStatusColor(selectedAgentForView.agent.agentStatus)}>
                            {getStatusText(selectedAgentForView.agent.agentStatus)}
                          </Badge>
                        )}
                      </div>
                    </div>
                  )}
                </Card.Body>
              </Card>

              <Card>
                <Card.Header>تیکت‌ها</Card.Header>
                <Card.Body className="p-0">
                  {agentTicketsLoading ? (
                    <div className="text-center py-4"><Spinner animation="border" /></div>
                  ) : !selectedAgentForView ? (
                    <div className="p-3 text-muted">پشتیبانی انتخاب نشده است.</div>
                  ) : selectedAgentForView.tickets?.length ? (
                    <ListGroup variant="flush">
                      {selectedAgentForView.tickets.map((t) => (
                        <ListGroup.Item
                          key={t.id}
                          action
                          active={selectedTicket?.id === t.id}
                          onClick={() => openTicket(t)}
                        >
                          <div className="d-flex justify-content-between">
                            <div>
                              <div className="fw-bold">تیکت #{t.id}</div>
                              <div className="small text-muted">{t.requesterName}</div>
                            </div>
                            <div className="text-end">
                              <Badge bg="light" text="dark">{t.regionTitle || 'بدون ناحیه'}</Badge>
                              <div className="small">Unread: {t.unreadCount}</div>
                            </div>
                          </div>
                        </ListGroup.Item>
                      ))}
                    </ListGroup>
                  ) : (
                    <div className="p-3 text-muted">تیکتی برای این پشتیبان یافت نشد.</div>
                  )}
                </Card.Body>
              </Card>
            </Col>

            <Col md={8}>
              <Card className="h-100">
                <Card.Header className="d-flex justify-content-between align-items-center">
                  <div className="d-flex align-items-center gap-2">
                    <MessageSquare size={18} />
                    <span>گفتگو</span>
                  </div>
                  <div className="d-flex gap-2">
                    {selectedTicket && (
                      <>
                        <InputGroup size="sm" style={{ maxWidth: 380 }}>
                          <Form.Select
                            disabled={loadingAvailableAgents}
                            value={transferAgentId}
                            onChange={(e) => setTransferAgentId(e.target.value)}
                          >
                            <option value="">انتخاب پشتیبان مقصد...</option>
                            {availableAgents.map(a => (
                              <option key={a.userId} value={a.userId}>{a.name} ({a.currentActiveChats}/{a.maxConcurrentChats})</option>
                            ))}
                          </Form.Select>
                          <Form.Control
                            placeholder="دلیل انتقال (اختیاری)"
                            value={transferReason}
                            onChange={(e) => setTransferReason(e.target.value)}
                          />
                          <Button size="sm" variant="outline-secondary" onClick={transferSelectedTicket} disabled={!transferAgentId}>
                            <Repeat size={14} className="me-1" /> انتقال
                          </Button>
                        </InputGroup>
                        <Button size="sm" variant="outline-danger" onClick={() => closeSelectedTicket(selectedTicket)}>
                          <XCircle size={14} className="me-1" /> بستن تیکت
                        </Button>
                      </>
                    )}
                  </div>
                </Card.Header>
                <Card.Body style={{ height: '60vh', overflowY: 'auto' }}>
                  {!selectedTicket ? (
                    <div className="text-muted">یک تیکت را از لیست انتخاب کنید.</div>
                  ) : ticketMessagesLoading ? (
                    <div className="text-center py-4"><Spinner animation="border" /></div>
                  ) : ticketMessages.length === 0 ? (
                    <div className="text-muted">پیامی موجود نیست.</div>
                  ) : (
                    <div className="d-flex flex-column gap-2">
                      {ticketMessages.map((m) => (
                        <div key={m.id} className={`p-2 rounded ${m.isOwn ? 'bg-primary text-white align-self-end' : 'bg-light'} `}>
                          <div className="small text-muted">{m.senderFullName || 'سیستم/مهمان'}</div>
                          <div>{m.content}</div>
                          {m.attachmentUrl && (
                            <a href={m.attachmentUrl} target="_blank" rel="noreferrer">دانلود فایل</a>
                          )}
                          <div className="small text-muted">{new Date(m.timestamp).toLocaleString()}</div>
                        </div>
                      ))}
                    </div>
                  )}
                </Card.Body>
                <Card.Footer>
                  <InputGroup>
                    <Form.Control
                      placeholder="ارسال پیام مدیریتی به این گفتگو..."
                      value={newMessage}
                      onChange={(e) => setNewMessage(e.target.value)}
                      onKeyDown={(e) => { if (e.key === 'Enter') { e.preventDefault(); sendMessageToTicket(); }}}
                    />
                    <Button onClick={sendMessageToTicket}>ارسال</Button>
                  </InputGroup>
                </Card.Footer>
              </Card>
            </Col>
          </Row>
        </Tab>
      </Tabs>

      {/* Create/Edit Modal */}
      <Modal show={showModal} onHide={handleCloseModal} centered>
        <Modal.Header closeButton>
          <Modal.Title>
            {modalMode === 'create' ? 'افزودن پشتیبان جدید' : 'ویرایش پشتیبان'}
          </Modal.Title>
        </Modal.Header>
        <Form onSubmit={handleSubmit}>
          <Modal.Body>
            {modalMode === 'create' ? (
              <Form.Group className="mb-3">
                <Form.Label>انتخاب کاربر</Form.Label>
                <Form.Select
                  value={formData.userId}
                  onChange={(e) => setFormData({ ...formData, userId: e.target.value })}
                  required
                >
                  <option value="">-- انتخاب کنید --</option>
                  {availableUsers.length > 0 ? (
                    availableUsers.map((user) => (
                      <option key={user.id} value={user.id}>
                        {user.fullName || `${user.firstName || ''} ${user.lastName || ''}`.trim() || 'بدون نام'} 
                        {user.userName ? ` (@${user.userName})` : ''} 
                        {user.mobile ? ` - ${user.mobile}` : user.email ? ` - ${user.email}` : ''}
                      </option>
                    ))
                  ) : (
                    <option disabled>همه کاربران قبلاً پشتیبان شده‌اند</option>
                  )}
                </Form.Select>
                <Form.Text className="text-muted">
                  {availableUsers.length} کاربر در دسترس
                </Form.Text>
              </Form.Group>
            ) : (
              <Alert variant="info">
                در حال ویرایش پشتیبان: {selectedAgent?.fullName}
              </Alert>
            )}

            <Form.Group className="mb-3">
              <Form.Label>حداکثر چت همزمان</Form.Label>
              <Form.Control
                type="number"
                min="1"
                max="20"
                value={formData.maxConcurrentChats}
                onChange={(e) =>
                  setFormData({ ...formData, maxConcurrentChats: parseInt(e.target.value) })
                }
                required
              />
              <Form.Text className="text-muted">
                تعداد چت‌هایی که این پشتیبان می‌تواند به صورت همزمان مدیریت کند (1-20)
              </Form.Text>
            </Form.Group>

            {modalMode === 'edit' && (
              <Form.Group className="mb-3">
                <Form.Check
                  type="switch"
                  id="isActive"
                  label="پشتیبان فعال است"
                  checked={formData.isActive}
                  onChange={(e) => setFormData({ ...formData, isActive: e.target.checked })}
                />
                <Form.Text className="text-muted">
                  اگر غیرفعال شود، تیکت‌های فعال به پشتیبان دیگری منتقل می‌شوند
                </Form.Text>
              </Form.Group>
            )}
          </Modal.Body>
          <Modal.Footer>
            <Button variant="secondary" onClick={handleCloseModal}>
              انصراف
            </Button>
            <Button variant="primary" type="submit">
              {modalMode === 'create' ? 'افزودن' : 'ذخیره تغییرات'}
            </Button>
          </Modal.Footer>
        </Form>
      </Modal>
    </Container>
  );
};

export default AgentManagement;
