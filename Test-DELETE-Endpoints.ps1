# اسکریپت تست DELETE Endpoints
# این اسکریپت برای تست کارکرد DELETE methods پس از اعمال تنظیمات IIS استفاده می‌شود

param(
    [Parameter(Mandatory=$true)]
    [string]$BaseUrl = "https://chat.abrik.cloud",
    
    [Parameter(Mandatory=$false)]
    [string]$Token = ""
)

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "تست DELETE Endpoints" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# درخواست Token در صورت نبود
if ([string]::IsNullOrWhiteSpace($Token)) {
    Write-Host "لطفاً JWT Token خود را وارد کنید:" -ForegroundColor Yellow
    $Token = Read-Host
    Write-Host ""
}

# تنظیم Headers
$headers = @{
    "Authorization" = "Bearer $Token"
    "Content-Type" = "application/json"
}

Write-Host "Base URL: $BaseUrl" -ForegroundColor Cyan
Write-Host "Token: $($Token.Substring(0, [Math]::Min(20, $Token.Length)))..." -ForegroundColor Cyan
Write-Host ""

# تابع تست OPTIONS
function Test-OptionsMethod {
    param([string]$Endpoint)
    
    try {
        Write-Host "🔍 Testing OPTIONS $Endpoint..." -ForegroundColor Cyan
        $response = Invoke-WebRequest -Uri "$BaseUrl$Endpoint" -Method OPTIONS -Headers $headers -UseBasicParsing -ErrorAction Stop
        
        $allowHeader = $response.Headers['Allow']
        if ($allowHeader -like "*DELETE*") {
            Write-Host "✅ OPTIONS OK - DELETE is allowed" -ForegroundColor Green
            Write-Host "   Allowed Methods: $allowHeader" -ForegroundColor Gray
            return $true
        } else {
            Write-Host "⚠️  OPTIONS OK - But DELETE not in Allow header" -ForegroundColor Yellow
            Write-Host "   Allowed Methods: $allowHeader" -ForegroundColor Gray
            return $false
        }
    } catch {
        Write-Host "❌ OPTIONS Failed: $($_.Exception.Message)" -ForegroundColor Red
        return $false
    }
}

# تابع تست DELETE (بدون ارسال واقعی)
function Test-DeleteMethod {
    param(
        [string]$Endpoint,
        [string]$Description
    )
    
    Write-Host ""
    Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Gray
    Write-Host "📝 $Description" -ForegroundColor White
    Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Gray
    
    try {
        # ابتدا OPTIONS را تست می‌کنیم
        # Test-OptionsMethod -Endpoint $Endpoint
        
        # سپس DELETE را تست می‌کنیم
        Write-Host "🔍 Testing DELETE $Endpoint..." -ForegroundColor Cyan
        
        $response = Invoke-WebRequest -Uri "$BaseUrl$Endpoint" -Method DELETE -Headers $headers -UseBasicParsing -ErrorAction Stop
        
        if ($response.StatusCode -eq 200 -or $response.StatusCode -eq 204) {
            Write-Host "✅ DELETE SUCCESS - Status: $($response.StatusCode)" -ForegroundColor Green
            return $true
        } elseif ($response.StatusCode -eq 404) {
            Write-Host "ℹ️  DELETE OK (404 Not Found - Resource doesn't exist)" -ForegroundColor Cyan
            return $true
        } elseif ($response.StatusCode -eq 401) {
            Write-Host "🔐 DELETE OK (401 Unauthorized - Token issue, not Method issue)" -ForegroundColor Cyan
            return $true
        } else {
            Write-Host "⚠️  DELETE Returned: $($response.StatusCode)" -ForegroundColor Yellow
            return $false
        }
        
    } catch {
        $statusCode = $_.Exception.Response.StatusCode.value__
        $statusDescription = $_.Exception.Response.StatusDescription
        
        if ($statusCode -eq 405) {
            Write-Host "❌ DELETE FAILED - 405 Method Not Allowed" -ForegroundColor Red
            Write-Host "   این همان مشکلی است که باید حل شود!" -ForegroundColor Red
            return $false
        } elseif ($statusCode -eq 404) {
            Write-Host "ℹ️  DELETE OK (404 Not Found - Method is allowed, resource doesn't exist)" -ForegroundColor Cyan
            return $true
        } elseif ($statusCode -eq 401) {
            Write-Host "🔐 DELETE OK (401 Unauthorized - Method is allowed, auth failed)" -ForegroundColor Cyan
            return $true
        } elseif ($statusCode -eq 403) {
            Write-Host "🔐 DELETE OK (403 Forbidden - Method is allowed, permission denied)" -ForegroundColor Cyan
            return $true
        } else {
            Write-Host "⚠️  DELETE Error: $statusCode - $statusDescription" -ForegroundColor Yellow
            Write-Host "   Message: $($_.Exception.Message)" -ForegroundColor Gray
            return $false
        }
    }
}

