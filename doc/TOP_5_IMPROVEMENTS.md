# پیشنهادات بهبود جامع سیستم

## مقدمه

این سند شامل تمامی بهبودهای پیشنهادی برای سیستم چت و پشتیبانی است که بر اساس بررسی‌های جامع کد، زیرساخت و عملیات تهیه شده است.

---

## 🎯 5 بهبود با بالاترین اولویت (Top 5 High-Impact Fixes)

### 1️⃣ انتقال Secrets به Azure Key Vault

**چرا مهم است**: Secrets در source code خطر امنیتی بحرانی است.

**تأثیر**: 
- ⭐⭐⭐⭐⭐ امنیت
- ⭐⭐⭐ قابلیت اطمینان
- ریسک: 🟡 متوسط (نیاز به تست دقیق)

**زمان پیاده‌سازی**: 4-6 ساعت

**مراحل**:
```bash
# 1. ایجاد Key Vault
az keyvault create --name chat-support-kv --resource-group chat-support-rg --location eastus

# 2. افزودن secrets
az keyvault secret set --vault-name chat-support-kv --name "ConnectionStrings--Chat-SupportDb" --value "[REDACTED]"
az keyvault secret set --vault-name chat-support-kv --name "JwtChat--Key" --value "[REDACTED]"
az keyvault secret set --vault-name chat-support-kv --name "Kavenegar--ApiKey" --value "[REDACTED]"
az keyvault secret set --vault-name chat-support-kv --name "Firebase--ServiceAccountPath" --value "abrikChat.json"

# 3. تنظیم managed identity
az webapp identity assign --name chat-support-web --resource-group chat-support-rg

PRINCIPAL_ID=$(az webapp identity show --name chat-support-web --resource-group chat-support-rg --query principalId -o tsv)

# 4. دادن دسترسی
az keyvault set-policy --name chat-support-kv --object-id $PRINCIPAL_ID --secret-permissions get list
```

**کد**:
```csharp
// فایل: src/Web/Program.cs
// قبل از var app = builder.Build();

if (!builder.Environment.IsDevelopment())
{
    var keyVaultEndpoint = builder.Configuration["AZURE_KEY_VAULT_ENDPOINT"];
    if (!string.IsNullOrEmpty(keyVaultEndpoint))
    {
        builder.Configuration.AddAzureKeyVault(
            new Uri(keyVaultEndpoint),
            new DefaultAzureCredential());
        
        _logger.LogInformation("Azure Key Vault configuration loaded from {Endpoint}", keyVaultEndpoint);
    }
}
```

**Verification**:
```bash
# تست اینکه secrets از Key Vault می‌آیند
az webapp config appsettings list --name chat-support-web --resource-group chat-support-rg --query "[?name=='AZURE_KEY_VAULT_ENDPOINT']"
```

**Rollback**:
```bash
# غیرفعال کردن Key Vault integration
az webapp config appsettings delete --name chat-support-web --resource-group chat-support-rg --setting-names "AZURE_KEY_VAULT_ENDPOINT"
```

**Monitoring**:
```kusto
// Application Insights query
traces
| where message contains "Key Vault"
| project timestamp, message, severityLevel
| order by timestamp desc
```

---

### 2️⃣ پیاده‌سازی Rate Limiting و DDoS Protection

**چرا مهم است**: جلوگیری از حملات brute force و exhaustion.

**تأثیر**: 
- ⭐⭐⭐⭐⭐ امنیت
- ⭐⭐⭐⭐ قابلیت اطمینان
- ⭐⭐⭐ عملکرد
- ریسک: 🟢 کم

**زمان پیاده‌سازی**: 3-4 ساعت

