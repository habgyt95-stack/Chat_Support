# راهنمای استقرار (Deployment Guide)

## مقدمه

این سند شامل دستورالعمل‌های کامل برای استقرار سیستم چت و پشتیبانی در محیط production است.

---

## 📋 پیش‌نیازها

### نیازمندی‌های فنی
- Azure Subscription با دسترسی مناسب
- .NET 9.0 SDK
- SQL Server 2019 یا بالاتر
- Node.js 20.x یا بالاتر
- Azure CLI نصب شده
- Git

### نیازمندی‌های دسترسی
- دسترسی به Azure Portal
- دسترسی به GitHub Repository
- دسترسی به پنل کاوه نگار
- دسترسی به Firebase Console

---

## 🏗️ معماری Production

```
┌─────────────────────────────────────────────────────────────┐
│                     Azure Front Door                         │
│                   (CDN + WAF + DDoS)                        │
└────────────────────┬────────────────────────────────────────┘
                     │
         ┌───────────┴───────────┐
         │                       │
    ┌────▼─────┐         ┌──────▼───────┐
    │  App     │         │   App        │
    │ Service  │◄───────►│  Service     │
    │ (East)   │         │  (West)      │
    └────┬─────┘         └──────┬───────┘
         │                      │
         └──────────┬───────────┘
                    │
         ┌──────────▼──────────┐
         │   Azure Cache       │
         │   for Redis         │
         │   (SignalR Scale)   │
         └──────────┬──────────┘
                    │
         ┌──────────▼──────────┐
         │   SQL Database      │
         │   (with Replica)    │
         └─────────────────────┘
```

---

## 🚀 مراحل استقرار (گام‌به‌گام)

### مرحله 1: آماده‌سازی Azure Resources

