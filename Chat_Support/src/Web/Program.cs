using System.IdentityModel.Tokens.Jwt; // این using را اضافه کنید
using System.Threading.RateLimiting;
using Chat_Support.Application;
using Chat_Support.Infrastructure;
using Chat_Support.Infrastructure.Hubs;
using Chat_Support.ServiceDefaults;
using Chat_Support.Web;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.FileProviders;

// این خط را برای جلوگیری از تغییر نام Claim های توکن اضافه کنید
JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();
JwtSecurityTokenHandler.DefaultMapInboundClaims = false;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.AddServiceDefaults();
// builder.AddKeyVaultIfConfigured(); // این خط را اگر از KeyVault استفاده نمی‌کنید، می‌توانید کامنت کنید
builder.AddApplicationServices();
builder.AddInfrastructureServices();
builder.AddWebServices();
builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = true; // برای دیباگ
});
builder.Services.AddLogging();

// اضافه کردن Rate Limiting
builder.Services.AddRateLimiter(options =>
{
    // سیاست عمومی: 100 request در دقیقه
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

    // سیاست Auth (خیلی محدود برای جلوگیری از brute force)
    options.AddFixedWindowLimiter("auth", rateLimitOptions =>
    {
        rateLimitOptions.PermitLimit = 5;
        rateLimitOptions.Window = TimeSpan.FromMinutes(1);
        rateLimitOptions.QueueLimit = 0;
    });

    // رفتار هنگام reject
    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            context.HttpContext.Response.Headers.RetryAfter = retryAfter.TotalSeconds.ToString();
        }

        var retrySeconds = retryAfter.TotalSeconds;
        await context.HttpContext.Response.WriteAsJsonAsync(
            new 
            { 
                error = "تعداد درخواست‌های شما بیش از حد مجاز است",
                message = "لطفاً چند لحظه صبر کنید و دوباره تلاش کنید",
                retryAfter = retrySeconds
            },
            cancellationToken: cancellationToken);
    };
});

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseExceptionHandler(options => { });

// اضافه کردن Security Headers
app.Use(async (context, next) =>
{
    context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Append("X-Frame-Options", "DENY");
    context.Response.Headers.Append("X-XSS-Protection", "1; mode=block");
    context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
    context.Response.Headers.Append("Permissions-Policy", "geolocation=(), microphone=(), camera=()");
    
    await next();
});

if (!app.Environment.IsDevelopment())
{
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseRateLimiter();

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(
        Path.Combine(builder.Environment.WebRootPath, "uploads")),
    RequestPath = "/uploads",
    OnPrepareResponse = ctx =>
    {
        // اضافه کردن Content-Disposition برای دانلود بهتر در WebView
        // این قبل از شروع ارسال response اجرا می‌شود
        var fileName = Path.GetFileName(ctx.File.Name);
        ctx.Context.Response.Headers.Append("Content-Disposition", $"attachment; filename=\"{fileName}\"");
    }
});
app.UseStaticFiles();
app.UseRouting();
app.UseCors("ChatSupportApp");
app.UseAuthentication();
app.UseAuthorization(); 
app.UseSwaggerUi(settings =>
{
    settings.Path = "/api";
    settings.DocumentPath = "/api/specification.json";
});

app.MapRazorPages();
app.MapControllers();
app.MapHub<ChatHub>("/chathub").RequireAuthorization();
app.MapHub<GuestChatHub>("/guestchathub");
app.MapFallbackToFile("index.html");
app.MapDefaultEndpoints();
app.MapEndpoints();

app.Run();

namespace Chat_Support.Web
{
    public partial class Program { }
}