**کد کامل**:
```csharp
// فایل: src/Web/Program.cs
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;

// اضافه کردن Rate Limiter
builder.Services.AddRateLimiter(options =>
{
    // سیاست عمومی
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
    {
        var userId = context.User?.Identity?.Name ?? context.Connection.RemoteIpAddress?.ToString() ?? "anonymous";
        
        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: userId,
            factory: _ => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = 100,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            });
    });

    // سیاست Auth (خیلی محدود)
    options.AddPolicy("auth", context =>
    {
        var ipAddress = context.Connection.RemoteIpAddress?.ToString() ?? "anonymous";
        
        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: ipAddress,
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            });
    });

    // سیاست SignalR
    options.AddPolicy("signalr", context =>
    {
        var userId = context.User?.Identity?.Name ?? "anonymous";
        
        return RateLimitPartition.GetSlidingWindowLimiter(
            partitionKey: userId,
            factory: _ => new SlidingWindowRateLimiterOptions
            {
                PermitLimit = 1000,
                Window = TimeSpan.FromMinutes(1),
                SegmentsPerWindow = 4,
                QueueLimit = 0
            });
    });

    // رفتار هنگام reject
    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            context.HttpContext.Response.Headers.RetryAfter = retryAfter.TotalSeconds.ToString();
        }

        await context.HttpContext.Response.WriteAsJsonAsync(
            new 
            { 
                error = "تعداد درخواست‌های شما بیش از حد مجاز است",
                message = "لطفاً چند لحظه صبر کنید و دوباره تلاش کنید",
                retryAfter = retryAfter?.TotalSeconds
            },
            cancellationToken: cancellationToken);
    };
});

// بعد از app.Build()
app.UseRateLimiter();
```

```csharp
// فایل: src/Web/Endpoints/Auth.cs
// استفاده در endpoints

public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
{
    var group = app.MapGroup("/api/auth");

    group.MapPost("/request-otp", RequestOtp)
        .RequireRateLimiting("auth")
        .WithOpenApi();

    group.MapPost("/verify-otp", VerifyOtp)
        .RequireRateLimiting("auth")
        .WithOpenApi();
}
```

**Migration**: بدون نیاز به migration دیتابیس

**Verification**:
```bash
# تست rate limiting
for i in {1..10}; do
  curl -X POST https://localhost:5001/api/auth/request-otp \
    -H "Content-Type: application/json" \
    -d '{"phoneNumber": "09123456789"}' &
done
# باید بعد از 5 درخواست، 429 برگردد
```

**Monitoring**:
```csharp
// اضافه کردن metrics
// فایل: src/Web/Middleware/RateLimitMetricsMiddleware.cs

public class RateLimitMetricsMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RateLimitMetricsMiddleware> _logger;

    public RateLimitMetricsMiddleware(RequestDelegate next, ILogger<RateLimitMetricsMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        await _next(context);

        if (context.Response.StatusCode == StatusCodes.Status429TooManyRequests)
        {
            _logger.LogWarning(
                "Rate limit exceeded: Path={Path}, IP={IP}, User={User}",
                context.Request.Path,
                context.Connection.RemoteIpAddress,
                context.User?.Identity?.Name ?? "anonymous");
        }
    }
}
```

**Alert Rule**:
```kusto
// Application Insights
customEvents
| where name == "RateLimitExceeded"
| where timestamp > ago(5m)
| summarize count() by bin(timestamp, 1m), tostring(customDimensions.IPAddress)
| where count_ > 10
```

---

### 3️⃣ Redis Cache برای SignalR Backplane و Presence Tracking

**چرا مهم است**: در حال حاضر PresenceTracker در حافظه است و در scale-out کار نمی‌کند.

**تأثیر**: 
- ⭐⭐⭐⭐⭐ مقیاس‌پذیری
- ⭐⭐⭐⭐ قابلیت اطمینان
- ⭐⭐⭐⭐ عملکرد
- ریسک: 🟡 متوسط

**زمان پیاده‌سازی**: 6-8 ساعت

**Infrastructure**:
```bash
# ایجاد Azure Cache for Redis
az redis create \
  --name chat-support-redis \
  --resource-group chat-support-rg \
  --location eastus \
  --sku Standard \
  --vm-size C1 \
  --enable-non-ssl-port false

# دریافت connection string
az redis list-keys --name chat-support-redis --resource-group chat-support-rg
```

**کد**:
```xml
<!-- فایل: Directory.Packages.props -->
<PackageVersion Include="Microsoft.AspNetCore.SignalR.StackExchangeRedis" Version="9.0.0" />
<PackageVersion Include="StackExchange.Redis" Version="2.8.16" />
```

