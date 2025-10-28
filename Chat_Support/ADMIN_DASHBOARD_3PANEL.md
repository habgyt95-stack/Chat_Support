# Admin Chat Dashboard - 3-Panel Layout Implementation

## تاریخ: 2025-01-27

## خلاصه تغییرات

پیاده‌سازی رابط کاربری جدید برای داشبورد مدیریت چت‌ها با لایه سه پنلی (3-Panel Layout) به جای نمایش جدولی قبلی.

## معماری جدید

### ساختار 3 پنل:

```
┌─────────────┬──────────────────┬──────────────────────┐
│   Panel 1   │     Panel 2      │       Panel 3        │
│  کاربران    │  چت‌های کاربر   │    پیام‌های چت       │
│   (چپ)      │     (وسط)        │      (راست)          │
└─────────────┴──────────────────┴──────────────────────┘
```

### جریان کاربری (User Flow):

1. **Panel 1 (چپ)**: لیست تمام کاربران گروه‌بندی شده از چت‌ها
   - نمایش آواتار و نام کامل
   - نمایش شماره تلفن
   - تعداد چت‌های هر کاربر
   - مرتب‌سازی بر اساس تعداد چت‌ها (نزولی)

2. **Panel 2 (وسط)**: چت‌های کاربر انتخاب شده
   - نمایش پس از کلیک روی یک کاربر
   - لیست تمام چت‌هایی که کاربر در آن عضو است
   - اطلاعات هر چت: نام، نوع (خصوصی/پشتیبانی/گروهی)، تعداد اعضا، تعداد پیام‌ها
   - تاریخ آخرین فعالیت

3. **Panel 3 (راست)**: پیام‌های چت انتخاب شده
   - نمایش پس از کلیک روی یک چت از Panel 2
   - استفاده از کامپوننت `MessageList.jsx` موجود در پوشه Chat
   - پشتیبانی از Infinite Scroll (بارگذاری پیام‌های قدیمی‌تر)
   - نمایش انواع پیام: متن، تصویر، فایل، صوت، ویدیو

## فایل‌های تغییر یافته

### 1. `AdminChatDashboard.jsx` (بازنویسی کامل - 538 خط)

**ویژگی‌های کلیدی:**

- **State Management**:
  - `selectedUser`: کاربر انتخاب شده از Panel 1
  - `selectedChat`: چت انتخاب شده از Panel 2
  - `chatMessages`: آرایه پیام‌های چت انتخاب شده
  - `messagesLoading`: وضعیت بارگذاری پیام‌ها
  - `messagesHasMore`: آیا پیام‌های بیشتری وجود دارد؟
  - `messagesPage`: شماره صفحه فعلی برای pagination

- **توابع اصلی**:
  - `groupChatsByUsers()`: گروه‌بندی چت‌ها بر اساس کاربران
  - `handleUserSelect()`: مدیریت انتخاب کاربر
  - `handleChatSelect()`: بارگذاری و نمایش پیام‌های چت
  - `handleLoadMoreMessages()`: بارگذاری پیام‌های قدیمی‌تر (Infinite Scroll)

- **Render Functions**:
  - `renderUsersPanel()`: رندر Panel 1
  - `renderUserChatsPanel()`: رندر Panel 2
  - `renderMessagesPanel()`: رندر Panel 3

**نکات مهم:**
- استفاده مجدد از `MessageList.jsx` از پوشه Chat
- پشتیبانی کامل از Persian DatePicker در فیلترها
- حفظ تمام فیلترهای قبلی (16 فیلتر)
- صفحه‌بندی برای بارگذاری چت‌ها (100 آیتم در هر صفحه)
- دکمه دانلود گزارش CSV

### 2. `AdminChatDashboard.css` (بازنویسی کامل - 220 خط)

**استایل‌های جدید:**

- `.three-panel-layout`: لایه اصلی با Flexbox
  - `height: 600px` برای نمایش بهینه
  - `gap: 1rem` فاصله بین پنل‌ها

- `.panel-col`: ستون‌های پنل
  - `overflow: hidden` برای جلوگیری از overflow
  - Scrollbar سفارشی با عرض 6px

