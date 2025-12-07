using System.Text;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Gatherstead.Db.Encryption;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Hosting;

namespace Gatherstead.Api.Encryption;

public class KeyVaultEncryptionKeyProvider : IEncryptionKeyProvider
{
    private const int Aes256KeyLength = 32;
    private readonly EncryptionOptions _options;
    private readonly ILogger<KeyVaultEncryptionKeyProvider> _logger;
    private readonly SecretClient? _secretClient;
    private readonly bool _isDevelopment;
    private byte[]? _cachedKey;

    public KeyVaultEncryptionKeyProvider(
        IOptions<EncryptionOptions> options,
        ILogger<KeyVaultEncryptionKeyProvider> logger,
        IHostEnvironment hostEnvironment)
    {
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _isDevelopment = (hostEnvironment ?? throw new ArgumentNullException(nameof(hostEnvironment))).IsDevelopment();

        if (!string.IsNullOrWhiteSpace(_options.KeyVault?.Uri))
        {
            _secretClient = new SecretClient(new Uri(_options.KeyVault.Uri!), new DefaultAzureCredential());
        }
    }

    public byte[] GetCurrentKey()
    {
        if (_cachedKey is not null)
        {
            return _cachedKey;
        }

        if (_secretClient is not null)
        {
            return _cachedKey = FetchFromKeyVault();
        }

        if (!string.IsNullOrWhiteSpace(_options.Key))
        {
            if (!_isDevelopment)
            {
                throw new InvalidOperationException(
                    "Development encryption key is not allowed outside the Development environment. Configure Key Vault instead.");
            }

            _logger.LogWarning(
                "Using development encryption key from configuration. Ensure production keys are stored in Key Vault.");
            return _cachedKey = ParseAndValidateKey(_options.Key);
        }

        throw new InvalidOperationException(
            "No encryption key configured. Provide Encryption:KeyVault or Encryption:Key for development.");
    }

    private byte[] FetchFromKeyVault()
    {
        try
        {
            var secretName = string.IsNullOrWhiteSpace(_options.KeyVault.SecretName)
                ? "app-encryption-key"
                : _options.KeyVault.SecretName;

            var secret = _secretClient!.GetSecret(secretName, _options.KeyVault.SecretVersion);
            var key = ParseAndValidateKey(secret.Value.Value);

            _logger.LogInformation(
                "Loaded encryption key from Key Vault {VaultUri} (secret {SecretName} version {SecretVersion})",
                _options.KeyVault.Uri,
                secretName,
                _options.KeyVault.SecretVersion ?? "(latest)");

            return key;
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Failed to retrieve encryption key from Key Vault {VaultUri}", _options.KeyVault.Uri);
            throw;
        }
    }

    private static byte[] ParseAndValidateKey(string? configuredKey)
    {
        if (string.IsNullOrWhiteSpace(configuredKey))
        {
            throw new InvalidOperationException(
                "Encryption key is missing. Ensure the Key Vault secret or configuration value is set.");
        }

        var key = TryParseBase64(configuredKey, out var parsed)
            ? parsed
            : Encoding.UTF8.GetBytes(configuredKey);

        if (key.Length != Aes256KeyLength)
        {
            throw new InvalidOperationException(
                $"Encryption key must be {Aes256KeyLength * 8}-bit ({Aes256KeyLength} bytes). Provided length: {key.Length} bytes.");
        }

        return key;
    }

    private static bool TryParseBase64(string value, out byte[] key)
    {
        try
        {
            key = Convert.FromBase64String(value);
            return true;
        }
        catch (FormatException)
        {
            key = Array.Empty<byte>();
            return false;
        }
    }
}
