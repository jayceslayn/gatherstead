using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Paseto;
using Paseto.Builder;
using Gatherstead.Api.Services;

namespace Gatherstead.Api.Security;

public sealed class PasetoAuthenticationHandler : AuthenticationHandler<PasetoAuthenticationOptions>
{
    public PasetoAuthenticationHandler(
        IOptionsMonitor<PasetoAuthenticationOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder) : base(options, logger, encoder)
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

            // Extract user context for logging
            var userId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? principal.FindFirst("sub")?.Value;
            var ipAddress = Request.HttpContext.Connection.RemoteIpAddress?.ToString();

            Logger.LogInformation(
                "PASETO authentication succeeded. UserId: {UserId}, IP: {IpAddress}, UserAgent: {UserAgent}",
                userId,
                ipAddress,
                Request.Headers.UserAgent.ToString()
            );

            var ticket = new AuthenticationTicket(principal, PasetoAuthenticationDefaults.AuthenticationScheme);
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
        catch (SecurityTokenExpiredException ex)
        {
            Logger.LogWarning(
                "PASETO authentication failed: Token expired. IP: {IpAddress}, Message: {Message}",
                Request.HttpContext.Connection.RemoteIpAddress,
                ex.Message
            );
            return Task.FromResult(AuthenticateResult.Fail(ex.Message));
        }
        catch (SecurityTokenException ex)
        {
            Logger.LogWarning(ex,
                "PASETO authentication failed: {Reason}, IP: {IpAddress}, UserAgent: {UserAgent}",
                ex.Message,
                Request.HttpContext.Connection.RemoteIpAddress,
                Request.Headers.UserAgent.ToString()
            );
            return Task.FromResult(AuthenticateResult.Fail(ex.Message));
        }
        catch (Exception ex)
        {
            Logger.LogError(ex,
                "PASETO authentication error (unexpected): IP: {IpAddress}",
                Request.HttpContext.Connection.RemoteIpAddress
            );
            return Task.FromResult(AuthenticateResult.Fail("Authentication failed due to an unexpected error."));
        }
    }

    private ClaimsPrincipal ValidatePasetoToken(string token)
    {
        try
        {
            // Use library's native fluent API - no reflection needed
            var publicKeyBytes = Base64UrlDecode(Options.PublicKeyBase64!).ToArray();

            // Configure validation parameters - library handles standard validations
            var validationParams = new PasetoTokenValidationParameters
            {
                ValidateLifetime = Options.RequireExpirationTime,
                ValidateAudience = Options.ValidateAudience,
                ValidAudience = Options.Audience,
                ValidateIssuer = Options.ValidateIssuer,
                ValidIssuer = Options.Issuer
            };

            // Build and configure the decoder using fluent API
            var builder = new PasetoBuilder()
                .Use(ProtocolVersion.V4, Purpose.Public)
                .WithPublicKey(publicKeyBytes);

            // Add implicit assertion if configured
            if (!string.IsNullOrWhiteSpace(Options.ImplicitAssertion))
            {
                builder = builder.AddImplicitAssertion(Options.ImplicitAssertion!);
            }

            // Decode and validate the token - library handles exp, nbf, aud, iss automatically
            var result = builder.Decode(token, validationParams);

            // Check if validation succeeded
            if (!result.IsValid)
            {
                var errorMessage = result.Exception?.Message ?? "Token validation failed";
                if (result.Exception != null)
                {
                    throw new SecurityTokenException(errorMessage, result.Exception);
                }
                throw new SecurityTokenException(errorMessage);
            }

            // Extract claims from the validated token
            var claims = ExtractClaims(result.Paseto);

            // Perform additional custom validations
            ValidateTokenClaims(claims);

            // Check for token revocation (application-specific)
            CheckTokenRevocation(claims);

            return BuildPrincipal(claims);
        }
        catch (SecurityTokenException)
        {
            throw;
        }
        catch (Exception ex) when (ex.InnerException != null)
        {
            // Check if inner exception indicates token expiration or validation failure
            var innerMsg = ex.InnerException.Message.ToLowerInvariant();
            if (innerMsg.Contains("expired") || innerMsg.Contains("exp"))
            {
                throw new SecurityTokenExpiredException($"Token expired: {ex.InnerException.Message}", ex.InnerException);
            }
            throw new SecurityTokenException($"PASETO validation error: {ex.InnerException.Message}", ex.InnerException);
        }
        catch (Exception ex)
        {
            throw new SecurityTokenException($"PASETO validation error: {ex.Message}", ex);
        }
    }

    private static List<Claim> ExtractClaims(PasetoToken pasetoToken)
    {
        var claims = new List<Claim>();

        // PasetoPayload inherits from Dictionary<string, object>, so we can iterate directly
        foreach (var claim in pasetoToken.Payload)
        {
            var value = claim.Value?.ToString();
            if (!string.IsNullOrWhiteSpace(value))
            {
                // Map standard PASETO claims to ClaimTypes
                var claimType = claim.Key switch
                {
                    "sub" => ClaimTypes.NameIdentifier,
                    "email" => ClaimTypes.Email,
                    "name" => ClaimTypes.Name,
                    _ => claim.Key
                };

                claims.Add(new Claim(claimType, value));
            }
        }

        return claims;
    }

    private void ValidateTokenClaims(List<Claim> claims)
    {
        // Note: Library's ValidateLifetime handles exp, nbf validations automatically
        // Only perform custom validations not handled by the library

        var now = DateTimeOffset.UtcNow;

        // Custom validation: Maximum token age (application-specific requirement)
        if (Options.MaxTokenAge > TimeSpan.Zero)
        {
            var iatClaim = claims.FirstOrDefault(c => c.Type == "iat");
            if (iatClaim != null && TryParseDateTimeOffset(iatClaim.Value, out var iat))
            {
                // Check if token was issued in the future (with clock skew tolerance)
                if (iat.Subtract(Options.ClockSkew) > now)
                {
                    throw new SecurityTokenException($"Token issued in the future: {iat}. Current time: {now}");
                }

                // Validate maximum token age
                var age = now - iat;
                if (age > Options.MaxTokenAge)
                {
                    throw new SecurityTokenExpiredException($"Token age ({age}) exceeds maximum allowed age ({Options.MaxTokenAge})");
                }
            }
        }
    }

    private void CheckTokenRevocation(List<Claim> claims)
    {
        var jtiClaim = claims.FirstOrDefault(c => c.Type == "jti");
        if (jtiClaim != null)
        {
            var revocationService = Context.RequestServices.GetService<ITokenRevocationService>();
            if (revocationService != null)
            {
                var isRevoked = revocationService.IsTokenRevokedAsync(jtiClaim.Value)
                    .GetAwaiter().GetResult();

                if (isRevoked)
                {
                    throw new SecurityTokenException("Token has been revoked");
                }
            }
        }
    }

    private static bool TryParseDateTimeOffset(string value, out DateTimeOffset result)
    {
        result = DateTimeOffset.MinValue;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        // Try parsing as ISO 8601 string
        if (DateTimeOffset.TryParse(value, null,
            System.Globalization.DateTimeStyles.RoundtripKind, out result))
        {
            return true;
        }

        // Try parsing as Unix timestamp (seconds since epoch)
        if (long.TryParse(value, out var longValue))
        {
            try
            {
                result = DateTimeOffset.FromUnixTimeSeconds(longValue);
                return true;
            }
            catch
            {
                return false;
            }
        }

        return false;
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

public class SecurityTokenException : Exception
{
    public SecurityTokenException(string message) : base(message)
    {
    }

    public SecurityTokenException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

public class SecurityTokenExpiredException : Exception
{
    public SecurityTokenExpiredException(string message) : base(message)
    {
    }

    public SecurityTokenExpiredException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
