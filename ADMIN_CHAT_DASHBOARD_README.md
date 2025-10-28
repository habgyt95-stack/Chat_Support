# داشبورد مدیریت چت‌ها - مستندات کامل

## خلاصه پروژه

یک داشبورد مدیریتی کامل برای مشاهده و مدیریت تمام چت‌های سیستم توسط مدیران و مدیران ناحیه‌ای.

## ویژگی‌های پیاده‌سازی شده

### Backend (ASP.NET Core + Clean Architecture)

#### 1. DTOs
- **`AdminChatRoomDto`**: اطلاعات کامل چت‌روم شامل:
  - اطلاعات پایه (شناسه، نام، توضیحات، نوع)
  - اطلاعات ناحیه و ایجادکننده
  - آمار (تعداد اعضا، تعداد پیام‌ها، آخرین پیام)
  - لیست اعضا با جزئیات کامل
  
- **`AdminChatMessageDto`**: اطلاعات کامل پیام شامل:
  - محتوا و متادیتا
  - اطلاعات فرستنده (نام، شماره، آواتار)
  - پیام پاسخ داده شده (Reply)
  - واکنش‌ها (Reactions)
  - وضعیت‌های خوانده شدن توسط کاربران
  
- **`AdminChatStatsDto`**: آمار کلی سیستم شامل:
  - تعداد کل چت‌ها (کل، فعال، گروهی، خصوصی، پشتیبانی)
  - تعداد پیام‌ها (کل، امروز)
  - میانگین پیام به ازای هر چت
  - میانگین اعضا در گروه‌ها
  - آمار به تفکیک ناحیه (برای مدیر کل)

#### 2. Queries

**`GetAllChatsForAdminQuery`**
- فیلترهای قدرتمند:
  - جستجوی متنی (نام چت، توضیحات، شماره موبایل اعضا، نام اعضا)
  - نوع چت (خصوصی، پشتیبانی، گروهی)
  - ناحیه
  - بازه زمانی ایجاد
  - وضعیت حذف شده
  - گروهی بودن
  - محدوده تعداد اعضا (حداقل/حداکثر)
  - محدوده تعداد پیام‌ها (حداقل/حداکثر)
  - بازه زمانی آخرین فعالیت
- مرتب‌سازی:
  - بر اساس: تاریخ ایجاد، نام، تعداد اعضا، تعداد پیام‌ها، آخرین فعالیت
  - صعودی/نزولی
- صفحه‌بندی کامل (Page Number, Page Size)
- Authorization:
  - مدیر کل: دسترسی به همه چت‌ها
  - مدیر ناحیه: فقط چت‌های ناحیه خودش
  - بررسی SupportAgent فعال بودن

**`GetChatMessagesForAdminQuery`**
- دریافت پیام‌های یک چت خاص
- فیلترها:
  - جستجو در محتوای پیام
  - فیلتر بر اساس فرستنده
  - بازه زمانی
- صفحه‌بندی
- شامل تمام اطلاعات پیام (Reply, Reactions, Read Statuses)

**`GetAdminChatStatsQuery`**
- محاسبه آمار کلی سیستم
- آمار چت‌های فعال (با پیام در 7 روز گذشته)
- آمار امروز (پیام‌ها و چت‌های جدید)
- آمار به تفکیک ناحیه (برای مدیر کل)

#### 3. API Endpoints

```
GET  /api/admin/chats              - لیست چت‌ها با فیلترها
GET  /api/admin/chats/stats        - آمار کلی
GET  /api/admin/chats/{id}/messages - پیام‌های یک چت
```

همه endpoint ها نیاز به احراز هویت دارند و بر اساس RegionId کاربر فیلتر می‌شوند.

#### 4. Exception Handling
- `NotFoundException`: چت پیدا نشد
- `ForbiddenAccessException`: کاربر دسترسی ندارد

### Frontend (React + Bootstrap)

#### 1. Components

**`AdminChatDashboard.jsx`** (کامپوننت اصلی)
- نمایش لیست چت‌ها در جدول responsive
- جستجوی زنده با debounce (500ms)
- فیلترهای پیشرفته (قابل جمع شدن)
- صفحه‌بندی کامل
- آمار کلی در cards
- دکمه خروجی CSV (placeholder)
- دکمه پاک کردن فیلترها
- مشاهده پیام‌های هر چت

**`AdminChatFilters.jsx`**
- فرم فیلترهای پیشرفته
- 12 فیلتر مختلف
- UI تمیز و user-friendly
- Responsive برای موبایل

**`AdminChatStatsCards.jsx`**
- 6 کارت آماری
- آیکون‌های رنگی
- انیمیشن fade-in با تاخیر
- Hover effects
- Responsive grid

**`ChatMessagesModal.jsx`**
- Modal برای نمایش پیام‌های یک چت
- جستجوی زنده در پیام‌ها
- نمایش Reply, Reactions, Read Statuses
- پشتیبانی از انواع پیام (متن، تصویر، فایل، صوت)
- بارگذاری صفحه‌بندی شده (Load More)
- Scrollbar سفارشی

