import React, { useEffect, useRef, useState } from 'react';
import { Form, Button, Container, Row, Col, Card, Spinner, Alert } from 'react-bootstrap';
import apiClient from "../../api/apiClient";
import { useAuth } from '../../hooks/useAuth';
import { addOrUpdateAccount } from "../../Utils/accounts";
function LoginPage() {
    const [mobile, setMobile] = useState('');
    const [code, setCode] = useState('');
    const [submitting, setSubmitting] = useState(false);
    const [error, setError] = useState('');
    const [isOtpSent, setIsOtpSent] = useState(false);
    const [showStorageAlert, setShowStorageAlert] = useState(true);
    const [storageItems, setStorageItems] = useState([]);

    // Helper: read localStorage into display-friendly items
    const reloadStorage = () => {
        try {
            const keys = Object.keys(localStorage || {}).sort();
            const items = keys.map((k) => {
                const raw = localStorage.getItem(k);
                let display = raw;
                try {
                    const parsed = JSON.parse(raw);
                    display = JSON.stringify(parsed, null, 2);
                } catch {
                    // leave as raw string
                }
                return { key: k, value: display };
            });
            setStorageItems(items);
        } catch {
            setStorageItems([]);
        }
    };

    // Collect localStorage and show alert AFTER full page load
    useEffect(() => {
        const showAlertWithStorage = () => {
            reloadStorage();
            setShowStorageAlert(true);
        };

        if (document.readyState === 'complete') {
            showAlertWithStorage();
            return;
        }
        window.addEventListener('load', showAlertWithStorage, { once: true });
        return () => window.removeEventListener('load', showAlertWithStorage);
    }, []);

    // Generate or get a stable deviceId to bind this browser/device
    const getOrCreateDeviceId = () => {
        let id = localStorage.getItem('device_id');
        if (!id) {
            try {
                const arr = new Uint8Array(16);
                window.crypto.getRandomValues(arr);
                id = Array.from(arr).map(b => b.toString(16).padStart(2, '0')).join('');
            } catch {
                id = Math.random().toString(36).slice(2) + Date.now().toString(36);
            }
            localStorage.setItem('device_id', id);
        }
        return id;
    };

    // Extract and normalize returnUrl from query string once, and keep it stable
    const returnUrlRef = useRef(null);
    const addModeRef = useRef(null);
    if (returnUrlRef.current === null || addModeRef.current === null) {
        const params = new URLSearchParams(window.location.search);
        const raw = params.get('returnUrl') || params.get('ReturnUrl');
        let normalized = null;
        if (raw) {
            try {
                if (raw.startsWith('/')) {
                    normalized = raw; // safe relative path
                } else if (/^https?:\/\//i.test(raw)) {
                    const u = new URL(raw);
                    normalized = `${u.pathname}${u.search}${u.hash}`; // keep path/query/hash only
                }
            } catch {
                normalized = null;
            }
        }
        returnUrlRef.current = normalized;
        addModeRef.current = params.get('add') === '1' || params.get('Add') === '1';
    }
    const { isAuthenticated, isLoading } = useAuth();

    // If already authenticated and NOT in add mode, redirect
    useEffect(() => {
        if (!isLoading && isAuthenticated && !addModeRef.current) {
            window.location.replace(returnUrlRef.current || '/chat');
        }
    }, [isAuthenticated, isLoading]);

    const handleRequestOtp = async (e) => {
        e.preventDefault();
        setSubmitting(true);
        setError('');
        try {
            const body = {
                userName: mobile,
                deviceInfo: {
                    deviceId: getOrCreateDeviceId(),
                    ip: ''
                },
                verifyCode: 0
            };
            const res = await apiClient.post("/AbrikChatAccount/AbrikChatLogin", body);
            if (res?.data && (res.data.otpSent || res.data.OtpSent)) {
                setIsOtpSent(true);
            } else {
                // In case backend directly returns tokens (now may be PascalCase)
                const data = res?.data || {};
                const accessToken = data.accessToken || data.AccessToken;
                const refreshToken = data.refreshToken || data.RefreshToken;
                if (accessToken && refreshToken) {
                    localStorage.setItem('token', accessToken);
                    localStorage.setItem('refreshToken', refreshToken);
                    try { addOrUpdateAccount(accessToken, refreshToken); } catch (e) { console.warn('account save failed', e); }
                    const safeUrl = returnUrlRef.current && returnUrlRef.current.startsWith('/')
                        ? returnUrlRef.current
                        : '/chat';
                    window.location.replace(safeUrl);
                    return;
                }
                setIsOtpSent(true);
            }
        } catch (err) {
            setError(err?.response?.data ?? 'شماره موبایل نامعتبر است یا درخواست شما بیش از حد مجاز بوده است.');
        } finally {
            setSubmitting(false);
        }
    };

    const handleVerifyOtp = async (e) => {
        e.preventDefault();
        setSubmitting(true);
        setError('');
        try {
            const verifyCode = parseInt(code, 10);
            const body = {
                userName: mobile,
                deviceInfo: {
                    deviceId: getOrCreateDeviceId(),
                    ip: ''
                },
                verifyCode: isNaN(verifyCode) ? 0 : verifyCode
            };
            const response = await apiClient.post("/AbrikChatAccount/AbrikChatLogin", body);
            const data = response?.data || {};
            const accessToken = data.accessToken || data.AccessToken;
            const refreshToken = data.refreshToken || data.RefreshToken;
            if (!accessToken || !refreshToken) {
                throw new Error('invalid tokens');
            }
            // ذخیره توکن‌ها
            localStorage.setItem('token', accessToken);
            localStorage.setItem('refreshToken', refreshToken);
            try { addOrUpdateAccount(accessToken, refreshToken); } catch (e) { console.warn('account save failed', e); }

            // هدایت کاربر: اگر returnUrl داریم به همان برگرد، وگرنه به صفحه چت
            const safeUrl = returnUrlRef.current && returnUrlRef.current.startsWith('/')
                ? returnUrlRef.current
                : '/chat';
            window.location.replace(safeUrl);

        } catch (err) {
            setError(err?.response?.data ?? 'کد وارد شده صحیح نیست یا منقضی شده است.');
        } finally {
            setSubmitting(false);
        }
    };

    return (
        <Container className="mt-5">
            <Row className="justify-content-md-center">
                <Col md={12}>
                    <Card>
                        <Card.Body>
                            <Card.Title className="text-center">{addModeRef.current ? 'افزودن حساب جدید' : 'ورود به سیستم'}</Card.Title>
                            {error && <Alert variant="danger">{error}</Alert>}
                            {showStorageAlert && storageItems.length > 0 && (
                                <Alert
                                    variant="info"
                                    dismissible
                                    onClose={() => setShowStorageAlert(false)}
                                    className="mb-3"
                                    style={{
                                        overflow: 'hidden',
                                    }}
                                >
                                    <div className="d-flex justify-content-between align-items-center mb-2">
                                        <strong>اطلاعات ذخیره‌شده در مرورگر</strong>
                                        <div className="d-flex align-items-center gap-2">
                                            <small className="text-muted">localStorage</small>
                                            <Button size="sm" variant="outline-secondary" onClick={reloadStorage}>
                                                بازخوانی
                                            </Button>
                                        </div>
                                    </div>
                                    <div
                                        style={{
                                            maxHeight: '40vh',
                                            overflowY: 'auto',
                                            WebkitOverflowScrolling: 'touch',
                                            border: '1px dashed rgba(0,0,0,0.1)',
                                            borderRadius: '6px',
                                            padding: '8px',
                                            background: '#fff',
                                        }}
                                    >
                                        {storageItems.map(({ key, value }) => (
                                            <div key={key} className="mb-2">
                                                <div className="text-muted" style={{ fontSize: '0.85rem' }}>{key}</div>
                                                <pre
                                                    style={{
                                                        margin: 0,
                                                        whiteSpace: 'pre-wrap',
                                                        wordBreak: 'break-word',
                                                        fontSize: '0.85rem',
                                                    }}
                                                >
                                                    {value}
                                                </pre>
                                            </div>
                                        ))}
                                    </div>
                                </Alert>
                            )}
                            
                            {!isOtpSent ? (
                                <Form onSubmit={handleRequestOtp}>
                                    <Form.Group className="mb-3" controlId="formMobile">
                                        <Form.Label>شماره موبایل</Form.Label>
                                        <Form.Control 
                                            type="tel" 
                                            placeholder="09123456789"
                                            value={mobile}
                                            onChange={(e) => setMobile(e.target.value)}
                                            required
                                        />
                                    </Form.Group>
                                    <Button variant="primary" type="submit" disabled={submitting} className="w-100">
                                        {submitting ? <Spinner as="span" animation="border" size="sm" /> : (addModeRef.current ? 'ارسال کد تایید و افزودن' : 'ارسال کد تایید')}
                                    </Button>
                                </Form>
                            ) : (
                                <Form onSubmit={handleVerifyOtp}>
                                    <p className="text-center">کد تایید به شماره {mobile} ارسال شد.</p>
                                    <Form.Group className="mb-3" controlId="formCode">
                                        <Form.Label>کد تایید</Form.Label>
                                        <Form.Control 
                                            type="text" 
                                            placeholder="کد ۴ رقمی را وارد کنید"
                                            value={code}
                                            maxLength={4}
                                            inputMode="numeric"
                                            pattern="\d{4}"
                                            onChange={(e) => setCode(e.target.value.replace(/[^0-9]/g, '').slice(0,4))}
                                            required
                                        />
                                    </Form.Group>
                                    <Button variant="success" type="submit" disabled={submitting} className="w-100">
                                        {submitting ? <Spinner as="span" animation="border" size="sm" /> : (addModeRef.current ? 'افزودن حساب' : 'ورود')}
                                    </Button>
                                    <Button variant="link" onClick={() => setIsOtpSent(false)} className="mt-2">تغییر شماره موبایل</Button>
                                </Form>
                            )}
                        </Card.Body>
                    </Card>
                </Col>
            </Row>
        </Container>
    );
}

export default LoginPage;