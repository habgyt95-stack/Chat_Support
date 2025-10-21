# بررسی جامع امنیت سیستم (Security Review)

## خلاصه اجرایی

این سند نتایج بررسی امنیتی جامع سیستم چت و پشتیبانی را ارائه می‌دهد. تمرکز اصلی بر شناسایی و رفع آسیب‌پذیری‌های امنیتی، بهبود authentication/authorization و محافظت در برابر حملات رایج است.

**وضعیت کلی امنیت**: 🟡 متوسط (نیاز به بهبود)

---

## 🔴 مسائل بحرانی (Critical - فوری)

### 1. Secrets در Source Code

**مشکل**: کلیدهای API و connection string‌ها در `appsettings.json` هستند.

**خطر**: 
- در صورت لو رفتن repository، تمام secrets فاش می‌شوند
- سطح ریسک: 🔴 **بحرانی**

**راه حل**:
```bash
# استفاده از Azure Key Vault
# فایل: src/Web/appsettings.json - حذف کردن secrets

# مرحله 1: ایجاد Key Vault
az keyvault create \
  --name [keyvault-name] \
  --resource-group [rg-name] \
  --location [location]

# مرحله 2: افزودن secrets
az keyvault secret set --vault-name [keyvault-name] --name "ConnectionStrings--Chat-SupportDb" --value "[connection-string]"
az keyvault secret set --vault-name [keyvault-name] --name "JwtChat--Key" --value "[jwt-key]"
az keyvault secret set --vault-name [keyvault-name] --name "Kavenegar--ApiKey" --value "[api-key]"

# مرحله 3: تنظیم managed identity برای App Service
az webapp identity assign --name [app-name] --resource-group [rg-name]

# مرحله 4: دادن دسترسی به Key Vault
az keyvault set-policy \
  --name [keyvault-name] \
  --object-id [app-managed-identity-id] \
  --secret-permissions get list
```

**Migration**:
```csharp
// فایل: src/Web/Program.cs
// اضافه کردن این خطوط قبل از builder.Build()

if (!builder.Environment.IsDevelopment())
{
    var keyVaultEndpoint = builder.Configuration["AZURE_KEY_VAULT_ENDPOINT"];
    if (!string.IsNullOrEmpty(keyVaultEndpoint))
    {
        builder.Configuration.AddAzureKeyVault(
            new Uri(keyVaultEndpoint),
            new DefaultAzureCredential());
    }
}
```

**Rollback**: فعلاً secrets در appsettings باقی می‌مانند تا Key Vault راه‌اندازی شود.

---

### 2. فقدان Rate Limiting

**مشکل**: هیچ محدودیتی برای تعداد request‌ها وجود ندارد.

**خطر**: 
- حملات DDoS
- Brute force در login
- Resource exhaustion
- سطح ریسک: 🔴 **بحرانی**

**راه حل**:

```csharp
// فایل: src/Web/Program.cs
// اضافه کردن Rate Limiting middleware

builder.Services.AddRateLimiter(options =>
{
    // سیاست عمومی: 100 request در دقیقه
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.User?.Identity?.Name ?? context.Request.Headers.Host.ToString(),
            factory: partition => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = 100,
                QueueLimit = 0,
                Window = TimeSpan.FromMinutes(1)
            }));

    // سیاست برای Auth endpoints: 5 request در دقیقه
    options.AddFixedWindowLimiter("auth", options =>
    {
        options.PermitLimit = 5;
        options.Window = TimeSpan.FromMinutes(1);
        options.QueueLimit = 0;
    });

    // سیاست برای SignalR: 1000 message در دقیقه
    options.AddFixedWindowLimiter("signalr", options =>
    {
        options.PermitLimit = 1000;
        options.Window = TimeSpan.FromMinutes(1);
        options.QueueLimit = 0;
    });
    
    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        await context.HttpContext.Response.WriteAsJsonAsync(
            new { error = "تعداد درخواست‌ها بیش از حد مجاز است. لطفاً کمی صبر کنید." },
            cancellationToken: cancellationToken);
    };
});

// بعد از app.Build()
app.UseRateLimiter();

// استفاده در endpoints:
// Auth endpoints
authGroup.MapPost("/request-otp", RequestOtp)
    .RequireRateLimiting("auth");
```