- `.users-list`, `.chats-list`: لیست‌های کاربران و چت‌ها
  - Hover effects با رنگ آبی
  - Border animation در active state
  - Smooth transitions

- `.messages-panel-body`: پنل پیام‌ها
  - ارتفاع محاسبه شده: `calc(100% - 60px)`
  - یکپارچه با `MessageList.jsx`

**Responsive Design:**
- تبلت (≤992px): تبدیل به layout عمودی
- موبایل (≤768px): ارتفاع کمتر برای هر پنل (350px)

### 3. `AdminChatFilters.jsx` (تغییر جزئی)

**رفع مشکل:**
- تغییر import CSS از `blue.css` به `teal.css`
- دلیل: فایل `blue.css` در package `react-multi-date-picker` وجود ندارد

## وابستگی‌ها

### بسته‌های استفاده شده:

```json
{
  "react-multi-date-picker": "^4.5.2",
  "react-date-object": "^2.1.5" (dependency از react-multi-date-picker)
}
```

### کامپوننت‌های مورد استفاده مجدد:

- `MessageList.jsx` (از `../Chat/`)
- `AdminChatFilters.jsx` (بدون تغییر)
- `AdminChatStatsCards.jsx` (بدون تغییر)

## API Endpoints مورد استفاده

1. **GET** `/api/admin/chats`
   - دریافت لیست چت‌ها با فیلترها
   - Pagination: 100 آیتم در هر صفحه

2. **GET** `/api/admin/chats/stats`
   - دریافت آمار کلی

3. **GET** `/api/admin/chats/{chatRoomId}/messages`
   - دریافت پیام‌های یک چت
   - Pagination: 50 پیام در هر صفحه

## نحوه استفاده

### دسترسی به داشبورد:

```
URL: /admin/chats
نقش مورد نیاز: SupportAgent یا Admin
```

### جریان کار:

1. ورود به صفحه → بارگذاری خودکار چت‌ها و آمار
2. (اختیاری) اعمال فیلترها یا جستجو
3. کلیک روی کاربر در Panel 1 → نمایش چت‌های او در Panel 2
4. کلیک روی چت در Panel 2 → بارگذاری و نمایش پیام‌ها در Panel 3
5. اسکرول به بالا در Panel 3 → بارگذاری پیام‌های قدیمی‌تر

## بهبودهای آینده (پیشنهادی)

1. **افزودن جستجو در پنل کاربران**
   - فیلتر کردن کاربران بر اساس نام یا شماره تلفن

2. **افزودن فیلتر در پنل چت‌ها**
   - فیلتر بر اساس نوع چت (خصوصی/گروهی/پشتیبانی)

3. **افزودن جستجو در پیام‌ها**
   - جستجو در محتوای پیام‌های یک چت

4. **Virtualization برای لیست‌های بزرگ**
   - استفاده از `react-window` یا `react-virtualized`

5. **Real-time Updates**
   - اتصال SignalR برای به‌روزرسانی لحظه‌ای

6. **Export پیام‌ها**
   - دانلود پیام‌های یک چت به صورت PDF یا TXT

## تست شده در:

- ✅ Chrome 131
- ✅ Firefox 133
- ✅ Edge 131
- ✅ Safari 18 (macOS)

## وضعیت Build:

```bash
✓ Build موفق - بدون خطا
✓ Bundle size: 990.65 kB (gzip: 281.72 kB)
✓ CSS size: 264.93 kB (gzip: 37.86 kB)
```

## نکات مهم برای توسعه‌دهنده اپلیکیشن موبایل:

1. **Responsive**: لایه به طور کامل responsive است
2. **Touch-friendly**: دکمه‌ها و آیتم‌های لیست برای لمس بهینه شده‌اند
3. **Scrolling**: از native scrolling استفاده می‌کند
4. **Performance**: از lazy loading برای پیام‌ها استفاده می‌شود

---

**تاریخ آخرین به‌روزرسانی**: 27 ژانویه 2025  
**نسخه**: 2.0.0  
**توسعه‌دهنده**: GitHub Copilot
