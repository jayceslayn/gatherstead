using Gatherstead.Db.Encryption;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Gatherstead.Api.Encryption;

public class EncryptionHealthCheck : IHealthCheck
{
    private readonly IEncryptionKeyProvider _encryptionKeyProvider;

    public EncryptionHealthCheck(IEncryptionKeyProvider encryptionKeyProvider)
    {
        _encryptionKeyProvider = encryptionKeyProvider;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var key = _encryptionKeyProvider.GetCurrentKey();

            return Task.FromResult(key.Length == 32
                ? HealthCheckResult.Healthy("Encryption key is available and 256-bit.")
                : HealthCheckResult.Unhealthy("Encryption key is not 256-bit."));
        }
        catch (Exception ex)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy("Failed to load encryption key.", ex));
        }
    }
}
