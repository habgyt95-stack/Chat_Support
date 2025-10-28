# راهنمای شروع سریع (Quick Start Guide)

> این سند برای شروع سریع پیاده‌سازی بهبودها طراحی شده است.

---

## 🚀 گام اول: بررسی و Merge PR

### 1. مرور تغییرات
```bash
# Clone repository
git clone https://github.com/habgyt95-stack/Chat_Support.git
cd Chat_Support/Chat_Support

# Checkout PR branch
git checkout copilot/improve-code-infrastructure

# بررسی تغییرات
git diff main...copilot/improve-code-infrastructure --stat
```

### 2. Build و Test
```bash
# Build
dotnet build -c Release

# Run tests (اختیاری)
dotnet test --filter "FullyQualifiedName!~AcceptanceTests"
```

### 3. Merge به main
```bash
# وقتی آماده شد
git checkout main
git merge copilot/improve-code-infrastructure
git push origin main
```

---

## ⚡ گام دوم: پیاده‌سازی اولین بهبود (Azure Key Vault)

**زمان**: 4-6 ساعت  
**اولویت**: 🔴 بحرانی

### پیش‌نیاز
```bash
# نصب Azure CLI (اگر ندارید)
curl -sL https://aka.ms/InstallAzureCLIDeb | sudo bash

# Login
az login
```

### مراحل

#### 1. تنظیم متغیرها
```bash
# تنظیم این متغیرها برای محیط خودتان
RESOURCE_GROUP="chat-support-prod-rg"
LOCATION="eastus"
KEYVAULT_NAME="chat-support-kv-prod"
APP_NAME="chat-support-prod"
```

#### 2. ایجاد Key Vault
```bash
# ایجاد Resource Group (اگر وجود ندارد)
az group create --name $RESOURCE_GROUP --location $LOCATION

# ایجاد Key Vault
az keyvault create \
  --name $KEYVAULT_NAME \
  --resource-group $RESOURCE_GROUP \
  --location $LOCATION \
  --enable-soft-delete true \
  --enable-purge-protection true
```

#### 3. افزودن Secrets
```bash
# تولید JWT Key قوی
JWT_KEY=$(openssl rand -base64 64)

# افزودن secrets
az keyvault secret set --vault-name $KEYVAULT_NAME --name "ConnectionStrings--Chat-SupportDb" --value "[YOUR_CONNECTION_STRING]"
az keyvault secret set --vault-name $KEYVAULT_NAME --name "JwtChat--Key" --value "$JWT_KEY"
az keyvault secret set --vault-name $KEYVAULT_NAME --name "JwtChat--Issuer" --value "chat-support-prod"
az keyvault secret set --vault-name $KEYVAULT_NAME --name "JwtChat--Audience" --value "chat-support-app"
az keyvault secret set --vault-name $KEYVAULT_NAME --name "Kavenegar--ApiKey" --value "[YOUR_KAVENEGAR_API_KEY]"
```

#### 4. تنظیم App Service
```bash
# Enable Managed Identity
az webapp identity assign --name $APP_NAME --resource-group $RESOURCE_GROUP

# دریافت Principal ID
PRINCIPAL_ID=$(az webapp identity show --name $APP_NAME --resource-group $RESOURCE_GROUP --query principalId -o tsv)

# دادن دسترسی به Key Vault
az keyvault set-policy \
  --name $KEYVAULT_NAME \
  --object-id $PRINCIPAL_ID \
  --secret-permissions get list
```

#### 5. تنظیم Application
```bash
# افزودن Key Vault endpoint به App Settings
az webapp config appsettings set \
  --name $APP_NAME \
  --resource-group $RESOURCE_GROUP \
  --settings AZURE_KEY_VAULT_ENDPOINT="https://${KEYVAULT_NAME}.vault.azure.net/"
```

#### 6. اضافه کردن NuGet Package
```bash
cd src/Web
dotnet add package Azure.Extensions.AspNetCore.Configuration.Secrets
dotnet add package Azure.Identity
```

#### 7. به‌روزرسانی کد
کد زیر را به `src/Web/Program.cs` اضافه کنید (قبل از `var app = builder.Build();`):