```bash
# 1. تنظیم متغیرها
RESOURCE_GROUP="chat-support-prod-rg"
LOCATION="eastus"
APP_NAME="chat-support-prod"
SQL_SERVER="chat-support-sql-prod"
SQL_DB="Chat_SupportDb"
KEYVAULT_NAME="chat-support-kv-prod"
REDIS_NAME="chat-support-redis-prod"
APP_SERVICE_PLAN="chat-support-plan-prod"

# 2. ایجاد Resource Group
az group create \
  --name $RESOURCE_GROUP \
  --location $LOCATION \
  --tags Environment=Production Project=ChatSupport

# 3. ایجاد Key Vault
az keyvault create \
  --name $KEYVAULT_NAME \
  --resource-group $RESOURCE_GROUP \
  --location $LOCATION \
  --enable-soft-delete true \
  --enable-purge-protection true \
  --retention-days 90

# 4. ایجاد SQL Server و Database
# Generate strong password
SQL_ADMIN_PASSWORD=$(openssl rand -base64 32)

az sql server create \
  --name $SQL_SERVER \
  --resource-group $RESOURCE_GROUP \
  --location $LOCATION \
  --admin-user sqladmin \
  --admin-password "$SQL_ADMIN_PASSWORD"

# Enable Azure Services firewall rule
az sql server firewall-rule create \
  --resource-group $RESOURCE_GROUP \
  --server $SQL_SERVER \
  --name AllowAzureServices \
  --start-ip-address 0.0.0.0 \
  --end-ip-address 0.0.0.0

# Create database
az sql db create \
  --resource-group $RESOURCE_GROUP \
  --server $SQL_SERVER \
  --name $SQL_DB \
  --service-objective S2 \
  --backup-storage-redundancy Geo \
  --zone-redundant false

# ذخیره connection string در Key Vault
SQL_CONNECTION_STRING="Server=tcp:${SQL_SERVER}.database.windows.net,1433;Initial Catalog=${SQL_DB};User ID=sqladmin;Password=${SQL_ADMIN_PASSWORD};Encrypt=True;TrustServerCertificate=False;"

az keyvault secret set \
  --vault-name $KEYVAULT_NAME \
  --name "ConnectionStrings--Chat-SupportDb" \
  --value "$SQL_CONNECTION_STRING"

# 5. ایجاد Redis Cache
az redis create \
  --name $REDIS_NAME \
  --resource-group $RESOURCE_GROUP \
  --location $LOCATION \
  --sku Standard \
  --vm-size C2 \
  --enable-non-ssl-port false \
  --minimum-tls-version 1.2

# دریافت و ذخیره Redis connection string
REDIS_KEY=$(az redis list-keys --name $REDIS_NAME --resource-group $RESOURCE_GROUP --query primaryKey -o tsv)
REDIS_CONNECTION_STRING="${REDIS_NAME}.redis.cache.windows.net:6380,password=${REDIS_KEY},ssl=True,abortConnect=False"

az keyvault secret set \
  --vault-name $KEYVAULT_NAME \
  --name "ConnectionStrings--Redis" \
  --value "$REDIS_CONNECTION_STRING"

# 6. ایجاد App Service Plan و App Service
az appservice plan create \
  --name $APP_SERVICE_PLAN \
  --resource-group $RESOURCE_GROUP \
  --location $LOCATION \
  --sku P1V3 \
  --is-linux

az webapp create \
  --name $APP_NAME \
  --resource-group $RESOURCE_GROUP \
  --plan $APP_SERVICE_PLAN \
  --runtime "DOTNETCORE:9.0"

# فعال کردن Managed Identity
az webapp identity assign \
  --name $APP_NAME \
  --resource-group $RESOURCE_GROUP

# دریافت Principal ID
PRINCIPAL_ID=$(az webapp identity show \
  --name $APP_NAME \
  --resource-group $RESOURCE_GROUP \
  --query principalId -o tsv)

# دادن دسترسی به Key Vault
az keyvault set-policy \
  --name $KEYVAULT_NAME \
  --object-id $PRINCIPAL_ID \
  --secret-permissions get list

# 7. تنظیم Application Settings
az webapp config appsettings set \
  --name $APP_NAME \
  --resource-group $RESOURCE_GROUP \
  --settings \
    AZURE_KEY_VAULT_ENDPOINT="https://${KEYVAULT_NAME}.vault.azure.net/" \
    ASPNETCORE_ENVIRONMENT="Production" \
    WEBSITE_TIME_ZONE="Iran Standard Time"

# 8. فعال کردن Always On و WebSockets
az webapp config set \
  --name $APP_NAME \
  --resource-group $RESOURCE_GROUP \
  --always-on true \
  --web-sockets-enabled true \
  --http20-enabled true \
  --min-tls-version 1.2

# 9. تنظیم HTTPS Only و HSTS
az webapp update \
  --name $APP_NAME \
  --resource-group $RESOURCE_GROUP \
  --https-only true

# 10. تنظیم Scale-out (Auto-scaling)
az monitor autoscale create \
  --resource-group $RESOURCE_GROUP \
  --resource $APP_NAME \
  --resource-type Microsoft.Web/sites \
  --name "${APP_NAME}-autoscale" \
  --min-count 2 \
  --max-count 5 \
  --count 2

az monitor autoscale rule create \
  --resource-group $RESOURCE_GROUP \
  --autoscale-name "${APP_NAME}-autoscale" \
  --condition "CpuPercentage > 70 avg 5m" \
  --scale out 1

az monitor autoscale rule create \
  --resource-group $RESOURCE_GROUP \
  --autoscale-name "${APP_NAME}-autoscale" \
  --condition "CpuPercentage < 30 avg 5m" \
  --scale in 1
```

### مرحله 2: تنظیم Secrets در Key Vault

```bash
# تولید JWT Key قوی
JWT_KEY=$(openssl rand -base64 64)

# ذخیره secrets
az keyvault secret set --vault-name $KEYVAULT_NAME --name "JwtChat--Key" --value "$JWT_KEY"
az keyvault secret set --vault-name $KEYVAULT_NAME --name "JwtChat--Issuer" --value "chat-support-prod"
az keyvault secret set --vault-name $KEYVAULT_NAME --name "JwtChat--Audience" --value "chat-support-app"
az keyvault secret set --vault-name $KEYVAULT_NAME --name "Kavenegar--ApiKey" --value "[YOUR_KAVENEGAR_API_KEY]"

# Upload Firebase service account file
# این باید manually انجام شود در Azure Portal
# Files > Firebase Service Account > Upload
```

### مرحله 3: اعمال Database Migrations

```bash
# 1. دانلود connection string
CONNECTION_STRING=$(az keyvault secret show --vault-name $KEYVAULT_NAME --name "ConnectionStrings--Chat-SupportDb" --query value -o tsv)

# 2. اجرای migrations
cd src/Infrastructure
dotnet ef database update --connection "$CONNECTION_STRING"

# 3. اعمال Performance Indexes
sqlcmd -S "${SQL_SERVER}.database.windows.net" -d $SQL_DB -U sqladmin -P "$SQL_ADMIN_PASSWORD" -i "../../docs/migrations/AddPerformanceIndexes.sql"

# 4. بررسی schema
sqlcmd -S "${SQL_SERVER}.database.windows.net" -d $SQL_DB -U sqladmin -P "$SQL_ADMIN_PASSWORD" -Q "SELECT * FROM __EFMigrationsHistory ORDER BY MigrationId DESC"
```

