# راهنمای عملیاتی سیستم چت و پشتیبانی (Runbook)

> این سند راهنمای کامل برای تیم on-call جهت شناسایی، عیب‌یابی و رفع مشکلات سیستم است.

## 📞 اطلاعات تماس اضطراری

### تیم فنی
- **تیم Backend**: [شماره تماس]
- **تیم Database**: [شماره تماس]
- **تیم DevOps**: [شماره تماس]
- **مدیر فنی**: [شماره تماس]

### سرویس‌های خارجی
- **پشتیبانی Azure**: [لینک پورتال]
- **پشتیبانی کاوه نگار (SMS)**: [لینک پنل]
- **پشتیبانی Firebase (FCM)**: [لینک کنسول]

---

## 🚨 سناریوهای رایج و رفع مشکل

### 1️⃣ سرویس در دسترس نیست (Service Down)

#### علائم
- وب‌سایت لود نمی‌شود یا خطای 503 می‌دهد
- Health check endpoint پاسخ نمی‌دهد
- Alert "Service Unavailable" فعال شده

#### تشخیص
```bash
# بررسی وضعیت سرویس
curl -I https://[your-domain]/health

# بررسی لاگ‌های اخیر
az webapp log tail --name [app-name] --resource-group [rg-name]

# بررسی استفاده از منابع
az monitor metrics list --resource [resource-id] --metric "CpuPercentage,MemoryPercentage"
```

#### رفع مشکل

**مرحله 1: بررسی وضعیت App Service**
```bash
# بررسی وضعیت app
az webapp show --name [app-name] --resource-group [rg-name] --query "state"

# اگر stopped است، start کنید
az webapp start --name [app-name] --resource-group [rg-name]
```

**مرحله 2: بررسی لاگ‌ها برای exception**
```bash
# دریافت لاگ‌های خطا
az webapp log download --name [app-name] --resource-group [rg-name] --log-file error.zip
```

**مرحله 3: Restart اپلیکیشن**
```bash
az webapp restart --name [app-name] --resource-group [rg-name]
```

**مرحله 4: اگر مشکل ادامه دارد، rollback به نسخه قبلی**
```bash
# لیست deployment‌ها
az webapp deployment list --name [app-name] --resource-group [rg-name]

# rollback به deployment قبلی
az webapp deployment source config-zip --name [app-name] --resource-group [rg-name] --src [previous-version.zip]
```

#### زمان تخمینی: 10-15 دقیقه
#### اولویت: 🔴 بحرانی (P0)

---

### 2️⃣ دیتابیس در دسترس نیست (Database Connection Issues)

#### علائم
- خطای "Cannot connect to database"
- Timeout در query‌ها
- Alert "Database Connection Pool Exhausted"

#### تشخیص
```bash
# بررسی وضعیت SQL Server
az sql server show --name [sql-server] --resource-group [rg-name] --query "state"

# بررسی connection pool
az monitor metrics list --resource [db-resource-id] --metric "connection_failed,connection_successful"

# تست connection از local
sqlcmd -S [server-name].database.windows.net -d [db-name] -U [username] -P [password] -Q "SELECT 1"
```

#### رفع مشکل

**مرحله 1: بررسی فایروال و دسترسی‌ها**
```bash
# بررسی firewall rules
az sql server firewall-rule list --server [sql-server] --resource-group [rg-name]

# اضافه کردن IP در صورت نیاز
az sql server firewall-rule create --server [sql-server] --resource-group [rg-name] --name AllowAppService --start-ip-address [app-ip] --end-ip-address [app-ip]
```

**مرحله 2: بررسی DTU و performance**
```bash
# بررسی DTU usage
az monitor metrics list --resource [db-resource-id] --metric "dtu_consumption_percent" --start-time 2024-01-01T00:00:00Z --end-time 2024-01-01T23:59:59Z

# اگر DTU بالاست، scale up
az sql db update --resource-group [rg-name] --server [sql-server] --name [db-name] --service-objective S2
```

**مرحله 3: بررسی long-running queries**
```sql
-- شناسایی query‌های طولانی
SELECT 
    r.session_id,
    r.start_time,
    r.status,
    r.command,
    r.wait_type,
    r.total_elapsed_time,
    t.text AS query_text
FROM sys.dm_exec_requests r
CROSS APPLY sys.dm_exec_sql_text(r.sql_handle) t
WHERE r.total_elapsed_time > 30000  -- بیش از 30 ثانیه
ORDER BY r.total_elapsed_time DESC;

-- kill کردن query در صورت نیاز
KILL [session_id];
```

