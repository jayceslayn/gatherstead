using Microsoft.AspNetCore.Authentication;

namespace Gatherstead.Api.Security;

public sealed class PasetoAuthenticationOptions : AuthenticationSchemeOptions
{
    public string? PublicKeyBase64 { get; set; }

    public string? Audience { get; set; }

    public string? Issuer { get; set; }

    public string? ImplicitAssertion { get; set; }

    // Validation configuration
    public TimeSpan ClockSkew { get; set; } = TimeSpan.FromMinutes(5);

    public TimeSpan MaxTokenAge { get; set; } = TimeSpan.FromHours(24);

    public bool RequireExpirationTime { get; set; } = true;

    public bool ValidateAudience { get; set; } = true;

    public bool ValidateIssuer { get; set; } = true;
}
