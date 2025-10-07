using Azure.Identity;
using Chat_Support.Application.Common.Interfaces;
using Chat_Support.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace Chat_Support.Web;
public static class DependencyInjection
{
    public static void AddWebServices(this IHostApplicationBuilder builder)
    {
        builder.Services.AddCors(options =>
        {
            options.AddPolicy(name: "ChatSupportApp",
                policy =>
                {
                    policy.WithOrigins(builder.Configuration["AllowedOrigins"]?.Split(';') ?? [])
                        .AllowAnyHeader()
                        .AllowAnyMethod()
                        .AllowCredentials();
                });
        });

        builder.Services.AddDatabaseDeveloperPageExceptionFilter();

        builder.Services.AddScoped<IUser, CurrentUser>();

        builder.Services.AddHttpContextAccessor();

        builder.Services.AddExceptionHandler<CustomExceptionHandler>();

        builder.Services.AddRazorPages();

        // Customise default API behaviour
        builder.Services.Configure<ApiBehaviorOptions>(options =>
            options.SuppressModelStateInvalidFilter = true);

        builder.Services.AddEndpointsApiExplorer();

        builder.Services.AddOpenApiDocument((configure, sp) =>
        {
            configure.Title = "Chat_Support API";

        });
    }

    public static void AddKeyVaultIfConfigured(this IHostApplicationBuilder builder)
    {
        var keyVaultUri = builder.Configuration["AZURE_KEY_VAULT_ENDPOINT"];
        if (!string.IsNullOrWhiteSpace(keyVaultUri))
        {
            builder.Configuration.AddAzureKeyVault(
                new Uri(keyVaultUri),
                new DefaultAzureCredential());
        }
    }
}
