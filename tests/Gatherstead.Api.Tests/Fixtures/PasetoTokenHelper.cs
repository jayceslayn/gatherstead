using System.Security.Cryptography;
using Paseto;
using Paseto.Builder;

namespace Gatherstead.Api.Tests.Fixtures;

public class PasetoTokenHelper
{
    public byte[] SecretKey { get; }
    public byte[] PublicKey { get; }
    public string PublicKeyBase64 { get; }

    public const string TestAudience = "test-audience";
    public const string TestIssuer = "test-issuer";

    public PasetoTokenHelper()
    {
        var seed = RandomNumberGenerator.GetBytes(32);
        var keyPair = new PasetoBuilder()
            .Use(ProtocolVersion.V4, Purpose.Public)
            .GenerateAsymmetricKeyPair(seed);

        SecretKey = keyPair.SecretKey.Key.Span.ToArray();
        PublicKey = keyPair.PublicKey.Key.Span.ToArray();
        PublicKeyBase64 = Convert.ToBase64String(PublicKey);
    }

    public string GenerateToken(
        string? sub = null,
        string? email = null,
        string? name = null,
        string? jti = null,
        DateTime? iat = null,
        DateTime? exp = null,
        string? audience = null,
        string? issuer = null,
        string? implicitAssertion = null,
        Dictionary<string, object>? additionalClaims = null)
    {
        var builder = new PasetoBuilder()
            .Use(ProtocolVersion.V4, Purpose.Public)
            .WithSecretKey(SecretKey);

        if (sub != null)
            builder = builder.Subject(sub);
        if (email != null)
            builder = builder.AddClaim("email", email);
        if (name != null)
            builder = builder.AddClaim("name", name);
        if (jti != null)
            builder = builder.TokenIdentifier(jti);

        builder = builder.IssuedAt(iat ?? DateTime.UtcNow);
        builder = builder.Expiration(exp ?? DateTime.UtcNow.AddHours(1));
        builder = builder.Audience(audience ?? TestAudience);
        builder = builder.Issuer(issuer ?? TestIssuer);

        if (!string.IsNullOrEmpty(implicitAssertion))
            builder = builder.AddImplicitAssertion(implicitAssertion);

        if (additionalClaims != null)
        {
            foreach (var claim in additionalClaims)
                builder = builder.AddClaim(claim.Key, claim.Value);
        }

        return builder.Encode();
    }

    public string GenerateTokenWithDifferentKey(
        string? sub = null,
        DateTime? exp = null)
    {
        var differentSeed = RandomNumberGenerator.GetBytes(32);
        var differentKeyPair = new PasetoBuilder()
            .Use(ProtocolVersion.V4, Purpose.Public)
            .GenerateAsymmetricKeyPair(differentSeed);

        return new PasetoBuilder()
            .Use(ProtocolVersion.V4, Purpose.Public)
            .WithSecretKey(differentKeyPair.SecretKey.Key.Span.ToArray())
            .Subject(sub ?? Guid.NewGuid().ToString())
            .IssuedAt(DateTime.UtcNow)
            .Expiration(exp ?? DateTime.UtcNow.AddHours(1))
            .Audience(TestAudience)
            .Issuer(TestIssuer)
            .Encode();
    }
}