### مرحله 4: Build و Publish Application

```bash
# 1. Build application
cd ../..
dotnet build -c Release

# 2. Publish
dotnet publish src/Web/Web.csproj -c Release -o ./publish

# 3. Deploy به Azure
cd publish
zip -r ../app.zip .
cd ..

az webapp deploy \
  --resource-group $RESOURCE_GROUP \
  --name $APP_NAME \
  --src-path app.zip \
  --type zip

# یا استفاده از GitHub Actions (توصیه می‌شود)
```

### مرحله 5: تنظیم Monitoring و Alerts

```bash
# 1. ایجاد Application Insights
az monitor app-insights component create \
  --app "${APP_NAME}-insights" \
  --location $LOCATION \
  --resource-group $RESOURCE_GROUP \
  --application-type web

# دریافت instrumentation key
INSIGHTS_KEY=$(az monitor app-insights component show \
  --app "${APP_NAME}-insights" \
  --resource-group $RESOURCE_GROUP \
  --query instrumentationKey -o tsv)

# تنظیم در App Service
az webapp config appsettings set \
  --name $APP_NAME \
  --resource-group $RESOURCE_GROUP \
  --settings APPLICATIONINSIGHTS_CONNECTION_STRING="InstrumentationKey=${INSIGHTS_KEY}"

# 2. ایجاد Action Group برای alerts
az monitor action-group create \
  --name "chat-support-alerts" \
  --resource-group $RESOURCE_GROUP \
  --short-name "ChatAlert" \
  --email-receiver name=DevOps email=devops@company.com

# 3. ایجاد Alert Rules
# Service Down Alert
az monitor metrics alert create \
  --name "Service-Unhealthy" \
  --resource-group $RESOURCE_GROUP \
  --scopes "/subscriptions/{subscription-id}/resourceGroups/${RESOURCE_GROUP}/providers/Microsoft.Web/sites/${APP_NAME}" \
  --condition "avg HealthCheckStatus < 1" \
  --description "سرویس در وضعیت unhealthy است" \
  --evaluation-frequency 1m \
  --window-size 5m \
  --severity 0 \
  --action chat-support-alerts

# High Response Time Alert
az monitor metrics alert create \
  --name "High-Response-Time" \
  --resource-group $RESOURCE_GROUP \
  --scopes "/subscriptions/{subscription-id}/resourceGroups/${RESOURCE_GROUP}/providers/Microsoft.Web/sites/${APP_NAME}" \
  --condition "avg HttpResponseTime > 2000" \
  --description "زمان پاسخ بالا است" \
  --evaluation-frequency 5m \
  --window-size 10m \
  --severity 2 \
  --action chat-support-alerts
```

---

## 🔄 استراتژی‌های Deployment

### 1. Blue-Green Deployment (توصیه شده)

```bash
# 1. ایجاد staging slot
az webapp deployment slot create \
  --name $APP_NAME \
  --resource-group $RESOURCE_GROUP \
  --slot staging

# 2. Deploy به staging
az webapp deploy \
  --resource-group $RESOURCE_GROUP \
  --name $APP_NAME \
  --slot staging \
  --src-path app.zip \
  --type zip

# 3. تست staging
curl https://${APP_NAME}-staging.azurewebsites.net/health

# 4. Swap (zero downtime)
az webapp deployment slot swap \
  --name $APP_NAME \
  --resource-group $RESOURCE_GROUP \
  --slot staging \
  --target-slot production

# 5. در صورت مشکل، rollback
az webapp deployment slot swap \
  --name $APP_NAME \
  --resource-group $RESOURCE_GROUP \
  --slot staging \
  --target-slot production
```

### 2. Canary Deployment

```bash
# 1. تنظیم traffic splitting
az webapp traffic-routing set \
  --name $APP_NAME \
  --resource-group $RESOURCE_GROUP \
  --distribution staging=10

# 2. مانیتور metrics
# بررسی error rate و response time

# 3. افزایش تدریجی traffic
az webapp traffic-routing set \
  --name $APP_NAME \
  --resource-group $RESOURCE_GROUP \
  --distribution staging=50

# 4. Full rollout
az webapp deployment slot swap \
  --name $APP_NAME \
  --resource-group $RESOURCE_GROUP \
  --slot staging
```

---

