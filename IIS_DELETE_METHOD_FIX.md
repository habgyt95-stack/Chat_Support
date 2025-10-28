# راهنمای رفع مشکل 405 Method Not Allowed برای DELETE در IIS

## مشکل
در سرور آنلاین، درخواست‌های DELETE با خطای 405 مواجه می‌شوند:
- `DELETE /api/chat/messages/{id}` → 405
- `DELETE /api/chat/rooms/{id}/members/{userId}` → 405
- `DELETE /api/chat/rooms/{id}` → 405

## علت اصلی
IIS به صورت پیش‌فرض WebDAV Module را فعال دارد که با HTTP verb های DELETE/PUT تداخل دارد.

---

## راه‌حل 1: تنظیمات web.config (انجام شده ✅)

فایل `web.config` به‌روزرسانی شد و شامل موارد زیر است:
- حذف WebDAV Module در سطح global و location
- اجازه صریح به DELETE verb در requestFiltering
- تنظیم handler برای ASP.NET Core

**نیازی به تغییر دیگری در web.config ندارید.**

---

## راه‌حل 2: تنظیمات IIS Server (باید روی سرور اجرا شود)

### گام 1: غیرفعال کردن WebDAV در سطح Server

#### روش A: از طریق IIS Manager
1. باز کردن **IIS Manager**
2. انتخاب **Server** (نه site خاص)
3. دوبار کلیک روی **Modules**
4. پیدا کردن `WebDAVModule` و **Remove** کردن آن
5. دوبار کلیک روی **Handler Mappings**
6. پیدا کردن `WebDAV` و **Remove** کردن آن

#### روش B: از طریق PowerShell (اجرا در سرور به عنوان Administrator)
```powershell
# حذف WebDAV Feature از IIS
Uninstall-WindowsFeature -Name Web-DAV-Publishing

# یا غیرفعال کردن آن
Remove-WebConfigurationProperty -pspath 'MACHINE/WEBROOT/APPHOST' -filter "system.webServer/handlers" -name "." -AtElement @{name='WebDAV'}
Remove-WebConfigurationProperty -pspath 'MACHINE/WEBROOT/APPHOST' -filter "system.webServer/modules" -name "." -AtElement @{name='WebDAVModule'}
```

---

### گام 2: بررسی Request Filtering در IIS

1. در IIS Manager، انتخاب **Site شما** (chat.abrik.cloud)
2. دوبار کلیک روی **Request Filtering**
3. رفتن به تب **HTTP Verbs**
4. اگر "Allow unlisted verbs" خاموش است، روشن کنید
5. یا DELETE را به صورت صریح Add کنید با Allow = True

#### PowerShell Command:
```powershell
# اجازه دادن به DELETE verb برای site خاص
Set-WebConfigurationProperty -pspath 'MACHINE/WEBROOT/APPHOST' -location 'Default Web Site/YourAppPath' -filter "system.webServer/security/requestFiltering/verbs" -name "allowUnlisted" -value "True"
```

---

### گام 3: بررسی URL Rewrite Rules

اگر از URL Rewrite استفاده می‌کنید:

1. در IIS Manager → Site → **URL Rewrite**
2. بررسی کنید که هیچ rule ای DELETE را block نکند
3. اگر rule ای وجود دارد که فقط GET/POST را اجازه می‌دهد، آن را ویرایش کنید

---

### گام 4: Restart Application Pool

بعد از هر تغییر:
```powershell
Restart-WebAppPool -Name "YourAppPoolName"

# یا از IIS Manager:
# Application Pools → Select your pool → Recycle
```

---

## راه‌حل 3: تست و Debug

### تست از Command Line (در سرور):
```powershell
# تست با curl (در PowerShell)
curl -X DELETE https://chat.abrik.cloud/api/chat/messages/123 -Headers @{"Authorization"="Bearer YOUR_TOKEN"}

# تست با Invoke-WebRequest
Invoke-WebRequest -Uri "https://chat.abrik.cloud/api/chat/messages/123" -Method DELETE -Headers @{"Authorization"="Bearer YOUR_TOKEN"}
```

### بررسی Failed Request Tracing (اگر همچنان کار نکرد):

1. در IIS Manager → Site → **Failed Request Tracing**
2. فعال کردن با status code 405
3. تست مجدد DELETE request
4. بررسی لاگ‌ها در: `%SystemDrive%\inetpub\logs\FailedReqLogFiles`

---

## راه‌حل 4: اگر همه موارد بالا کار نکردند

### بررسی Web.config در سطح Server:
فایل `applicationHost.config` را چک کنید (معمولاً در: `C:\Windows\System32\inetsrv\config\`)

به دنبال این بخش بگردید:
```xml
<system.webServer>
    <security>
        <requestFiltering>
            <verbs allowUnlisted="true">
                <add verb="DELETE" allowed="true" />
            </verbs>
        </requestFiltering>
    </security>
</system.webServer>
```

---

## Checklist نهایی برای Admin سرور

- [ ] WebDAV Module حذف یا غیرفعال شده باشد (Server Level)
- [ ] WebDAV Handler حذف شده باشد (Server Level)
- [ ] Request Filtering برای site اجازه DELETE را بدهد
- [ ] URL Rewrite rules DELETE را block نکنند
- [ ] Application Pool restart شده باشد
- [ ] فایل web.config به‌روز در سرور deploy شده باشد

---

## تست نهایی

بعد از اعمال تغییرات، این endpoint ها باید کار کنند:

✅ `DELETE https://chat.abrik.cloud/api/chat/messages/{messageId}`  
✅ `DELETE https://chat.abrik.cloud/api/chat/rooms/{roomId}/members/{userId}`  
✅ `DELETE https://chat.abrik.cloud/api/chat/rooms/{roomId}`

---

## نکته مهم

اگر هنوز مشکل ادامه داشت، از admin سرور بخواهید:
1. Screenshot از IIS Manager → Modules (برای WebDAV)
2. Screenshot از IIS Manager → Handler Mappings
3. Screenshot از Request Filtering → HTTP Verbs
4. لاگ Failed Request Tracing را بررسی کنید

---

## پشتیبانی

در صورت بروز مشکل، این اطلاعات را جمع‌آوری کنید:
- Response Headers از درخواست 405
- IIS Version: `Get-ItemProperty HKLM:\SOFTWARE\Microsoft\InetStp\ | Select-Object MajorVersion,MinorVersion`
- .NET Runtime Version در Application Pool
- محتوای دقیق خطا از IIS logs

