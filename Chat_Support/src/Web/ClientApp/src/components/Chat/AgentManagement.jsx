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
} from 'react-bootstrap';
import { Plus, Edit2, Trash2, RefreshCw, User, Circle } from 'lucide-react';
import { chatApi } from '../../services/chatApi';

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

  useEffect(() => {
    loadAgents();
    loadUsers();
  }, []);

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
      if (modalMode === 'create') {
        await chatApi.createAgent(parseInt(formData.userId), formData.maxConcurrentChats);
      } else {
        await chatApi.updateAgent(selectedAgent.userId, {
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

  const handleDelete = async (agentId) => {
    if (!window.confirm('آیا از حذف این پشتیبان اطمینان دارید؟ تیکت‌های فعال او به پشتیبان دیگری منتقل خواهد شد.')) {
      return;
    }

    try {
      await chatApi.deleteAgent(agentId);
      await loadAgents();
    } catch (err) {
      setError('خطا در حذف پشتیبان: ' + (err.message || 'Unknown error'));
      console.error('Failed to delete agent:', err);
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
                        <tr key={agent.userId}>
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
                              >
                                <Edit2 size={14} />
                              </Button>
                              <Button
                                variant="outline-danger"
                                size="sm"
                                onClick={() => handleDelete(agent.userId)}
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
                  {users.map((user) => (
                    <option key={user.id} value={user.id}>
                      {user.firstName} {user.lastName} ({user.email})
                    </option>
                  ))}
                </Form.Select>
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
