# PASETO Token Security Implementation Plan

## Context

The Gatherstead API uses PASETO (Platform-Agnostic Security Tokens) v4 Public tokens for authentication in a multi-tenant SaaS architecture. The current implementation has critical security gaps that expose the application to unauthorized access and token replay attacks. This plan addresses these vulnerabilities by implementing industry best practices from the PASETO specification and the paseto-dotnet reference library.

**Why this change is needed:**
- Tokens currently never expire, creating an unlimited attack window if compromised
- No time-based claim validation (exp, nbf, iat) leaves the system vulnerable
- Sensitive cryptographic keys stored in configuration files instead of secure vaults
- No rate limiting allows brute force authentication attacks
- Reflection-based token parsing is fragile and version-dependent
- Insufficient logging prevents detection of ongoing attacks

**Intended outcome:**
- Tokens expire after reasonable timeframes with proper validation
- Cryptographic keys securely stored in Azure Key Vault
- Rate limiting prevents brute force attacks
- Robust claim extraction without reflection dependencies
- Comprehensive security logging and monitoring
- Foundation for token revocation and refresh token strategies

## Current State Analysis

### Implementation Details

**Authentication Handler:** [PasetoAuthenticationHandler.cs](src/Gatherstead.Api/Security/PasetoAuthenticationHandler.cs)
- PASETO v4 Public protocol (Ed25519 asymmetric cryptography)
- Uses Paseto.Core v1.5.0 library
- Validates signature, audience, and issuer only
- Extracts token from `Authorization: Bearer <token>` header
- Uses extensive reflection to handle API compatibility

**Configuration:** [appsettings.json](src/Gatherstead.Api/appsettings.json)
- Public key: Base64-encoded Ed25519 public key (sample placeholder in version control)
- Audience: `gatherstead-api`
- Issuer: `https://login.example.com/your-tenant-id`
- Implicit assertion support (currently empty)

**Key Observation:** This API validates tokens only; it does not generate them. Tokens are issued by an external identity provider.

### Critical Security Gaps

1. **No Token Expiration Validation** ⚠️ CRITICAL
   - No `exp` (expiration) claim validation
   - Compromised tokens valid indefinitely
   - Violates PASETO best practices

2. **Missing Time-Based Validations** ⚠️ HIGH
   - No `nbf` (not-before) validation
   - No `iat` (issued-at) validation
   - No maximum token age enforcement

3. **Insecure Key Storage** ⚠️ HIGH
   - Public key stored in appsettings.json
   - Committed to version control (placeholder value)
   - Should use Azure Key Vault (already used for database encryption)

4. **No Rate Limiting** ⚠️ HIGH
   - Authentication endpoint unprotected
   - Vulnerable to brute force attacks
   - No IP-based or user-based throttling

5. **Fragile Claim Extraction** ⚠️ MEDIUM
   - Extensive reflection-based parsing (lines 65-120)
   - Version-dependent implementation
   - Difficult to test and maintain

6. **Insufficient Logging** ⚠️ MEDIUM
   - Generic warning on auth failure (line 58)
   - No structured logging with context
   - Cannot detect attack patterns

7. **No Token Revocation** ⚠️ MEDIUM
   - Cannot invalidate compromised tokens
   - No logout mechanism at token level
   - Missing for security incident response

## Implementation Plan

### Phase 1: Critical Token Validation (Priority: CRITICAL) 🚨

**Goal:** Prevent acceptance of expired or not-yet-valid tokens

#### 1.1 Add Token Expiration Validation