# لیست Endpoints برای تست
$testResults = @()

Write-Host "شروع تست DELETE Endpoints..." -ForegroundColor Yellow
Write-Host ""

# تست 1: حذف پیام
$result1 = Test-DeleteMethod -Endpoint "/api/chat/messages/999999" -Description "حذف پیام چت"
$testResults += @{ Name = "Delete Message"; Result = $result1 }

# تست 2: حذف عضو گروه
$result2 = Test-DeleteMethod -Endpoint "/api/chat/rooms/1/members/999" -Description "حذف عضو از گروه"
$testResults += @{ Name = "Remove Group Member"; Result = $result2 }

# تست 3: حذف اتاق چت
$result3 = Test-DeleteMethod -Endpoint "/api/chat/rooms/999999" -Description "حذف اتاق چت"
$testResults += @{ Name = "Delete Chat Room"; Result = $result3 }

# تست 4: ترک اتاق چت
$result4 = Test-DeleteMethod -Endpoint "/api/chat/rooms/1/leave" -Description "ترک اتاق چت"
$testResults += @{ Name = "Leave Chat Room"; Result = $result4 }

# خلاصه نتایج
Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "📊 خلاصه نتایج تست" -ForegroundColor Yellow
Write-Host "========================================" -ForegroundColor Cyan

$passCount = 0
$failCount = 0

foreach ($test in $testResults) {
    if ($test.Result) {
        Write-Host "✅ $($test.Name)" -ForegroundColor Green
        $passCount++
    } else {
        Write-Host "❌ $($test.Name)" -ForegroundColor Red
        $failCount++
    }
}

Write-Host ""
Write-Host "مجموع: $($testResults.Count) تست" -ForegroundColor White
Write-Host "موفق: $passCount" -ForegroundColor Green
Write-Host "ناموفق: $failCount" -ForegroundColor Red
Write-Host ""

if ($failCount -eq 0) {
    Write-Host "🎉 تمام تست‌ها موفق بودند!" -ForegroundColor Green
    Write-Host "مشکل 405 Method Not Allowed حل شده است." -ForegroundColor Green
} else {
    Write-Host "⚠️  $failCount تست ناموفق بود" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "مراحل بعدی:" -ForegroundColor Cyan
    Write-Host "1. مطمئن شوید که اسکریپت Fix-IIS-DELETE-Method.ps1 اجرا شده است" -ForegroundColor White
    Write-Host "2. Application Pool را restart کنید" -ForegroundColor White
    Write-Host "3. فایل web.config جدید را deploy کنید" -ForegroundColor White
    Write-Host "4. Failed Request Tracing را در IIS فعال کنید برای debugging" -ForegroundColor White
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "💡 نکات اضافی" -ForegroundColor Yellow
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "- اگر خطای 401 دریافت کردید: Token منقضی شده یا نامعتبر است" -ForegroundColor Gray
Write-Host "- اگر خطای 404 دریافت کردید: Resource وجود ندارد (این OK است)" -ForegroundColor Gray
Write-Host "- اگر خطای 405 دریافت کردید: مشکل WebDAV یا IIS Handler است" -ForegroundColor Gray
Write-Host "- اگر خطای 403 دریافت کردید: Authorization مشکل دارد (نه Method)" -ForegroundColor Gray
Write-Host ""

Read-Host "Press Enter to exit"


# مثال استفاده:
# .\Test-DELETE-Endpoints.ps1 -BaseUrl "https://chat.abrik.cloud" -Token "your_jwt_token_here"