**مرحله 4: Restart connection pool در application**
```bash
az webapp restart --name [app-name] --resource-group [rg-name]
```

#### زمان تخمینی: 15-20 دقیقه
#### اولویت: 🔴 بحرانی (P0)

---

### 3️⃣ پیام‌های چت ارسال نمی‌شوند (SignalR Issues)

#### علائم
- کاربران پیام‌های جدید را دریافت نمی‌کنند
- پیام "Connection lost" در client
- Alert "SignalR Connections High"

#### تشخیص
```bash
# بررسی تعداد connection‌های فعال
# در Application Insights
az monitor app-insights query --app [app-insights-name] --resource-group [rg-name] --analytics-query "
customMetrics
| where name == 'signalr_connections'
| summarize avg(value) by bin(timestamp, 5m)
| render timechart
"

# بررسی error rate در SignalR
az webapp log tail --name [app-name] --resource-group [rg-name] --filter "SignalR"
```

#### رفع مشکل

**مرحله 1: بررسی WebSocket و sticky session**
```bash
# فعال کردن Web Sockets
az webapp config set --name [app-name] --resource-group [rg-name] --web-sockets-enabled true

# فعال کردن ARR Affinity (sticky sessions)
az webapp update --name [app-name] --resource-group [rg-name] --client-affinity-enabled true
```

**مرحله 2: بررسی JWT token expiration**
- توکن‌های JWT باید valid باشند
- چک کنید که clock skew مشکلی ایجاد نکرده باشد

**مرحله 3: پاک کردن PresenceTracker cache**
```bash
# restart application برای پاک کردن in-memory cache
az webapp restart --name [app-name] --resource-group [rg-name]
```

**مرحله 4: Scale out اگر تعداد connection زیاد است**
```bash
# افزایش تعداد instance‌ها
az appservice plan update --name [plan-name] --resource-group [rg-name] --number-of-workers 3
```

#### زمان تخمینی: 10-15 دقیقه
#### اولویت: 🟡 بالا (P1)

---

### 4️⃣ نوتیفیکیشن‌ها ارسال نمی‌شوند (Push Notification Failures)

#### علائم
- کاربران پیام‌های push دریافت نمی‌کنند
- خطای "FCM authentication failed"
- Alert "Notification Failure Rate High"

#### تشخیص
```bash
# بررسی لاگ‌های FCM
az webapp log tail --name [app-name] --resource-group [rg-name] --filter "FcmNotification"

# بررسی authentication با Firebase
# در کنسول Firebase، بررسی کنید که service account فعال است
```

#### رفع مشکل

**مرحله 1: بررسی Firebase credentials**
```bash
# بررسی وجود فایل service account
az webapp config appsettings list --name [app-name] --resource-group [rg-name] | grep Firebase

# اگر مشکل دارد، مجدداً upload کنید
az webapp config appsettings set --name [app-name] --resource-group [rg-name] --settings Firebase__ServiceAccountPath="abrikChat.json"
```

**مرحله 2: بررسی rate limit Firebase**
- Firebase محدودیت 500,000 پیام در روز دارد
- در کنسول Firebase، استفاده روزانه را چک کنید

**مرحله 3: بررسی device token‌های invalid**
```sql
-- شناسایی token‌های منقضی شده
SELECT UserId, FcmToken, UpdatedAt
FROM KciUsers
WHERE FcmToken IS NOT NULL
  AND UpdatedAt < DATEADD(day, -30, GETDATE());

-- پاک کردن token‌های قدیمی
UPDATE KciUsers
SET FcmToken = NULL
WHERE FcmToken IS NOT NULL
  AND UpdatedAt < DATEADD(day, -90, GETDATE());
```

**مرحله 4: Retry با exponential backoff**
```bash
# restart background service
az webapp restart --name [app-name] --resource-group [rg-name]
```

#### زمان تخمینی: 15-20 دقیقه
#### اولویت: 🟡 متوسط (P2)

---

**آخرین به‌روزرسانی**: {تاریخ}  
**نسخه**: 1.0  
**مسئول نگهداری**: تیم DevOps/SRE
