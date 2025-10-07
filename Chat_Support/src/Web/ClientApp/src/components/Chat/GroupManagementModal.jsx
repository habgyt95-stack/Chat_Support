
import React, {useState, useEffect} from 'react';
import {Modal, Button, Tabs, Tab, ListGroup, Badge, Spinner, Alert, Form} from 'react-bootstrap';
import {PersonPlusFill, Trash, X, Search, ShieldLockFill, PeopleFill, GearFill, BoxArrowLeft} from 'react-bootstrap-icons';
import {chatApi} from '../../services/chatApi';

const GroupManagementModal = ({show, onHide, chatRoom, currentUserId, onGroupUpdated, defaultTab = 'members'}) => {
  const [members, setMembers] = useState([]);
  const [searchQuery, setSearchQuery] = useState('');
  const [searchResults, setSearchResults] = useState([]);
  const [selectedUsers, setSelectedUsers] = useState([]);
  const [isSearching, setIsSearching] = useState(false);
  const [isSearchFocused, setIsSearchFocused] = useState(false);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState('');
  const [editName, setEditName] = useState('');
  const [editDescription, setEditDescription] = useState('');

  const isOwner = members.find((m) => m.id === currentUserId)?.role === 2; // 2: Owner role

  useEffect(() => {
    if (show && chatRoom) {
      loadMembers();
      // Reset states on modal open
      setError('');
      setSearchQuery('');
      setSearchResults([]);
      setSelectedUsers([]);
      setEditName(chatRoom.name || '');
      setEditDescription(chatRoom.description || '');
    }
  }, [show, chatRoom]);

  const loadMembers = async () => {
    try {
      setIsLoading(true);
      const data = await chatApi.getChatRoomMembers(chatRoom.id);
      setMembers(data);
    } catch (err) {
      setError('خطا در بارگذاری اعضا');
    } finally {
      setIsLoading(false);
    }
  };


  // Load initial users when search input is focused
  useEffect(() => {
    if (isSearchFocused && searchResults.length === 0 && !isSearching) {
      loadInitialUsers();
    }
    // eslint-disable-next-line
  }, [isSearchFocused]);

  const loadInitialUsers = async () => {
    try {
      setIsSearching(true);
      const users = await chatApi.searchUsers('');
      const memberIds = members.map((m) => m.id);
      setSearchResults(users.filter((u) => !memberIds.includes(u.id)));
    } catch (error) {
      setError('خطا در بارگذاری کاربران');
    } finally {
      setIsSearching(false);
    }
  };

  // Debounced search
  useEffect(() => {
    const timeoutId = setTimeout(async () => {
      if (searchQuery.trim().length >= 2) {
        await searchUsers(searchQuery);
      } else {
        setSearchResults([]);
      }
    }, 500);
    return () => clearTimeout(timeoutId);
    // eslint-disable-next-line
  }, [searchQuery]);

  const searchUsers = async (query) => {
    try {
      setIsSearching(true);
      const users = await chatApi.searchUsers(query);
      const memberIds = members.map((m) => m.id);
      setSearchResults(users.filter((u) => !memberIds.includes(u.id)));
    } catch (error) {
      setError('خطا در جستجوی کاربر');
    } finally {
      setIsSearching(false);
    }
  };

  const addMembers = async () => {
    if (selectedUsers.length === 0) return;
    try {
      setIsLoading(true);
      await chatApi.addGroupMembers(
        chatRoom.id,
        selectedUsers.map((u) => u.id)
      );
      await loadMembers();
      setSelectedUsers([]);
      setSearchQuery('');
      setSearchResults([]);
      onGroupUpdated();
    } catch (err) {
      setError('خطا در افزودن اعضا');
    } finally {
      setIsLoading(false);
    }
  };

  const removeMember = async (userId) => {
    if (!window.confirm('آیا از حذف این عضو اطمینان دارید؟')) return;
    try {
      setIsLoading(true);
      await chatApi.removeGroupMember(chatRoom.id, userId);
      await loadMembers();
      onGroupUpdated();
    } catch (err) {
      setError('خطا در حذف عضو');
    } finally {
      setIsLoading(false);
    }
  };

  const deleteGroup = async () => {
    if (!window.confirm('آیا از حذف این گروه اطمینان دارید؟ این عمل قابل بازگشت نیست.')) return;
    try {
      setIsLoading(true);
      await chatApi.deleteChatRoom(chatRoom.id);
      onGroupUpdated();
      onHide();
    } catch (err) {
      setError('خطا در حذف گروه');
    } finally {
      setIsLoading(false);
    }
  };

  const leaveGroup = async () => {
    if (!window.confirm('آیا از ترک گروه اطمینان دارید؟')) return;
    try {
      setIsLoading(true);
      await chatApi.leaveChatRoom(chatRoom.id);
      onGroupUpdated();
      onHide();
    } catch (err) {
      setError('خطا در ترک گروه');
    } finally {
      setIsLoading(false);
    }
  };

  const updateGroupInfo = async () => {
    try {
      setIsLoading(true);
      await chatApi.updateChatRoom(chatRoom.id, {
        name: editName || undefined,
        description: editDescription || undefined
      });
      setError('');
      onGroupUpdated();
    } catch (err) {
      setError('خطا در به‌روزرسانی اطلاعات گروه');
    } finally {
      setIsLoading(false);
    }
  };


  const handleUserSelect = (user) => {
    if (selectedUsers.find((u) => u.id === user.id)) {
      const newSelected = selectedUsers.filter((u) => u.id !== user.id);
      setSelectedUsers(newSelected);
    } else {
      const newSelected = [...selectedUsers, user];
      setSelectedUsers(newSelected);
    }
  };

  const removeSelectedUser = (userId) => {
    const newSelected = selectedUsers.filter((u) => u.id !== userId);
    setSelectedUsers(newSelected);
  };

  const renderLoading = () => (
    <div className="text-center py-5">
      <Spinner animation="border" variant="primary" />
    </div>
  );

  return (
    <Modal show={show} onHide={onHide} centered size="lg" scrollable>
      <Modal.Header closeButton>
        <Modal.Title>
          <PeopleFill className="me-2" /> مدیریت گروه: {chatRoom?.name}
        </Modal.Title>
      </Modal.Header>

      <Modal.Body>
        {error && (
          <Alert variant="danger" onClose={() => setError('')} dismissible>
            {error}
          </Alert>
        )}

        <Tabs defaultActiveKey={defaultTab} id="group-management-tabs" className="mb-3" justify>
          <Tab
            eventKey="members"
            title={
              <>
                <PeopleFill className="me-1" /> اعضای گروه
              </>
            }
          >
            {isLoading ? (
              renderLoading()
            ) : (
              <ListGroup>
                {members.map((member) => (
                  <ListGroup.Item key={member.id} as="li" className="d-flex justify-content-between align-items-center">
                    <div>
                      {member.fullName || `${member.firstName || ''} ${member.lastName || ''}`.trim() || member.userName}
                      {member.role === 2 && ( // Owner
                        <Badge bg="warning" className="ms-2">
                          <ShieldLockFill className="me-1" /> مالک
                        </Badge>
                      )}
                      {member.role === 1 && ( // Admin
                        <Badge bg="info" className="ms-2">
                          مدیر
                        </Badge>
                      )}
                    </div>
                    {isOwner && member.id !== currentUserId && (
                      <Button variant="outline-danger" size="sm" onClick={() => removeMember(member.id)}>
                        <Trash /> حذف
                      </Button>
                    )}
                  </ListGroup.Item>
                ))}
              </ListGroup>
            )}
          </Tab>

          {isOwner && (
            <Tab
              eventKey="add"
              title={
                <>
                  <PersonPlusFill className="me-1" /> افزودن عضو
                </>
              }
            >
              <Form.Group className="mb-3">
                <Form.Label>افزودن اعضا</Form.Label>
                <Form.Control
                  type="text"
                  placeholder="نام یا نام کاربری را وارد کنید..."
                  value={searchQuery}
                  onChange={(e) => setSearchQuery(e.target.value)}
                  onFocus={() => setIsSearchFocused(true)}
                />
              </Form.Group>

              {/* Selected Members Preview */}
              {selectedUsers.length > 0 && (
                <div className="mb-3">
                  <Form.Label>اعضای انتخاب شده ({selectedUsers.length})</Form.Label>
                  <div className="d-flex flex-wrap gap-2">
                    {selectedUsers.map((user) => (
                      <Badge key={user.id} bg="primary" className="d-flex align-items-center gap-1 p-2">
                        {user.userName}
                        <Button variant="link" size="sm" className="p-0 text-white" onClick={() => removeSelectedUser(user.id)}>
                          <i className="bi bi-x"></i>
                        </Button>
                      </Badge>
                    ))}
                  </div>
                </div>
              )}

              {/* Search Results for group members */}
              {(isSearching || searchResults.length > 0 || isSearchFocused) && (
                <div className="mb-3">
                  <Form.Label>نتایج جستجو</Form.Label>
                  {isSearching && <div className="text-center text-muted">در حال جستجو...</div>}
                  {!isSearching && searchResults.length === 0 && <div className="text-center text-muted">کاربری یافت نشد.</div>}
                  <ListGroup style={{maxHeight: '200px', overflowY: 'auto'}}>
                    {searchResults.map((user) => {
                      const isSelected = selectedUsers.some((su) => su.id === user.id);
                      return (
                        <ListGroup.Item key={user.id} action active={isSelected} onClick={() => handleUserSelect(user)} className="d-flex align-items-center">
                          <div className="me-3">
                            {user.avatar ? (
                              <img src={user.avatar} alt={user.userName} className="rounded-circle" style={{width: '32px', height: '32px'}} />
                            ) : (
                              <div className="rounded-circle bg-secondary text-white d-flex align-items-center justify-content-center" style={{width: '32px', height: '32px', fontSize: '0.8rem'}}>
                                {user.userName?.charAt(0).toUpperCase()}
                              </div>
                            )}
                          </div>
                          <div className="flex-grow-1">
                            <div className="fw-semibold">{user.userName}</div>
                            {user.fullName && <small className="text-muted">{user.fullName}</small>}
                          </div>
                          {isSelected && <i className="bi bi-check-circle-fill text-primary ms-auto"></i>}
                        </ListGroup.Item>
                      );
                    })}
                  </ListGroup>
                </div>
              )}

              <Button variant="success" onClick={addMembers} disabled={isLoading || selectedUsers.length === 0} className="mt-2">
                <PersonPlusFill /> افزودن {selectedUsers.length} عضو
              </Button>
            </Tab>
          )}

          {(isOwner || members.find((m) => m.id === currentUserId)?.role === 1) && (
            <Tab
              eventKey="edit"
              title={
                <>
                  <GearFill className="me-1" /> ویرایش گروه
                </>
              }
            >
              <Form.Group className="mb-3">
                <Form.Label>نام گروه</Form.Label>
                <Form.Control
                  type="text"
                  value={editName}
                  onChange={(e) => setEditName(e.target.value)}
                  placeholder="نام گروه را وارد کنید..."
                />
              </Form.Group>

              <Form.Group className="mb-3">
                <Form.Label>توضیحات گروه</Form.Label>
                <Form.Control
                  as="textarea"
                  rows={3}
                  value={editDescription}
                  onChange={(e) => setEditDescription(e.target.value)}
                  placeholder="توضیحات گروه را وارد کنید..."
                />
              </Form.Group>

              <Button variant="primary" onClick={updateGroupInfo} disabled={isLoading || !editName.trim()}>
                ذخیره تغییرات
              </Button>
            </Tab>
          )}

          {isOwner && (
            <Tab
              eventKey="settings"
              title={
                <>
                  <GearFill className="me-1" /> تنظیمات
                </>
              }
            >
              <Alert variant="danger">
                <Alert.Heading>منطقه خطر</Alert.Heading>
                <p>عملیات زیر قابل بازگشت نیست. لطفاً با احتیاط ادامه دهید.</p>
                <hr />
                <Button variant="danger" onClick={deleteGroup} disabled={isLoading}>
                  <Trash /> حذف کامل گروه
                </Button>
              </Alert>
            </Tab>
          )}
        </Tabs>
      </Modal.Body>

      <Modal.Footer className="d-flex justify-content-between">
        {!isOwner && (
          <Button variant="warning" onClick={leaveGroup} disabled={isLoading}>
            <BoxArrowLeft /> ترک گروه
          </Button>
        )}
        <Button variant="secondary" onClick={onHide} className="ms-auto">
          بستن
        </Button>
      </Modal.Footer>
    </Modal>
  );
};

export default GroupManagementModal;
