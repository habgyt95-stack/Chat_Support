using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Chat_Support.Infrastructure.Service;

public interface IFirebaseAccessTokenProvider
{
    Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default);
    string GetProjectId();
}

internal sealed class FirebaseAccessTokenProvider : IFirebaseAccessTokenProvider
{
    private const string Scope = "https://www.googleapis.com/auth/firebase.messaging";
    private readonly IConfiguration _configuration;
    private readonly IHostEnvironment _env;
    private readonly ILogger<FirebaseAccessTokenProvider> _logger;

    private static readonly object _sync = new();
    private static string? _cachedToken;
    private static DateTimeOffset _cachedTokenExpiresAt;
    private ServiceAccount? _serviceAccount;

    public FirebaseAccessTokenProvider(IConfiguration configuration, IHostEnvironment env, ILogger<FirebaseAccessTokenProvider> logger)
    {
        _configuration = configuration;
        _env = env;
        _logger = logger;
    }

    public string GetProjectId()
    {
        EnsureServiceAccountLoaded();
        return _serviceAccount!.project_id ??
               _configuration["Firebase:ProjectId"] ??
               throw new InvalidOperationException("Firebase project id is not configured. Set 'Firebase:ProjectId' or include 'project_id' in the service account json.");
    }

    public async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        EnsureServiceAccountLoaded();

        lock (_sync)
        {
            if (!string.IsNullOrEmpty(_cachedToken) && _cachedTokenExpiresAt > DateTimeOffset.UtcNow.AddMinutes(1))
            {
                return _cachedToken!;
            }
        }

        var now = DateTimeOffset.UtcNow;
        var iat = now.ToUnixTimeSeconds();
        var exp = now.AddMinutes(55).ToUnixTimeSeconds(); // <= 1h

        var header = new Dictionary<string, object>
        {
            ["alg"] = "RS256",
            ["typ"] = "JWT"
        };

        var audience = _serviceAccount!.token_uri ?? "https://oauth2.googleapis.com/token";
        var payload = new Dictionary<string, object>
        {
            ["iss"] = _serviceAccount!.client_email!,
            ["scope"] = Scope,
            ["aud"] = audience,
            ["iat"] = iat,
            ["exp"] = exp
        };

        var headerEncoded = Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(header));
        var payloadEncoded = Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(payload));
        var signingInput = Encoding.ASCII.GetBytes($"{headerEncoded}.{payloadEncoded}");

        byte[] signature;
        using (var rsa = CreateRsaFromPem(_serviceAccount!.private_key!))
        {
            signature = rsa.SignData(signingInput, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        }

        var jwt = $"{headerEncoded}.{payloadEncoded}.{Base64UrlEncode(signature)}";

        using var http = new HttpClient();
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "urn:ietf:params:oauth:grant-type:jwt-bearer",
            ["assertion"] = jwt
        });
        using var resp = await http.PostAsync(audience, content, cancellationToken);
        resp.EnsureSuccessStatusCode();
        var stream = await resp.Content.ReadAsStreamAsync(cancellationToken);
        var tokenResponse = await JsonSerializer.DeserializeAsync<TokenResponse>(stream, cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Invalid token response from Google OAuth2");

        var token = tokenResponse.access_token ?? throw new InvalidOperationException("Missing access_token in token response");
        var expiresInSec = tokenResponse.expires_in;

        lock (_sync)
        {
            _cachedToken = token;
            _cachedTokenExpiresAt = DateTimeOffset.UtcNow.AddSeconds(Math.Max(60, expiresInSec - 60));
        }

        return token;
    }

    private void EnsureServiceAccountLoaded()
    {
        if (_serviceAccount != null) return;

        // 1) Env variables take precedence
        var inlineJson = Environment.GetEnvironmentVariable("Firebase__ServiceAccountJson")
                         ?? _configuration["Firebase:ServiceAccountJson"];
        if (!string.IsNullOrWhiteSpace(inlineJson))
        {
            _serviceAccount = JsonSerializer.Deserialize<ServiceAccount>(inlineJson);
            if (_serviceAccount == null)
            {
                throw new InvalidOperationException("Provided Firebase:ServiceAccountJson is invalid JSON.");
            }
            _logger.LogInformation("Firebase service account loaded from inline JSON.");
            return;
        }

        // 2) Resolve path from env or configuration
        var path = Environment.GetEnvironmentVariable("Firebase__ServiceAccountPath")
                   ?? _configuration["Firebase:ServiceAccountPath"];

        // 3) Fallbacks to common filenames when not configured
        var candidates = new List<string>();
        if (!string.IsNullOrWhiteSpace(path)) candidates.Add(path);
        candidates.Add("abrikChat.json");
        candidates.Add("firebase-service-account.json");

        string? resolved = null;
        foreach (var candidate in candidates)
        {
            if (string.IsNullOrWhiteSpace(candidate)) continue;

            // Absolute path
            if (Path.IsPathRooted(candidate) && File.Exists(candidate))
            {
                resolved = candidate;
                break;
            }

            // As-is relative to CWD
            if (File.Exists(candidate))
            {
                resolved = Path.GetFullPath(candidate);
                break;
            }

            // Relative to ContentRoot
            var cr = Path.Combine(_env.ContentRootPath, candidate);
            if (File.Exists(cr))
            {
                resolved = cr;
                break;
            }

            // Relative to base directory
            var bd = Path.Combine(AppContext.BaseDirectory, candidate);
            if (File.Exists(bd))
            {
                resolved = bd;
                break;
            }
        }

        if (resolved == null)
        {
            _logger.LogError("Firebase service account not found. Looked for configured path and defaults near: CWD={Cwd}, ContentRoot={CR}, BaseDir={BD}",
                Directory.GetCurrentDirectory(), _env.ContentRootPath, AppContext.BaseDirectory);
            throw new InvalidOperationException("Firebase service account configuration missing. Provide 'Firebase:ServiceAccountJson' or 'Firebase:ServiceAccountPath'.");
        }

        var fileContent = File.ReadAllText(resolved);
        _serviceAccount = JsonSerializer.Deserialize<ServiceAccount>(fileContent);
        if (_serviceAccount == null || string.IsNullOrWhiteSpace(_serviceAccount.private_key) || string.IsNullOrWhiteSpace(_serviceAccount.client_email))
        {
            throw new InvalidOperationException($"Invalid Firebase service account content at '{resolved}'.");
        }

        _logger.LogInformation("Firebase service account loaded from file: {Path}", resolved);
    }

    private static RSA CreateRsaFromPem(string privateKeyPem)
    {
        var rsa = RSA.Create();
        rsa.ImportFromPem(privateKeyPem.AsSpan());
        return rsa;
    }

    private static string Base64UrlEncode(byte[] input)
    {
        return Convert.ToBase64String(input)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private sealed class TokenResponse
    {
        public string? access_token { get; set; }
        public int expires_in { get; set; }
        public string? token_type { get; set; }
    }

    private sealed class ServiceAccount
    {
        public string? type { get; set; }
        public string? project_id { get; set; }
        public string? private_key_id { get; set; }
        public string? private_key { get; set; }
        public string? client_email { get; set; }
        public string? client_id { get; set; }
        public string? auth_uri { get; set; }
        public string? token_uri { get; set; }
        public string? auth_provider_x509_cert_url { get; set; }
        public string? client_x509_cert_url { get; set; }
        public string? universe_domain { get; set; }
    }
}
