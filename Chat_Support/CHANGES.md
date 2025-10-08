# تغییرات اعمال شده در سیستم چت و پشتیبانی

## تاریخ: 2024

### خلاصه تغییرات

این سند شامل تمامی تغییرات و بهبودهای اعمال شده بر روی سیستم چت و پشتیبانی است.

---

## ✅ مشکلات چت که برطرف شدند

### 1. امکان ویرایش نام گروه و توضیحات ✔️

**مشکل**: امکان ویرایش نام گروه و اعضای آن وجود نداشت.

**راه حل**: 
- دستور `UpdateChatRoomCommand` جدید در بکند اضافه شد
- API endpoint برای به‌روزرسانی گروه (`PUT /api/chat/rooms/{roomId}`)
- تب "ویرایش گروه" در `GroupManagementModal` اضافه شد
- مالک و ادمین‌های گروه می‌توانند نام و توضیحات گروه را ویرایش کنند

**فایل‌های تغییر یافته**:
- `src/Application/Chats/Commands/UpdateChatRoomCommand.cs` (جدید)
- `src/Web/Endpoints/Chat.cs`
- `src/Web/ClientApp/src/services/chatApi.js`
- `src/Web/ClientApp/src/components/Chat/GroupManagementModal.jsx`

---

### 2. اصلاح نمایش نام کاربران در گروه ✔️

**مشکل**: برای تمام کاربران در گروه نام "مهمان" نمایش داده می‌شد یا یک فاصله اضافی در ابتدای نام وجود داشت.

**راه حل**: 
- فاصله اضافی در ابتدای رشته نام کاربر در تمام موارد استفاده در `ChatHub.cs` حذف شد
- از `$"{user.FirstName} {user.LastName}"` به جای `$" {user.FirstName} {user.LastName}"` تغییر یافت

**فایل‌های تغییر یافته**:
- `src/Infrastructure/Hubs/ChatHub.cs`

---

### 3. اصلاح شمارنده پیام‌های خوانده نشده ✔️

**مشکل**: وقتی کاربر آفلاین بود و پیام دریافت می‌کرد، باید دوبار وارد چت می‌شد تا شمارنده صفر شود.

**راه حل**: 
- منطق `MARK_ALL_MESSAGES_AS_READ_IN_ROOM` در `ChatContext` بهبود یافت
- حالا حتی اگر پیام‌ها بارگذاری نشده باشند، شمارنده unread به صفر تنظیم می‌شود
- وابستگی به بارگذاری پیام‌ها برای به‌روزرسانی شمارنده حذف شد

**فایل‌های تغییر یافته**:
- `src/Web/ClientApp/src/contexts/ChatContext.jsx`

---

### 8. توضیح (Caption) اجباری برای فایل‌های آپلود شده ✔️

**مشکل**: امکان آپلود فایل بدون توضیح وجود داشت.

**راه حل**: 
- قبل از ارسال فایل، از کاربر درخواست توضیح اجباری می‌شود
- اگر کاربر توضیح وارد نکند یا کنسل کند، فایل ارسال نمی‌شود
- پیام خطا به فارسی نمایش داده می‌شود

**فایل‌های تغییر یافته**:
- `src/Web/ClientApp/src/components/Chat/MessageInput.jsx`

---

## ✅ بهبودهای سیستم پشتیبانی

### 9-11. سیستم مدیریت پشتیبان و سیاست‌های جدید ✔️

**مشکلات**: 
- سیاست تخصیص و انتقال تیکت‌ها نادرست بود
- وقتی پشتیبان آفلاین می‌شد، چت‌ها رها می‌شدند
- امکان مدیریت پشتیبان‌ها وجود نداشت

**راه‌حل‌های پیاده‌سازی شده**:

#### الف) سیستم مدیریت پشتیبان (Backend)

