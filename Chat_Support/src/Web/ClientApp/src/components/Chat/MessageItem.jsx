import React, {useState, useEffect, useRef} from 'react';
import ReactDOM from 'react-dom';
import {Check2, Check2All, Clock, Reply, Pencil, Forward, Trash, EmojiSmile, Download, Eye} from 'react-bootstrap-icons';
import {MessageDeliveryStatus} from '../../types/chat';
import {useChat} from '../../hooks/useChat';
import {getUserIdFromToken} from '../../Utils/jwt';
import './Chat.css';
import {downloadFile} from '../../Utils/fileUtils';
import {VoiceMessagePlayer} from './VoiceRecorderComponent';
import ReadReceiptsModal from './ReadReceiptsModal';
import EmojiPicker from './EmojiPicker';

const MENU_WIDTH = 180; // حداقل عرض منو از CSS
const MENU_HEIGHT = 220;

const MessageItem = ({message, isGroupChat = false}) => {
  const {deleteMessage, setReplyingToMessage, setEditingMessage, setForwardingMessage, sendReaction} = useChat();
  const currentUserId = getUserIdFromToken(localStorage.getItem('token'));
  const isOwnMessage = Number(message.senderId) === Number(currentUserId);
  const [contextMenu, setContextMenu] = useState({visible: false, styles: {}});
  const [showEmojiPicker, setShowEmojiPicker] = useState(false);
  const [emojiPickerPosition, setEmojiPickerPosition] = useState({});
  const [imageModalOpen, setImageModalOpen] = useState(false);
  const [isHoveringMedia, setIsHoveringMedia] = useState(false);
  const [showReadReceipts, setShowReadReceipts] = useState(false);
  const [displayFileName, setDisplayFileName] = useState('');
  const longPressTimer = useRef();

  const getFileNameFromUrl = (url) => {
    if (!url) return '';
    try {
      const u = new URL(url, window.location.origin);
      const raw = u.pathname.split('/').pop() || '';
      return decodeURIComponent(raw);
    } catch {
      const idx = url.lastIndexOf('/')
      return idx !== -1 ? decodeURIComponent(url.substring(idx + 1)) : url;
    }
  };

  const fetchMetaName = async (filePath) => {
    try {
      const res = await fetch(`/api/chat/file-meta?filePath=${encodeURIComponent(filePath)}`, { credentials: 'include' });
      if (!res.ok) return '';
      const data = await res.json();
      return data?.fileName || '';
    } catch {
      return '';
    }
  };

  // جلوگیری از اسکرول body وقتی مودال تصویر باز است
  useEffect(() => {
    if (imageModalOpen) {
      document.body.style.overflow = 'hidden';
    } else {
      document.body.style.overflow = '';
    }
    return () => {
      document.body.style.overflow = '';
    };
  }, [imageModalOpen]);

  useEffect(() => {
    if (message.type !== 2 || !message.attachmentUrl) {
      setDisplayFileName('');
      return;
    }
    let cancelled = false;
    (async () => {
      // priority: message.fileName -> server meta -> fallback derived
      const direct = message.fileName && typeof message.fileName === 'string' ? message.fileName : '';
      if (direct) { setDisplayFileName(direct); return; }

      const meta = await fetchMetaName(message.attachmentUrl);
      if (!cancelled && meta) { setDisplayFileName(meta); return; }

      const fallback = getFileNameFromUrl(message.attachmentUrl);
      if (!cancelled) setDisplayFileName(fallback);
    })();
    return () => { cancelled = true; };
  }, [message.type, message.attachmentUrl, message.fileName]);

  const handleContextMenu = (e) => {
    e.preventDefault();

    // ابعاد و موقعیت والد (message-item-wrapper)
    const rect = e.currentTarget.getBoundingClientRect();

    // ۱. بررسی سرریز افقی (آیا منو از سمت راست صفحه بیرون می‌زند؟)
    const opensLeft = e.clientX + MENU_WIDTH > window.innerWidth;

    // ۲. بررسی سرریز عمودی (آیا منو از پایین صفحه بیرون می‌زد؟)
    const opensUp = e.clientY + MENU_HEIGHT > window.innerHeight;

    const styles = {};

    // تنظیم موقعیت عمودی
    if (opensUp) {
      styles.bottom = rect.height - (e.clientY - rect.top);
    } else {
      styles.top = e.clientY - rect.top;
    }

    // تنظیم موقعیت افقی بر اساس نوع پیام (sent/received)
    if (isOwnMessage) {
      // پیام سمت راست (sent)
      if (!opensLeft) {
        // فضای کافی سمت راست پیام: منو را از راست پیام باز کن
        styles.right = 0;
      } else {
        // فضای کافی نیست: منو را از چپ پیام باز کن
        styles.left = 0;
      }
    } else {
      // پیام سمت چپ (received)
      if (!opensLeft) {
        // فضای کافی سمت چپ پیام: منو را از چپ پیام باز کن
        styles.left = 0;
      } else {
        // فضای کافی نیست: منو را از راست پیام باز کن
        styles.right = 0;
      }
    }

    setContextMenu({visible: true, styles: styles});
  };

  const handleTouchStart = (e) => {
    longPressTimer.current = setTimeout(() => {
      const touch = e.touches[0];
      setContextMenu({visible: true, x: touch.clientX, y: touch.clientY});
    }, 500); // 500ms for long press
  };

  const handleTouchEnd = () => {
    clearTimeout(longPressTimer.current);
  };

  const closeContextMenu = () => {
    setContextMenu({ visible: false, styles: {} });
    setShowEmojiPicker(false);
  };

  useEffect(() => {
    if (contextMenu.visible) {
      document.addEventListener('click', closeContextMenu);
      return () => document.removeEventListener('click', closeContextMenu);
    }
  }, [contextMenu.visible]);

  const handleAction = (action) => {
    closeContextMenu();
    action();
  };

  const handleReaction = (emoji) => {
    sendReaction(message.id, message.chatRoomId, emoji);
    setShowEmojiPicker(false);
    closeContextMenu();
  };

  const handleReply = () => {
    setReplyingToMessage(message);
  };

  const handleEdit = () => {
    setEditingMessage(message);
  };

  const handleForward = () => {
    setForwardingMessage(message);
  };

  const handleDownload = () => {
    if (message.attachmentUrl) {
      const suggestedName = displayFileName || getFileNameFromUrl(message.attachmentUrl) || `media_${message.id}`;
      downloadFile(message.attachmentUrl, suggestedName);
    }
  };

  const handleDelete = async () => {
    if (window.confirm('آیا از حذف این پیام اطمینان دارید؟')) {
      try {
        await deleteMessage(message.id);
      } catch (error) {
        console.error('Error deleting message:', error);
      }
    }
  };

  const handleShowReadReceipts = () => {
    setShowReadReceipts(true);
  };

  const renderDeliveryStatus = () => {
    if (!isOwnMessage) return null;

    switch (message.deliveryStatus) {
      case MessageDeliveryStatus.Sent:
      case MessageDeliveryStatus.Delivered:
        return <Check2 size={18} className="text-muted" />; // یک تیک خاکستری برای ارسال و رسیده
      case MessageDeliveryStatus.Read:
        return <Check2All size={18} className="text-primary" />; // دو تیک آبی برای خوانده‌شده
      default:
        return <Clock size={14} />;
    }
  };

  const renderRepliedMessage = () => {
    if (!message.replyToMessageId || !message.repliedMessageContent) return null;

    return (
      <div className="p-2 rounded mb-1" style={{background: 'rgba(0,0,0,0.05)', borderRight: '2px solid var(--primary-color)'}}>
        <div className="fw-bold text-primary" style={{fontSize: '0.85rem'}}>
          {message.repliedMessageSenderFullName || 'کاربر'}
        </div>
        <div className="text-muted text-truncate" style={{fontSize: '0.8rem'}}>
          {message.repliedMessageContent}
        </div>
      </div>
    );
  };

  const renderReactions = () => {
    if (!message.reactions || message.reactions.length === 0) return null;

    return (
      <div className="message-reactions mt-1">
        {message.reactions.map((reaction) => (
          <span 
            key={reaction.emoji} 
            className={`reaction-badge ${reaction.isReactedByCurrentUser ? 'reacted-by-me' : ''}`}
            onClick={() => handleReaction(reaction.emoji)} 
            title={reaction.userFullNames?.join(', ') || ''}
          >
            {reaction.emoji} {reaction.count}
          </span>
        ))}
      </div>
    );
  };

  const formatTime = (dateString) => {
    return new Date(dateString).toLocaleTimeString('fa-IR', {hour: '2-digit', minute: '2-digit'});
  };

  if (message.isDeleted) {
    return (
      <div className={`message-item-wrapper ${isOwnMessage ? 'sent' : 'received'}`}>
        <div className="message-bubble fst-italic text-muted">پیام حذف شد</div>
      </div>
    );
  }

  return (
    <>
      <div className={`message-item-wrapper ${isOwnMessage ? 'sent' : 'received'}`} onContextMenu={handleContextMenu} onTouchStart={handleTouchStart} onTouchEnd={handleTouchEnd} onTouchMove={handleTouchEnd}>
        <div className={`message-bubble ${isOwnMessage ? 'message-sent' : 'message-received'}`}>
          {!isOwnMessage && isGroupChat && (
            <div className="fw-bold text-primary mb-1" style={{fontSize: '0.85rem'}}>
              {message.senderFullName}
            </div>
          )}
          {renderRepliedMessage()}

          {/* Render different content based on message type */}

          {message.type === 0 && <div className="message-content">{message.content}</div>}

          {message.type === 1 && message.attachmentUrl && (
            <div
              className="message-media-wrapper"
              onMouseEnter={() => setIsHoveringMedia(true)}
              onMouseLeave={() => setIsHoveringMedia(false)}
            >
              <img
                src={message.attachmentUrl}
                alt={message.content || 'تصویر'}
                className="message-image"
                onClick={() => setImageModalOpen(true)}
              />
              {isHoveringMedia && (
                <button
                  className="media-download-btn"
                  type="button"
                  onClick={(e) => { e.stopPropagation(); handleDownload(); }}
                  aria-label="دانلود تصویر"
                >
                  <Download size={18} />
                </button>
              )}
            </div>
          )}
          {message.type === 1 && message.content && (
            <div className="media-caption">{message.content}</div>
          )}

          {message.type === 2 && message.attachmentUrl && (
            <div className="message-file">
              <a 
                href={`/api/chat/download?filePath=${encodeURIComponent(message.attachmentUrl)}`} 
                download
                target="_blank" 
                rel="noopener noreferrer"
              >
                📎 {displayFileName || 'دانلود فایل'}
              </a>
              {message.content && message.content !== (displayFileName || '') && (
                <div className="media-caption">{message.content}</div>
              )}
            </div>
          )}

          {((message.type === 3 && message.attachmentUrl) || (message.attachmentUrl && message.content === 'پیام صوتی')) && <VoiceMessagePlayer audioUrl={message.attachmentUrl} duration={message.duration} />}

          {message.type === 4 && message.attachmentUrl && (
            <div
              className="message-media-wrapper video"
              onMouseEnter={() => setIsHoveringMedia(true)}
              onMouseLeave={() => setIsHoveringMedia(false)}
            >
              <video controls className="message-video" style={{maxWidth: '100%'}}>
                <source src={message.attachmentUrl} />
              </video>
              {isHoveringMedia && (
                <button
                  className="media-download-btn"
                  type="button"
                  onClick={(e) => { e.stopPropagation(); handleDownload(); }}
                  aria-label="دانلود ویدیو"
                >
                  <Download size={18} />
                </button>
              )}
            </div>
          )}
          {message.type === 4 && message.content && (
            <div className="media-caption">{message.content}</div>
          )}

          {message.isEdited && (
            <span className="text-muted" style={{fontSize: '0.7rem', marginLeft: '0.5rem'}}>
              (ویرایش شده)
            </span>
          )}

          <div className="message-footer">
            <span className="message-time">{formatTime(message.timestamp)}</span>
            <span className="message-status">{renderDeliveryStatus()}</span>
          </div>
          {console.log(message)}
          {renderReactions()}
        </div>

        {contextMenu.visible && (
          <div
            className="custom-context-menu"
            style={contextMenu.styles}
            onClick={(e) => e.stopPropagation()}
          >
            <ul>
              <li onClick={() => handleAction(handleReply)}>
                <Reply /> پاسخ
              </li>
              <li onClick={() => handleAction(handleForward)}>
                <Forward /> هدایت
              </li>
              {isGroupChat && (
                <li onClick={() => handleAction(handleShowReadReceipts)}>
                  <Eye /> خوانده شده توسط
                </li>
              )}
              {isOwnMessage && message.type === 0 && (
                <li onClick={() => handleAction(handleEdit)}>
                  <Pencil /> ویرایش
                </li>
              )}
              <li 
                onClick={(e) => {
                  e.stopPropagation();
                  const rect = e.currentTarget.getBoundingClientRect();
                  setEmojiPickerPosition({
                    top: rect.top,
                    left: rect.left + rect.width + 10,
                  });
                  setShowEmojiPicker(!showEmojiPicker);
                }}
                style={{position: 'relative'}}
              >
                <EmojiSmile /> واکنش
              </li>
              {(message.type === 1 || message.type === 4 || message.type === 2) && message.attachmentUrl && (
                <li onClick={() => handleAction(handleDownload)}>
                  <Download /> دانلود
                </li>
              )}
              {isOwnMessage && (
                <li className="danger" onClick={() => handleAction(handleDelete)}>
                  <Trash /> حذف
                </li>
              )}
            </ul>
          </div>
        )}
      </div>
      {/* Image Modal - Using Portal to render at document body level */}
      {imageModalOpen && message.type === 1 && message.attachmentUrl && ReactDOM.createPortal(
        <div 
          className="image-modal-overlay" 
          onClick={() => setImageModalOpen(false)}
          style={{ 
            position: 'fixed',
            top: 0,
            left: 0,
            right: 0,
            bottom: 0,
            zIndex: 9999
          }}
        >
          <img 
            src={message.attachmentUrl} 
            alt={message.content || 'تصویر'} 
            className="image-modal-img" 
            onClick={(e) => e.stopPropagation()} 
          />
        </div>,
        document.body
      )}

      {/* Read Receipts Modal */}
      {showReadReceipts && (
        <ReadReceiptsModal
          show={showReadReceipts}
          onHide={() => setShowReadReceipts(false)}
          messageId={message.id}
        />
      )}

      {/* Emoji Picker - Using Portal */}
      {showEmojiPicker && ReactDOM.createPortal(
        <EmojiPicker 
          onSelect={handleReaction} 
          onClose={() => setShowEmojiPicker(false)}
          position={emojiPickerPosition}
        />,
        document.body
      )}
    </>
  );
};

export default MessageItem;
