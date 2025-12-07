using System.Text;
using Gatherstead.Db.Encryption;

namespace Gatherstead.Api.Encryption;

public class ConfigurationEncryptionKeyProvider : IEncryptionKeyProvider
{
    private const int Aes256KeyLength = 32;
    private readonly byte[] _key;

    public ConfigurationEncryptionKeyProvider(IConfiguration configuration)
    {
        if (configuration is null)
        {
            throw new ArgumentNullException(nameof(configuration));
        }

        _key = NormalizeKey(ParseKey(configuration["Encryption:Key"]));
    }

    public byte[] GetCurrentKey() => _key;

    private static byte[] ParseKey(string? configuredKey)
    {
        if (string.IsNullOrWhiteSpace(configuredKey))
        {
            throw new InvalidOperationException("Encryption key configuration 'Encryption:Key' is required for database encryption.");
        }

        if (TryParseBase64(configuredKey, out var parsed))
        {
            return parsed;
        }

        return Encoding.UTF8.GetBytes(configuredKey);
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

    private static byte[] NormalizeKey(byte[] key)
    {
        if (key.Length == Aes256KeyLength)
        {
            return key;
        }

        var normalized = new byte[Aes256KeyLength];
        Array.Copy(key, normalized, Math.Min(key.Length, normalized.Length));
        return normalized;
    }
}
