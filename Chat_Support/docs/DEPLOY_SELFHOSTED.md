# راهنمای استقرار سرویس‌های Self-Hosted

## گام 1: راه‌اندازی Gotify (Push Notification)

```bash
# راه‌اندازی با docker-compose
cd /path/to/Chat_Support
docker-compose -f docker-compose.selfhosted.yml up -d gotify

# بررسی وضعیت
docker-compose -f docker-compose.selfhosted.yml ps
docker logs chat-support-gotify

# دسترسی به پنل: http://localhost:8080
# کاربری: admin / admin123
```

**تنظیمات:**
1. ورود به Gotify: http://localhost:8080
2. ایجاد Application برای Chat_Support
3. کپی کردن App Token
4. ذخیره توکن در جدول `UserFcmTokenInfoMobileAbrikChat` برای هر کاربر

## گام 2: راه‌اندازی Redis (SignalR Backplane)

```bash
# راه‌اندازی Redis
docker-compose -f docker-compose.selfhosted.yml up -d redis

# تست اتصال
docker exec -it chat-support-redis redis-cli -a redis123 ping
# باید "PONG" برگرداند

# فعال‌سازی در appsettings.json:
# خط زیر را uncomment کنید:
"ConnectionStrings": {
  "Redis": "localhost:6379,password=redis123,ssl=false,abortConnect=false"
}

# Restart application
# اکنون SignalR از Redis backplane استفاده می‌کند
# می‌توانید چند instance از app را اجرا کنید
```

**ویژگی‌های فعال شده:**
- ✅ SignalR Backplane: پشتیبانی چند instance
- ✅ Redis Presence Tracker: tracking آنلاین کاربران در scale-out
- ✅ Auto-fallback: در صورت نبود Redis، in-memory استفاده می‌شود

## گام 3: راه‌اندازی Monitoring (اختیاری)

```bash
# راه‌اندازی Prometheus + Grafana + Loki
docker-compose -f docker-compose.selfhosted.yml up -d

# دسترسی:
# Grafana: http://localhost:3000 (admin/admin123)
# Prometheus: http://localhost:9090
```

## Rollback

```bash
# توقف سرویس‌ها
docker-compose -f docker-compose.selfhosted.yml down

# حذف data (در صورت نیاز)
docker-compose -f docker-compose.selfhosted.yml down -v
```

## Migration: FCM به Gotify

**بدون نیاز به migration دیتابیس** - از همان جدول `UserFcmTokenInfoMobileAbrikChat` استفاده می‌شود.

توکن‌های Gotify باید توسط کلاینت موبایل در همان جدول ذخیره شوند:
```sql
INSERT INTO UserFcmTokenInfoMobileAbrikChat (UserId, FcmToken, DeviceId, AddedDate)
VALUES (@UserId, @GotifyToken, @DeviceId, @UnixTimestamp);
```

## Verification

```bash
# تست Gotify
curl -X POST "http://localhost:8080/message?token=YOUR_APP_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"title":"تست","message":"پیام آزمایشی","priority":5}'

# تست Redis
docker exec -it chat-support-redis redis-cli -a redis123 SET test "OK"
docker exec -it chat-support-redis redis-cli -a redis123 GET test
```

## تنظیمات Production

1. **تغییر پسوردها** در `docker-compose.selfhosted.yml`
2. **SSL/TLS**: استفاده از nginx reverse proxy
3. **Backup**: Volume مسیرهای `/app/data` (Gotify) و `/data` (Redis)
4. **Auto-restart**: `restart: unless-stopped` فعال است

## هزینه و منابع

- **Gotify**: ~50MB RAM, ~10MB disk
- **Redis**: ~50MB RAM, ~100MB disk
- **Prometheus**: ~200MB RAM, ~1GB disk
- **Grafana**: ~100MB RAM, ~100MB disk
- **Total**: ~400MB RAM, ~1.2GB disk
