# راهنمای دانلود فایل در WebView اندروید

## تغییرات انجام شده در سمت سرور (Backend)

### ✅ تمام تغییرات لازم از سمت سرور اعمال شده است:

1. **Endpoint اختصاصی دانلود با هدرهای استاندارد**
   - مسیر: `GET /api/chat/download?filePath={filePath}`
   - هدر `Content-Disposition: attachment; filename="..."` به صورت خودکار اضافه می‌شود
   - هدر `Content-Type` بر اساس پسوند فایل تعیین می‌شود (50+ فرمت پشتیبانی شده)
   - پشتیبانی از Range Requests برای دانلود قابل ادامه (Resume)

2. **پشتیبانی از انواع فایل‌ها**
   - تصاویر: jpg, png, gif, bmp, webp, svg
   - ویدیوها: mp4, avi, mov, wmv, webm
   - صوت: mp3, wav, ogg, m4a, aac
   - اسناد: pdf, doc, docx, xls, xlsx, ppt, pptx, txt, csv
   - فشرده‌شده: zip, rar, 7z, tar, gz
   - و سایر فرمت‌ها

3. **Middleware اضافی برای فایل‌های استاتیک**
   - هدر `Content-Disposition` برای فایل‌های مسیر `/uploads` اضافه می‌شود
   - امنیت در برابر Directory Traversal Attack

4. **تغییر در فرانت‌اند**
   - حذف استفاده از `fetch()` و `blob` که WebView نمی‌تواند آن را به عنوان دانلود تشخیص دهد
   - استفاده از لینک مستقیم `<a href>` با attribute `download`
   - تمام لینک‌های دانلود به endpoint `/api/chat/download` هدایت می‌شوند

---

## نمونه درخواست و پاسخ

### درخواست نمونه:
```http
GET /api/chat/download?filePath=%2Fuploads%2F123%2Ftest.pdf HTTP/1.1
Host: your-domain.com
```

### پاسخ سرور:
```http
HTTP/1.1 200 OK
Content-Type: application/pdf
Content-Disposition: attachment; filename="test.pdf"
Content-Length: 1234567
Accept-Ranges: bytes
Last-Modified: Mon, 12 Oct 2025 10:30:00 GMT

[محتوای فایل...]
```

---

## تست و بررسی

### برای تست دانلود در مرورگر عادی:
1. باز کردن یک چت و ارسال فایل
2. کلیک روی لینک دانلود
3. فایل باید به صورت خودکار دانلود شود

### برای تست در WebView اندروید:
لینک‌های دانلود به صورت زیر هستند:
```
https://your-domain.com/api/chat/download?filePath=%2Fuploads%2FuserId%2Ffilename.ext
```

---

## آنچه باید در سمت WebView اندروید بررسی شود

### ✅ چک‌لیست برای برنامه‌نویس اپ:

1. **DownloadListener فعال باشد**
```kotlin
webView.setDownloadListener { url, userAgent, contentDisposition, mimeType, contentLength ->
    val request = DownloadManager.Request(Uri.parse(url))
    
    // استخراج نام فایل از Content-Disposition
    val fileName = extractFileName(contentDisposition, url)
    
    request.setTitle(fileName)
    request.setDescription("دانلود فایل از چت")
    request.setMimeType(mimeType)
    
    // نمایش نوتیفیکیشن
    request.setNotificationVisibility(DownloadManager.Request.VISIBILITY_VISIBLE_NOTIFY_COMPLETED)
    
    // ذخیره در پوشه Download
    request.setDestinationInExternalPublicDir(Environment.DIRECTORY_DOWNLOADS, fileName)
    
    // اضافه کردن هدرهای Cookie و Authorization در صورت نیاز
    val cookies = CookieManager.getInstance().getCookie(url)
    request.addRequestHeader("Cookie", cookies)
    
    // شروع دانلود
    val downloadManager = getSystemService(Context.DOWNLOAD_SERVICE) as DownloadManager
    downloadManager.enqueue(request)
    
    Toast.makeText(this, "دانلود شروع شد", Toast.LENGTH_SHORT).show()
}

// تابع کمکی برای استخراج نام فایل
fun extractFileName(contentDisposition: String?, url: String): String {
    var fileName = "file"
    
    if (contentDisposition != null) {
        val index = contentDisposition.indexOf("filename=")
        if (index > 0) {
            fileName = contentDisposition.substring(index + 9)
                .replace("\"", "")
                .trim()
        }
    }
    
    // اگر نام فایل پیدا نشد، از URL استخراج کن
    if (fileName == "file") {
        fileName = url.substring(url.lastIndexOf('/') + 1)
    }
    
    return fileName
}
```

