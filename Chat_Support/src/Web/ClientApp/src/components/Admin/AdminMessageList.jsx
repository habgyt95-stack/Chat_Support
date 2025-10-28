import React from 'react';
import { Spinner, Button, Badge } from 'react-bootstrap';
import { Reply, Pencil, Trash, Check2, Check2All } from 'react-bootstrap-icons';
import './AdminMessageList.css';

const AdminMessageList = ({ messages, isLoading, hasMoreMessages, onLoadMoreMessages }) => {
  const formatDateHeader = (date) => {
    const messageDate = new Date(date);
    const today = new Date();
    const yesterday = new Date(today);
    yesterday.setDate(yesterday.getDate() - 1);

    if (messageDate.toDateString() === today.toDateString()) return 'امروز';
    if (messageDate.toDateString() === yesterday.toDateString()) return 'دیروز';
    return messageDate.toLocaleDateString('fa-IR', { year: 'numeric', month: 'long', day: 'numeric' });
  };

  const formatTime = (timestamp) => {
    return new Date(timestamp).toLocaleTimeString('fa-IR', {
      hour: '2-digit',
      minute: '2-digit',
    });
  };

  const formatFullDate = (timestamp) => {
    return new Date(timestamp).toLocaleString('fa-IR', {
      year: 'numeric',
      month: 'long',
      day: 'numeric',
      hour: '2-digit',
      minute: '2-digit',
    });
  };

  const getMessageTypeLabel = (type) => {
    switch (type) {
      case 0: return null; // Text
      case 1: return '📷 عکس';
      case 2: return '📎 فایل';
      case 3: return '🎙️ صوت';
      case 4: return '🎥 ویدیو';
      default: return null;
    }
  };

  const getDeliveryStatusIcon = (status) => {
    switch (status) {
      case 0: // Sent
        return <Check2 size={14} className="text-muted" />;
      case 1: // Delivered
        return <Check2All size={14} className="text-muted" />;
      case 2: // Read
        return <Check2All size={14} className="text-primary" />;
      default:
        return null;
    }
  };

  const renderReplyInfo = (message) => {
    // Support both shapes: admin DTO (replyToMessageContent/replyToSenderName) and legacy repliedToMessage object
    const hasAdminReply = message.replyToMessageId && (message.replyToMessageContent || message.replyToSenderName);
    const hasLegacyReplyObj = message.repliedToMessageId && message.repliedToMessage;
    if (!hasAdminReply && !hasLegacyReplyObj) return null;

    const content = hasAdminReply
      ? message.replyToMessageContent
      : message.repliedToMessage?.content;

    return (
      <div className="admin-message-reply-info">
        <Reply size={12} className="me-1" />
        <span className="reply-to-text">پاسخ به{hasAdminReply && message.replyToSenderName ? ` ${message.replyToSenderName}` : ''}:</span>
        {content && (
          <span className="reply-content">
            {content.substring(0, 50)}
            {content.length > 50 ? '...' : ''}
          </span>
        )}
      </div>
    );
  };

  const renderReactions = (message) => {
    if (!message.reactions || message.reactions.length === 0) return null;

    const looksAggregated = typeof message.reactions[0]?.count === 'number' || Array.isArray(message.reactions[0]?.userFullNames);

    if (looksAggregated) {
      return (
        <div className="admin-message-reactions">
          {message.reactions.map((r) => (
            <span key={r.emoji} className="reaction-badge" title={(r.userFullNames || []).join(', ')}>
              {r.emoji} {r.count > 1 && <span className="reaction-count">{r.count}</span>}
            </span>
          ))}
        </div>
      );
    }

    // Legacy: group array of individual reactions
    const groupedReactions = message.reactions.reduce((acc, reaction) => {
      if (!acc[reaction.emoji]) {
        acc[reaction.emoji] = [];
      }
      acc[reaction.emoji].push(reaction);
      return acc;
    }, {});

    return (
      <div className="admin-message-reactions">
        {Object.entries(groupedReactions).map(([emoji, reactions]) => (
          <span key={emoji} className="reaction-badge" title={reactions.map(r => r.userFullName).join(', ')}>
            {emoji} {reactions.length > 1 && <span className="reaction-count">{reactions.length}</span>}
          </span>
        ))}
      </div>
    );
  };

  const renderMessageContent = (message) => {
  const typeVal = (message.messageType !== undefined && message.messageType !== null) ? message.messageType : message.type;
  const typeLabel = getMessageTypeLabel(typeVal);
    
    return (
      <div className="admin-message-content">
        {/* Message Header */}
        <div className="admin-message-header">
          <div className="admin-message-sender-info">
            <span className="admin-message-sender">
              {message.senderFullName || 'کاربر ناشناس'}
            </span>
            {message.isEdited && (
              <span className="admin-message-edited" title="ویرایش شده">
                <Pencil size={10} className="ms-1" />
                ویرایش شده
              </span>
            )}
            {message.isDeleted && (
              <span className="admin-message-deleted" title="حذف شده">
                <Trash size={10} className="ms-1" />
                حذف شده
              </span>
            )}
          </div>
          <div className="admin-message-time" title={formatFullDate(message.timestamp)}>
            {formatTime(message.timestamp)}
          </div>
        </div>

        {/* Reply Info */}
        {renderReplyInfo(message)}

        {/* Message Type Label */}
        {typeLabel && <div className="admin-message-type">{typeLabel}</div>}

        {/* Message Content */}
        {message.content && !message.isDeleted && (
          <div className="admin-message-text">{message.content}</div>
        )}

        {message.isDeleted && (
          <div className="admin-message-text deleted-text">
            <em>این پیام حذف شده است</em>
          </div>
        )}

        {/* Attachment */}
        {(message.attachmentUrl || message.filePath) && !message.isDeleted && (
          <div className="admin-message-attachment">
            <a href={message.attachmentUrl || message.filePath} target="_blank" rel="noopener noreferrer">
              مشاهده پیوست
            </a>
          </div>
        )}

        {/* Message Footer */}
        <div className="admin-message-footer">
          {/* Reactions */}
          {renderReactions(message)}

          {/* Delivery Status */}
          <div className="admin-message-status">
            {getDeliveryStatusIcon(message.deliveryStatus)}
          </div>
        </div>

        {/* Forward Info */}
        {message.forwardedFromMessageId && (
          <div className="admin-message-forward-info">
            <Badge bg="secondary" className="forward-badge">
              فوروارد شده
            </Badge>
          </div>
        )}
      </div>
    );
  };

  const renderMessages = () => {
    let lastDate = null;
    return messages.map((message) => {
      const messageDate = new Date(message.timestamp).toDateString();
      const showDateHeader = messageDate !== lastDate;
      lastDate = messageDate;

      return (
        <React.Fragment key={message.id}>
          {showDateHeader && (
            <div className="admin-message-date-header">
              <span className="admin-message-date-badge">{formatDateHeader(message.timestamp)}</span>
            </div>
          )}
          <div className="admin-message-item" data-message-id={message.id}>
            {renderMessageContent(message)}
          </div>
        </React.Fragment>
      );
    });
  };

  return (
    <div className="admin-message-list-container">
      {isLoading && messages.length === 0 ? (
        <div className="flex-grow-1 d-flex align-items-center justify-content-center">
          <Spinner animation="border" />
        </div>
      ) : (
        <>
          {hasMoreMessages && (
            <div className="text-center my-2">
              <Button variant="light" size="sm" onClick={onLoadMoreMessages} disabled={isLoading}>
                {isLoading ? <Spinner size="sm" /> : 'بارگذاری پیام‌های قدیمی‌تر'}
              </Button>
            </div>
          )}
          {messages.length === 0 && !isLoading ? (
            <div className="flex-grow-1 d-flex align-items-center justify-content-center text-muted">
              <p>پیامی در این چت وجود ندارد</p>
            </div>
          ) : (
            <div className="admin-messages-wrapper">
              {renderMessages()}
            </div>
          )}
        </>
      )}
    </div>
  );
};

export default AdminMessageList;
