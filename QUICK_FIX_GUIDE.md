# راهنمای سریع رفع مشکل 405 Method Not Allowed

## ✅ کارهای انجام شده

### 1. به‌روزرسانی `web.config`
فایل `Chat_Support/src/Web/web.config` به‌روز شد و شامل تنظیمات زیر است:

- ✅ حذف WebDAV Module در سطح Global و Location
- ✅ حذف WebDAV Handler
- ✅ اجازه صریح به DELETE verb در requestFiltering
- ✅ فعال‌سازی allowUnlisted برای HTTP Verbs
- ✅ تنظیم ASP.NET Core Handler

### 2. ایجاد اسکریپت‌های کمکی

**فایل‌های ایجاد شده:**

📄 `IIS_DELETE_METHOD_FIX.md` - راهنمای کامل مستندات و troubleshooting  
📄 `Fix-IIS-DELETE-Method.ps1` - اسکریپت خودکار برای تنظیم IIS در سرور  
📄 `Test-DELETE-Endpoints.ps1` - اسکریپت تست تمام DELETE endpoints

---

## 🚀 مراحل حل مشکل در سرور

### مرحله 1: Deploy کردن web.config جدید

فایل `web.config` جدید را همراه با application خود در سرور deploy کنید:

```bash
# در مسیر پروژه local:
cd d:\Projects\SupportChat\Chat_Support\src\Web
dotnet publish -c Release -o .\bin\publish

# فایل web.config از مسیر زیر کپی و به سرور منتقل شود:
# d:\Projects\SupportChat\Chat_Support\src\Web\web.config
```

### مرحله 2: اجرای اسکریپت در سرور (توسط Admin)

فایل `Fix-IIS-DELETE-Method.ps1` را به سرور منتقل کنید و به عنوان **Administrator** اجرا کنید:

```powershell
# در سرور (Run as Administrator):
cd C:\Path\To\Scripts
.\Fix-IIS-DELETE-Method.ps1
```

این اسکریپت به صورت خودکار:
- WebDAV Module و Handler را حذف می‌کند
- DELETE verb را فعال می‌کند
- Application Pool را restart می‌کند

### مرحله 3: تست کردن Endpoints

بعد از اعمال تغییرات، با اسکریپت تست، صحت کار را بررسی کنید:

```powershell
# در سرور یا local:
.\Test-DELETE-Endpoints.ps1 -BaseUrl "https://chat.abrik.cloud" -Token "YOUR_JWT_TOKEN"
```

اگر تمام تست‌ها سبز شدند (✅)، مشکل حل شده است!

---

## 🔍 Troubleshooting

### اگر هنوز خطای 405 می‌دهد:

#### 1. بررسی WebDAV در IIS Manager (سرور):
- IIS Manager → Server → **Modules** → مطمئن شوید `WebDAVModule` وجود ندارد
- IIS Manager → Server → **Handler Mappings** → مطمئن شوید `WebDAV` وجود ندارد

#### 2. بررسی Request Filtering:
- IIS Manager → Site (chat.abrik.cloud) → **Request Filtering**
- تب **HTTP Verbs** → بررسی کنید DELETE allowed باشد

#### 3. بررسی URL Rewrite:
- اگر URL Rewrite rules دارید، مطمئن شوید DELETE را block نکنند

#### 4. بررسی لاگ‌ها:
```powershell
# چک کردن Event Viewer در سرور:
Get-WinEvent -LogName "Microsoft-IIS-Configuration/Operational" -MaxEvents 50 | Where-Object { $_.Message -like "*405*" }
```

#### 5. فعال‌سازی Failed Request Tracing:
- IIS Manager → Site → **Failed Request Tracing**
- Status code: 405
- بعد از تست، لاگ‌ها در `%SystemDrive%\inetpub\logs\FailedReqLogFiles` قابل مشاهده است

---

## 📋 Checklist نهایی

قبل از بستن تیکت، این موارد را چک کنید:

- [ ] فایل `web.config` جدید در سرور deploy شده
- [ ] اسکریپت `Fix-IIS-DELETE-Method.ps1` در سرور اجرا شده
- [ ] WebDAV Module در IIS Manager وجود ندارد
- [ ] Application Pool restart شده
- [ ] تست `DELETE /api/chat/messages/{id}` موفق است
- [ ] تست `DELETE /api/chat/rooms/{id}/members/{userId}` موفق است
- [ ] تست `DELETE /api/chat/rooms/{id}` موفق است

---

## 🎯 تست سریع از Browser Console

می‌توانید از Console مرورگر هم تست کنید:

```javascript
// در صفحه chat.abrik.cloud، Console را باز کنید و این کد را اجرا کنید:

fetch('https://chat.abrik.cloud/api/chat/messages/999999', {
    method: 'DELETE',
    headers: {
        'Authorization': 'Bearer ' + localStorage.getItem('token'),
        'Content-Type': 'application/json'
    }
})
.then(response => {
    console.log('Status:', response.status);
    if (response.status === 405) {
        console.error('❌ مشکل 405 هنوز وجود دارد');
    } else if (response.status === 404 || response.status === 401 || response.status === 200) {
        console.log('✅ DELETE Method کار می‌کند!');
    }
    return response.json();
})
.then(data => console.log('Response:', data))
.catch(err => console.error('Error:', err));
```

---

## 📞 در صورت نیاز به پشتیبانی

اگر مشکل حل نشد، این اطلاعات را جمع‌آوری کنید:

1. **Screenshot از IIS Manager:**
   - Modules list
   - Handler Mappings
   - Request Filtering → HTTP Verbs

2. **IIS Version:**
   ```powershell
   Get-ItemProperty HKLM:\SOFTWARE\Microsoft\InetStp\ | Select-Object MajorVersion,MinorVersion
   ```

3. **Response Headers از خطای 405:**
   - در Browser Console → Network tab → کلیک روی request → Headers

4. **Web.config content در سرور:**
   - محتوای دقیق فایل web.config که در سرور deploy شده

---

## 📚 منابع مفید

- [Microsoft Docs: Common IIS 405 Errors](https://docs.microsoft.com/en-us/iis/troubleshoot/diagnosing-http-errors/405-errors)
- [How to Disable WebDAV in IIS](https://docs.microsoft.com/en-us/iis/configuration/system.webserver/webdav/)
- [ASP.NET Core with IIS](https://docs.microsoft.com/en-us/aspnet/core/host-and-deploy/iis/)

---

## 🎉 نکته آخر

مشکل 405 برای DELETE methods یک مشکل رایج در IIS است که معمولاً به دلیل WebDAV Module رخ می‌دهد. 
با اعمال تنظیمات بالا، این مشکل باید **100%** حل شود.

موفق باشید! 🚀