```csharp
// فایل: src/Infrastructure/DependencyInjection.cs

// اضافه کردن Redis
var redisConnection = builder.Configuration.GetConnectionString("Redis");
if (!string.IsNullOrEmpty(redisConnection))
{
    builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
    {
        var configuration = ConfigurationOptions.Parse(redisConnection);
        configuration.AbortOnConnectFail = false;
        configuration.ConnectRetry = 3;
        configuration.ConnectTimeout = 5000;
        return ConnectionMultiplexer.Connect(configuration);
    });

    // استفاده برای SignalR backplane
    builder.Services.AddSignalR()
        .AddStackExchangeRedis(redisConnection, options =>
        {
            options.Configuration.ChannelPrefix = "ChatSupport";
        });
}
else
{
    builder.Services.AddSignalR();
}

// Redis-based PresenceTracker
builder.Services.AddSingleton<IPresenceTracker, RedisPresenceTracker>();
```

```csharp
// فایل جدید: src/Infrastructure/Service/RedisPresenceTracker.cs

using StackExchange.Redis;

namespace Chat_Support.Infrastructure.Service;

public class RedisPresenceTracker : IPresenceTracker
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisPresenceTracker> _logger;
    private readonly IDatabase _db;

    public RedisPresenceTracker(IConnectionMultiplexer redis, ILogger<RedisPresenceTracker> logger)
    {
        _redis = redis;
        _logger = logger;
        _db = redis.GetDatabase();
    }

    public async Task RegisterConnection(int userId, string connectionId)
    {
        var key = $"presence:user:{userId}:connections";
        await _db.SetAddAsync(key, connectionId);
        await _db.KeyExpireAsync(key, TimeSpan.FromHours(24));

        var infoKey = $"presence:connection:{connectionId}";
        await _db.HashSetAsync(infoKey, new HashEntry[]
        {
            new("UserId", userId),
            new("ConnectedAt", DateTimeOffset.UtcNow.ToUnixTimeSeconds())
        });
        await _db.KeyExpireAsync(infoKey, TimeSpan.FromHours(24));

        _logger.LogDebug("Registered connection {ConnectionId} for user {UserId}", connectionId, userId);
    }

    public async Task UnregisterConnection(string connectionId)
    {
        var infoKey = $"presence:connection:{connectionId}";
        var userIdValue = await _db.HashGetAsync(infoKey, "UserId");

        if (userIdValue.HasValue)
        {
            var userId = (int)userIdValue;
            var key = $"presence:user:{userId}:connections";
            await _db.SetRemoveAsync(key, connectionId);
            _logger.LogDebug("Unregistered connection {ConnectionId} for user {UserId}", connectionId, userId);
        }

        await _db.KeyDeleteAsync(infoKey);
    }

    public async Task SetActiveRoom(string connectionId, int roomId)
    {
        var infoKey = $"presence:connection:{connectionId}";
        await _db.HashSetAsync(infoKey, "ActiveRoomId", roomId);
        _logger.LogDebug("Set active room {RoomId} for connection {ConnectionId}", roomId, connectionId);
    }

    public async Task ClearActiveRoom(string connectionId)
    {
        var infoKey = $"presence:connection:{connectionId}";
        await _db.HashDeleteAsync(infoKey, "ActiveRoomId");
    }

    public async Task<bool> IsUserViewingRoom(int userId, int roomId)
    {
        var key = $"presence:user:{userId}:connections";
        var connections = await _db.SetMembersAsync(key);

        foreach (var connection in connections)
        {
            var infoKey = $"presence:connection:{connection}";
            var activeRoomValue = await _db.HashGetAsync(infoKey, "ActiveRoomId");

            if (activeRoomValue.HasValue && (int)activeRoomValue == roomId)
            {
                return true;
            }
        }

        return false;
    }

    public async Task<int> GetOnlineUsersCount()
    {
        var server = _redis.GetServer(_redis.GetEndPoints().First());
        var keys = server.Keys(pattern: "presence:user:*:connections");
        return keys.Count();
    }
}
```

