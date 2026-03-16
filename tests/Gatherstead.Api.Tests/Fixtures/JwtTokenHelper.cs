using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;

namespace Gatherstead.Api.Tests.Fixtures;

public class JwtTokenHelper
{
    public RsaSecurityKey SecurityKey { get; }
    public SigningCredentials SigningCredentials { get; }

    public const string TestAudience = "test-audience";
    public const string TestIssuer = "https://test-idp.example.com/test-tenant-id/v2.0/";

    public JwtTokenHelper()
    {
        var rsa = RSA.Create(2048);
        SecurityKey = new RsaSecurityKey(rsa) { KeyId = "test-key-id" };
        SigningCredentials = new SigningCredentials(SecurityKey, SecurityAlgorithms.RsaSha256);
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
        Dictionary<string, object>? additionalClaims = null)
    {
        var claims = new List<Claim>();

        if (sub != null)
            claims.Add(new Claim(JwtRegisteredClaimNames.Sub, sub));
        if (email != null)
            claims.Add(new Claim("emails", email));
        if (name != null)
            claims.Add(new Claim("name", name));
        if (jti != null)
            claims.Add(new Claim(JwtRegisteredClaimNames.Jti, jti));

        if (additionalClaims != null)
        {
            foreach (var claim in additionalClaims)
                claims.Add(new Claim(claim.Key, claim.Value.ToString()!));
        }

        var issuedAt = iat ?? DateTime.UtcNow;
        var expires = exp ?? DateTime.UtcNow.AddHours(1);

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            IssuedAt = issuedAt,
            NotBefore = issuedAt,
            Expires = expires,
            Audience = audience ?? TestAudience,
            Issuer = issuer ?? TestIssuer,
            SigningCredentials = SigningCredentials,
        };

        var handler = new JwtSecurityTokenHandler();
        var token = handler.CreateToken(tokenDescriptor);
        return handler.WriteToken(token);
    }

    public string GenerateTokenWithDifferentKey(
        string? sub = null,
        DateTime? exp = null)
    {
        var differentRsa = RSA.Create(2048);
        var differentKey = new RsaSecurityKey(differentRsa) { KeyId = "different-key" };
        var differentCredentials = new SigningCredentials(differentKey, SecurityAlgorithms.RsaSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, sub ?? Guid.NewGuid().ToString()),
        };

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            IssuedAt = DateTime.UtcNow,
            NotBefore = DateTime.UtcNow,
            Expires = exp ?? DateTime.UtcNow.AddHours(1),
            Audience = TestAudience,
            Issuer = TestIssuer,
            SigningCredentials = differentCredentials,
        };

        var handler = new JwtSecurityTokenHandler();
        var token = handler.CreateToken(tokenDescriptor);
        return handler.WriteToken(token);
    }
}