**Migration**: نیازی به migration دیتابیس ندارد. فقط deployment جدید.

**Rollback**: حذف middleware از pipeline.

---

### 3. Admin Policy وجود ندارد

**مشکل**: Endpoints مدیریتی (مثل مدیریت پشتیبان‌ها) فقط `RequireAuthorization()` دارند.

**خطر**: 
- هر کاربر احراز هویت شده می‌تواند به منابع admin دسترسی پیدا کند
- سطح ریسک: 🔴 **بحرانی**

**راه حل**:

```csharp
// فایل: src/Infrastructure/DependencyInjection.cs
// اضافه کردن policy

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(Policies.CanPurge, policy => policy.RequireRole(Roles.Administrator));
    options.AddPolicy("Agent", policy => policy.RequireRole("Agent"));
    
    // Policy جدید برای Admin
    options.AddPolicy("AdminOnly", policy => policy.RequireRole(Roles.Administrator));
});

// فایل: src/Web/Endpoints/Support.cs
// به‌روزرسانی endpoints

group.MapGet("/agents", GetAllAgents)
    .RequireAuthorization("AdminOnly");

group.MapPost("/agents", CreateAgent)
    .RequireAuthorization("AdminOnly");

group.MapPut("/agents/{agentId}", UpdateAgent)
    .RequireAuthorization("AdminOnly");

group.MapDelete("/agents/{agentId}", DeleteAgent)
    .RequireAuthorization("AdminOnly");
```

**Migration**: نیازی به migration دیتابیس ندارد.

**Rollback**: برگرداندن به `RequireAuthorization()` ساده.

---

## 🟡 مسائل مهم (High Priority)

### 4. JWT Key ضعیف و Hardcoded

**مشکل**: JWT key از یک توکن Firebase استفاده می‌کند و در appsettings است.

**خطر**:
- امکان forge کردن توکن‌ها
- سطح ریسک: 🟡 **بالا**

**راه حل**:

```bash
# تولید کلید قوی
openssl rand -base64 64

# ذخیره در Key Vault
az keyvault secret set --vault-name [keyvault-name] --name "JwtChat--Key" --value "[generated-key]"
```

```csharp
// بررسی طول کلید
if (builder.Configuration["JwtChat:Key"]?.Length < 32)
{
    throw new InvalidOperationException("JWT Key must be at least 32 characters");
}
```

---

### 5. SQL Injection Risk در Raw Queries

**مشکل**: بعضی query‌ها ممکن است از string interpolation استفاده کنند.

**راه حل**: بررسی تمام query‌ها و استفاده از parameterized queries یا EF LINQ.

```csharp
// ❌ اشتباه
var users = await _context.KciUsers
    .FromSqlRaw($"SELECT * FROM KciUsers WHERE Id = {userId}")
    .ToListAsync();

// ✅ صحیح
var users = await _context.KciUsers
    .FromSqlInterpolated($"SELECT * FROM KciUsers WHERE Id = {userId}")
    .ToListAsync();

// یا بهتر
var users = await _context.KciUsers
    .Where(u => u.Id == userId)
    .ToListAsync();
```

---

### 6. XSS Risk در Message Content

**مشکل**: محتوای پیام‌ها ممکن است بدون sanitization نمایش داده شوند.

**راه حل**:

```javascript
// فایل: src/Web/ClientApp/src/components/Chat/MessageItem.jsx
// استفاده از DOMPurify برای sanitize کردن HTML

import DOMPurify from 'dompurify';

function MessageContent({ content }) {
    const sanitized = DOMPurify.sanitize(content, {
        ALLOWED_TAGS: ['b', 'i', 'em', 'strong', 'a'],
        ALLOWED_ATTR: ['href']
    });
    
    return <div dangerouslySetInnerHTML={{ __html: sanitized }} />;
}
```

---

### 7. File Upload Vulnerabilities

