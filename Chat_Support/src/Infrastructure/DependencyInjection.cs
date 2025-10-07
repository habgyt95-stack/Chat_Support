using System.Security.Claims;
using System.Text;
using Chat_Support.Application.Common.Interfaces;
using Chat_Support.Domain.Constants;
using Chat_Support.Infrastructure.Data;
using Chat_Support.Infrastructure.Data.Interceptors;
using Chat_Support.Infrastructure.Hubs;
using Chat_Support.Infrastructure.Identity;
using Chat_Support.Infrastructure.Service;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace Chat_Support.Infrastructure;
public static class DependencyInjection
{
    public static void AddInfrastructureServices(this IHostApplicationBuilder builder)
    {
        var connectionString = builder.Configuration.GetConnectionString("Chat_SupportDb");
        Guard.Against.Null(connectionString, message: "Connection string 'Chat_SupportDb' not found.");

        builder.Services.AddScoped<ISaveChangesInterceptor, AuditableEntityInterceptor>();
        builder.Services.AddScoped<ISaveChangesInterceptor, DispatchDomainEventsInterceptor>();


        builder.Services.AddDbContext<ApplicationDbContext>((sp, options) =>
        {
            options.AddInterceptors(sp.GetServices<ISaveChangesInterceptor>());
            options.UseSqlServer(connectionString)
                .EnableSensitiveDataLogging()
                .EnableDetailedErrors();
            //.AddAsyncSeeding(sp);
            options.ReplaceService<IMigrationsSqlGenerator, CustomSqlServerMigrationsSqlGenerator>();
        });

        builder.EnrichSqlServerDbContext<ApplicationDbContext>();

        builder.Services.AddScoped<IApplicationDbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());

        //builder.Services.AddScoped<ApplicationDbContextInitialiser>();

        builder.Services.AddSingleton(TimeProvider.System);

        builder.Services.AddScoped<IAgentAssignmentService, AgentAssignmentService>();
        builder.Services.AddScoped<IChatHubService, ChatHubService>();
        builder.Services.AddScoped<IFileStorageService, FileStorageService>();
        builder.Services.AddScoped<IIdentityService, IdentityService>();
        builder.Services.AddScoped<ISmsService, KavenegarSmsService>();
        builder.Services.AddScoped<IJwtService, JwtService>();
        builder.Services.AddSingleton<IPresenceTracker, PresenceTracker>();
        builder.Services.AddHttpClient<IMessageNotificationService, FcmNotificationService>();
        builder.Services.AddScoped<INewMessageNotifier, NewMessageNotifier>();

        // SignalR user id mapping based on JWT sub claim
        builder.Services.AddSingleton<IUserIdProvider, SubClaimUserIdProvider>();

        builder.Services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = builder.Configuration["JwtChat:Issuer"],
                ValidAudience = builder.Configuration["JwtChat:Audience"],
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["JwtChat:Key"] ?? string.Empty)),
                ClockSkew = TimeSpan.Zero
            };

            options.Events = new JwtBearerEvents
            {
                OnMessageReceived = context =>
                {
                    var accessToken = context.Request.Query["access_token"];

                    var path = context.HttpContext.Request.Path;
                    if (!string.IsNullOrEmpty(accessToken) &&
                        (path.StartsWithSegments("/chathub") || path.StartsWithSegments("/guestchathub")))
                    {
                        context.Token = accessToken;
                    }

                    return Task.CompletedTask;
                },
                OnAuthenticationFailed = context =>
                {
                    var loggerFactory = context.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>();
                    var logger = loggerFactory.CreateLogger("JwtBearerEvents");
                    logger.LogError($"Authentication failed: {context.Exception}");
                    return Task.CompletedTask;
                },
                OnTokenValidated = context =>
                {
                    var loggerFactory = context.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>();
                    var logger = loggerFactory.CreateLogger("JwtBearerEvents");
                    var userId = context.Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                    logger.LogInformation($"Token validated for user: {userId}");
                    return Task.CompletedTask;
                }
            };
        })

        .AddJwtBearer("JwtAppChat", options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = false, // همانطور که قبلا گفتیم، چون Audience ندارد false است
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = builder.Configuration["JwtAppChat:Issuer"], // از تنظیمات اپلیکیشن
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["JwtAppChat:Key"] ?? string.Empty)) // از تنظیمات اپلیکیشن
            };
        });

        builder.Services.AddAuthorization(options =>
        {
            options.AddPolicy(Policies.CanPurge, policy => policy.RequireRole(Roles.Administrator));
            // پالیسی Agent برای رفع خطا اضافه شد
            options.AddPolicy("Agent", policy => policy.RequireRole("Agent"));
        });
    }
}