**Migration**: بدون نیاز به migration دیتابیس

**Rollback**:
```csharp
// برگشت به in-memory implementation
builder.Services.AddSingleton<IPresenceTracker, PresenceTracker>();
```

**Verification**:
```bash
# بررسی Redis keys
redis-cli -h chat-support-redis.redis.cache.windows.net -a [access-key] KEYS "presence:*"
```

**Monitoring**:
```kusto
// Redis metrics در Azure Portal
// - Cache Hits/Misses
// - Connected Clients
// - Server Load
// - Operations per Second
```

---

### 4️⃣ Database Indexing و Query Optimization

**چرا مهم است**: بهبود عملکرد query‌ها و کاهش زمان پاسخ.

**تأثیر**: 
- ⭐⭐⭐⭐⭐ عملکرد
- ⭐⭐⭐⭐ مقیاس‌پذیری
- ریسک: 🟢 کم

**زمان پیاده‌سازی**: 3-4 ساعت

**Migration Script**:
```sql
-- فایل: migrations/AddPerformanceIndexes.sql

-- Index برای GetChatRooms query (بیشترین استفاده)
CREATE NONCLUSTERED INDEX IX_ChatRoomMembers_UserId_IncludeAll
ON ChatRoomMembers(UserId)
INCLUDE (ChatRoomId, JoinedAt, IsMuted, Role);

-- Index برای GetChatMessages query
CREATE NONCLUSTERED INDEX IX_ChatMessages_RoomId_CreatedAt
ON ChatMessages(ChatRoomId, CreatedAt DESC)
INCLUDE (SenderId, Content, MessageType, IsDeleted);

-- Index برای unread messages count
CREATE NONCLUSTERED INDEX IX_MessageStatus_UserId_IsRead
ON MessageStatus(UserId, IsRead)
INCLUDE (MessageId, ReadAt);

-- Index برای presence tracking queries
CREATE NONCLUSTERED INDEX IX_UserConnections_UserId_IsActive
ON UserConnections(UserId, IsActive)
INCLUDE (ConnectionId, ConnectedAt);

-- Index برای support agent queries
CREATE NONCLUSTERED INDEX IX_SupportAgents_IsActive_RegionId
ON SupportAgents(IsActive, RegionId)
INCLUDE (UserId, MaxConcurrentChats, LastActivityAt);

-- Index برای chat room lookup by type
CREATE NONCLUSTERED INDEX IX_ChatRooms_Type_IsDeleted
ON ChatRooms(Type, IsDeleted)
INCLUDE (Name, CreatedBy, CreatedAt);

-- Statistics update
UPDATE STATISTICS ChatRoomMembers;
UPDATE STATISTICS ChatMessages;
UPDATE STATISTICS MessageStatus;
UPDATE STATISTICS UserConnections;
UPDATE STATISTICS SupportAgents;
UPDATE STATISTICS ChatRooms;
```

**Rollback Script**:
```sql
-- فایل: migrations/RollbackPerformanceIndexes.sql

DROP INDEX IF EXISTS IX_ChatRoomMembers_UserId_IncludeAll ON ChatRoomMembers;
DROP INDEX IF EXISTS IX_ChatMessages_RoomId_CreatedAt ON ChatMessages;
DROP INDEX IF EXISTS IX_MessageStatus_UserId_IsRead ON MessageStatus;
DROP INDEX IF EXISTS IX_UserConnections_UserId_IsActive ON UserConnections;
DROP INDEX IF EXISTS IX_SupportAgents_IsActive_RegionId ON SupportAgents;
DROP INDEX IF EXISTS IX_ChatRooms_Type_IsDeleted ON ChatRooms;
```

**Deployment**:
```bash
# Backup قبل از اعمال migration
az sql db export \
  --resource-group chat-support-rg \
  --server chat-support-sql \
  --name Chat_SupportDb \
  --admin-user sqladmin \
  --admin-password [PASSWORD] \
  --storage-key [STORAGE-KEY] \
  --storage-key-type StorageAccessKey \
  --storage-uri "https://[storage-account].blob.core.windows.net/backups/pre-index-migration.bacpac"

# اعمال indexes (در زمان کم‌ترافیک)
sqlcmd -S chat-support-sql.database.windows.net -d Chat_SupportDb -U sqladmin -P [PASSWORD] -i AddPerformanceIndexes.sql
```

