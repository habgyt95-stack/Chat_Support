import React, { useState, useEffect } from 'react';
import { Modal, ListGroup, Spinner, Alert } from 'react-bootstrap';
import { Check2All } from 'react-bootstrap-icons';
import { chatApi } from '../../services/chatApi';

const ReadReceiptsModal = ({ show, onHide, messageId }) => {
  const [receipts, setReceipts] = useState([]);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState(null);

  useEffect(() => {
    if (show && messageId) {
      loadReceipts();
    }
  }, [show, messageId]);

  const loadReceipts = async () => {
    setIsLoading(true);
    setError(null);
    try {
      const data = await chatApi.getMessageReadReceipts(messageId);
      setReceipts(data);
    } catch (err) {
      setError('خطا در بارگذاری اطلاعات خوانده شدن پیام');
      console.error('Failed to load read receipts:', err);
    } finally {
      setIsLoading(false);
    }
  };

  const formatDateTime = (dateStr) => {
    const date = new Date(dateStr);
    return date.toLocaleString('fa-IR', {
      year: 'numeric',
      month: 'long',
      day: 'numeric',
      hour: '2-digit',
      minute: '2-digit',
    });
  };

  return (
    <Modal show={show} onHide={onHide} centered>
      <Modal.Header closeButton>
        <Modal.Title className="d-flex align-items-center gap-2">
          <Check2All className="text-primary" size={24} />
          خوانده شده توسط
        </Modal.Title>
      </Modal.Header>
      <Modal.Body>
        {isLoading ? (
          <div className="text-center py-4">
            <Spinner animation="border" variant="primary" />
            <p className="mt-2 text-muted">در حال بارگذاری...</p>
          </div>
        ) : error ? (
          <Alert variant="danger">{error}</Alert>
        ) : receipts.length === 0 ? (
          <Alert variant="info">هنوز کسی این پیام را نخوانده است.</Alert>
        ) : (
          <ListGroup variant="flush">
            {receipts.map((receipt) => (
              <ListGroup.Item
                key={receipt.userId}
                className="d-flex align-items-center gap-3 py-3"
              >
                {receipt.avatarUrl ? (
                  <img
                    src={receipt.avatarUrl}
                    alt={receipt.fullName}
                    className="rounded-circle"
                    style={{ width: '40px', height: '40px', objectFit: 'cover' }}
                  />
                ) : (
                  <div
                    className="rounded-circle bg-primary text-white d-flex align-items-center justify-content-center"
                    style={{ width: '40px', height: '40px' }}
                  >
                    {receipt.fullName.charAt(0).toUpperCase()}
                  </div>
                )}
                <div className="flex-grow-1">
                  <div className="fw-bold">{receipt.fullName}</div>
                  <small className="text-muted">{formatDateTime(receipt.readAt)}</small>
                </div>
                <Check2All className="text-success" size={20} />
              </ListGroup.Item>
            ))}
          </ListGroup>
        )}
      </Modal.Body>
    </Modal>
  );
};

export default ReadReceiptsModal;
