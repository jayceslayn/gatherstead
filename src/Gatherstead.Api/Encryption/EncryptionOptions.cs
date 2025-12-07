namespace Gatherstead.Api.Encryption;

public class EncryptionOptions
{
    public const string SectionName = "Encryption";

    public string? Key { get; set; }

    public KeyVaultOptions KeyVault { get; set; } = new();
}

public class KeyVaultOptions
{
    public string? Uri { get; set; }

    public string SecretName { get; set; } = "app-encryption-key";

    public string? SecretVersion { get; set; }
}
