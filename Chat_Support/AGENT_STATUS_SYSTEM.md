# سیستم مدیریت هوشمند وضعیت پشتیبانان

## خلاصه

این سیستم به صورت خودکار و هوشمند وضعیت پشتیبانان را مدیریت می‌کند و همچنین به پشتیبانان اجازه می‌دهد که به صورت دستی وضعیت خود را تنظیم کنند.

## معماری سیستم

### 1. فیلدهای جدید در `SupportAgent`

```csharp
// زمان تنظیم دستی وضعیت
public DateTime? ManualStatusSetAt { get; set; }

// زمان انقضای وضعیت دستی (4 ساعت بعد از تنظیم)
public DateTime? ManualStatusExpiry { get; set; }

// وضعیت تشخیص داده شده توسط سیستم
public AgentStatus? AutoDetectedStatus { get; set; }
```

### 2. تشخیص خودکار وضعیت

سیستم بر اساس **زمان آخرین فعالیت** و **بار کاری** وضعیت را تشخیص می‌دهد:

| آستانه | وضعیت |
|--------|-------|
| فعالیت < 5 دقیقه پیش | `Available` (اگر ظرفیت دارد) یا `Busy` |
| فعالیت بین 5-15 دقیقه | `Away` |
| فعالیت > 15 دقیقه | `Offline` |

### 3. تنظیم دستی با TTL

زمانی که پشتیبان وضعیت خود را **دستی** تنظیم می‌کند:
- وضعیت برای **4 ساعت** معتبر است
- بعد از انقضا، سیستم به **تشخیص خودکار** برمی‌گردد
- در UI نمایش داده می‌شود که وضعیت "دستی" است و چند دقیقه باقیمانده

### 4. Background Service

`AgentStatusMonitorService` هر **2 دقیقه** یکبار:
- وضعیت‌های دستی منقضی شده را به خودکار برمی‌گرداند
- وضعیت خودکار تمام پشتیبانان را به‌روزرسانی می‌کند

### 5. ثبت فعالیت

فعالیت پشتیبان در موارد زیر ثبت می‌شود:
- اتصال به SignalR (`OnConnectedAsync`)
- خواندن پیام (`MarkMessageAsRead`)
- ارسال پیام (از طریق commands)

## نحوه استفاده

### Backend

#### تنظیم دستی وضعیت (توسط پشتیبان)
```http
POST /api/support/agent/status
Content-Type: application/json

{
  "status": 1  // 0=Available, 1=Busy, 2=Away, 3=Offline
}
```

**پاسخ:**
```json
{
  "status": "Busy",
  "message": "وضعیت شما به صورت دستی تنظیم شد و تا 4 ساعت آینده معتبر خواهد بود.",
  "expiresAt": "2025-10-13T10:28:15Z"
}
```

#### دریافت اطلاعات کامل وضعیت
```http
GET /api/support/agent/status-info
```

**پاسخ:**
```json
{
  "currentStatus": "Busy",
  "isManuallySet": true,
  "expiresAt": "2025-10-13T10:28:15Z",
  "timeRemainingMinutes": 237.5,
  "autoDetectedStatus": "Available",
  "lastActivityAt": "2025-10-13T06:25:00Z"
}
```

### Frontend (AgentDashboard)

وضعیت به صورت خودکار نمایش داده می‌شود:

```jsx
// Badge نمایش نوع وضعیت
{statusInfo.isManuallySet && (
  <Badge bg="info">
    🕒 دستی: 3س 57د باقیمانده
  </Badge>
)}

{!statusInfo.isManuallySet && (
  <Badge bg="secondary">
    ⚙️ خودکار
  </Badge>
)}
```

## سناریوهای مختلف

### سناریو 1: پشتیبان آنلاین و فعال
- فعالیت: کمتر از 5 دقیقه پیش
- چت‌های فعال: کمتر از حداکثر ظرفیت
- **وضعیت**: `Available` ✅

### سناریو 2: پشتیبان پر مشغله
- فعالیت: کمتر از 5 دقیقه پیش
- چت‌های فعال: برابر یا بیشتر از حداکثر ظرفیت
- **وضعیت**: `Busy` 🔴

