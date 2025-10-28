# اسکریپت رفع مشکل 405 Method Not Allowed برای DELETE در IIS
# این اسکریپت باید در سرور به عنوان Administrator اجرا شود

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "اسکریپت رفع مشکل DELETE Method در IIS" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# بررسی اجرا به عنوان Administrator
$isAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Write-Host "❌ خطا: این اسکریپت باید به عنوان Administrator اجرا شود" -ForegroundColor Red
    Write-Host "راست کلیک روی PowerShell → Run as Administrator" -ForegroundColor Yellow
    exit 1
}

Write-Host "✅ اسکریپت با دسترسی Administrator در حال اجرا است" -ForegroundColor Green
Write-Host ""

# Import WebAdministration Module
try {
    Import-Module WebAdministration -ErrorAction Stop
    Write-Host "✅ ماژول WebAdministration بارگذاری شد" -ForegroundColor Green
} catch {
    Write-Host "❌ خطا در بارگذاری WebAdministration Module" -ForegroundColor Red
    Write-Host "نصب IIS Management Scripts: " -ForegroundColor Yellow
    Write-Host "Install-WindowsFeature -Name Web-Scripting-Tools" -ForegroundColor Cyan
    exit 1
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "مرحله 1: حذف WebDAV Module" -ForegroundColor Yellow
Write-Host "========================================" -ForegroundColor Cyan

# حذف WebDAV Module از Global Modules
try {
    $webdavModule = Get-WebConfigurationProperty -pspath 'MACHINE/WEBROOT/APPHOST' -filter "system.webServer/modules" -name "." | Where-Object { $_.name -eq 'WebDAVModule' }
    
    if ($webdavModule) {
        Remove-WebConfigurationProperty -pspath 'MACHINE/WEBROOT/APPHOST' -filter "system.webServer/modules" -name "." -AtElement @{name='WebDAVModule'}
        Write-Host "✅ WebDAVModule از Global Modules حذف شد" -ForegroundColor Green
    } else {
        Write-Host "ℹ️  WebDAVModule از قبل وجود ندارد" -ForegroundColor Gray
    }
} catch {
    Write-Host "⚠️  خطا در حذف WebDAVModule: $($_.Exception.Message)" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "مرحله 2: حذف WebDAV Handler" -ForegroundColor Yellow
Write-Host "========================================" -ForegroundColor Cyan

# حذف WebDAV Handler
try {
    $webdavHandler = Get-WebConfigurationProperty -pspath 'MACHINE/WEBROOT/APPHOST' -filter "system.webServer/handlers" -name "." | Where-Object { $_.name -eq 'WebDAV' }
    
    if ($webdavHandler) {
        Remove-WebConfigurationProperty -pspath 'MACHINE/WEBROOT/APPHOST' -filter "system.webServer/handlers" -name "." -AtElement @{name='WebDAV'}
        Write-Host "✅ WebDAV Handler حذف شد" -ForegroundColor Green
    } else {
        Write-Host "ℹ️  WebDAV Handler از قبل وجود ندارد" -ForegroundColor Gray
    }
} catch {
    Write-Host "⚠️  خطا در حذف WebDAV Handler: $($_.Exception.Message)" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "مرحله 3: فعال‌سازی DELETE Verb" -ForegroundColor Yellow
Write-Host "========================================" -ForegroundColor Cyan

# فعال کردن allowUnlisted برای verbs
try {
    Set-WebConfigurationProperty -pspath 'MACHINE/WEBROOT/APPHOST' -filter "system.webServer/security/requestFiltering/verbs" -name "allowUnlisted" -value $true
    Write-Host "✅ allowUnlisted برای HTTP Verbs فعال شد" -ForegroundColor Green
} catch {
    Write-Host "⚠️  خطا در تنظیم allowUnlisted: $($_.Exception.Message)" -ForegroundColor Yellow
}

# اضافه کردن DELETE verb به صورت صریح
try {
    $deleteVerb = Get-WebConfigurationProperty -pspath 'MACHINE/WEBROOT/APPHOST' -filter "system.webServer/security/requestFiltering/verbs" -name "." | Where-Object { $_.verb -eq 'DELETE' }
    
    if (-not $deleteVerb) {
        Add-WebConfigurationProperty -pspath 'MACHINE/WEBROOT/APPHOST' -filter "system.webServer/security/requestFiltering/verbs" -name "." -value @{verb='DELETE';allowed='True'}
        Write-Host "✅ DELETE verb به صورت صریح اضافه شد" -ForegroundColor Green
    } else {
        Write-Host "ℹ️  DELETE verb از قبل تنظیم شده است" -ForegroundColor Gray
    }
} catch {
    Write-Host "⚠️  خطا در اضافه کردن DELETE verb: $($_.Exception.Message)" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "مرحله 4: Restart Application Pools" -ForegroundColor Yellow
Write-Host "========================================" -ForegroundColor Cyan

# لیست Application Pools مرتبط با ASP.NET Core
$appPools = Get-ChildItem IIS:\AppPools | Where-Object { $_.managedRuntimeVersion -eq '' -or $_.managedRuntimeVersion -eq 'No Managed Code' }

if ($appPools.Count -eq 0) {
    Write-Host "⚠️  هیچ Application Pool با .NET Core پیدا نشد" -ForegroundColor Yellow
    Write-Host "لطفاً نام Application Pool خود را وارد کنید (یا Enter برای رد کردن):" -ForegroundColor Cyan
    $poolName = Read-Host
    
    if ($poolName) {
        $appPools = @(Get-Item "IIS:\AppPools\$poolName" -ErrorAction SilentlyContinue)
    }
}

foreach ($pool in $appPools) {
    try {
        Write-Host "🔄 در حال Restart: $($pool.Name)..." -ForegroundColor Cyan
        Restart-WebAppPool -Name $pool.Name
        Write-Host "✅ $($pool.Name) restart شد" -ForegroundColor Green
    } catch {
        Write-Host "⚠️  خطا در restart: $($pool.Name) - $($_.Exception.Message)" -ForegroundColor Yellow
    }
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "مرحله 5: بررسی WebDAV Feature" -ForegroundColor Yellow
Write-Host "========================================" -ForegroundColor Cyan

# بررسی نصب بودن WebDAV Feature
try {
    $webdavFeature = Get-WindowsFeature -Name Web-DAV-Publishing -ErrorAction SilentlyContinue
    
    if ($webdavFeature -and $webdavFeature.Installed) {
        Write-Host "⚠️  WebDAV Publishing Feature نصب شده است" -ForegroundColor Yellow
        Write-Host "آیا می‌خواهید آن را Uninstall کنید؟ (Y/N)" -ForegroundColor Cyan
        $response = Read-Host
        
        if ($response -eq 'Y' -or $response -eq 'y') {
            Uninstall-WindowsFeature -Name Web-DAV-Publishing -Remove
            Write-Host "✅ WebDAV Publishing Feature حذف شد" -ForegroundColor Green
        } else {
            Write-Host "⏭️  WebDAV Feature همچنان نصب است" -ForegroundColor Gray
        }
    } else {
        Write-Host "✅ WebDAV Publishing Feature نصب نیست" -ForegroundColor Green
    }
} catch {
    Write-Host "ℹ️  بررسی Windows Feature ممکن نیست (احتمالاً Windows Server نیست)" -ForegroundColor Gray
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host "✅ تنظیمات با موفقیت اعمال شدند!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""
Write-Host "مراحل بعدی:" -ForegroundColor Yellow
Write-Host "1. مطمئن شوید که فایل web.config جدید در سرور deploy شده است" -ForegroundColor White
Write-Host "2. Application را مجدداً publish کنید" -ForegroundColor White
Write-Host "3. DELETE endpoints را تست کنید" -ForegroundColor White
Write-Host ""
Write-Host "برای تست:" -ForegroundColor Cyan
Write-Host 'Invoke-WebRequest -Uri "https://chat.abrik.cloud/api/chat/messages/123" -Method DELETE -Headers @{"Authorization"="Bearer YOUR_TOKEN"}' -ForegroundColor Gray
Write-Host ""

# خلاصه تغییرات
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "خلاصه تغییرات:" -ForegroundColor Yellow
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "✅ WebDAVModule حذف شد" -ForegroundColor Green
Write-Host "✅ WebDAV Handler حذف شد" -ForegroundColor Green
Write-Host "✅ allowUnlisted Verbs فعال شد" -ForegroundColor Green
Write-Host "✅ DELETE verb به صورت صریح اجازه داده شد" -ForegroundColor Green
Write-Host "✅ Application Pool(s) restart شد" -ForegroundColor Green
Write-Host ""

Read-Host "Press Enter to exit"
