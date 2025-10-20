import React, { useState, useRef, useEffect } from 'react';

import { Form, Button, Spinner, CloseButton, OverlayTrigger, Popover } from 'react-bootstrap';

import { useChat } from '../../hooks/useChat';

import { MessageType } from '../../types/chat';

import './Chat.css';

import { IoSend, IoCheckmark } from 'react-icons/io5';

import { BsEmojiSmile } from 'react-icons/bs';

import EmojiPicker, { EmojiStyle } from 'emoji-picker-react';

import VoiceRecorderComponent from './VoiceRecorderComponent';

import FileUploadComponent from './FileUploadComponent';

const MessageInput = ({ roomId }) => {
    const [message, setMessage] = useState('');

    const { sendMessage, startTyping, stopTyping, replyingToMessage, clearReplyingToMessage, editingMessage, clearEditingMessage, editMessage, forwardingMessage, clearForwardingMessage, forwardMessage } = useChat();

    const [isSending, setIsSending] = useState(false);

    const textareaRef = useRef(null);

    const typingTimeoutRef = useRef(null);

    // const [showEmojiPicker, setShowEmojiPicker] = useState(false);
    const [voiceActive, setVoiceActive] = useState(false); // ضبط/پیش‌نمایش فعال

    const [pendingAttachment, setPendingAttachment] = useState(null); // { url, name, type, size, mimeType }

    const MAX_TEXTAREA_HEIGHT = 180; // حداکثر ارتفاع ورودی (px) شبیه تلگرام

    useEffect(() => {
        const el = textareaRef.current;
        if (el) {
            // reset height to measure natural scrollHeight
            el.style.height = 'auto';
            const next = Math.min(el.scrollHeight, MAX_TEXTAREA_HEIGHT);
            el.style.height = next + 'px';
            if (el.scrollHeight > MAX_TEXTAREA_HEIGHT) {
                el.style.overflowY = 'auto';
            } else {
                el.style.overflowY = 'hidden';
            }
            // Mobile dynamic height propagation so محتوای بالاتر فشرده نشود و رشد به سمت بالا حس شود
            if (window.innerWidth <= 768) {
                const container = el.closest('.message-input-container');
                if (container) {
                    // height of wrapper including padding
                    const wrapper = container.querySelector('.message-input-wrapper');
                    if (wrapper) {
                        const h = wrapper.getBoundingClientRect().height;
                        // Set CSS variable consumed by chat panel content bottom calc
                        document.documentElement.style.setProperty('--chat-input-dyn', h + 'px');
                    }
                }
            }
        }
    }, [message, pendingAttachment]);

    useEffect(() => {
        if (replyingToMessage) {
            textareaRef.current?.focus();
        }
    }, [replyingToMessage]);

    const isEditing = !!editingMessage;

    const isReplying = !!replyingToMessage;

    const isForwarding = !!forwardingMessage;

    useEffect(() => {
        if (isEditing) {
            setMessage(editingMessage.content);

            textareaRef.current?.focus();
        } else if (isReplying || isForwarding) {
            textareaRef.current?.focus();
        }
    }, [isEditing, editingMessage, isReplying, isForwarding]);

    const handleCancelAction = () => {
        if (isEditing) clearEditingMessage();

        if (isReplying) clearReplyingToMessage();

        if (isForwarding) clearForwardingMessage();

        setMessage('');
        setPendingAttachment(null);
    };

    const handleSubmit = async () => {
        const content = message.trim();

        if (!content && !isForwarding) return;

        if (isSending) return;

        setIsSending(true);

        try {
            if (isEditing) {
                await editMessage(editingMessage.id, content);
            } else if (isReplying) {
                await sendMessage(roomId, { content, type: MessageType.Text, replyToMessageId: replyingToMessage.id });
            } else if (isForwarding) {
                // برای هدایت، محتوای جدیدی ارسال نمی‌شود

                await forwardMessage(forwardingMessage.id, roomId);
            } else if (pendingAttachment) {
                await sendMessage(roomId, {
                    content,
                    type: pendingAttachment.type,
                    attachmentUrl: pendingAttachment.url,
                    replyToMessageId: replyingToMessage?.id,
                });
            } else {
                await sendMessage(roomId, { content, type: MessageType.Text });
            }

            handleCancelAction(); // پاک کردن همه حالت‌ها

            setMessage('');
        } catch (error) {
            console.error('Action failed:', error);

            // پیام را در صورت خطا بازگردان

            if (!isForwarding) setMessage(content);
        } finally {
            setIsSending(false);

            // تأخیر کوتاه برای اطمینان از حفظ focus

            setTimeout(() => {
                textareaRef.current?.focus();
            }, 150);
        }
    };

    const handleTyping = () => {
        if (!typingTimeoutRef.current) {
            startTyping(roomId);
        } else {
            clearTimeout(typingTimeoutRef.current);
        }

        typingTimeoutRef.current = setTimeout(() => {
            stopTyping(roomId);

            typingTimeoutRef.current = null;
        }, 1500);
    };

    const handleSendMessage = async () => {
        const content = message.trim();

        if (!content || isSending) return;

        // ذخیره reference به textarea قبل از تغییر state

        const textareaElement = textareaRef.current;

        setIsSending(true);

        setMessage('');

        try {
            if (pendingAttachment) {
                await sendMessage(roomId, {
                    content,
                    type: pendingAttachment.type,
                    attachmentUrl: pendingAttachment.url,
                    replyToMessageId: replyingToMessage?.id,
                });
                setPendingAttachment(null);
            } else {
                await sendMessage(roomId, {
                    content,

                    type: MessageType.Text,

                    replyToMessageId: replyingToMessage?.id,
                });
            }

            clearReplyingToMessage();
        } catch (error) {
            console.error('Failed to send message:', error);

            setMessage(content); // Restore message on failure
        } finally {
            setIsSending(false);

            // تأخیر برای حفظ focus بعد از ارسال پیام

            setTimeout(() => {
                if (textareaElement) {
                    textareaElement.focus();

                    // در موبایل، گاهی نیاز است cursor را تنظیم کنیم

                    if (/Mobi|Android/i.test(navigator.userAgent)) {
                        textareaElement.setSelectionRange(0, 0);
                    }
                }
            }, 200);
        }
    };

    // Key handling: Enter => newline, Ctrl+Enter (or Cmd+Enter) => send
    const handleKeyPress = (e) => {
        if (e.key === 'Enter') {
            // If user pressed Ctrl+Enter (Windows/Linux) or Cmd+Enter (Mac) send message
            if ((e.ctrlKey || e.metaKey) && !e.shiftKey) {
                e.preventDefault();
                // Avoid sending empty messages
                handleSendMessage();
            } else {
                // Plain Enter: allow newline (default). Do nothing.
                // (Previously Enter would send; requirement changed.)
            }
        }
    };

    const onEmojiClick = (emojiObject) => {
        setMessage((prev) => prev + emojiObject.emoji);

        textareaRef.current?.focus();
    };

    // Callback for file upload component
    const onFileUploaded = async ({ url, name, type, size, mimeType }) => {
        // مرحله جدید: فایل آپلود شده در حالت انتظار قرار می‌گیرد و کاربر باید کپشن را وارد کند.
        setPendingAttachment({ url, name, type, size, mimeType });
        // کپشن باید اجباری باشد؛ مقدار اولیه ورودی خالی می‌ماند تا کاربر حتما بنویسد.
        setMessage('');
        // فوکوس روی تکست‌باکس برای نوشتن کپشن
        setTimeout(() => textareaRef.current?.focus(), 0);
    };

    // Callback for voice recorder component

    const onVoiceRecorded = async ({ url, type }) => {
        try {
            setIsSending(true);

            await sendMessage(roomId, {
                content: 'پیام صوتی',

                type,

                attachmentUrl: url,
            });
        } catch (error) {
            console.error('Failed to send voice message:', error);

            alert('خطا در ارسال پیام صوتی');
        } finally {
            setIsSending(false);

            // حفظ focus بعد از ارسال پیام صوتی

            setTimeout(() => {
                textareaRef.current?.focus();
            }, 150);
        }
    };

    // Emoji picker popover

    const emojiPickerPopover = (
        <Popover id="emoji-picker-popover" style={{ maxWidth: 'none', direction: 'ltr' }}>
            <Popover.Body className="p-0">
                <EmojiPicker onEmojiClick={onEmojiClick} emojiStyle={EmojiStyle.NATIVE} />
            </Popover.Body>
        </Popover>
    );

    const renderActionPreview = () => {
        if (isEditing) {
            return (
                <div className="action-preview">
                    <div className="fw-bold text-primary">ویرایش پیام</div>

                    <div className="preview-content text-muted">{editingMessage.content}</div>

                    <Button variant="link" className="p-0 text-danger" onClick={handleCancelAction} title="لغو ویرایش">
                        ×
                    </Button>
                </div>
            );
        }

        if (isReplying) {
            return (
                <div className="action-preview">
                    <div className="fw-bold text-primary">پاسخ به: {replyingToMessage.senderFullName}</div>

                    <div className="preview-content text-muted">{replyingToMessage.content}</div>

                    <Button variant="link" className="p-0 text-danger" onClick={handleCancelAction} title="لغو پاسخ">
                        ×
                    </Button>
                </div>
            );
        }

        if (isForwarding) {
            return (
                <div className="action-preview">
                    <div className="fw-bold text-primary">هدایت پیام</div>

                    <div className="preview-content text-muted">از: {forwardingMessage.senderFullName}</div>

                    <Button variant="link" className="p-0 text-danger" onClick={handleCancelAction} title="لغو هدایت">
                        ×
                    </Button>
                </div>
            );
        }

        return null;
    };

    const renderPendingAttachmentPreview = () => {
        if (!pendingAttachment) return null;
        const { url, name, type, mimeType } = pendingAttachment;
        const isImage = type === MessageType.Image || (mimeType && mimeType.startsWith('image/'));
        const isVideo = type === MessageType.Video || (mimeType && mimeType.startsWith('video/'));
        const isAudio = type === MessageType.Audio || (mimeType && mimeType.startsWith('audio/'));
        return (
            <div className="attachment-preview">
                <div className="thumb">
                    {isImage ? (
                        <img src={url} alt={name} />
                    ) : isVideo ? (
                        <div className="thumb-icon">🎬</div>
                    ) : isAudio ? (
                        <div className="thumb-icon">🎵</div>
                    ) : (
                        <div className="thumb-icon">📎</div>
                    )}
                </div>
                <div className="meta">
                    <div className="name" title={name}>{name}</div>
                    <div className="hint">کپشن اجباری است؛ لطفاً در کادر زیر وارد کنید</div>
                </div>
                <CloseButton className="remove" onClick={() => setPendingAttachment(null)} title="حذف فایل" />
            </div>
        );
    };

    const placeholder = pendingAttachment
        ? 'کپشن فایل را بنویسید...'
        : (isForwarding ? 'برای ارسال پیام هدایت شده، دکمه ارسال را بزنید' : 'پیام...');

    const canShowSendBtn = isEditing
        ? !!message.trim()
        : !!message.trim() && !isForwarding; // برای فایل نیز باید کپشن پر باشد

    return (
        <div className="message-input-container p-2">
            {renderActionPreview()}
            {renderPendingAttachmentPreview()}

            <div className="message-input-wrapper gap-1">
                {/* align-items-end so ارتفاع فقط رو به بالا حس شود */}
                <div className="message-input-actions d-flex flex-grow-1 gap-1 align-items-end">
                    <div style={{ display: voiceActive ? 'none' : 'block' }}>
                        {/* Emoji picker button */}
                        <OverlayTrigger trigger="click" placement="top" overlay={emojiPickerPopover} rootClose>
                            <Button variant="link" className="attachment-button p-1" disabled={isSending} title="افزودن ایموجی">
                                <BsEmojiSmile size={20} />
                            </Button>
                        </OverlayTrigger>
                    </div>

                    <div className="flex-grow-1" style={{ display: voiceActive ? 'none' : 'block' }}>
                        <Form.Control
                            ref={textareaRef}
                            as="textarea"
                            rows={1}
                            placeholder={placeholder}
                            value={message}
                            onChange={(e) => {
                                setMessage(e.target.value);
                                handleTyping();
                            }}
                            onKeyDown={handleKeyPress}
                            disabled={isForwarding}
                            className="message-input-field"
                            style={{
                                fontSize: '16px',
                                minHeight: '44px',
                                maxHeight: MAX_TEXTAREA_HEIGHT,
                                overflowY: 'hidden', // کنترل داینامیک در useEffect
                                resize: 'none',
                            }}
                        />
                    </div>

                    {/* Voice component - always mounted to keep state; expands when active */}
                    <div className={voiceActive ? 'flex-grow-1' : ''} style={{ minWidth: voiceActive ? 0 : 'auto' }}>
                        <VoiceRecorderComponent onVoiceRecorded={onVoiceRecorded} disabled={isSending} chatRoomId={roomId} onActiveChange={setVoiceActive} />
                    </div>

                    <div className="d-flex gap-1 align-items-center" style={{ display: voiceActive ? 'none' : 'flex' }}>
                        <FileUploadComponent onFileUploaded={onFileUploaded} disabled={isSending || !!pendingAttachment} chatRoomId={roomId} />

                        {isEditing ? (
                            <Button
                                variant="link"
                                onMouseDown={(e) => e.preventDefault()}
                                onPointerDown={(e) => e.preventDefault()}
                                onClick={handleSubmit}
                                disabled={!message.trim() || isSending}
                                className="send-button p-1"
                                title="ثبت ویرایش"
                            >
                                {isSending ? <Spinner size="sm" /> : <IoCheckmark size={20} color="#198754" />}
                            </Button>
                        ) : canShowSendBtn ? (
                            <Button
                                variant="link"
                                onMouseDown={(e) => e.preventDefault()}
                                onPointerDown={(e) => e.preventDefault()}
                                onClick={handleSendMessage}
                                disabled={!message.trim() || isSending}
                                className="send-button p-1"
                                title={pendingAttachment ? 'ارسال فایل با کپشن' : 'ارسال پیام'}
                            >
                                {isSending ? <Spinner size="sm" /> : <IoSend size={20} className="icon_flip" />}
                            </Button>
                        ) : null}
                    </div>
                </div>
            </div>
        </div>
    );
};

export default MessageInput;