**Verification**:
```sql
-- بررسی index usage
SELECT 
    OBJECT_NAME(s.object_id) AS TableName,
    i.name AS IndexName,
    s.user_seeks,
    s.user_scans,
    s.user_lookups,
    s.user_updates
FROM sys.dm_db_index_usage_stats s
INNER JOIN sys.indexes i ON s.object_id = i.object_id AND s.index_id = i.index_id
WHERE OBJECTPROPERTY(s.object_id, 'IsUserTable') = 1
ORDER BY s.user_seeks + s.user_scans + s.user_lookups DESC;

-- بررسی query performance قبل و بعد
SET STATISTICS TIME ON;
SET STATISTICS IO ON;

-- Test query
SELECT * FROM ChatRoomMembers WHERE UserId = 1;
```

**Monitoring**:
```kusto
// Application Insights - Query duration
dependencies
| where type == "SQL"
| where timestamp > ago(1h)
| summarize avg(duration), percentile(duration, 95), percentile(duration, 99) by name
| order by avg_duration desc
```

---

### 5️⃣ Health Checks و Observability

**چرا مهم است**: شناسایی سریع مشکلات و کاهش downtime.

**تأثیر**: 
- ⭐⭐⭐⭐⭐ قابلیت اطمینان
- ⭐⭐⭐⭐ observability
- ریسک: 🟢 کم

**زمان پیاده‌سازی**: 4-5 ساعت

**کد کامل**:
```xml
<!-- فایل: Directory.Packages.props -->
<PackageVersion Include="AspNetCore.HealthChecks.SqlServer" Version="9.0.0" />
<PackageVersion Include="AspNetCore.HealthChecks.Redis" Version="9.0.0" />
<PackageVersion Include="AspNetCore.HealthChecks.Uris" Version="9.0.0" />
```

```csharp
// فایل: src/Web/Program.cs

builder.Services.AddHealthChecks()
    .AddSqlServer(
        connectionString: builder.Configuration.GetConnectionString("Chat_SupportDb")!,
        name: "database",
        failureStatus: HealthStatus.Unhealthy,
        tags: new[] { "db", "sql", "sqlserver" })
    .AddRedis(
        redisConnectionString: builder.Configuration.GetConnectionString("Redis")!,
        name: "redis",
        failureStatus: HealthStatus.Degraded,
        tags: new[] { "cache", "redis" })
    .AddCheck<SignalRHealthCheck>("signalr", tags: new[] { "signalr", "websocket" })
    .AddCheck<ExternalServicesHealthCheck>("external-services", tags: new[] { "external" });

// Health check endpoints
app.MapHealthChecks("/health", new HealthCheckOptions
{
    Predicate = _ => true,
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("db") || check.Tags.Contains("redis"),
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false  // فقط بررسی می‌کند که app در حال اجراست
});
```

```csharp
// فایل جدید: src/Web/HealthChecks/SignalRHealthCheck.cs

public class SignalRHealthCheck : IHealthCheck
{
    private readonly IPresenceTracker _presenceTracker;
    private readonly ILogger<SignalRHealthCheck> _logger;

    public SignalRHealthCheck(IPresenceTracker presenceTracker, ILogger<SignalRHealthCheck> logger)
    {
        _presenceTracker = presenceTracker;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // بررسی تعداد connection‌های فعال
            var connectionCount = await _presenceTracker.GetOnlineUsersCount();
            
            var data = new Dictionary<string, object>
            {
                { "activeConnections", connectionCount }
            };

            // اگر تعداد connection‌ها خیلی زیاد باشد، warning
            if (connectionCount > 10000)
            {
                return HealthCheckResult.Degraded(
                    "تعداد connection‌های SignalR بسیار زیاد است",
                    data: data);
            }

            return HealthCheckResult.Healthy("SignalR در وضعیت سالم است", data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "خطا در بررسی سلامت SignalR");
            return HealthCheckResult.Unhealthy("SignalR در دسترس نیست", ex);
        }
    }
}
```