```csharp
// اضافه کردن Azure Key Vault configuration
if (!builder.Environment.IsDevelopment())
{
    var keyVaultEndpoint = builder.Configuration["AZURE_KEY_VAULT_ENDPOINT"];
    if (!string.IsNullOrEmpty(keyVaultEndpoint))
    {
        builder.Configuration.AddAzureKeyVault(
            new Uri(keyVaultEndpoint),
            new DefaultAzureCredential());
        
        builder.Services.AddSingleton<ILogger>(sp => 
            sp.GetRequiredService<ILoggerFactory>().CreateLogger("Startup"))
            .BuildServiceProvider()
            .GetRequiredService<ILogger>()
            .LogInformation("Azure Key Vault configuration loaded from {Endpoint}", keyVaultEndpoint);
    }
}
```

#### 8. پاک کردن Secrets از appsettings.json
```json
// فایل: src/Web/appsettings.json
// حذف این بخش‌ها (secrets اکنون از Key Vault می‌آیند)
{
  "ConnectionStrings": {
    // حذف شد - از Key Vault می‌آید
  },
  "JwtChat": {
    "Key": "", // حذف شد - از Key Vault می‌آید
    "Issuer": "", // حذف شد - از Key Vault می‌آید
    "Audience": "", // حذف شد - از Key Vault می‌آید
    "AccessTokenExpirationMinutes": 10080,
    "RefreshTokenExpirationDays": 30
  },
  "Kavenegar": {
    "ApiKey": "" // حذف شد - از Key Vault می‌آید
  }
}
```

#### 9. Build و Deploy
```bash
# Build
dotnet publish -c Release -o ./publish

# Deploy
cd publish
zip -r ../app.zip .
cd ..

az webapp deploy \
  --resource-group $RESOURCE_GROUP \
  --name $APP_NAME \
  --src-path app.zip \
  --type zip
```

#### 10. Verification
```bash
# بررسی لاگ‌ها
az webapp log tail --name $APP_NAME --resource-group $RESOURCE_GROUP

# باید پیام زیر را ببینید:
# "Azure Key Vault configuration loaded from https://chat-support-kv-prod.vault.azure.net/"

# تست health check
curl https://${APP_NAME}.azurewebsites.net/health
```

---

## 📊 گام سوم: اعمال Database Indexes

**زمان**: 30 دقیقه - 1 ساعت  
**اولویت**: 🔴 بحرانی

### مراحل

#### 1. Backup Database
```bash
# ایجاد backup قبل از migration
az sql db export \
  --resource-group $RESOURCE_GROUP \
  --server [your-sql-server] \
  --name Chat_SupportDb \
  --admin-user sqladmin \
  --admin-password [password] \
  --storage-key [storage-key] \
  --storage-key-type StorageAccessKey \
  --storage-uri "https://[storage-account].blob.core.windows.net/backups/pre-index-$(date +%Y%m%d).bacpac"
```

#### 2. اجرای Migration Script
```bash
# اتصال به database
sqlcmd -S [server].database.windows.net -d Chat_SupportDb -U sqladmin -P [password]

# اجرای script
:r docs/migrations/AddPerformanceIndexes.sql
GO
```

#### 3. Verification
```sql
-- بررسی indexes ایجاد شده
SELECT 
    OBJECT_NAME(i.object_id) AS TableName,
    i.name AS IndexName,
    i.type_desc AS IndexType
FROM sys.indexes i
WHERE i.name LIKE 'IX_%'
    AND OBJECT_NAME(i.object_id) IN ('ChatRoomMembers', 'ChatMessages', 'MessageStatus')
ORDER BY TableName, IndexName;

-- تست query performance
SET STATISTICS TIME ON;
SET STATISTICS IO ON;

SELECT * FROM ChatRoomMembers WHERE UserId = 1;

SET STATISTICS TIME OFF;
SET STATISTICS IO OFF;
```

---

## 🎯 گام چهارم: تنظیم Monitoring

**زمان**: 2-3 ساعت  
**اولویت**: 🟡 بالا

### مراحل

#### 1. ایجاد Application Insights
```bash
az monitor app-insights component create \
  --app "${APP_NAME}-insights" \
  --location $LOCATION \
  --resource-group $RESOURCE_GROUP \
  --application-type web

# دریافت connection string
INSIGHTS_CONNECTION=$(az monitor app-insights component show \
  --app "${APP_NAME}-insights" \
  --resource-group $RESOURCE_GROUP \
  --query connectionString -o tsv)

# تنظیم در App Service
az webapp config appsettings set \
  --name $APP_NAME \
  --resource-group $RESOURCE_GROUP \
  --settings APPLICATIONINSIGHTS_CONNECTION_STRING="$INSIGHTS_CONNECTION"
```