**مشکل**: فایل‌های آپلود شده بدون بررسی نوع و اندازه ذخیره می‌شوند.

**راه حل**:

```csharp
// فایل: src/Infrastructure/Service/FileStorageService.cs

public async Task<string> SaveFileAsync(IFormFile file, int userId)
{
    // بررسی اندازه (حداکثر 10MB)
    if (file.Length > 10 * 1024 * 1024)
    {
        throw new ValidationException("حجم فایل نباید بیشتر از 10 مگابایت باشد");
    }

    // بررسی نوع فایل مجاز
    var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".pdf", ".doc", ".docx", ".xls", ".xlsx" };
    var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
    
    if (!allowedExtensions.Contains(extension))
    {
        throw new ValidationException("نوع فایل مجاز نیست");
    }

    // بررسی content type واقعی فایل (نه فقط extension)
    var fileSignature = await GetFileSignatureAsync(file);
    if (!IsValidFileSignature(fileSignature, extension))
    {
        throw new ValidationException("محتوای فایل با پسوند آن مطابقت ندارد");
    }

    // استفاده از GUID برای نام فایل (جلوگیری از path traversal)
    var fileName = $"{Guid.NewGuid()}{extension}";
    var userFolder = Path.Combine(_uploadsPath, userId.ToString());
    Directory.CreateDirectory(userFolder);
    
    var filePath = Path.Combine(userFolder, fileName);
    
    // ذخیره فایل
    using var stream = new FileStream(filePath, FileMode.Create);
    await file.CopyToAsync(stream);
    
    return $"/uploads/{userId}/{fileName}";
}

private async Task<byte[]> GetFileSignatureAsync(IFormFile file)
{
    using var stream = file.OpenReadStream();
    var bytes = new byte[8];
    await stream.ReadAsync(bytes, 0, 8);
    return bytes;
}

private bool IsValidFileSignature(byte[] signature, string extension)
{
    // بررسی signature فایل
    // JPEG: FF D8 FF
    // PNG: 89 50 4E 47
    // PDF: 25 50 44 46
    // و غیره...
    
    return extension switch
    {
        ".jpg" or ".jpeg" => signature[0] == 0xFF && signature[1] == 0xD8 && signature[2] == 0xFF,
        ".png" => signature[0] == 0x89 && signature[1] == 0x50 && signature[2] == 0x4E && signature[3] == 0x47,
        ".pdf" => signature[0] == 0x25 && signature[1] == 0x50 && signature[2] == 0x44 && signature[3] == 0x46,
        _ => true // فعلاً برای سایر فرمت‌ها
    };
}
```

---

### 8. CORS Misconfiguration

**مشکل**: CORS برای تمام origin‌ها باز است.

**راه حل**:

```csharp
// فایل: src/Web/DependencyInjection.cs

services.AddCors(options =>
{
    options.AddPolicy("ChatSupportApp",
        policy =>
        {
            var allowedOrigins = builder.Configuration["AllowedOrigins"]?.Split(';') ?? Array.Empty<string>();
            
            policy.WithOrigins(allowedOrigins)
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials()  // برای SignalR ضروری است
                  .SetIsOriginAllowedToAllowWildcardSubdomains();
        });
});
```

---

## 🟢 بهبودهای توصیه شده (Recommended)

### 9. Password Hashing برای OTP

**توصیه**: OTP‌ها را هش کنید قبل از ذخیره در دیتابیس.

```csharp
// استفاده از BCrypt که از قبل در پروژه وجود دارد
var hashedOtp = BCrypt.Net.BCrypt.HashPassword(otp);
```

---

### 10. Audit Logging برای عملیات حساس

**توصیه**: لاگ کامل برای login، تغییر نقش، حذف داده و غیره.