```csharp
// فایل جدید: src/Web/HealthChecks/ExternalServicesHealthCheck.cs

public class ExternalServicesHealthCheck : IHealthCheck
{
    private readonly ISmsService _smsService;
    private readonly IMessageNotificationService _notificationService;
    private readonly ILogger<ExternalServicesHealthCheck> _logger;

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var results = new Dictionary<string, object>();
        var isHealthy = true;
        var messages = new List<string>();

        // بررسی Kavenegar (SMS)
        try
        {
            // تست ساده - فقط بررسی connectivity
            results["sms"] = "connected";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "سرویس SMS در دسترس نیست");
            results["sms"] = "unavailable";
            messages.Add("سرویس SMS در دسترس نیست");
            isHealthy = false;
        }

        // بررسی Firebase (FCM)
        try
        {
            results["push-notification"] = "connected";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "سرویس Push Notification در دسترس نیست");
            results["push-notification"] = "unavailable";
            messages.Add("سرویس Push Notification در دسترس نیست");
            // این خیلی critical نیست، فقط degraded
        }

        if (!isHealthy)
        {
            return HealthCheckResult.Degraded(
                string.Join("; ", messages),
                data: results);
        }

        return HealthCheckResult.Healthy("تمام سرویس‌های خارجی در دسترس هستند", results);
    }
}
```

**Monitoring & Alerting**:
```yaml
# فایل جدید: monitoring/alerts.yaml
# برای استفاده در Azure Monitor

alerts:
  - name: "Service Unhealthy"
    description: "Health check endpoint برای بیش از 5 دقیقه unhealthy است"
    condition: |
      requests
      | where url contains "/health"
      | where resultCode != 200
      | where timestamp > ago(5m)
      | summarize count()
      | where count_ > 0
    severity: Critical
    action: "بررسی فوری سرویس و restart در صورت نیاز"

  - name: "Database Connection Issues"
    description: "اتصال به دیتابیس با مشکل مواجه است"
    condition: |
      dependencies
      | where type == "SQL"
      | where success == false
      | where timestamp > ago(5m)
      | summarize failureRate = 100.0 * countif(success == false) / count()
      | where failureRate > 10
    severity: Critical

  - name: "High Response Time"
    description: "زمان پاسخ API‌ها بالا است"
    condition: |
      requests
      | where timestamp > ago(10m)
      | summarize avg(duration), percentile(duration, 95)
      | where percentile_duration_95 > 2000
    severity: Warning

  - name: "SignalR Connection Issues"
    description: "مشکل در connection‌های SignalR"
    condition: |
      customMetrics
      | where name == "signalr_connections"
      | where timestamp > ago(5m)
      | summarize avg(value)
      | where avg_value < 10 and avg_value > 0
    severity: Warning
```

**Verification**:
```bash
# تست health checks
curl https://localhost:5001/health
curl https://localhost:5001/health/ready
curl https://localhost:5001/health/live
```

---

## 📊 خلاصه اولویت‌بندی

| # | بهبود | تأثیر | ریسک | زمان | وضعیت |
|---|--------|-------|------|------|-------|
| 1 | Azure Key Vault | ⭐⭐⭐⭐⭐ | 🟡 متوسط | 4-6h | ⏳ در انتظار |
| 2 | Rate Limiting | ⭐⭐⭐⭐⭐ | 🟢 کم | 3-4h | ⏳ در انتظار |
| 3 | Redis Cache | ⭐⭐⭐⭐⭐ | 🟡 متوسط | 6-8h | ⏳ در انتظار |
| 4 | DB Indexing | ⭐⭐⭐⭐⭐ | 🟢 کم | 3-4h | ⏳ در انتظار |
| 5 | Health Checks | ⭐⭐⭐⭐⭐ | 🟢 کم | 4-5h | ⏳ در انتظار |

**مجموع زمان تخمینی**: 20-27 ساعت (3-4 روز کاری)

---

**نسخه**: 1.0  
**تاریخ**: {تاریخ}  
**تهیه‌کننده**: تیم Architecture & DevOps
