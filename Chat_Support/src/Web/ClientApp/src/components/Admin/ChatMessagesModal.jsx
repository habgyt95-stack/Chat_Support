import React, { useState, useEffect, useCallback } from "react";
import {
  Modal,
  Spinner,
  Alert,
  Badge,
  Form,
  InputGroup,
  ListGroup,
  Card,
  Button,
} from "react-bootstrap";
import { Search, X, FileEarmark, Image, Mic } from "react-bootstrap-icons";
import { adminChatApi } from "../../services/adminChatApi";
import "./ChatMessagesModal.css";

const MessageTypeIcons = {
  0: null, // Text
  1: <Image size={16} />, // Image
  2: <FileEarmark size={16} />, // File
  6: <Mic size={16} />, // Voice
};

const ChatMessagesModal = ({ show, onHide, chatRoom }) => {
  const [messages, setMessages] = useState([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState(null);
  const [searchTerm, setSearchTerm] = useState("");
  const [currentPage, setCurrentPage] = useState(1);
  const [hasMore, setHasMore] = useState(false);

  // Load messages
  const loadMessages = useCallback(
    async (page = 1) => {
      if (!chatRoom) return;

      setLoading(true);
      setError(null);

      try {
        const response = await adminChatApi.getChatMessages(chatRoom.id, {
          pageNumber: page,
          pageSize: 50,
          searchTerm: searchTerm || null,
        });

        if (page === 1) {
          setMessages(response.items || []);
        } else {
          setMessages((prev) => [...prev, ...(response.items || [])]);
        }

        setHasMore(response.hasNextPage || false);
        setCurrentPage(page);
      } catch (err) {
        console.error("Error loading messages:", err);
        setError(err.response?.data?.message || "خطا در بارگذاری پیام‌ها");
      } finally {
        setLoading(false);
      }
    },
    [chatRoom, searchTerm]
  );

  // Initial load
  useEffect(() => {
    if (show && chatRoom) {
      loadMessages(1);
    }
  }, [show, chatRoom, loadMessages]);

  // Search handler
  const handleSearch = () => {
    loadMessages(1);
  };

  // Load more
  const handleLoadMore = () => {
    if (!loading && hasMore) {
      loadMessages(currentPage + 1);
    }
  };

  // Format date
  const formatDate = (dateString) => {
    const date = new Date(dateString);
    return new Intl.DateTimeFormat("fa-IR", {
      year: "numeric",
      month: "short",
      day: "numeric",
      hour: "2-digit",
      minute: "2-digit",
    }).format(date);
  };

  // Format message content
  const renderMessageContent = (message) => {
    if (message.messageType === 0) {
      // Text message
      return <div className="message-text">{message.content}</div>;
    } else if (message.messageType === 1) {
      // Image
      return (
        <div>
          <Image size={16} className="me-1" />
          <span className="text-muted">تصویر</span>
          {message.fileCaption && (
            <div className="mt-1 small">{message.fileCaption}</div>
          )}
        </div>
      );
    } else if (message.messageType === 2) {
      // File
      return (
        <div>
          <FileEarmark size={16} className="me-1" />
          <span className="text-muted">فایل</span>
          {message.fileCaption && (
            <div className="mt-1 small">{message.fileCaption}</div>
          )}
          {message.filePath && (
            <div className="mt-1">
              <a
                href={message.filePath}
                target="_blank"
                rel="noopener noreferrer"
                className="small"
              >
                دانلود فایل
              </a>
            </div>
          )}
        </div>
      );
    } else if (message.messageType === 6) {
      // Voice
      return (
        <div>
          <Mic size={16} className="me-1" />
          <span className="text-muted">پیام صوتی</span>
        </div>
      );
    }
    return message.content;
  };

  return (
    <Modal
      show={show}
      onHide={onHide}
      size="lg"
      centered
      className="chat-messages-modal"
    >
      <Modal.Header closeButton>
        <Modal.Title>
          <div className="d-flex align-items-center">
            <div>پیام‌های چت: {chatRoom?.name}</div>
            <Badge bg="secondary" className="ms-2">
              {messages.length} پیام
            </Badge>
          </div>
        </Modal.Title>
      </Modal.Header>

      <Modal.Body>
        {/* Search */}
        <div className="mb-3">
          <InputGroup>
            <InputGroup.Text>
              <Search />
            </InputGroup.Text>
            <Form.Control
              type="text"
              placeholder="جستجو در پیام‌ها..."
              value={searchTerm}
              onChange={(e) => setSearchTerm(e.target.value)}
              onKeyPress={(e) => {
                if (e.key === "Enter") {
                  handleSearch();
                }
              }}
            />
            {searchTerm && (
              <Button variant="outline-secondary" onClick={() => setSearchTerm("")}>
                <X />
              </Button>
            )}
          </InputGroup>
        </div>

        {/* Error Alert */}
        {error && (
          <Alert variant="danger" onClose={() => setError(null)} dismissible>
            {error}
          </Alert>
        )}

        {/* Messages List */}
        {loading && currentPage === 1 ? (
          <div className="text-center py-5">
            <Spinner animation="border" variant="primary" />
            <p className="mt-3 text-muted">در حال بارگذاری پیام‌ها...</p>
          </div>
        ) : messages.length === 0 ? (
          <div className="text-center py-5">
            <p className="text-muted">پیامی یافت نشد</p>
          </div>
        ) : (
          <>
            <ListGroup className="messages-list">
              {messages.map((message) => (
                <ListGroup.Item key={message.id} className="message-item">
                  <div className="message-header d-flex justify-content-between align-items-start mb-2">
                    <div>
                      <strong>{message.senderName}</strong>
                      {message.senderPhone && (
                        <small className="text-muted ms-2">
                          ({message.senderPhone})
                        </small>
                      )}
                      {MessageTypeIcons[message.messageType] && (
                        <span className="ms-2">
                          {MessageTypeIcons[message.messageType]}
                        </span>
                      )}
                    </div>
                    <small className="text-muted">
                      {formatDate(message.timestamp)}
                    </small>
                  </div>

                  {/* Reply To Message */}
                  {message.replyToMessageId && (
                    <Card
                      className="reply-to-card mb-2"
                      style={{
                        borderRight: "3px solid var(--bs-primary)",
                        backgroundColor: "var(--bs-light)",
                      }}
                    >
                      <Card.Body className="p-2">
                        <small className="text-muted d-block">
                          پاسخ به: {message.replyToSenderName}
                        </small>
                        <small className="d-block">
                          {message.replyToMessageContent}
                        </small>
                      </Card.Body>
                    </Card>
                  )}

                  {/* Message Content */}
                  <div className="message-content">
                    {renderMessageContent(message)}
                  </div>

                  {/* Message Footer */}
                  <div className="message-footer mt-2 d-flex justify-content-between align-items-center">
                    <div>
                      {message.isEdited && (
                        <Badge bg="secondary" className="me-2">
                          ویرایش شده
                        </Badge>
                      )}
                      {message.reactions.length > 0 && (
                        <span className="reactions">
                          {message.reactions.map((reaction, idx) => (
                            <span key={idx} className="reaction me-1">
                              {reaction.emoji} {reaction.count}
                            </span>
                          ))}
                        </span>
                      )}
                    </div>
                    <small className="text-muted">#{message.id}</small>
                  </div>

                  {/* Read Statuses */}
                  {message.readStatuses && message.readStatuses.length > 0 && (
                    <div className="read-statuses mt-2">
                      <small className="text-muted">
                        خوانده شده توسط:{" "}
                        {message.readStatuses
                          .filter((s) => s.status === 2) // ReadStatus.Read = 2
                          .map((s) => s.userName)
                          .join(", ")}
                      </small>
                    </div>
                  )}
                </ListGroup.Item>
              ))}
            </ListGroup>

            {/* Load More Button */}
            {hasMore && (
              <div className="text-center mt-3">
                <Button
                  variant="outline-primary"
                  onClick={handleLoadMore}
                  disabled={loading}
                >
                  {loading ? (
                    <>
                      <Spinner
                        as="span"
                        animation="border"
                        size="sm"
                        className="me-2"
                      />
                      در حال بارگذاری...
                    </>
                  ) : (
                    "بارگذاری پیام‌های بیشتر"
                  )}
                </Button>
              </div>
            )}
          </>
        )}
      </Modal.Body>
    </Modal>
  );
};

export default ChatMessagesModal;
