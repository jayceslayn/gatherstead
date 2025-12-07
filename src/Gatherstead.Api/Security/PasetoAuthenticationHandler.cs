using System.Linq;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Paseto;
using Paseto.Cryptography.Key;

namespace Gatherstead.Api.Security;

public sealed class PasetoAuthenticationHandler : AuthenticationHandler<PasetoAuthenticationOptions>
{
    public PasetoAuthenticationHandler(
        IOptionsMonitor<PasetoAuthenticationOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        ISystemClock clock) : base(options, logger, encoder, clock)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("Authorization", out var authorizationHeader))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var header = authorizationHeader.ToString();
        if (string.IsNullOrWhiteSpace(header))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var prefix = PasetoAuthenticationDefaults.TokenPrefix + " ";
        if (!header.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var token = header[prefix.Length..].Trim();
        if (string.IsNullOrWhiteSpace(token))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        if (string.IsNullOrWhiteSpace(Options.PublicKeyBase64))
        {
            return Task.FromResult(AuthenticateResult.Fail("PASETO public key is not configured."));
        }

        try
        {
            var principal = ValidatePasetoToken(token);
            var ticket = new AuthenticationTicket(principal, PasetoAuthenticationDefaults.AuthenticationScheme);
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "PASETO authentication failed");
            return Task.FromResult(AuthenticateResult.Fail(ex));
        }
    }

    private ClaimsPrincipal ValidatePasetoToken(string token)
    {
        var builderType = Type.GetType("Paseto.Builder.PasetoBuilder, Paseto.Core")
            ?? Type.GetType("Paseto.PasetoBuilder, Paseto.Core");

        if (builderType is null)
        {
            throw new SecurityTokenException("Paseto.Core builder type was not found.");
        }

        var publicKeyType = Type.GetType("Paseto.Cryptography.Key.Ed25519PublicKey, Paseto.Core");
        if (publicKeyType is null)
        {
            throw new SecurityTokenException("Paseto.Core public key type was not found.");
        }

        var builderInstance = Activator.CreateInstance(builderType)
            ?? throw new InvalidOperationException("Unable to construct PASETO builder.");

        var publicKey = Activator.CreateInstance(publicKeyType, Base64UrlDecode(Options.PublicKeyBase64!).ToArray());
        if (publicKey is null)
        {
            throw new SecurityTokenException("Unable to construct PASETO public key instance.");
        }

        var configuredBuilder = InvokeIfExists(builderInstance, "UseV4Public", publicKey) ?? builderInstance;
        configuredBuilder = InvokeIfExists(configuredBuilder, "WithAudience", Options.Audience) ?? configuredBuilder;
        configuredBuilder = InvokeIfExists(configuredBuilder, "WithIssuer", Options.Issuer) ?? configuredBuilder;

        if (!string.IsNullOrWhiteSpace(Options.ImplicitAssertion))
        {
            configuredBuilder = InvokeIfExists(configuredBuilder, "WithImplicitAssertion", Options.ImplicitAssertion!) ?? configuredBuilder;
        }

        var decodeMethod = configuredBuilder
            .GetType()
            .GetMethods()
            .FirstOrDefault(m => m.Name == "Decode" && m.GetParameters().Length is 1 or 2);

        if (decodeMethod is null)
        {
            throw new SecurityTokenException("Paseto.Core decode method could not be located.");
        }

        var parameters = decodeMethod.GetParameters().Length == 1
            ? new object?[] { token }
            : new object?[] { token, Options.ImplicitAssertion };

        var pasetoToken = decodeMethod.Invoke(configuredBuilder, parameters)
            ?? throw new SecurityTokenException("PASETO token could not be decoded.");

        var payload = TryGetPayload(pasetoToken);
        if (payload is null)
        {
            throw new SecurityTokenException("Paseto.Core returned an empty payload.");
        }

        return BuildPrincipal(payload);
    }

    private static IReadOnlyCollection<Claim>? TryGetPayload(object pasetoToken)
    {
        var payloadProperty = pasetoToken.GetType().GetProperty("Payload")
            ?? pasetoToken.GetType().GetProperty("Claims");

        if (payloadProperty?.GetValue(pasetoToken) is not System.Collections.IEnumerable entries)
        {
            return null;
        }

        var claims = new List<Claim>();

        foreach (var entry in entries)
        {
            var kvpType = entry.GetType();
            var keyProperty = kvpType.GetProperty("Key");
            var valueProperty = kvpType.GetProperty("Value");

            if (keyProperty?.GetValue(entry) is not string name)
            {
                continue;
            }

            var value = valueProperty?.GetValue(entry)?.ToString();
            if (!string.IsNullOrWhiteSpace(value))
            {
                claims.Add(new Claim(name, value));
            }
        }

        return claims.Count == 0 ? null : claims;
    }

    private static object? InvokeIfExists(object instance, string methodName, params object?[] parameters)
    {
        var method = instance
            .GetType()
            .GetMethods()
            .FirstOrDefault(m => m.Name == methodName && m.GetParameters().Length == parameters.Length);

        return method?.Invoke(instance, parameters);
    }

    private static ClaimsPrincipal BuildPrincipal(IEnumerable<Claim> claims)
    {
        var identity = new ClaimsIdentity(claims, PasetoAuthenticationDefaults.AuthenticationScheme);
        return new ClaimsPrincipal(identity);
    }

    private static ReadOnlyMemory<byte> Base64UrlDecode(string value)
    {
        var padded = value.Replace('-', '+').Replace('_', '/');
        switch (padded.Length % 4)
        {
            case 2:
                padded += "==";
                break;
            case 3:
                padded += "=";
                break;
        }

        return Convert.FromBase64String(padded);
    }
}

public sealed class SecurityTokenException : Exception
{
    public SecurityTokenException(string message) : base(message)
    {
    }
}