**دستورات جدید**:
- `CreateSupportAgentCommand`: افزودن پشتیبان جدید
- `UpdateSupportAgentCommand`: ویرایش تنظیمات پشتیبان (فعال/غیرفعال، حداکثر چت همزمان)
- `DeleteSupportAgentCommand`: حذف پشتیبان (با انتقال چت‌های فعال)
- `GetAllAgentsQuery`: دریافت لیست تمام پشتیبان‌ها

**API Endpoints جدید**:
- `GET /api/support/agents` - دریافت لیست پشتیبان‌ها
- `POST /api/support/agents` - افزودن پشتیبان جدید
- `PUT /api/support/agents/{agentId}` - ویرایش پشتیبان
- `DELETE /api/support/agents/{agentId}` - حذف پشتیبان

#### ب) بهبود سیستم تخصیص و انتقال

**تغییرات در `AgentAssignmentService`**:

1. **انتقال هوشمند تیکت‌ها هنگام آفلاین شدن پشتیبان**:
   - ابتدا سعی می‌شود پشتیبان دیگری در همان منطقه پیدا شود
   - اگر پشتیبان موجود نباشد، پیام سیستمی به کاربر نمایش داده می‌شود
   - تیکت به حالت `Open` برمی‌گردد تا پشتیبان بعدی آن را بردارد

2. **تخصیص خودکار تیکت‌های در انتظار**:
   - وقتی پشتیبان آنلاین می‌شود، تیکت‌های بدون پاسخ خودکار به او تخصیص داده می‌شود
   - در نظر گرفتن منطقه و حداکثر ظرفیت پشتیبان

3. **پیام‌های سیستمی به فارسی**:
   - "چت شما به پشتیبان دیگری منتقل شد. لطفاً منتظر بمانید."
   - "در حال حاضر پشتیبانی آنلاین در دسترس نیست. به محض ورود پشتیبان، به شما پاسخ داده خواهد شد."
   - "پشتیبان به چت متصل شد."

4. **سناریوهای پوشش داده شده**:
   - پشتیبان در حین پاسخگویی آفلاین می‌شود (شیفت تمام می‌شود)
   - پشتیبان به حداکثر ظرفیت می‌رسد
   - هیچ پشتیبانی آنلاین نیست
   - پشتیبان جدید آنلاین می‌شود و تیکت‌های در انتظار وجود دارد

**فایل‌های تغییر یافته**:
- `src/Application/Support/Commands/CreateSupportAgentCommand.cs` (جدید)
- `src/Application/Support/Commands/UpdateSupportAgentCommand.cs` (جدید)
- `src/Application/Support/Commands/DeleteSupportAgentCommand.cs` (جدید)
- `src/Application/Support/Queries/GetAllAgentsQuery.cs` (جدید)
- `src/Infrastructure/Service/AgentAssignmentService.cs`
- `src/Web/Endpoints/Support.cs`

---

## ⏳ موارد باقی‌مانده (نیاز به پیاده‌سازی بیشتر)

### 4. سایلنت کردن نوتیفیکیشن برای هر گروه ✔️

**پیاده‌سازی شده**:
- ✅ فیلد `IsMuted` در جدول `ChatRoomMember` از قبل موجود بود
- ✅ API endpoint برای toggle کردن mute (`PUT /api/chat/rooms/{roomId}/mute`)
- ✅ Command جدید: `ToggleChatRoomMuteCommand`
- ✅ افزودن فیلد `IsMuted` به `ChatRoomDto` و بازگرداندن آن در `GetChatRoomsQuery`
- ✅ UI در frontend برای فعال/غیرفعال کردن نوتیف:
  - دکمه toggle mute در header چت (آیکن زنگ/زنگ خاموش)
  - نمایش آیکن `BellSlash` در لیست چت‌ها برای گروه‌های mute شده
- ✅ فیلتر کردن نوتیفیکیشن‌ها بر اساس وضعیت mute در سمت سرور