**File:** [PasetoAuthenticationHandler.cs:63-121](src/Gatherstead.Api/Security/PasetoAuthenticationHandler.cs#L63-L121)

**Changes to `ValidatePasetoToken` method:**

1. After extracting claims (line 114), add expiration validation:
   ```csharp
   private ClaimsPrincipal ValidatePasetoToken(string token)
   {
       // ... existing decode logic ...
       var payload = TryGetPayload(pasetoToken);
       if (payload is null)
       {
           throw new SecurityTokenException("Paseto.Core returned an empty payload.");
       }

       // NEW: Validate time-based claims
       ValidateTokenLifetime(payload);

       return BuildPrincipal(payload);
   }
   ```

2. Add new validation method:
   ```csharp
   private void ValidateTokenLifetime(IReadOnlyCollection<Claim> claims)
   {
       var now = DateTime.UtcNow;
       var clockSkew = TimeSpan.FromMinutes(5); // Tolerate 5 min clock drift

       // Validate expiration (required)
       var expClaim = claims.FirstOrDefault(c => c.Type == "exp");
       if (expClaim == null)
       {
           throw new SecurityTokenException("Token missing required 'exp' (expiration) claim");
       }

       if (!DateTime.TryParse(expClaim.Value, null, System.Globalization.DateTimeStyles.RoundtripKind, out var expiration))
       {
           throw new SecurityTokenException("Invalid 'exp' claim format. Expected ISO 8601 DateTime.");
       }

       if (expiration.Add(clockSkew) < now)
       {
           throw new SecurityTokenExpiredException($"Token expired at {expiration:O}");
       }

       // Validate not-before (optional but recommended)
       var nbfClaim = claims.FirstOrDefault(c => c.Type == "nbf");
       if (nbfClaim != null)
       {
           if (DateTime.TryParse(nbfClaim.Value, null, System.Globalization.DateTimeStyles.RoundtripKind, out var notBefore))
           {
               if (notBefore.Subtract(clockSkew) > now)
               {
                   throw new SecurityTokenException($"Token not valid until {notBefore:O}");
               }
           }
       }

       // Validate issued-at and maximum age (optional but recommended)
       var iatClaim = claims.FirstOrDefault(c => c.Type == "iat");
       if (iatClaim != null)
       {
           if (DateTime.TryParse(iatClaim.Value, null, System.Globalization.DateTimeStyles.RoundtripKind, out var issuedAt))
           {
               var maxAge = TimeSpan.FromHours(24); // Reject tokens older than 24 hours
               if (issuedAt.Add(maxAge) < now)
               {
                   throw new SecurityTokenException($"Token too old. Issued at {issuedAt:O}");
               }
           }
       }
   }
   ```

3. Add custom exception type at end of file:
   ```csharp
   public sealed class SecurityTokenExpiredException : SecurityTokenException
   {
       public SecurityTokenExpiredException(string message) : base(message) { }
   }
   ```

**Configuration:** Add to [PasetoAuthenticationOptions.cs:6-14](src/Gatherstead.Api/Security/PasetoAuthenticationOptions.cs#L6-L14)
```csharp
public TimeSpan ClockSkew { get; set; } = TimeSpan.FromMinutes(5);
public TimeSpan MaxTokenAge { get; set; } = TimeSpan.FromHours(24);
public bool RequireExpirationTime { get; set; } = true;
```

#### 1.2 Enhanced Security Logging

**File:** [PasetoAuthenticationHandler.cs:20-61](src/Gatherstead.Api/Security/PasetoAuthenticationHandler.cs#L20-L61)

**Changes to `HandleAuthenticateAsync` method:**

1. Replace generic logging (line 58) with structured logging:
   ```csharp
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
   ```

### Phase 2: Secure Key Management (Priority: HIGH) 🔐

**Goal:** Move cryptographic keys from configuration files to Azure Key Vault

#### 2.1 Azure Key Vault Integration

**File:** [Program.cs](src/Gatherstead.Api/Program.cs) - Authentication configuration section

**Prerequisites:**
- Azure Key Vault instance (reuse existing from database encryption setup)
- Secret named `paseto-public-key` containing Base64-encoded Ed25519 public key
- Managed Identity with Key Vault Secrets User role (already configured for database)

**Changes:**

1. Add Key Vault secret retrieval during authentication configuration:
   ```csharp
   // Replace existing authentication configuration with:
   builder.Services.AddSingleton<Azure.Security.KeyVault.Secrets.SecretClient>(sp =>
   {
       var keyVaultUrl = builder.Configuration["KeyVault:VaultUrl"]
           ?? throw new InvalidOperationException("KeyVault:VaultUrl configuration required");
       return new Azure.Security.KeyVault.Secrets.SecretClient(
           new Uri(keyVaultUrl),
           new Azure.Identity.DefaultAzureCredential()
       );
   });

   builder.Services
       .AddAuthentication(PasetoAuthenticationDefaults.AuthenticationScheme)
       .AddScheme<PasetoAuthenticationOptions, PasetoAuthenticationHandler>(
           PasetoAuthenticationDefaults.AuthenticationScheme,
           options =>
           {
               var authenticationSection = builder.Configuration.GetSection("Authentication");

               // Load public key from Key Vault
               var secretClient = builder.Services.BuildServiceProvider()
                   .GetRequiredService<Azure.Security.KeyVault.Secrets.SecretClient>();
               var secretName = authenticationSection["PublicKeySecretName"] ?? "paseto-public-key";
               var publicKeySecret = secretClient.GetSecret(secretName);

               options.PublicKeyBase64 = publicKeySecret.Value.Value;
               options.Audience = authenticationSection["Audience"];
               options.Issuer = authenticationSection["Issuer"];
               options.ImplicitAssertion = authenticationSection["ImplicitAssertion"];

               // Apply validation options from config
               if (TimeSpan.TryParse(authenticationSection["ClockSkew"], out var clockSkew))
                   options.ClockSkew = clockSkew;
               if (TimeSpan.TryParse(authenticationSection["MaxTokenAge"], out var maxTokenAge))
                   options.MaxTokenAge = maxTokenAge;
           });
   ```

#### 2.2 Configuration Updates

**File:** [appsettings.json:12-17](src/Gatherstead.Api/appsettings.json#L12-L17)

**Changes:**
```json
"Authentication": {
  "PublicKeySecretName": "paseto-public-key",
  "Audience": "gatherstead-api",
  "Issuer": "https://login.example.com/your-tenant-id",
  "ImplicitAssertion": "",
  "ClockSkew": "00:05:00",
  "MaxTokenAge": "24:00:00"
},
"KeyVault": {
  "VaultUrl": "https://your-keyvault.vault.azure.net/"
}
```

**Note:** Remove `PublicKey` field entirely - it will now come from Key Vault only.

**File:** [appsettings.Development.json](src/Gatherstead.Api/appsettings.Development.json) (create if doesn't exist)

For local development, allow override:
```json
{
  "Authentication": {
    "PublicKey": "MCowBQYDK2VwAyEA[dev-key-here]"
  }
}
```

Add fallback logic in Program.cs to use `PublicKey` from config if `PublicKeySecretName` not available (development mode).

### Phase 3: Rate Limiting (Priority: HIGH) 🛡️

**Goal:** Prevent brute force authentication attacks

#### 3.1 ASP.NET Core Rate Limiting Middleware

**File:** [Program.cs](src/Gatherstead.Api/Program.cs) - Service registration section

**Changes:**

1. Add rate limiting services (uses built-in .NET 10 middleware):
   ```csharp
   using System.Threading.RateLimiting;

   builder.Services.AddRateLimiter(options =>
   {
       // Fixed window rate limiter for authentication attempts
       options.AddFixedWindowLimiter("authentication", config =>
       {
           config.PermitLimit = 10; // 10 attempts
           config.Window = TimeSpan.FromMinutes(1); // per minute
           config.QueueLimit = 0; // No queueing
       });

       // More restrictive limiter for repeated failures
       options.AddFixedWindowLimiter("authentication-strict", config =>
       {
           config.PermitLimit = 3;
           config.Window = TimeSpan.FromMinutes(5);
           config.QueueLimit = 0;
       });

       // Global fallback
       options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
       {
           return RateLimitPartition.GetFixedWindowLimiter(
               context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
               _ => new FixedWindowRateLimiterOptions
               {
                   PermitLimit = 100,
                   Window = TimeSpan.FromMinutes(1)
               });
       });

       options.OnRejected = async (context, cancellationToken) =>
       {
           context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;

           var logger = context.HttpContext.RequestServices
               .GetRequiredService<ILogger<Program>>();
           logger.LogWarning(
               "Rate limit exceeded for IP: {IpAddress}, Endpoint: {Endpoint}",
               context.HttpContext.Connection.RemoteIpAddress,
               context.HttpContext.Request.Path
           );

           await context.HttpContext.Response.WriteAsJsonAsync(
               new { error = "Too many requests. Please try again later." },
               cancellationToken: cancellationToken
           );
       };
   });
   ```

2. Add middleware to pipeline (after UseRouting, before UseAuthentication):
   ```csharp
   app.UseRouting();
   app.UseRateLimiter(); // ADD THIS
   app.UseAuthentication();
   app.UseAuthorization();
   ```

3. Apply rate limiting to controllers handling authentication:
   ```csharp
   [ApiController]
   [Authorize]
   [EnableRateLimiting("authentication")]
   public class TenantsController : ControllerBase
   {
       // ...
   }
   ```

**Note:** Rate limiting is applied globally via GlobalLimiter, so explicit attributes are optional but provide finer control.

### Phase 4: Improved Claim Extraction (Priority: MEDIUM) 🔧

**Goal:** Remove reflection dependencies and improve maintainability

#### 4.1 Refactor Token Validation

**File:** [PasetoAuthenticationHandler.cs:63-187](src/Gatherstead.Api/Security/PasetoAuthenticationHandler.cs#L63-L187)

**Option A: Continue with Paseto.Core (Recommended for stability)**

Replace reflection-based approach with direct JSON parsing:

1. Add NuGet package: `System.Text.Json` (if not already present)

2. Modify `ValidatePasetoToken` to extract payload as JSON:
   ```csharp
   private ClaimsPrincipal ValidatePasetoToken(string token)
   {
       // Validate signature using existing Paseto.Core logic
       var pasetoToken = DecodeAndValidateSignature(token);

       // Extract payload property
       var payloadProperty = pasetoToken.GetType().GetProperty("Payload")
           ?? pasetoToken.GetType().GetProperty("Claims");
       var payloadObject = payloadProperty?.GetValue(pasetoToken);

       // Convert to JSON for easier parsing
       var payloadJson = System.Text.Json.JsonSerializer.Serialize(payloadObject);
       var claims = ExtractClaimsFromJson(payloadJson);

       ValidateTokenLifetime(claims);

       return BuildPrincipal(claims);
   }

   private List<Claim> ExtractClaimsFromJson(string payloadJson)
   {
       using var document = System.Text.Json.JsonDocument.Parse(payloadJson);
       var claims = new List<Claim>();

       foreach (var property in document.RootElement.EnumerateObject())
       {
           var value = property.Value.ValueKind == System.Text.Json.JsonValueKind.String
               ? property.Value.GetString()
               : property.Value.ToString();

           if (!string.IsNullOrEmpty(value))
           {
               claims.Add(new Claim(property.Name, value));
           }
       }

       return claims;
   }
   ```

**Option B: Migrate to paseto-dotnet (Future consideration)**

Evaluate migration to `Paseto` (daviddesmet/paseto-dotnet) library in a future major version:
- Provides `PasetoTokenValidationParameters` for built-in validation
- Cleaner API without reflection
- Better maintained and documented

**Recommendation:** Implement Option A now, plan Option B for next major version.

### Phase 5: Token Revocation (Priority: MEDIUM) 🚫

**Goal:** Enable invalidation of compromised tokens before expiration

#### 5.1 Database Schema for Revoked Tokens

**New File:** `src/Gatherstead.Data/Entities/RevokedToken.cs`

```csharp
namespace Gatherstead.Data.Entities;

public class RevokedToken
{
    public Guid Id { get; set; }

    /// <summary>
    /// JWT ID (jti claim) - unique identifier for the token
    /// </summary>
    public string Jti { get; set; } = null!;

    /// <summary>
    /// User who owned the token
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Tenant context (for multi-tenant isolation)
    /// </summary>
    public Guid? TenantId { get; set; }

    /// <summary>
    /// When the token was revoked
    /// </summary>
    public DateTime RevokedAt { get; set; }

    /// <summary>
    /// When the token expires (auto-cleanup after this)
    /// </summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// Reason for revocation (logout, password change, admin action, compromise)
    /// </summary>
    public string Reason { get; set; } = null!;
}
```

**Database Migration:** Create migration to add `RevokedTokens` table with indexes on `Jti` and `ExpiresAt`.

#### 5.2 Revocation Service

**New File:** `src/Gatherstead.Api/Services/ITokenRevocationService.cs`

```csharp
public interface ITokenRevocationService
{
    Task RevokeTokenAsync(string jti, Guid userId, Guid? tenantId, string reason, CancellationToken cancellationToken = default);
    Task<bool> IsTokenRevokedAsync(string jti, CancellationToken cancellationToken = default);
    Task CleanupExpiredRevocationsAsync(CancellationToken cancellationToken = default);
}
```

**New File:** `src/Gatherstead.Api/Services/TokenRevocationService.cs`

```csharp
public class TokenRevocationService : ITokenRevocationService
{
    private readonly GathersteadDbContext _dbContext;

    public TokenRevocationService(GathersteadDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task RevokeTokenAsync(string jti, Guid userId, Guid? tenantId, string reason, CancellationToken cancellationToken = default)
    {
        var revokedToken = new RevokedToken
        {
            Id = Guid.NewGuid(),
            Jti = jti,
            UserId = userId,
            TenantId = tenantId,
            RevokedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddHours(24), // Match max token age
            Reason = reason
        };

        _dbContext.RevokedTokens.Add(revokedToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> IsTokenRevokedAsync(string jti, CancellationToken cancellationToken = default)
    {
        return await _dbContext.RevokedTokens
            .AnyAsync(rt => rt.Jti == jti && rt.ExpiresAt > DateTime.UtcNow, cancellationToken);
    }

    public async Task CleanupExpiredRevocationsAsync(CancellationToken cancellationToken = default)
    {
        var expiredTokens = _dbContext.RevokedTokens
            .Where(rt => rt.ExpiresAt <= DateTime.UtcNow);

        _dbContext.RevokedTokens.RemoveRange(expiredTokens);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
```

#### 5.3 Integration with Authentication Handler

**File:** [PasetoAuthenticationHandler.cs:63-121](src/Gatherstead.Api/Security/PasetoAuthenticationHandler.cs#L63-L121)

Add revocation check to `ValidatePasetoToken`:

```csharp
private ClaimsPrincipal ValidatePasetoToken(string token)
{
    // ... existing validation ...
    var payload = TryGetPayload(pasetoToken);
    if (payload is null)
    {
        throw new SecurityTokenException("Paseto.Core returned an empty payload.");
    }

    ValidateTokenLifetime(payload);

    // NEW: Check if token has been revoked
    var jtiClaim = payload.FirstOrDefault(c => c.Type == "jti");
    if (jtiClaim != null)
    {
        var revocationService = Context.RequestServices.GetService<ITokenRevocationService>();
        if (revocationService != null)
        {
            var isRevoked = revocationService.IsTokenRevokedAsync(jtiClaim.Value).GetAwaiter().GetResult();
            if (isRevoked)
            {
                throw new SecurityTokenException("Token has been revoked");
            }
        }
    }

    return BuildPrincipal(payload);
}
```

**Note:** Consider caching revocation lookups with distributed cache (Redis) for high-volume scenarios.

#### 5.4 Logout Endpoint

**File:** [TenantsController.cs](src/Gatherstead.Api/Controllers/TenantsController.cs) or new AuthController

```csharp
[HttpPost("logout")]
[EnableRateLimiting("authentication")]
public async Task<IActionResult> Logout()
{
    var jti = User.FindFirst("jti")?.Value;
    var userId = _currentUserContext.UserId;
    var tenantId = _currentTenantContext.TenantId;

    if (jti == null || userId == null)
    {
        return BadRequest("Invalid token context");
    }

    await _tokenRevocationService.RevokeTokenAsync(
        jti,
        userId.Value,
        tenantId,
        "User logout",
        HttpContext.RequestAborted
    );

    return Ok(new { message = "Logged out successfully" });
}
```

### Phase 6: Testing (Priority: HIGH) ✅

#### 6.1 Unit Tests

**New File:** `tests/Gatherstead.Api.Tests/Security/PasetoAuthenticationHandlerTests.cs`

Test cases:
- ✅ Valid token with all claims passes authentication
- ✅ Expired token is rejected (`exp` in the past)
- ✅ Not-yet-valid token is rejected (`nbf` in the future)
- ✅ Token without `exp` claim is rejected
- ✅ Token with malformed `exp` claim is rejected
- ✅ Clock skew tolerance works correctly (+/- 5 minutes)
- ✅ Revoked token (by `jti`) is rejected
- ✅ Valid audience and issuer pass validation
- ✅ Invalid audience or issuer fails validation
- ✅ Malformed token returns authentication failure

#### 6.2 Integration Tests

**New File:** `tests/Gatherstead.Api.Tests/Integration/AuthenticationFlowTests.cs`

Test scenarios:
- ✅ End-to-end authentication with valid PASETO token
- ✅ Tenant isolation with multi-tenant tokens
- ✅ Rate limiting enforcement (429 after limit exceeded)
- ✅ Logout endpoint revokes token successfully
- ✅ Revoked token cannot access protected endpoints
- ✅ Token expiration prevents access after TTL

#### 6.3 Security Testing

Manual security validation:
- ✅ Attempt to use expired token → 401 Unauthorized
- ✅ Attempt to brute force authentication → 429 Too Many Requests
- ✅ Revoke token and verify cannot be reused → 401 Unauthorized
- ✅ Verify public key not present in configuration files
- ✅ Verify audit logs capture authentication events

## Critical Files Summary

### Files to Modify

1. **[src/Gatherstead.Api/Security/PasetoAuthenticationHandler.cs](src/Gatherstead.Api/Security/PasetoAuthenticationHandler.cs)**
   - Add `ValidateTokenLifetime` method for exp/nbf/iat validation
   - Enhance error handling with specific exception types
   - Add revocation check using `ITokenRevocationService`
   - Improve structured logging throughout

2. **[src/Gatherstead.Api/Security/PasetoAuthenticationOptions.cs](src/Gatherstead.Api/Security/PasetoAuthenticationOptions.cs)**
   - Add `ClockSkew`, `MaxTokenAge`, `RequireExpirationTime` properties
   - Add `PublicKeySecretName` for Key Vault integration

3. **[src/Gatherstead.Api/Program.cs](src/Gatherstead.Api/Program.cs)**
   - Configure Azure Key Vault secret client
   - Update authentication configuration to load key from Key Vault
   - Add rate limiting middleware
   - Register `ITokenRevocationService`

4. **[src/Gatherstead.Api/appsettings.json](src/Gatherstead.Api/appsettings.json)**
   - Remove `PublicKey` field
   - Add `PublicKeySecretName` and `KeyVault:VaultUrl`
   - Add `ClockSkew` and `MaxTokenAge` configuration
   - Update for rate limiting settings (optional)

### Files to Create

5. **src/Gatherstead.Data/Entities/RevokedToken.cs**
   - Entity for tracking revoked tokens

6. **src/Gatherstead.Api/Services/ITokenRevocationService.cs**
   - Interface for token revocation operations

7. **src/Gatherstead.Api/Services/TokenRevocationService.cs**
   - Implementation of token revocation service

8. **tests/Gatherstead.Api.Tests/Security/PasetoAuthenticationHandlerTests.cs**
   - Comprehensive unit tests for authentication handler

9. **tests/Gatherstead.Api.Tests/Integration/AuthenticationFlowTests.cs**
   - End-to-end integration tests

### Database Migration

10. **Create migration:** `dotnet ef migrations add AddRevokedTokenTable`
    - Creates `RevokedTokens` table with proper indexes

## Verification Plan

### Phase 1 Verification: Token Validation

1. **Setup test token issuer** that creates tokens with `exp`, `nbf`, and `iat` claims
2. **Test expired token:**
   ```bash
   # Create token with exp: 2026-01-01T00:00:00Z (past)
   curl -H "Authorization: Bearer [expired-token]" https://localhost:5001/api/tenants
   # Expected: 401 Unauthorized with "Token expired" message
   ```

3. **Test valid token:**
   ```bash
   # Create token with exp: 2026-12-31T23:59:59Z (future)
   curl -H "Authorization: Bearer [valid-token]" https://localhost:5001/api/tenants
   # Expected: 200 OK with tenant data
   ```

4. **Test not-yet-valid token:**
   ```bash
   # Create token with nbf: 2026-12-31T00:00:00Z (future)
   curl -H "Authorization: Bearer [future-token]" https://localhost:5001/api/tenants
   # Expected: 401 Unauthorized with "Token not valid until" message
   ```

5. **Check logs** in Application Insights or console:
   - Verify structured logging includes UserId, IP, UserAgent
   - Verify failed attempts logged with specific reasons

### Phase 2 Verification: Key Vault Integration

1. **Store public key in Key Vault:**
   ```bash
   az keyvault secret set \
     --vault-name your-keyvault \
     --name paseto-public-key \
     --value "MCowBQYDK2VwAyEA[your-real-key-here]"
   ```

2. **Verify application starts successfully** and loads key from Key Vault
   - Check startup logs for Key Vault connection
   - Verify no errors about missing public key

3. **Verify appsettings.json** does not contain `PublicKey` field (removed)

4. **Test authentication** with Key Vault-sourced key works identically

### Phase 3 Verification: Rate Limiting

1. **Rapid authentication attempts:**
   ```bash
   # Send 15 requests in quick succession
   for i in {1..15}; do
     curl -H "Authorization: Bearer invalid" https://localhost:5001/api/tenants
   done
   # Expected: First 10 return 401, next 5 return 429 Too Many Requests
   ```

2. **Verify rate limit headers** in response:
   ```
   RateLimit-Limit: 10
   RateLimit-Remaining: 0
   RateLimit-Reset: [timestamp]
   ```

3. **Check logs** for rate limit warnings with IP address

### Phase 4 Verification: Claim Extraction

1. **Test tokens with various payload structures:**
   - Nested claims
   - Array values
   - Non-string values (numbers, booleans)

2. **Verify authentication succeeds** with well-formed tokens

3. **Run unit tests:** `dotnet test --filter FullyQualifiedName~PasetoAuthenticationHandlerTests`
   - All tests should pass

### Phase 5 Verification: Token Revocation

1. **Authenticate with valid token:**
   ```bash
   TOKEN="v4.public.[valid-token-with-jti]"
   curl -H "Authorization: Bearer $TOKEN" https://localhost:5001/api/tenants
   # Expected: 200 OK
   ```

2. **Revoke the token:**
   ```bash
   curl -X POST -H "Authorization: Bearer $TOKEN" https://localhost:5001/api/auth/logout
   # Expected: 200 OK
   ```

3. **Attempt to use revoked token:**
   ```bash
   curl -H "Authorization: Bearer $TOKEN" https://localhost:5001/api/tenants
   # Expected: 401 Unauthorized with "Token has been revoked" message
   ```

4. **Check database:**
   ```sql
   SELECT * FROM RevokedTokens WHERE Jti = '[jti-value]';
   -- Should return one row with RevokedAt and ExpiresAt timestamps
   ```

5. **Verify cleanup job** removes expired revocations:
   ```bash
   # Wait for tokens to expire, then run cleanup
   # Check database has no expired revocations remaining
   ```

### End-to-End Verification

1. **Multi-tenant token test:**
   - Create token for User A in Tenant 1
   - Verify User A can access Tenant 1 resources
   - Verify User A cannot access Tenant 2 resources (403 Forbidden)

2. **Security monitoring:**
   - Generate authentication failures
   - Verify Application Insights captures events with:
     - Event type (success/failure)
     - User ID
     - IP address
     - Reason for failure
     - Timestamp

3. **Performance testing:**
   - Measure token validation latency (should be <10ms p95)
   - Verify rate limiting overhead is minimal (<5ms p95)
   - Test under load (1000 req/sec) to ensure stability

### Rollback Plan

If critical issues arise:

1. **Disable expiration validation temporarily:**
   - Set `RequireExpirationTime = false` in configuration
   - Deploy configuration change
   - Monitor for resolution

2. **Disable rate limiting:**
   - Comment out `app.UseRateLimiter()` in Program.cs
   - Redeploy

3. **Fallback to appsettings public key:**
   - Add fallback logic in Program.cs to use `PublicKey` from config if Key Vault unavailable
   - Temporarily add `PublicKey` back to appsettings.json

4. **Disable revocation checks:**
   - Comment out revocation check in `ValidatePasetoToken`
   - Redeploy

Each rollback should be accompanied by monitoring to ensure stability before proceeding with fixes.

---

## Implementation Priority Summary

**Week 1 (Critical Security Fixes):**
- ✅ Phase 1: Token expiration validation (`exp`, `nbf`, `iat`)
- ✅ Phase 1: Enhanced security logging
- ✅ Phase 3: Rate limiting middleware

**Week 2 (Secure Infrastructure):**
- ✅ Phase 2: Azure Key Vault integration for public key
- ✅ Phase 6: Unit tests for validation logic
- ✅ Phase 6: Integration tests for authentication flow

**Week 3-4 (Defense in Depth):**
- ✅ Phase 4: Improved claim extraction (remove reflection)
- ✅ Phase 5: Token revocation service and database schema
- ✅ Phase 5: Logout endpoint
- ✅ Phase 6: Security testing and validation

**Future Enhancements (Backlog):**
- Consider migration to paseto-dotnet library (major version)
- Implement refresh token strategy (when token generation added)
- Add Redis caching for revocation lookups (if performance requires)
- Multi-key support for seamless key rotation

---

## References

- [PASETO Claims Specification](https://github.com/paseto-standard/paseto-spec/blob/master/docs/02-Implementation-Guide/04-Claims.md)
- [paseto-dotnet Library (daviddesmet)](https://github.com/daviddesmet/paseto-dotnet)
- [ASP.NET Core Rate Limiting](https://learn.microsoft.com/en-us/aspnet/core/performance/rate-limit)
- [Azure Key Vault Best Practices](https://learn.microsoft.com/en-us/azure/key-vault/general/best-practices)
- [OWASP Authentication Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/Authentication_Cheat_Sheet.html)