#### 2. Services

**`adminChatApi.js`**
- سه متد اصلی:
  - `getAllChats(filters)`: دریافت لیست چت‌ها
  - `getChatStats()`: دریافت آمار
  - `getChatMessages(chatRoomId, filters)`: دریافت پیام‌ها
- مدیریت query parameters
- استفاده از apiClient مرکزی

#### 3. Styling

- **`AdminChatDashboard.css`**
  - Hover effects روی ردیف‌های جدول
  - انیمیشن slideDown برای فیلترها
  - Responsive برای موبایل
  - Print styles

- **`AdminChatStatsCards.css`**
  - انیمیشن fadeInUp با stagger effect
  - Hover effects
  - Responsive

- **`ChatMessagesModal.css`**
  - Border animation روی پیام‌ها
  - Scrollbar styling
  - Responsive

#### 4. Routing

```jsx
<Route path="/admin/chats" element={<ProtectedRoute><AdminChatDashboard /></ProtectedRoute>} />
```

## نحوه استفاده

### دسترسی

1. کاربر باید یک `SupportAgent` فعال باشد
2. مدیر ناحیه: فقط چت‌های `RegionId` خودش را می‌بیند
3. مدیر کل (`RegionId <= 0`): همه چت‌ها را می‌بیند

### مسیر دسترسی

```
https://yourapp.com/admin/chats
```

### استفاده از فیلترها

1. **جستجوی سریع**: از نوار جستجو بالا استفاده کنید
2. **فیلترهای پیشرفته**: روی دکمه "فیلترها" کلیک کنید
3. **مرتب‌سازی**: از dropdown مرتب‌سازی استفاده کنید
4. **پاک کردن**: دکمه ✕ قرمز همه فیلترها را پاک می‌کند

### مشاهده پیام‌ها

1. روی دکمه "مشاهده" کنار هر چت کلیک کنید
2. در modal باز شده، می‌توانید:
   - پیام‌ها را مشاهده کنید
   - در پیام‌ها جستجو کنید
   - اطلاعات کامل هر پیام را ببینید
   - پیام‌های بیشتر را بارگذاری کنید

## معماری و Best Practices

### Backend

✅ **Clean Architecture**: تمام لایه‌ها جدا شده‌اند
✅ **CQRS**: استفاده از Queries برای خواندن داده
✅ **Authorization**: بررسی دسترسی در Handler
✅ **DTOs**: جداسازی Entity از DTO
✅ **AutoMapper**: Mapping خودکار
✅ **Pagination**: صفحه‌بندی در سطح دیتابیس
✅ **Filtering**: فیلترهای قدرتمند با EF Core
✅ **Exception Handling**: استفاده از Exception های سفارشی

### Frontend

✅ **Component-Based**: کامپوننت‌های قابل استفاده مجدد
✅ **Separation of Concerns**: جداسازی UI از Logic
✅ **API Service Layer**: مدیریت مرکزی API calls
✅ **Debouncing**: جلوگیری از درخواست‌های زیاد
✅ **Loading States**: نمایش وضعیت بارگذاری
✅ **Error Handling**: مدیریت خطاها
✅ **Responsive Design**: پشتیبانی از همه دستگاه‌ها
✅ **Accessibility**: استفاده از semantic HTML
✅ **CSS Organization**: فایل‌های CSS جداگانه
✅ **Performance**: Lazy loading و pagination

## UI/UX Features

### Desktop
- جدول کامل با تمام اطلاعات
- Hover effects
- Sort indicators
- Badge های رنگی
- Tooltip ها
- Smooth animations

### Tablet
- جدول responsive
- فیلترهای تاشو
- Cards برای آمار

### Mobile
- جدول قابل اسکرول افقی
- فرم‌های stack شده
- دکمه‌های بزرگتر
- Font size کوچکتر

## Performance Optimizations

1. **Backend**:
   - Include مناسب در EF queries
   - AsNoTracking برای خواندن
   - Pagination در دیتابیس
   - محاسبه آمار بهینه

2. **Frontend**:
   - Debouncing برای search
   - Lazy loading برای پیام‌ها
   - Memoization در component ها
   - CSS animations بجای JS

## توسعه‌های آینده

- [ ] Export به CSV/Excel
- [ ] نمودارهای آماری (Charts)
- [ ] فیلتر پیشرفته بر اساس Region (dropdown)
- [ ] Bulk actions (حذف، آرشیو و...)
- [ ] Real-time updates با SignalR
- [ ] نقش‌های دسترسی پیچیده‌تر
- [ ] Audit log برای عملیات مدیران
- [ ] Advanced search با ElasticSearch

## نتیجه‌گیری

این داشبورد یک سیستم مدیریتی کامل، قدرتمند و کاربرپسند است که:
- از معماری تمیز پیروی می‌کند
- UI/UX حرفه‌ای دارد
- کاملاً Responsive است
- Performance بالایی دارد
- قابل توسعه است
- مستند است

همه چیز آماده استفاده و production-ready است! 🚀