**فایل‌های اضافه/تغییر یافته**:
- `src/Application/Chats/Commands/ToggleChatRoomMuteCommand.cs` (جدید)
- `src/Web/Endpoints/Chat.cs` (اضافه شدن endpoint)
- `src/Application/Chats/DTOs/ChatRoomDto.cs` (افزودن فیلد IsMuted)
- `src/Application/Chats/Queries/GetChatRoomsQuery.cs` (تنظیم IsMuted)
- `src/Web/ClientApp/src/services/chatApi.js` (اضافه شدن toggleChatRoomMute)
- `src/Web/ClientApp/src/components/Chat/Chat.jsx` (دکمه toggle و handler)
- `src/Web/ClientApp/src/components/Chat/ChatRoomList.jsx` (نمایش آیکن mute)

### 5. نوتیفیکیشن برای کاربران در دو منطقه ✔️

**پیاده‌سازی شده**:
- ✅ اصلاح منطق ارسال نوتیفیکیشن در `NewMessageNotifier`
- ✅ فیلتر کردن کاربرانی که چت را mute کرده‌اند (IsMuted = true)
- ✅ نوتیفیکیشن برای کاربران با IsMuted = false ارسال می‌شود

**فایل‌های تغییر یافته**:
- `src/Infrastructure/Service/NewMessageNotifier.cs` (اضافه شدن شرط !m.IsMuted)

### 6. مشاهده خوانده شدن پیام در گروه (Read Receipts) ✔️

**پیاده‌سازی شده**:
- ✅ Query جدید برای دریافت لیست کاربرانی که پیام را خوانده‌اند (`GetMessageReadReceiptsQuery`)
- ✅ API endpoint جدید (`GET /api/chat/messages/{messageId}/read-receipts`)
- ✅ کامپوننت `ReadReceiptsModal` برای نمایش لیست خوانندگان
- ✅ منوی context در پیام‌های گروهی با گزینه "خوانده شده توسط"
- ✅ نمایش آواتار، نام و زمان خواندن برای هر کاربر

**فایل‌های اضافه/تغییر یافته**:
- `src/Application/Chats/Queries/GetMessageReadReceiptsQuery.cs` (جدید)
- `src/Web/Endpoints/Chat.cs` (اضافه شدن endpoint)
- `src/Web/ClientApp/src/services/chatApi.js` (اضافه شدن getMessageReadReceipts)
- `src/Web/ClientApp/src/components/Chat/ReadReceiptsModal.jsx` (جدید)
- `src/Web/ClientApp/src/components/Chat/MessageItem.jsx` (افزودن منو و modal)

### 7. دانلود فایل در WebView ✔️

**پیاده‌سازی شده**:
- ✅ API endpoint جدید برای دانلود فایل (`GET /api/chat/download?filePath=...`)
- ✅ تنظیم Content-Type مناسب برای انواع مختلف فایل (PDF, تصاویر، ویدیو، صوت، اسناد آفیس، و غیره)
- ✅ تنظیم Content-Disposition برای نمایش نام صحیح فایل
- ✅ فعال‌سازی Range Processing برای پشتیبانی از resume و streaming
- ✅ محافظت در برابر Directory Traversal attacks
- ✅ مجوز دسترسی برای مهمان‌ها (AllowAnonymous) با CORS

**فایل‌های اضافه/تغییر یافته**:
- `src/Web/Endpoints/Chat.cs` (اضافه شدن endpoint و متد GetContentType)

### 12. رابط کاربری (Frontend) برای مدیریت پشتیبان‌ها ✔️

**پیاده‌سازی شده**:
- ✅ صفحه مدیریت پشتیبان‌ها در React (`AgentManagement.jsx`)
- ✅ نمایش لیست پشتیبان‌ها با وضعیت آنلاین/آفلاین
- ✅ فرم افزودن پشتیبان جدید
- ✅ فرم ویرایش پشتیبان (تغییر وضعیت فعال/غیرفعال، حداکثر چت همزمان)
- ✅ قابلیت حذف پشتیبان
- ✅ نمایش آماری از تیکت‌های هر پشتیبان (چت‌های فعال، بار کاری)
- ✅ افزودن route در AppRouter برای دسترسی به صفحه
- ✅ رفع مشکلات import path در فایل‌های موجود

