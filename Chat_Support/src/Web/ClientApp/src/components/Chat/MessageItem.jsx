import React, {useState, useEffect, useRef} from 'react';
import ReactDOM from 'react-dom';
import {Check2, Check2All, Clock, Reply, Pencil, Forward, Trash, Download, Eye, Clipboard} from 'react-bootstrap-icons';
import {MessageDeliveryStatus} from '../../types/chat';
import {useChat} from '../../hooks/useChat';
import {getUserIdFromToken} from '../../Utils/jwt';
import './Chat.css';
import {downloadFile} from '../../Utils/fileUtils';
import {VoiceMessagePlayer} from './VoiceRecorderComponent';
import ReadReceiptsModal from './ReadReceiptsModal';
import { TELEGRAM_REACTIONS } from './reactions';

const MENU_WIDTH = 180; // حداقل عرض منو از CSS
// Height hint for positioning; menu may grow when reaction grid expands
const MENU_HEIGHT = 320;

const MessageItem = ({
  message,
  isGroupChat = false,
  // Multi-select controls passed from MessageList
  selectionMode = false,
  selected = false,
  onToggleSelect,
  onRequestEnterSelection,
}) => {
  const {deleteMessage, setReplyingToMessage, setEditingMessage, setForwardingMessage, sendReaction} = useChat();
  const currentUserId = getUserIdFromToken(localStorage.getItem('token'));
  const isOwnMessage = Number(message.senderId) === Number(currentUserId);
  const [contextMenu, setContextMenu] = useState({visible: false, styles: {}});
  const [showFullReactions, setShowFullReactions] = useState(false);
  const [imageModalOpen, setImageModalOpen] = useState(false);
  const [isHoveringMedia, setIsHoveringMedia] = useState(false);
  const [showReadReceipts, setShowReadReceipts] = useState(false);
  const [displayFileName, setDisplayFileName] = useState('');
  const longPressTimer = useRef();
  const wrapperRef = useRef(null);
  const mouseHoldTimer = useRef();
  const suppressNextClick = useRef(false);
  const lastTapTime = useRef(0);
  const tapTimeout = useRef(null);
  const touchStartPos = useRef({ x: 0, y: 0 });
  const hasMoved = useRef(false);
  const isTouchEnv = (typeof window !== 'undefined') && (('ontouchstart' in window) || (navigator.maxTouchPoints || 0) > 0);
  const getIsSmallScreen = () => (typeof window !== 'undefined') && window.innerWidth <= 768;

  // selection within this message (for partial copy)
  const [hasLocalSelection, setHasLocalSelection] = useState(false);
  const [selectedText, setSelectedText] = useState('');

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

  const captureLocalTextSelection = () => {
    try {
      const sel = window.getSelection && window.getSelection();
      if (!sel || sel.rangeCount === 0) {
        setHasLocalSelection(false);
        setSelectedText('');
        return;
      }
      const range = sel.getRangeAt(0);
      const text = String(sel).trim();
      const containerEl = wrapperRef.current;
      if (!containerEl || !text) {
        setHasLocalSelection(false);
        setSelectedText('');
        return;
      }
      // Ensure both ends of selection are inside this message wrapper
      const within = containerEl.contains(range.startContainer) && containerEl.contains(range.endContainer);
      setHasLocalSelection(Boolean(within && text));
      setSelectedText(within ? text : '');
    } catch {
      setHasLocalSelection(false);
      setSelectedText('');
    }
  };

  const openContextMenuAt = (clientX, clientY, targetEl) => {
    const rect = (targetEl || wrapperRef.current)?.getBoundingClientRect();
    if (!rect) return;

    // Close any other open menus across messages, but keep this one
    try {
      document.dispatchEvent(new CustomEvent('chat-close-all-menus', { detail: { activeId: message.id } }));
    } catch {
      // ignore
    }

    const opensLeft = clientX + MENU_WIDTH > window.innerWidth;
    const opensUp = clientY + MENU_HEIGHT > window.innerHeight;

    const styles = {};
    if (opensUp) {
      styles.bottom = rect.height - (clientY - rect.top);
    } else {
      styles.top = clientY - rect.top;
    }

    if (isOwnMessage) {
      if (!opensLeft) {
        styles.right = 0;
      } else {
        styles.left = 0;
      }
    } else {
      if (!opensLeft) {
        styles.left = 0;
      } else {
        styles.right = 0;
      }
    }

    setContextMenu({visible: true, styles});
  };

  const handleContextMenu = (e) => {
    e.preventDefault();

    // On touch devices, ignore native contextmenu to avoid conflicts with long-press logic
    if (isTouchEnv || getIsSmallScreen()) {
      return;
    }

    // If in selection mode, toggle selection instead of opening menu
    if (selectionMode && onToggleSelect) {
      onToggleSelect(message.id);
      return;
    }

    captureLocalTextSelection();
    openContextMenuAt(e.clientX, e.clientY, e.currentTarget);
  };

  const handleTouchStart = (e) => {
    // Record touch start position
    const touch = e.touches[0];
    touchStartPos.current = { x: touch.clientX, y: touch.clientY };
    hasMoved.current = false;
    
    // Start long-press timer
    longPressTimer.current = setTimeout(() => {
      // Long-press detected
      if (selectionMode) {
        // In selection mode: do nothing, allow native text selection
        return;
      }
      // Not in selection mode: enter selection mode
      if (onRequestEnterSelection) {
        onRequestEnterSelection(message.id);
        suppressNextClick.current = true;
      }
    }, 500);
  };

  const handleTouchMove = (e) => {
    // Detect if user is scrolling
    const touch = e.touches[0];
    const deltaX = Math.abs(touch.clientX - touchStartPos.current.x);
    const deltaY = Math.abs(touch.clientY - touchStartPos.current.y);
    
    // If moved more than 10px, consider it a scroll/swipe
    if (deltaX > 10 || deltaY > 10) {
      hasMoved.current = true;
      clearTimeout(longPressTimer.current);
    }
  };

  const handleTouchEnd = (e) => {
    clearTimeout(longPressTimer.current);
    
    // Don't process tap if long-press was triggered or if user scrolled
    if (suppressNextClick.current || hasMoved.current) {
      hasMoved.current = false;
      return;
    }

    // Handle double-tap for menu
    const now = Date.now();
    const timeSinceLastTap = now - lastTapTime.current;
    
    if (timeSinceLastTap < 300 && timeSinceLastTap > 0) {
      // Double-tap detected
      clearTimeout(tapTimeout.current);
      lastTapTime.current = 0;
      
      if (!selectionMode) {
        // Open menu on double-tap
        e.preventDefault();
        const touch = e.changedTouches[0];
        openContextMenuAt(touch.clientX, touch.clientY, wrapperRef.current);
      }
    } else {
      // Single tap - wait to see if it's a double-tap
      lastTapTime.current = now;
      tapTimeout.current = setTimeout(() => {
        lastTapTime.current = 0;
        // Single tap confirmed - toggle selection if in selection mode
        if (selectionMode && onToggleSelect) {
          onToggleSelect(message.id);
        }
      }, 300);
    }
  };

  const handleMouseDown = (e) => {
    // Desktop: long-hold with left click -> enter selection mode
    if (e.button !== 0) return; // only left button
    if (selectionMode) return;
    mouseHoldTimer.current = setTimeout(() => {
      if (onRequestEnterSelection) {
        onRequestEnterSelection(message.id);
        suppressNextClick.current = true;
      }
    }, 600);
  };

  const handleMouseUp = () => {
    clearTimeout(mouseHoldTimer.current);
  };

  const handleMouseLeave = () => {
    clearTimeout(mouseHoldTimer.current);
  };

  const closeContextMenu = () => {
    setContextMenu({ visible: false, styles: {} });
    setShowFullReactions(false);
    // don't clear selectedText here to allow quick re-open
  };

  useEffect(() => {
    if (!contextMenu.visible) return;
    const handleGlobalClick = () => {
      // اگر گرید باز است، اول فقط گرید بسته شود و منو باقی بماند
      if (showFullReactions) {
        setShowFullReactions(false);
      } else {
        closeContextMenu();
      }
    };
    document.addEventListener('click', handleGlobalClick);
    return () => document.removeEventListener('click', handleGlobalClick);
  }, [contextMenu.visible, showFullReactions]);

  // Listen for global "close all menus" events to ensure only one context menu is open
  useEffect(() => {
    const onCloseAll = (ev) => {
      const activeId = ev?.detail?.activeId;
      if (activeId !== message.id) {
        setContextMenu({ visible: false, styles: {} });
        setShowFullReactions(false);
      }
    };
    document.addEventListener('chat-close-all-menus', onCloseAll);
    return () => document.removeEventListener('chat-close-all-menus', onCloseAll);
  }, [message.id]);

  const handleAction = (action) => {
    closeContextMenu();
    action();
  };

  const copyToClipboard = async (text) => {
    try {
      if (navigator.clipboard && window.isSecureContext) {
        await navigator.clipboard.writeText(text);
      } else {
        // fallback textarea method
        const ta = document.createElement('textarea');
        ta.value = text;
        ta.style.position = 'fixed';
        ta.style.opacity = '0';
        document.body.appendChild(ta);
        ta.focus();
        ta.select();
        document.execCommand('copy');
        document.body.removeChild(ta);
      }
    } catch (err) {
      console.error('Copy failed', err);
    }
  };

  const describeMessageForCopy = () => {
    // Return best-effort text to copy for this message
    switch (message.type) {
      case 0: // text
        return message.content || '';
      case 1: // image
        return message.content || '📷 عکس';
      case 2: // file
        return message.content || (displayFileName ? `📎 ${displayFileName}` : '📎 فایل');
      case 3: // voice
        return '🎙️ پیام صوتی';
      case 4: // video
        return message.content || '🎥 ویدیو';
      default:
        return message.content || '';
    }
  };

  const handleCopySelectedText = async () => {
    if (hasLocalSelection && selectedText) {
      await copyToClipboard(selectedText);
    }
  };

  const handleCopyMessage = async () => {
    const base = describeMessageForCopy();
    const withMeta = isGroupChat && message.senderFullName ? `${message.senderFullName}: ${base}` : base;
    await copyToClipboard(withMeta);
  };

  const handleReaction = (emoji) => {
    sendReaction(message.id, message.chatRoomId, emoji);
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
    return null;
  }

  return (
    <>
      <div
        ref={wrapperRef}
        data-message-id={message.id}
  className={`message-item-wrapper ${isOwnMessage ? 'sent' : 'received'} ${selectionMode ? 'is-selecting' : ''} ${selected ? 'is-selected' : ''} ${(isTouchEnv && !selectionMode) ? 'touch-no-select' : ''}`}
        onContextMenu={handleContextMenu}
        onMouseDown={handleMouseDown}
        onMouseUp={handleMouseUp}
        onMouseLeave={handleMouseLeave}
        onTouchStart={handleTouchStart}
        onTouchEnd={handleTouchEnd}
        onTouchMove={handleTouchMove}
        onClick={() => {
          // prevent click after long-press action
          if (suppressNextClick.current) { 
            suppressNextClick.current = false; 
            return; 
          }
          
          // Desktop only: single click in selection mode toggles
          if (!isTouchEnv && selectionMode && onToggleSelect) {
            onToggleSelect(message.id);
          }
        }}
      >
        {/* No explicit checkbox in selection mode; visual selection handled by bubble outline */}

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
            {/* Telegram-like reactions row above menu */}
            <div className="context-reaction-wrap">
              <div className="context-reaction-row" dir="ltr">
                <div className="context-reaction-scroll hide-scrollbar">
                  {TELEGRAM_REACTIONS.slice(0, 5).map((r, idx) => (
                    <button
                      key={`qr-${idx}`}
                      type="button"
                      className="reaction-quick-btn"
                      onClick={() => handleReaction(r.emoji)}
                      aria-label={`React ${r.emoji}`}
                    >
                      {r.emoji}
                    </button>
                  ))}
                </div>
                <button
                  type="button"
                  className="reaction-more-toggle"
                  onClick={(e) => { e.stopPropagation(); setShowFullReactions((v) => !v); }}
                  aria-expanded={showFullReactions}
                  aria-label={showFullReactions ? 'کمتر' : 'بیشتر'}
                >
                  <span className="reaction-toggle-arrow" aria-hidden="true">
                    {showFullReactions ? '▴' : '▾'}
                  </span>
                </button>
              </div>
              {showFullReactions && (
                <div className="context-reaction-grid" dir="ltr">
                  {TELEGRAM_REACTIONS.map((r, idx) => (
                    <button
                      key={`rg-${idx}`}
                      type="button"
                      className="reaction-grid-btn"
                      onClick={() => handleReaction(r.emoji)}
                      aria-label={`React ${r.emoji}`}
                    >
                      {r.emoji}
                    </button>
                  ))}
                </div>
              )}
            </div>
            {!showFullReactions && (
              <ul>
                {/* Copy (smart): selected text if present, otherwise whole message */}
                <li onClick={() => handleAction(() => {
                  if (hasLocalSelection && selectedText) { handleCopySelectedText(); }
                  else { handleCopyMessage(); }
                })}>
                  <Clipboard /> کپی
                </li>
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
            )}
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

      {/* Emoji picker portal removed; reactions integrated in context menu */}
    </>
  );
};

export default MessageItem;
