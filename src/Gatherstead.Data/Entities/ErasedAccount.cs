using System;
using System.Security.Cryptography;
using System.Text;

namespace Gatherstead.Data.Entities;

/// <summary>
/// Short-lived, PII-free tombstone written when an account is erased. Bootstrap consults it so a
/// still-valid access token issued before the erasure cannot silently re-provision (resurrect) the
/// account. Only a hash of the external id is stored — never the identifier itself — and rows are
/// ignored once <see cref="ExpiresAt"/> passes, so a genuine later sign-up provisions normally.
/// </summary>
public class ErasedAccount
{
    /// <summary>
    /// How long the tombstone blocks re-provisioning: the maximum lifetime a token issued before
    /// the erasure could still be valid (matches the 24h revocation horizon in
    /// <c>TokenRevocationService</c>).
    /// </summary>
    public static readonly TimeSpan TombstoneLifetime = TimeSpan.FromHours(24);

    public Guid Id { get; set; }

    /// <summary>SHA-256 of the external (Entra subject) id — see <see cref="HashExternalId"/>.</summary>
    public byte[] ExternalIdHash { get; set; } = null!;

    public DateTime ErasedAt { get; set; }

    /// <summary>After this instant no pre-erasure token can still be valid; the row is ignored.</summary>
    public DateTime ExpiresAt { get; set; }

    public static byte[] HashExternalId(string externalId) =>
        SHA256.HashData(Encoding.UTF8.GetBytes(externalId));
}