#### 2. ایجاد Action Group
```bash
az monitor action-group create \
  --name "chat-support-alerts" \
  --resource-group $RESOURCE_GROUP \
  --short-name "ChatAlert" \
  --email-receiver name=DevOps email=devops@company.com
```

#### 3. ایجاد Alert Rules
```bash
# Service Unhealthy Alert
az monitor metrics alert create \
  --name "Service-Unhealthy" \
  --resource-group $RESOURCE_GROUP \
  --scopes "/subscriptions/[sub-id]/resourceGroups/${RESOURCE_GROUP}/providers/Microsoft.Web/sites/${APP_NAME}" \
  --condition "avg HealthCheckStatus < 1" \
  --description "سرویس unhealthy است" \
  --evaluation-frequency 1m \
  --window-size 5m \
  --severity 0 \
  --action chat-support-alerts

# High Response Time Alert
az monitor metrics alert create \
  --name "High-Response-Time" \
  --resource-group $RESOURCE_GROUP \
  --scopes "/subscriptions/[sub-id]/resourceGroups/${RESOURCE_GROUP}/providers/Microsoft.Web/sites/${APP_NAME}" \
  --condition "avg HttpResponseTime > 2000" \
  --description "زمان پاسخ بالا است" \
  --evaluation-frequency 5m \
  --window-size 10m \
  --severity 2 \
  --action chat-support-alerts
```

---

## ✅ Checklist

### بعد از Key Vault
- [ ] تمام secrets از appsettings.json حذف شدند
- [ ] App Service به Key Vault دسترسی دارد
- [ ] Application با موفقیت start می‌شود
- [ ] لاگ "Azure Key Vault configuration loaded" نمایش داده می‌شود

### بعد از Database Indexes
- [ ] Backup گرفته شده است
- [ ] Migration script اجرا شده است
- [ ] تمام indexes ایجاد شده‌اند
- [ ] Query performance بهبود یافته است

### بعد از Monitoring
- [ ] Application Insights راه‌اندازی شده است
- [ ] Alert rules ایجاد شده‌اند
- [ ] Action group تنظیم شده است
- [ ] Test alert ارسال شده است

---

## 📞 در صورت مشکل

### مشکل: Key Vault access denied
```bash
# بررسی access policies
az keyvault show --name $KEYVAULT_NAME --resource-group $RESOURCE_GROUP --query properties.accessPolicies

# بررسی managed identity
az webapp identity show --name $APP_NAME --resource-group $RESOURCE_GROUP
```

### مشکل: Database timeout
```bash
# افزایش DTU
az sql db update --resource-group $RESOURCE_GROUP --server [server] --name Chat_SupportDb --service-objective S2
```

### مشکل: Application start نمی‌شود
```bash
# بررسی لاگ‌ها
az webapp log download --name $APP_NAME --resource-group $RESOURCE_GROUP
```

---

## 📚 منابع

- **Security Review**: `docs/SECURITY_REVIEW.md`
- **Top 5 Improvements**: `docs/TOP_5_IMPROVEMENTS.md`
- **Deployment Guide**: `docs/DEPLOYMENT_GUIDE.md`
- **Runbook**: `docs/RUNBOOK.md`
- **Summary**: `docs/SUMMARY.md`

---

## 🎓 نتیجه

با اجرای این 4 گام:
1. ✅ امنیت سیستم به طور قابل توجهی بهبود می‌یابد
2. ✅ عملکرد database بهبود می‌یابد
3. ✅ Monitoring و alerting فعال می‌شود
4. ✅ سیستم برای production آماده می‌شود

**زمان کل**: 8-12 ساعت (1.5-2 روز کاری)

**گام‌های بعدی** (اختیاری اما توصیه می‌شود):
- راه‌اندازی Redis Cache (6-8 ساعت)
- راه‌اندازی Health Checks (4-5 ساعت)
- تنظیم Auto-scaling (2-3 ساعت)

---

**تاریخ**: 2025-01-21  
**نسخه**: 1.0