### سناریو 3: پشتیبان دور از سیستم
- فعالیت: بین 5 تا 15 دقیقه پیش
- **وضعیت**: `Away` 🟡

### سناریو 4: پشتیبان آفلاین
- فعالیت: بیش از 15 دقیقه پیش
- **وضعیت**: `Offline` ⚫
- چت‌های فعال به پشتیبان دیگری منتقل می‌شوند

### سناریو 5: تنظیم دستی موقت
1. پشتیبان وضعیت را به "مشغول" تغییر می‌دهد
2. سیستم: "وضعیت دستی برای 4 ساعت ثبت شد"
3. UI نمایش می‌دهد: "🕒 دستی: 3س 59د باقیمانده"
4. بعد از 4 ساعت: خودکار به `Available` یا `Away` برمی‌گردد

## مزایای سیستم

✅ **تشخیص هوشمند**: بدون نیاز به تنظیم دستی مداوم
✅ **انعطاف‌پذیری**: پشتیبان می‌تواند موقتاً وضعیت را تنظیم کند
✅ **شفافیت**: نمایش واضح نوع وضعیت (دستی/خودکار)
✅ **بهینه‌سازی**: جلوگیری از انتساب تیکت به پشتیبان غیرفعال
✅ **مقیاس‌پذیری**: Background Service مستقل و کارآمد

## پیکربندی

### تنظیمات TTL
در `AgentStatusManager.cs`:
```csharp
private static readonly TimeSpan ManualStatusTTL = TimeSpan.FromHours(4);
```

### تنظیمات آستانه‌های تشخیص
```csharp
private static readonly TimeSpan AvailableThreshold = TimeSpan.FromMinutes(5);
private static readonly TimeSpan AwayThreshold = TimeSpan.FromMinutes(15);
```

### فاصله بررسی Background Service
در `AgentStatusMonitorService.cs`:
```csharp
private static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(2);
```

## Migration

فیلدهای جدید به جدول `Support_Agents` اضافه شده‌اند:
- `ManualStatusSetAt` (datetime2, nullable)
- `ManualStatusExpiry` (datetime2, nullable)
- `AutoDetectedStatus` (int, nullable)

```bash
# اعمال migration
cd src/Infrastructure
dotnet ef database update --startup-project ../Web
```

## تست

### تست دستی:
1. وارد پنل پشتیبان شوید
2. وضعیت را به "مشغول" تغییر دهید
3. بررسی کنید که Badge "دستی" نمایش داده شود
4. منتظر بمانید تا 4 ساعت بگذرد (یا TTL را کوتاه‌تر کنید)
5. تایید کنید که به وضعیت "خودکار" برگشته است

### لاگ‌های مفید:
```
🔄 Agent Status Monitor Service started
🔍 Checking agent statuses...
✅ Agent statuses updated successfully
```

## نکات امنیتی

⚠️ **مهم**: فقط پشتیبان خود می‌تواند وضعیت خودش را تغییر دهد
- Endpoint ها با `[Authorize("Agent")]` محافظت شده‌اند
- `agentId` از JWT token استخراج می‌شود

## خطایابی

### چک کردن وضعیت فعلی یک پشتیبان:
```sql
SELECT 
    UserId,
    AgentStatus,
    ManualStatusSetAt,
    ManualStatusExpiry,
    AutoDetectedStatus,
    LastActivityAt
FROM Support_Agents
WHERE UserId = @userId;
```

### بررسی Background Service:
لاگ‌های application را چک کنید:
```
grep "Agent Status Monitor" application.log
```

## تغییرات آینده (پیشنهادی)

- [ ] اضافه کردن تنظیمات شخصی‌سازی TTL برای هر پشتیبان
- [ ] نوتیفیکیشن قبل از انقضای وضعیت دستی (5 دقیقه قبل)
- [ ] Dashboard مدیریت با آمار وضعیت‌های تاریخی
- [ ] ثبت تاریخچه تغییرات وضعیت در جدول جداگانه
