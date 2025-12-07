using Microsoft.AspNetCore.Authentication;

namespace Gatherstead.Api.Security;

public sealed class PasetoAuthenticationOptions : AuthenticationSchemeOptions
{
    public string? PublicKeyBase64 { get; set; }

    public string? Audience { get; set; }

    public string? Issuer { get; set; }

    public string? ImplicitAssertion { get; set; }
}