**فایل‌های اضافه/تغییر یافته**:
- `src/Web/ClientApp/src/components/Chat/AgentManagement.jsx` (جدید)
- `src/Web/ClientApp/src/services/chatApi.js` (اضافه شدن APIهای مدیریت پشتیبان)
- `src/Web/ClientApp/src/AppRouter.jsx` (اضافه شدن route)
- رفع مشکل import در `Chat.jsx`, `MessageItem.jsx`, `ChatContext.jsx`

---

## 📝 نکات مهم برای توسعه‌دهنده

### ساختار پروژه

- **Backend**: .NET 9 با Clean Architecture
  - `Domain`: Entity ها و Enum ها
  - `Application`: Command ها، Query ها و Interface ها
  - `Infrastructure`: پیاده‌سازی سرویس‌ها و Hub های SignalR
  - `Web`: Endpoint ها و Controllers

- **Frontend**: React با Vite
  - `components`: کامپوننت‌های UI
  - `contexts`: Context های React برای state management
  - `services`: سرویس‌های API و SignalR
  - `hooks`: Custom hooks

### چگونه تست کنیم

1. **بیلد پروژه**:
   ```bash
   cd /home/runner/work/Chat_Support/Chat_Support/Chat_Support
   dotnet build -c Release
   ```

2. **اجرای پروژه**:
   ```bash
   dotnet run --project src/Web
   ```

3. **تست Frontend**:
   ```bash
   cd src/Web/ClientApp
   npm install
   npm run dev
   ```

### نکات امنیتی

- همه endpoint های مدیریت پشتیبان نیاز به احراز هویت دارند
- بهتر است یک Policy خاص برای Admin اضافه شود
- در حال حاضر با `RequireAuthorization()` محافظت شده‌اند

---

## 🎯 پیشنهادات برای آینده

1. **داشبورد پشتیبانی**:
   - نمایش آمار real-time از تیکت‌های فعال
   - نمودار تعداد تیکت‌ها در روز/هفته/ماه
   - میانگین زمان پاسخگویی

2. **سیستم رتبه‌بندی**:
   - امکان رتبه‌دهی به پشتیبان توسط کاربر
   - نمایش میانگین رضایت مشتری

3. **گزارش‌گیری**:
   - گزارش عملکرد هر پشتیبان
   - گزارش تیکت‌های حل شده/باز
   - Export به Excel/PDF

4. **بهبود نوتیفیکیشن**:
   - استفاده از Firebase Cloud Messaging برای push notification
   - صدای اعلان قابل تنظیم
   - نمایش preview پیام در نوتیف

5. **چت بات ساده**:
   - پاسخ خودکار به سوالات متداول
   - راهنمایی کاربر قبل از ارتباط با پشتیبان

---

## ✅ چک‌لیست تست

- [x] ویرایش نام گروه
- [x] نمایش درست نام کاربران در تایپینگ
- [x] شمارنده unread درست کار می‌کند
- [x] توضیح فایل اجباری است
- [x] افزودن پشتیبان جدید
- [x] ویرایش اطلاعات پشتیبان
- [x] حذف پشتیبان
- [x] انتقال تیکت هنگام آفلاین شدن پشتیبان
- [x] تخصیص خودکار تیکت‌های در انتظار
- [x] رابط کاربری (Frontend) مدیریت پشتیبان‌ها
- [x] نوتیف سایلنت برای گروه‌ها
- [x] مشاهده خوانده شدن پیام در گروه
- [x] نوتیف برای کاربران (با احترام به تنظیم IsMuted)
- [x] دانلود فایل در WebView

---

## 📞 پشتیبانی

در صورت بروز هر مشکلی یا نیاز به توضیحات بیشتر، لطفاً با تیم توسعه تماس بگیرید.

تمام تغییرات در branch `copilot/fix-d8b4e991-7b78-403e-a900-eeb216dc66ca` قرار دارند.