2. **تنظیمات WebView صحیح باشند**
```kotlin
webView.settings.apply {
    javaScriptEnabled = true
    domStorageEnabled = true
    allowFileAccess = true
    allowContentAccess = true
    
    // اجازه دانلود از هر منبعی (HTTP/HTTPS)
    mixedContentMode = WebSettings.MIXED_CONTENT_ALWAYS_ALLOW // در صورت نیاز
}
```

3. **مجوزهای لازم در Manifest**
```xml
<!-- برای اندروید 10 به بالا (API 29+) نیاز نیست -->
<uses-permission android:name="android.permission.INTERNET" />
<uses-permission android:name="android.permission.WRITE_EXTERNAL_STORAGE"
    android:maxSdkVersion="28" />
```

4. **درخواست مجوزهای Runtime (فقط API < 29)**
```kotlin
if (Build.VERSION.SDK_INT < Build.VERSION_CODES.Q) {
    if (ContextCompat.checkSelfPermission(this, Manifest.permission.WRITE_EXTERNAL_STORAGE)
        != PackageManager.PERMISSION_GRANTED) {
        ActivityCompat.requestPermissions(
            this,
            arrayOf(Manifest.permission.WRITE_EXTERNAL_STORAGE),
            REQUEST_CODE
        )
    }
}
```

5. **بررسی لاگ برای دیباگ**
```kotlin
webView.setDownloadListener { url, userAgent, contentDisposition, mimeType, contentLength ->
    Log.d("WebView", "Download URL: $url")
    Log.d("WebView", "Content-Disposition: $contentDisposition")
    Log.d("WebView", "MIME Type: $mimeType")
    Log.d("WebView", "Content Length: $contentLength")
    
    // ... ادامه کد دانلود
}
```

---

## دلایل احتمالی عدم کارکرد دانلود (از سمت اپ)

### 🔴 اگر هنوز دانلود کار نمی‌کند، بررسی کنید:

1. **DownloadListener صدا زده نمی‌شود؟**
   - بررسی کنید که WebView.setDownloadListener() فراخوانی شده باشد
   - لاگ اضافه کنید تا مطمئن شوید listener صدا زده می‌شود

2. **DownloadManager کار نمی‌کند؟**
   - بررسی کنید سرویس DownloadManager در دستگاه فعال است
   - در برخی ROM های سفارشی ممکن است غیرفعال باشد

3. **فایل دانلود نمی‌شود اما listener فراخوانی می‌شود؟**
   - بررسی مجوز WRITE_EXTERNAL_STORAGE (برای API < 29)
   - بررسی Scoped Storage برای API 29+
   - تست دانلود با یک URL خارجی (مثل Google) برای اطمینان از کارکرد DownloadManager

4. **Cookie یا Authentication لازم است؟**
   - اگر فایل‌ها نیاز به احراز هویت دارند، Cookie را اضافه کنید:
   ```kotlin
   val cookies = CookieManager.getInstance().getCookie(url)
   request.addRequestHeader("Cookie", cookies)
   ```

5. **MIME Type نادرست**
   - سرور ما MIME Type صحیح ارسال می‌کند
   - اما اگر هنوز مشکل دارید، می‌توانید به صورت دستی تنظیم کنید:
   ```kotlin
   val mimeType = when (url.substringAfterLast('.').lowercase()) {
       "pdf" -> "application/pdf"
       "jpg", "jpeg" -> "image/jpeg"
       "png" -> "image/png"
       // ...
       else -> mimeType ?: "application/octet-stream"
   }
   ```

---

## نمونه کد کامل WebView با قابلیت دانلود

