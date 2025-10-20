# اصلاحات واکنش‌های پیام (Message Reactions)

تاریخ: 13 اکتبر 2025

## مشکلات برطرف شده

### 1. مشکل پاک شدن واکنش‌ها پس از بازگشت به لیست پیام‌ها ✅

**علت مشکل**: واکنش‌ها در Query بارگیری نمی‌شدند.

**راه‌حل**:
- افزودن `.Include(m => m.Reactions).ThenInclude(r => r.User)` به `GetChatMessagesQuery.cs`
- تنظیم `currentUserId` در mapping context برای شناسایی واکنش‌های کاربر فعلی

**فایل‌های تغییر یافته**:
- `src/Application/Chats/Queries/GetChatMessagesQuery.cs`
- `src/Application/Chats/DTOs/ChatMessageDto.cs`

### 2. محدودیت ایموجی‌های واکنش ✅

**قبل**: فقط 6 ایموجی محدود
**بعد**: 70+ ایموجی (مشابه تلگرام، بدون موارد حساسیت‌برانگیز)

**تغییرات**:
- ساخت کامپوننت جدید `EmojiPicker.jsx` با UI مدرن
- استایل شبیه تلگرام با انیمیشن و hover effects
- دسته‌بندی ایموجی‌ها: مثبت، خنده، تعجب، ناراحتی، عصبانیت، دعا، متفرقه

**فایل‌های جدید**:
- `src/Web/ClientApp/src/components/Chat/EmojiPicker.jsx`
- `src/Web/ClientApp/src/components/Chat/EmojiPicker.css`

## تغییرات ساختاری

### Backend Changes

1. **ReactionInfo DTO** (`src/Application/Chats/DTOs/ReactionInfo.cs`):
   ```csharp
   public class ReactionInfo
   {
       public string Emoji { get; set; }
       public int Count { get; set; }
       public bool IsReactedByCurrentUser { get; set; }
       public List<string> UserFullNames { get; set; }
   }
   ```

2. **ChatMessageDto Mapping**:
   - واکنش‌ها گروه‌بندی می‌شوند بر اساس emoji
   - برای هر گروه، تعداد، لیست کاربران، و وضعیت واکنش کاربر فعلی محاسبه می‌شود

### Frontend Changes

1. **MessageItem.jsx**:
   - استفاده از `EmojiPicker` جدید به جای `EmojiReactionPicker` قدیمی
   - پشتیبانی از ساختار جدید `ReactionInfo`
   - نمایش واکنش کاربر فعلی با کلاس `reacted-by-me`

2. **ChatContext.jsx**:
   - reducer برای `MESSAGE_REACTION_SUCCESS` بازنویسی شد
   - پشتیبانی از ساختار جدید با `count`, `userFullNames`, `isReactedByCurrentUser`

3. **Chat.css**:
   - استایل جدید برای `.reaction-badge.reacted-by-me`
   - رنگ آبی و bold برای واکنش‌های کاربر فعلی

## ویژگی‌های جدید

✨ **نمایش واکنش کاربر**: واکنش‌هایی که خودتان گذاشته‌اید با رنگ آبی مشخص می‌شوند
✨ **انتخاب گسترده**: بیش از 70 ایموجی برای انتخاب
✨ **UI مدرن**: طراحی شبیه تلگرام با انیمیشن smooth
✨ **پایداری داده**: واکنش‌ها پس از رفرش یا تغییر صفحه حفظ می‌شوند
✨ **Tooltip**: نمایش نام کاربرانی که واکنش گذاشته‌اند با hover

## تست

برای تست:
1. برنامه را اجرا کنید: `dotnet run --project src/Web/Web.csproj`
2. روی یک پیام راست‌کلیک کنید یا در موبایل چند لحظه نگاه دارید
3. گزینه "واکنش" را انتخاب کنید
4. یکی از 70+ ایموجی را انتخاب کنید
5. به لیست پیام‌ها برگردید و دوباره به همان پیام بروید
6. واکنش شما باید با رنگ آبی نمایش داده شود و حفظ شده باشد

## نکات فنی

- واکنش‌ها در دیتابیس با entity `MessageReaction` ذخیره می‌شوند
- SignalR event `MessageReacted` تغییرات را real-time به کلاینت‌ها می‌فرستد
- کلیک روی یک واکنش موجود، آن را toggle می‌کند (اضافه/حذف)
- هر کاربر فقط می‌تواند یک نوع واکنش روی هر پیام داشته باشد
