import { useEffect, useState, useCallback } from 'react';
import { Modal, Button } from 'react-bootstrap';

// Simple desktop detection (Windows/Mac/Linux) and not mobile
function isDesktop() {
  const ua = navigator.userAgent || navigator.vendor || window.opera;
  const isMobile = /android|iphone|ipad|ipod|iemobile|blackberry|mobile/i.test(ua);
  // pointer:fine helps exclude TVs/tablets with coarse pointer
  const hasFinePointer = window.matchMedia && window.matchMedia('(pointer:fine)').matches;
  return !isMobile && hasFinePointer;
}

const LS_KEYS = {
  installed: 'pwa-installed',
  lastPromptAt: 'pwa-install-last-prompt-at'
};

function hasPromptedToday() {
  try {
    const v = localStorage.getItem(LS_KEYS.lastPromptAt);
    if (!v) return false;
    const last = new Date(v);
    if (Number.isNaN(last.getTime())) return false;
    const now = new Date();
    // Same calendar day check
    return (
      last.getFullYear() === now.getFullYear() &&
      last.getMonth() === now.getMonth() &&
      last.getDate() === now.getDate()
    );
  } catch {
    return false;
  }
}

export default function PWAInstallPrompt() {
  const [deferredPrompt, setDeferredPrompt] = useState(null);
  const [show, setShow] = useState(false);

  const markInstalled = useCallback(() => {
    try { localStorage.setItem(LS_KEYS.installed, 'true'); } catch { /* ignore */ }
    setShow(false);
    setDeferredPrompt(null);
  }, []);

  useEffect(() => {
    // Already installed? (standalone display mode or localStorage)
    const isStandalone = window.matchMedia && window.matchMedia('(display-mode: standalone)').matches;
    const alreadyInstalled = isStandalone || localStorage.getItem(LS_KEYS.installed) === 'true';
    if (alreadyInstalled) return;

    // Only on desktop
    if (!isDesktop()) return;

    const onBeforeInstallPrompt = (e) => {
      // Prevent Chrome from showing its own mini-infobar
      e.preventDefault();
      // Gate: only first time per day on this page
      if (hasPromptedToday()) return;
      setDeferredPrompt(e);
      setShow(true);
      try { localStorage.setItem(LS_KEYS.lastPromptAt, new Date().toISOString()); } catch { /* ignore */ }
    };

    const onAppInstalled = () => {
      markInstalled();
    };

    window.addEventListener('beforeinstallprompt', onBeforeInstallPrompt);
    window.addEventListener('appinstalled', onAppInstalled);

    return () => {
      window.removeEventListener('beforeinstallprompt', onBeforeInstallPrompt);
      window.removeEventListener('appinstalled', onAppInstalled);
    };
  }, [markInstalled]);

  const handleClose = useCallback(() => {
    setShow(false);
    setDeferredPrompt(null);
  }, []);

  const handleInstall = useCallback(async () => {
    if (!deferredPrompt) return;
    try {
      deferredPrompt.prompt();
      const { outcome } = await deferredPrompt.userChoice;
      if (outcome === 'accepted') {
        markInstalled();
      } else {
        // User dismissed the browser prompt
        handleClose();
      }
    } catch {
      handleClose();
    }
  }, [deferredPrompt, handleClose, markInstalled]);

  if (!show) return null;

  return (
    <Modal show={show} onHide={handleClose} centered>
      <Modal.Header closeButton>
        <Modal.Title>نصب نسخه وب‌اپ</Modal.Title>
      </Modal.Header>
      <Modal.Body>
        <p className="mb-0">مایلید نسخه قابل نصب (PWA) ابریک‌چت را روی دسکتاپ خود نصب کنید؟</p>
        <small className="text-muted">نصب سریع، دسترسی راحت‌تر و تجربه بهتر.</small>
      </Modal.Body>
      <Modal.Footer>
        <Button variant="secondary" onClick={handleClose}>بعداً</Button>
        <Button variant="primary" onClick={handleInstall}>نصب</Button>
      </Modal.Footer>
    </Modal>
  );
}
