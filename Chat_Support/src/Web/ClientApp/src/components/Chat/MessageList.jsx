import React, { useRef, useEffect, useCallback, useLayoutEffect } from 'react';
import {Spinner, Button} from 'react-bootstrap';
import MessageItem from './MessageItem';
import './Chat.css';

const MessageList = ({messages, isLoading, hasMoreMessages, onLoadMoreMessages, isGroupChat = false, roomId}) => {
  const listRef = useRef(null);
  const hasInitializedRef = useRef(false);
  const lastMessageIdRef = useRef(null);
  // track if user scrolled up (not used for now) - removed state to satisfy linter

  const scrollToBottom = useCallback((smooth = false) => {
    if (listRef.current) {
      // استفاده از requestAnimationFrame برای اطمینان از اجرای بعد از رندر کامل
      requestAnimationFrame(() => {
        if (listRef.current) {
          const scrollHeight = listRef.current.scrollHeight;
          listRef.current.scrollTo({
            top: scrollHeight,
            behavior: smooth ? 'smooth' : 'auto',
          });
        }
      });
    }
  }, []);

  // پس از مقداردهی اولیه، فقط هنگام اضافه شدن پیام جدید به پایین اسکرول کن
  useEffect(() => {
    const lastMsg = messages[messages.length - 1];
    const prevLastId = lastMessageIdRef.current;
    lastMessageIdRef.current = lastMsg?.id ?? null;

    // هنوز مقداردهی اولیه نشده؛ اجازه بده useLayoutEffect این را مدیریت کند
    if (!hasInitializedRef.current) return;

    // اگر اولین بار مقداردهی لیست است، هیچ اسکرولی نکن
    if (!prevLastId) return;

    // وقتی پیام جدیدی به انتها اضافه شده، همیشه به پایین اسکرول کن (با انیمیشن نرم)
    if (lastMsg && lastMsg.id !== prevLastId) {
      // استفاده از setTimeout برای اطمینان از رندر شدن کامل پیام
      setTimeout(() => {
        scrollToBottom(true);
      }, 100);
    }
  }, [messages, scrollToBottom]);

  // Ensure initial load for a room starts scrolled to bottom without a visual jump.
  // useLayoutEffect runs before the browser paints so the user won't see the list start at top
  // and then jump down.
  // Ensure we re-initialize when room changes
  useEffect(() => {
    hasInitializedRef.current = false;
  }, [roomId]);

  useLayoutEffect(() => {
    const el = listRef.current;
    if (!el) return;

    // reset initialization when room changes
    if (!hasInitializedRef.current && messages.length > 0) {
      // مخفی کردن موقت scroll bar برای جلوگیری از نمایش جهش
      const originalOverflow = el.style.overflow;
      el.style.overflow = 'hidden';
      
      // set scroll immediately (no smooth) before paint
      el.scrollTop = el.scrollHeight;
      hasInitializedRef.current = true;
      // initialize lastMessageId for later comparisons
      lastMessageIdRef.current = messages[messages.length - 1]?.id ?? null;
      
      // restore scroll bar after a short delay
      setTimeout(() => {
        if (el) {
          el.style.overflow = originalOverflow || 'auto';
        }
      }, 50);
    }
  }, [roomId, messages]);

  // Mobile keyboard handling: در هنگام باز/بسته شدن کیبورد از پرش معکوس جلوگیری کن
  useEffect(() => {
    const isMobileUA = /Mobi|Android|iPhone|iPad|iPod/i.test(navigator.userAgent);
    if (!isMobileUA) return;

    let prevVh = (window.visualViewport?.height ?? window.innerHeight);
    let wasAtBottomBefore = false;

    const onResize = () => {
      const el = listRef.current;
      if (!el) return;
      const curVh = (window.visualViewport?.height ?? window.innerHeight);
      const delta = curVh - prevVh;

      // کیبورد باز می‌شود
      if (delta < -80) {
        wasAtBottomBefore = (el.scrollHeight - (el.scrollTop + el.clientHeight)) < 50;
      }
      // کیبورد بسته می‌شود
      else if (delta > 80) {
        if (wasAtBottomBefore) {
          // اگر کاربر پایین بود، بعد از بسته شدن کیبورد دقیقا به پایین برو (بدون انیمیشن)
          requestAnimationFrame(() => {
            const node = listRef.current;
            if (node) node.scrollTop = node.scrollHeight;
          });
        }
        wasAtBottomBefore = false;
      }

      prevVh = curVh;
    };

    if (window.visualViewport) {
      window.visualViewport.addEventListener('resize', onResize);
      window.visualViewport.addEventListener('scroll', onResize);
    }
    window.addEventListener('resize', onResize);

    return () => {
      if (window.visualViewport) {
        window.visualViewport.removeEventListener('resize', onResize);
        window.visualViewport.removeEventListener('scroll', onResize);
      }
      window.removeEventListener('resize', onResize);
    };
  }, [roomId]);

  const handleScroll = useCallback(() => {
    const container = listRef.current;
    if (container) {
      // use a tolerant bottom check (allow a few pixels of difference)
  // if user scrolled near the top, load more (infinite scroll up)
      if (container.scrollTop < 50 && hasMoreMessages && !isLoading) {
        onLoadMoreMessages();
      }
    }
  }, [hasMoreMessages, isLoading, onLoadMoreMessages]);

  const formatDateHeader = (date) => {
    const messageDate = new Date(date);
    const today = new Date();
    const yesterday = new Date(today);
    yesterday.setDate(yesterday.getDate() - 1);

    if (messageDate.toDateString() === today.toDateString()) return 'امروز';
    if (messageDate.toDateString() === yesterday.toDateString()) return 'دیروز';
    return messageDate.toLocaleDateString('fa-IR', {year: 'numeric', month: 'long', day: 'numeric'});
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
            <div className="message-date-header">
              <span className="message-date-header-badge">{formatDateHeader(message.timestamp)}</span>
            </div>
          )}
          <MessageItem message={message} isGroupChat={isGroupChat} />
        </React.Fragment>
      );
    });
  };

  return (
    <div ref={listRef} className="message-list-container hide-scrollbar p-2" onScroll={handleScroll}>
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
              <p>اولین پیام را ارسال کنید!</p>
            </div>
          ) : (
            renderMessages()
          )}
        </>
      )}
    </div>
  );
};

export default MessageList;