```kotlin
class MainActivity : AppCompatActivity() {
    
    private lateinit var webView: WebView
    
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        setContentView(R.layout.activity_main)
        
        webView = findViewById(R.id.webView)
        setupWebView()
        
        webView.loadUrl("https://your-domain.com")
    }
    
    private fun setupWebView() {
        webView.settings.apply {
            javaScriptEnabled = true
            domStorageEnabled = true
            allowFileAccess = true
            allowContentAccess = true
        }
        
        // تنظیم DownloadListener برای هندل کردن دانلود فایل‌ها
        webView.setDownloadListener { url, userAgent, contentDisposition, mimeType, contentLength ->
            handleDownload(url, contentDisposition, mimeType)
        }
        
        webView.webViewClient = WebViewClient()
        webView.webChromeClient = WebChromeClient()
    }
    
    private fun handleDownload(url: String, contentDisposition: String?, mimeType: String?) {
        try {
            val request = DownloadManager.Request(Uri.parse(url))
            
            // استخراج نام فایل
            val fileName = extractFileName(contentDisposition, url)
            
            // تنظیمات دانلود
            request.apply {
                setTitle(fileName)
                setDescription("دانلود فایل از چت")
                setMimeType(mimeType ?: "application/octet-stream")
                setNotificationVisibility(DownloadManager.Request.VISIBILITY_VISIBLE_NOTIFY_COMPLETED)
                setDestinationInExternalPublicDir(Environment.DIRECTORY_DOWNLOADS, fileName)
                
                // اضافه کردن Cookie برای احراز هویت
                val cookies = CookieManager.getInstance().getCookie(url)
                if (cookies != null) {
                    addRequestHeader("Cookie", cookies)
                }
            }
            
            // شروع دانلود
            val downloadManager = getSystemService(Context.DOWNLOAD_SERVICE) as DownloadManager
            val downloadId = downloadManager.enqueue(request)
            
            Toast.makeText(this, "دانلود شروع شد: $fileName", Toast.LENGTH_SHORT).show()
            
            Log.d("Download", "Download started - URL: $url, File: $fileName, ID: $downloadId")
            
        } catch (e: Exception) {
            Log.e("Download", "Error starting download", e)
            Toast.makeText(this, "خطا در شروع دانلود: ${e.message}", Toast.LENGTH_LONG).show()
        }
    }
    
    private fun extractFileName(contentDisposition: String?, url: String): String {
        var fileName = "file"
        
        // تلاش برای استخراج از Content-Disposition
        if (contentDisposition != null) {
            try {
                val index = contentDisposition.indexOf("filename=")
                if (index > 0) {
                    fileName = contentDisposition.substring(index + 9)
                        .replace("\"", "")
                        .replace("'", "")
                        .trim()
                    
                    // URL Decode
                    fileName = Uri.decode(fileName)
                }
            } catch (e: Exception) {
                Log.e("Download", "Error extracting filename from Content-Disposition", e)
            }
        }
        
        // اگر نام فایل پیدا نشد، از URL استخراج کن
        if (fileName == "file") {
            try {
                fileName = Uri.parse(url).lastPathSegment ?: "file"
                fileName = Uri.decode(fileName)
            } catch (e: Exception) {
                fileName = "file"
            }
        }
        
        // اضافه کردن timestamp برای یکتا بودن
        // val timestamp = System.currentTimeMillis()
        // fileName = "${fileName.substringBeforeLast('.')}_$timestamp.${fileName.substringAfterLast('.')}"
        
        return fileName
    }
}
```

---

## تست و بررسی نهایی

### لینک‌های تست:

1. **تست دانلود PDF:**
```
https://your-domain.com/api/chat/download?filePath=%2Fuploads%2Ftest.pdf
```

2. **تست دانلود تصویر:**
```
https://your-domain.com/api/chat/download?filePath=%2Fuploads%2Ftest.jpg
```

3. **بررسی هدرها با cURL:**
```bash
curl -I "https://your-domain.com/api/chat/download?filePath=%2Fuploads%2Ftest.pdf"
```

باید خروجی مشابه زیر را ببینید:
```
HTTP/1.1 200 OK
Content-Type: application/pdf
Content-Disposition: attachment; filename="test.pdf"
Content-Length: 123456
Accept-Ranges: bytes
```

---

## نتیجه‌گیری

✅ **از سمت سرور (Backend):** تمام تنظیمات و هدرهای استاندارد برای دانلود در WebView اعمال شده است.

✅ **از سمت فرانت‌اند:** لینک‌های دانلود به صورت مستقیم و بدون استفاده از JavaScript fetch/blob هستند.

🔴 **اگر هنوز دانلود در WebView کار نمی‌کند:** مشکل از پیاده‌سازی DownloadListener در اپ اندروید است و باید کدهای بالا را بررسی کنید.

---

## پشتیبانی و سوالات

اگر پس از اعمال کدهای بالا هنوز مشکل دارید، لطفاً اطلاعات زیر را ارسال کنید:

1. لاگ WebView (از قسمت `Log.d("WebView", ...)`)
2. نسخه اندروید دستگاه
3. آیا با URL های خارجی (مثل لینک دانلود گوگل) دانلود کار می‌کند؟
4. آیا DownloadListener اصلاً صدا زده می‌شود؟

---

**تاریخ آخرین بروزرسانی:** 12 اکتبر 2025