## ✅ Checklist قبل از Production

### امنیت
- [ ] تمام secrets در Key Vault هستند
- [ ] هیچ credential در source code نیست
- [ ] HTTPS اجباری است
- [ ] TLS 1.2+ فعال است
- [ ] Rate limiting فعال است
- [ ] Web Application Firewall (WAF) تنظیم شده
- [ ] DDoS Protection فعال است

### عملکرد
- [ ] Database indexes اعمال شده
- [ ] Redis cache برای SignalR راه‌اندازی شده
- [ ] Auto-scaling تنظیم شده
- [ ] CDN برای static files فعال است
- [ ] Connection pooling بهینه شده

### قابلیت اطمینان
- [ ] Health checks فعال هستند
- [ ] Backup دیتابیس تنظیم شده (روزانه)
- [ ] Geo-replication فعال است
- [ ] Disaster recovery plan موجود است
- [ ] Rollback procedure تست شده

### Monitoring
- [ ] Application Insights راه‌اندازی شده
- [ ] Alert rules تنظیم شده
- [ ] Dashboard monitoring موجود است
- [ ] Log retention policy تنظیم شده
- [ ] On-call rotation مشخص است

---

## 📊 Verification بعد از Deployment

```bash
# 1. بررسی health check
curl https://${APP_NAME}.azurewebsites.net/health

# 2. بررسی لاگ‌ها
az webapp log tail --name $APP_NAME --resource-group $RESOURCE_GROUP

# 3. بررسی metrics
az monitor metrics list \
  --resource "/subscriptions/{subscription-id}/resourceGroups/${RESOURCE_GROUP}/providers/Microsoft.Web/sites/${APP_NAME}" \
  --metric "CpuPercentage,MemoryPercentage,HttpResponseTime"

# 4. تست عملکرد
# استفاده از Apache Bench یا JMeter
ab -n 1000 -c 10 https://${APP_NAME}.azurewebsites.net/health
```

---

## 🔙 Rollback Procedure

### Rollback Application

```bash
# روش 1: Swap slots (اگر از blue-green استفاده کرده‌اید)
az webapp deployment slot swap \
  --name $APP_NAME \
  --resource-group $RESOURCE_GROUP \
  --slot staging

# روش 2: Deploy نسخه قبلی
az webapp deploy \
  --resource-group $RESOURCE_GROUP \
  --name $APP_NAME \
  --src-path previous-version.zip \
  --type zip
```

### Rollback Database

```bash
# 1. اتصال به database
sqlcmd -S "${SQL_SERVER}.database.windows.net" -d $SQL_DB -U sqladmin -P "$SQL_ADMIN_PASSWORD"

# 2. اجرای rollback script
:r rollback_migration_[version].sql
GO

# 3. بررسی schema version
SELECT * FROM __EFMigrationsHistory ORDER BY MigrationId DESC;
GO
```

---

## 🚨 عیب‌یابی مشکلات رایج

### مشکل: Application start نمی‌شود

```bash
# بررسی لاگ‌های startup
az webapp log download --name $APP_NAME --resource-group $RESOURCE_GROUP

# بررسی configuration
az webapp config appsettings list --name $APP_NAME --resource-group $RESOURCE_GROUP
```

### مشکل: Database connection failed

```bash
# بررسی firewall rules
az sql server firewall-rule list --server $SQL_SERVER --resource-group $RESOURCE_GROUP

# تست connection
sqlcmd -S "${SQL_SERVER}.database.windows.net" -d $SQL_DB -U sqladmin -P "$SQL_ADMIN_PASSWORD" -Q "SELECT 1"
```

### مشکل: High CPU usage

```bash
# Scale up
az appservice plan update --name $APP_SERVICE_PLAN --resource-group $RESOURCE_GROUP --sku P2V3

# یا scale out
az appservice plan update --name $APP_SERVICE_PLAN --resource-group $RESOURCE_GROUP --number-of-workers 3
```

---

## 📚 منابع اضافی

- [Azure App Service Documentation](https://docs.microsoft.com/azure/app-service/)
- [SQL Database Best Practices](https://docs.microsoft.com/azure/sql-database/sql-database-best-practices)
- [Azure Redis Cache Documentation](https://docs.microsoft.com/azure/azure-cache-for-redis/)
- [Application Insights Documentation](https://docs.microsoft.com/azure/azure-monitor/app/app-insights-overview)

---

**آخرین به‌روزرسانی**: {تاریخ}  
**نسخه**: 1.0  
**مسئول**: تیم DevOps
