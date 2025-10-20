سلام مهندس، وقت بخیر

بررسی کامل سمت سرور و فرانت‌اند انجام شد و **تمام تنظیمات لازم برای دانلود فایل در WebView اندروید اعمال شده است**.

## ✅ تغییرات انجام شده در سرور:

1. **Endpoint اختصاصی دانلود با هدرهای استاندارد**
   - مسیر: `GET /api/chat/download?filePath={filePath}`
   - هدر `Content-Disposition: attachment; filename="..."` اضافه شده
   - هدر `Content-Type` بر اساس نوع فایل تنظیم می‌شود (50+ فرمت پشتیبانی شده)
   - پشتیبانی از Range Requests برای دانلود قابل ادامه

2. **تغییر در فرانت‌اند**
   - حذف استفاده از `fetch()` و `blob` که WebView نمی‌تونه تشخیص بده
   - استفاده از لینک مستقیم `<a href>` با attribute `download`

3. **Middleware برای فایل‌های استاتیک**
   - هدر `Content-Disposition` برای تمام فایل‌های `/uploads` اضافه شده

## 🔍 تست سرور:

لینک‌های دانلود حالا به این صورت هستند:
```
https://[domain]/api/chat/download?filePath=%2Fuploads%2FuserId%2Ffilename.ext
```

می‌تونید با cURL تست کنید:
```bash
curl -I "https://[domain]/api/chat/download?filePath=%2Fuploads%2Ftest.pdf"
```

باید هدرهای زیر را ببینید:
```
Content-Type: application/pdf
Content-Disposition: attachment; filename="test.pdf"
Accept-Ranges: bytes
```

---

## ⚠️ نکات مهم برای WebView اندروید:

### 1. DownloadListener حتماً باید تنظیم شود:

```kotlin
webView.setDownloadListener { url, userAgent, contentDisposition, mimeType, contentLength ->
    val request = DownloadManager.Request(Uri.parse(url))
    
    // استخراج نام فایل از Content-Disposition
    var fileName = "file"
    if (contentDisposition != null) {
        val index = contentDisposition.indexOf("filename=")
        if (index > 0) {
            fileName = contentDisposition.substring(index + 9)
                .replace("\"", "")
                .trim()
        }
    }
    
    request.setTitle(fileName)
    request.setDescription("دانلود فایل از چت")
    request.setMimeType(mimeType ?: "application/octet-stream")
    request.setNotificationVisibility(DownloadManager.Request.VISIBILITY_VISIBLE_NOTIFY_COMPLETED)
    request.setDestinationInExternalPublicDir(Environment.DIRECTORY_DOWNLOADS, fileName)
    
    // اضافه کردن Cookie برای احراز هویت (مهم!)
    val cookies = CookieManager.getInstance().getCookie(url)
    if (cookies != null) {
        request.addRequestHeader("Cookie", cookies)
    }
    
    val downloadManager = getSystemService(Context.DOWNLOAD_SERVICE) as DownloadManager
    downloadManager.enqueue(request)
    
    Toast.makeText(this, "دانلود شروع شد", Toast.LENGTH_SHORT).show()
}
```

### 2. تنظیمات WebView:

```kotlin
webView.settings.apply {
    javaScriptEnabled = true
    domStorageEnabled = true
    allowFileAccess = true
    allowContentAccess = true
}
```

### 3. لاگ برای دیباگ:

برای اینکه مطمئن بشیم DownloadListener صدا زده می‌شه، لطفاً این لاگ‌ها رو اضافه کنید:

```kotlin
webView.setDownloadListener { url, userAgent, contentDisposition, mimeType, contentLength ->
    Log.d("WebView-Download", "URL: $url")
    Log.d("WebView-Download", "Content-Disposition: $contentDisposition")
    Log.d("WebView-Download", "MIME Type: $mimeType")
    Log.d("WebView-Download", "Content Length: $contentLength")
    
    // ... ادامه کد دانلود
}
```

---

## 🎯 چک‌لیست بررسی:

✅ آیا `webView.setDownloadListener()` فراخوانی شده؟
✅ آیا در Logcat پیام‌های "WebView-Download" نمایش داده می‌شود؟
✅ آیا با یک لینک خارجی (مثل https://subtitlestar.com/...) دانلود کار می‌کند؟
✅ آیا Cookie ها به درخواست دانلود اضافه می‌شوند؟ (برای احراز هویت)
✅ آیا مجوز WRITE_EXTERNAL_STORAGE برای API < 29 گرفته شده؟

---

## 📄 مستندات کامل:

یک فایل کامل با نمونه کد، تست، و راهنمای دیباگ آماده شده که شامل:
- کد کامل WebView با DownloadListener
- نحوه استخراج نام فایل از Content-Disposition
- راهنمای دیباگ و حل مشکلات رایج
- لینک‌های تست

این فایل در مسیر پروژه با نام `WEBVIEW_DOWNLOAD_GUIDE.md` قرار داده شده.

---

## نتیجه‌گیری:

از سمت سرور و فرانت‌اند **همه چیز استاندارد و صحیح پیاده‌سازی شده**. لینک‌های دانلود مستقیم هستند و هدرهای `Content-Disposition` و `Content-Type` به درستی ارسال می‌شوند.

اگر بعد از اعمال کدهای بالا هنوز دانلود کار نمی‌کند، لطفاً:
1. لاگ Logcat را ارسال کنید
2. بررسی کنید آیا DownloadListener اصلاً صدا زده می‌شود یا نه
3. با یک URL خارجی تست کنید که آیا DownloadManager کار می‌کند

در صورت نیاز به راهنمایی بیشتر، در خدمت هستم.

موفق باشید 🙏
