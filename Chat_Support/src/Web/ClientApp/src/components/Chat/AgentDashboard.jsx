import React, {useState, useEffect} from 'react';
import {Container, Row, Col, Card, Button, Form, Badge, ListGroup, Spinner, Alert, ButtonGroup, Modal} from 'react-bootstrap';
import {MessageSquare, Clock, CheckCircle, ArrowRight, RefreshCw, Mail, Circle} from 'lucide-react';
import { chatApi } from '../../services/chatApi';
import './Chat.css';

const AgentDashboard = () => {
  const [tickets, setTickets] = useState([]);
  const [selectedTicket, setSelectedTicket] = useState(null);
  const [agentStatus, setAgentStatus] = useState('Available');
  const [stats, setStats] = useState({
    activeChats: 0,
    resolvedToday: 0,
    avgResponseTime: '2m 15s',
  });
  const [isLoading, setIsLoading] = useState(true);
  const [agentsModal, setAgentsModal] = useState({ show: false, agents: [], selected: null, reason: '' });

  const loadTickets = async () => {
    try {
      const tickets = await chatApi.getSupportTickets();
      setTickets(tickets);
      setStats((prev) => ({...prev, activeChats: tickets.filter((t) => t.status < 2).length}));
    } catch (error) {
      console.error('Failed to load tickets:', error);
    } finally {
      setIsLoading(false);
    }
  };

  useEffect(() => {
    loadTickets();
    const interval = setInterval(loadTickets, 30000);
    return () => clearInterval(interval);
  }, []);

  const handleJoinChat = (ticket) => {
    const chatUrl = `/chat/${ticket.chatRoomId}?support=true&ticketId=${ticket.id}`;
    window.open(chatUrl, '_blank');
  };

  const handleAgentStatusChange = async (value) => {
    setAgentStatus(value);
    try {
      await fetch(`${window.location.origin}/api/support/agent/status`, {
        method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ status: value })
      });
    } catch (e) { /* ignore */ }
  };

  const handleOpenTransfer = async (ticket) => {
    setSelectedTicket(ticket);
    try {
      const agents = await chatApi.getAvailableAgents(ticket.regionId);
      setAgentsModal({ show: true, agents, selected: null, reason: '' });
    } catch (e) {
      setAgentsModal({ show: true, agents: [], selected: null, reason: '' });
    }
  };

  const handleTransfer = async () => {
    if (!selectedTicket || !agentsModal.selected) return;
    try {
      await chatApi.transferTicket(selectedTicket.id, agentsModal.selected, agentsModal.reason || undefined);
      setAgentsModal({ show: false, agents: [], selected: null, reason: '' });
      await loadTickets();
    } catch (e) { /* ignore */ }
  };

  const handleClose = async (ticket) => {
    try {
      await chatApi.closeTicket(ticket.id, 'Closed by agent');
      await loadTickets();
      if (selectedTicket && selectedTicket.id === ticket.id) setSelectedTicket({ ...selectedTicket, status: 3 });
    } catch (e) { /* ignore */ }
  };

  const getStatusVariant = (status) => ({0: 'primary', 1: 'warning', 2: 'success', 3: 'secondary'}[status] || 'secondary');
  const getStatusText = (status) => ({0: 'باز', 1: 'در حال بررسی', 2: 'حل شده', 3: 'بسته شده'}[status] || 'نامشخص');
  const getAgentStatusColor = (status) => ({Available: 'success', Busy: 'warning', Away: 'danger', Offline: 'secondary'}[status] || 'secondary');

  const renderSidebar = () => (
    <div className="dashboard-sidebar">
      <div className="dashboard-sidebar-header p-3">
        <div className="d-flex justify-content-between align-items-center mb-3">
          <h5 className="mb-0">داشبورد پشتیبان</h5>
          <Button variant="light" size="sm" onClick={loadTickets} className="refresh-btn" aria-label="بازخوانی">
            <RefreshCw size={16} />
          </Button>
        </div>
        <div className="d-flex align-items-center gap-2">
          <Circle size={12} fill="currentColor" className={`text-${getAgentStatusColor(agentStatus)}`} />
          <Form.Select aria-label="وضعیت کارشناس" value={agentStatus} onChange={(e) => handleAgentStatusChange(e.target.value)} size="sm" className="status-select">
            <option value="Available">در دسترس</option>
            <option value="Busy">مشغول</option>
            <option value="Away">غایب</option>
            <option value="Offline">آفلاین</option>
          </Form.Select>
        </div>
        <Row className="g-2 mt-3">
          <Col xs={6}>
            <Card bg="primary" text="white" className="text-center p-2 shadow-sm rounded-3">
              <h6 className="mb-0">{stats.activeChats}</h6>
              <small>گفتگوهای فعال</small>
            </Card>
          </Col>
          <Col xs={6}>
            <Card bg="success" text="white" className="text-center p-2 shadow-sm rounded-3">
              <h6 className="mb-0">{stats.resolvedToday}</h6>
              <small>حل شده امروز</small>
            </Card>
          </Col>
        </Row>
      </div>
      <div className="p-3 pt-2">
        <h6 className="text-muted mb-3">تیکت‌های پشتیبانی</h6>
        <div className="ticket-list">
        {isLoading ? (
          <div className="text-center py-5">
            <Spinner />
          </div>
        ) : tickets.length === 0 ? (
          <Alert variant="info">تیکتی یافت نشد.</Alert>
        ) : (
          <ListGroup variant="flush">
            {tickets.map((ticket) => (
              <ListGroup.Item
                key={ticket.id}
                action
                active={selectedTicket?.id === ticket.id}
                onClick={() => setSelectedTicket(ticket)}
                className="ticket-item mb-2 border rounded"
              >
                <div className="ticket-item-header d-flex justify-content-between align-items-center mb-1">
                  <strong className="ticket-title text-truncate">{ticket.requesterName}</strong>
                  <div className="ticket-badges d-flex align-items-center gap-2">
                    {ticket.regionId && <Badge bg="info">Region #{ticket.regionId}</Badge>}
                    <Badge bg={getStatusVariant(ticket.status)}>{getStatusText(ticket.status)}</Badge>
                  </div>
                </div>
                <p className="ticket-snippet text-muted small mb-1">{ticket.lastMessage?.content}</p>
                <div className="d-flex justify-content-between align-items-center">
                  <small className="ticket-time text-muted">{new Date(ticket.created).toLocaleTimeString()}</small>
                  <small className="text-muted">#{ticket.id}</small>
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
    <div className="dashboard-main p-4">
      {selectedTicket ? (
        <>
          <Card className="mb-4 ticket-details-card shadow-sm">
            <Card.Header as="h5">جزئیات تیکت #{selectedTicket.id}</Card.Header>
            <Card.Body>
              <h4 className="mb-2">{selectedTicket.requesterName}</h4>
              <div className="ticket-meta d-flex flex-wrap gap-3 text-muted small mb-3">
                {selectedTicket.requesterEmail && (
                  <span>
                    <Mail size={16} className="me-1" />
                    {selectedTicket.requesterEmail}
                  </span>
                )}
                <span>
                  <Clock size={16} className="me-1" />
                  {new Date(selectedTicket.created).toLocaleString()}
                </span>
                {selectedTicket.regionId && (
                  <span><Badge bg="info">Region #{selectedTicket.regionId}</Badge></span>
                )}
              </div>
              <Row>
                <Col sm={6}>
                  <dt className="text-muted">وضعیت</dt>
                  <dd>
                    <Badge bg={getStatusVariant(selectedTicket.status)}>{getStatusText(selectedTicket.status)}</Badge>
                  </dd>
                </Col>
                <Col sm={6}>
                  <dt className="text-muted">شناسه چت</dt>
                  <dd>#{selectedTicket.chatRoomId}</dd>
                </Col>
              </Row>
              <ButtonGroup className="mt-3 d-flex flex-wrap gap-2 responsive-btns">
                <Button variant="primary" onClick={() => handleJoinChat(selectedTicket)} className="px-3">
                  <MessageSquare size={16} className="me-1" /> ورود به چت
                </Button>
                <Button variant="outline-secondary" onClick={() => handleOpenTransfer(selectedTicket)} className="px-3">
                  <ArrowRight size={16} className="me-1" /> انتقال
                </Button>
                <Button variant="outline-success" onClick={() => handleClose(selectedTicket)} className="px-3">
                  <CheckCircle size={16} className="me-1" /> بستن تیکت
                </Button>
              </ButtonGroup>
            </Card.Body>
          </Card>
        </>
      ) : (
        <div className="d-flex align-items-center justify-content-center h-100 text-center text-muted">
          <div>
            <MessageSquare size={64} className="mb-3" />
            <h4>تیکتی انتخاب نشده است</h4>
            <p>برای مشاهده جزئیات، یک تیکت را از لیست انتخاب کنید.</p>
          </div>
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
    <Container fluid className="agent-dashboard">
      <Row className="g-0 h-100">
  <Col md={4} lg={3} className="border-end h-100">
          {renderSidebar()}
        </Col>
  <Col md={8} lg={9} className="h-100">
          {renderMainContent()}
        </Col>
      </Row>
    </Container>
  );
};

export default AgentDashboard;