```csharp
// فایل جدید: src/Application/Common/Behaviours/AuditLoggingBehaviour.cs

public class AuditLoggingBehaviour<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly ILogger<AuditLoggingBehaviour<TRequest, TResponse>> _logger;
    private readonly IUser _user;

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        // لاگ برای command‌های مهم
        if (IsAuditableCommand(request))
        {
            _logger.LogInformation(
                "Audit: User {UserId} executing {CommandName} at {Timestamp}",
                _user.Id,
                typeof(TRequest).Name,
                DateTime.UtcNow);
        }

        var response = await next();

        return response;
    }

    private bool IsAuditableCommand(TRequest request)
    {
        // command‌های حساس
        var auditableCommands = new[]
        {
            "DeleteTodoList",
            "PurgeTodoLists",
            "DeleteSupportAgent",
            "CreateSupportAgent",
            "DeleteMessage"
        };

        return auditableCommands.Any(c => typeof(TRequest).Name.Contains(c));
    }
}
```

---

### 11. Content Security Policy (CSP)

**توصیه**: افزودن CSP headers برای جلوگیری از XSS.

```csharp
// فایل: src/Web/Program.cs

app.Use(async (context, next) =>
{
    context.Response.Headers.Append("Content-Security-Policy",
        "default-src 'self'; " +
        "script-src 'self' 'unsafe-inline' 'unsafe-eval'; " +
        "style-src 'self' 'unsafe-inline'; " +
        "img-src 'self' data: https:; " +
        "font-src 'self' data:; " +
        "connect-src 'self' wss: https:;");
    
    context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Append("X-Frame-Options", "DENY");
    context.Response.Headers.Append("X-XSS-Protection", "1; mode=block");
    context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
    
    await next();
});
```

---

## 📋 Checklist قبل از Production

### امنیت Configuration
- [ ] تمام secrets در Azure Key Vault هستند
- [ ] Connection string‌ها از Key Vault می‌آیند
- [ ] JWT key قوی است (حداقل 32 کاراکتر)
- [ ] CORS فقط برای domain‌های مجاز باز است
- [ ] HTTPS اجباری است (HSTS enabled)

### احراز هویت و مجوز
- [ ] Admin policy برای endpoint‌های مدیریتی فعال است
- [ ] JWT expiration time مناسب است (نه خیلی کم، نه خیلی زیاد)
- [ ] Rate limiting برای login و OTP فعال است
- [ ] Audit logging برای عملیات حساس فعال است

### Input Validation
- [ ] تمام ورودی‌های کاربر validate می‌شوند
- [ ] File upload محدودیت اندازه و نوع دارد
- [ ] SQL injection تست شده است
- [ ] XSS تست شده است

### Network Security
- [ ] Rate limiting فعال است
- [ ] DDoS protection در Azure فعال است
- [ ] Web Application Firewall (WAF) فعال است

### Monitoring
- [ ] لاگ‌های امنیتی در Application Insights ذخیره می‌شوند
- [ ] Alert برای failed login attempts وجود دارد
- [ ] Alert برای unusual traffic pattern وجود دارد

---

## 🔍 ابزارهای بررسی امنیت

### توصیه‌های Tooling
1. **OWASP Dependency Check**: بررسی آسیب‌پذیری‌های NuGet packages
2. **SonarQube**: Static code analysis
3. **ZAP (OWASP ZAP)**: Dynamic security testing
4. **Snyk**: Continuous security monitoring
5. **Azure Security Center**: Cloud security posture management

---

## 📅 برنامه پیاده‌سازی

### فاز 1 (هفته 1): مسائل بحرانی
- [ ] انتقال secrets به Key Vault
- [ ] پیاده‌سازی rate limiting
- [ ] اضافه کردن admin policy

### فاز 2 (هفته 2): مسائل مهم
- [ ] تقویت JWT key
- [ ] بررسی و رفع SQL injection
- [ ] پیاده‌سازی XSS protection
- [ ] تقویت file upload security

### فاز 3 (هفته 3): بهبودهای توصیه شده
- [ ] Audit logging
- [ ] CSP headers
- [ ] Security monitoring

### فاز 4 (هفته 4): تست و documentation
- [ ] Penetration testing
- [ ] Security documentation
- [ ] Team training

---

**تهیه‌کننده**: تیم Security/DevOps  
**تاریخ**: {تاریخ}  
**نسخه**: 1.0  
**بررسی بعدی**: هر 3 ماه یکبار
