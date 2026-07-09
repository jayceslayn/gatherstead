using System.Security.Claims;
using Gatherstead.Api.Contracts.Invitations;
using Gatherstead.Api.Contracts.Responses;
using Gatherstead.Api.Security;
using Gatherstead.Api.Services.Membership;
using Gatherstead.Api.Services.Observability;
using Gatherstead.Data;
using Gatherstead.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Gatherstead.Api.Services.Provisioning;

public class UserProvisioningService : IUserProvisioningService
{
    private readonly GathersteadDbContext _dbContext;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ISecurityEventLogger _securityEventLogger;
    private readonly IAuthCache _authCache;

    public UserProvisioningService(
        GathersteadDbContext dbContext,
        IHttpContextAccessor httpContextAccessor,
        ISecurityEventLogger securityEventLogger,
        IAuthCache authCache)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
        _securityEventLogger = securityEventLogger ?? throw new ArgumentNullException(nameof(securityEventLogger));
        _authCache = authCache ?? throw new ArgumentNullException(nameof(authCache));
    }

    public async Task<UserBootstrapResponse> BootstrapAsync(CancellationToken cancellationToken = default)
    {
        var response = new UserBootstrapResponse();

        var httpContext = _httpContextAccessor.HttpContext;
        var principal = httpContext?.User;
        if (httpContext is null || principal?.Identity?.IsAuthenticated != true)
        {
            response.AddResponseMessage(MessageType.ERROR, "Not authenticated.");
            return response;
        }

        var externalId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? principal.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(externalId))
        {
            response.AddResponseMessage(MessageType.ERROR, "Authenticated user is missing a required identifier claim.");
            return response;
        }

        var email = ResolveEmail(principal);

        var user = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.ExternalId == externalId, cancellationToken);

        Guid userId;
        if (user is null)
        {
            userId = Guid.NewGuid();
            // Pre-seed the resolved user id so the auditing interceptor can stamp the brand-new
            // (self-created) row before it becomes queryable.
            httpContext.Items[HttpContextCurrentUserContext.CacheKey] = (Guid?)userId;

            // DisplayName is seeded once here from the token "name" claim. Unlike Email (refreshed
            // below on every login), it is never re-synced afterwards so an in-app edit survives.
            _dbContext.Users.Add(new User
            {
                Id = userId,
                ExternalId = externalId,
                Email = email,
                DisplayName = ResolveDisplayName(principal),
            });
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        else
        {
            userId = user.Id;
            httpContext.Items[HttpContextCurrentUserContext.CacheKey] = (Guid?)userId;
            if (!string.IsNullOrEmpty(email) && !string.Equals(user.Email, email, StringComparison.Ordinal))
            {
                user.Email = email;
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
            // Intentionally do NOT touch DisplayName here: it is app-owned after first login.
        }

        // Seed the cross-request ExternalId → UserId mapping now that the row is durable.
        await _authCache.SetUserIdAsync(externalId, userId, cancellationToken);

        var claimed = 0;
        if (!string.IsNullOrEmpty(email))
            claimed = await ClaimInvitationsAsync(userId, email, cancellationToken);

        // Bootstrap is not tenant-scoped, so the global tenant filter would hide tenant rows.
        // Drop only the tenant filter; soft-delete stays enforced (the explicit !IsDeleted is
        // redundant defense-in-depth).
        var tenants = await _dbContext.TenantUsers
            .IgnoreQueryFilters([GathersteadDbContext.TenantFilter])
            .AsNoTracking()
            .Where(tu => tu.UserId == userId && !tu.IsDeleted)
            .Select(tu => new BootstrapTenantDto(tu.TenantId, tu.Role))
            .ToListAsync(cancellationToken);

        // A brand-new (self-created) user is never an app admin; only an existing row can carry the flag.
        var isAppAdmin = user?.IsAppAdmin ?? false;

        response.SetSuccess(new UserBootstrapDto(userId, isAppAdmin, claimed, tenants));
        return response;
    }

    public async Task<MeResponse> GetMeAsync(CancellationToken cancellationToken = default)
    {
        var response = new MeResponse();
        var user = await ResolveCurrentUserAsync(response, cancellationToken);
        if (user is null)
            return response;

        response.SetSuccess(new MeDto(user.Id, ResolveDisplayEmail(user), user.DisplayName));
        return response;
    }

    public async Task<MeResponse> UpdateDisplayNameAsync(string displayName, CancellationToken cancellationToken = default)
    {
        var response = new MeResponse();
        var user = await ResolveCurrentUserAsync(response, cancellationToken);
        if (user is null)
            return response;

        var normalized = (displayName ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            response.AddResponseMessage(MessageType.ERROR, "Display name is required.");
            return response;
        }
        if (normalized.Length > 256)
        {
            response.AddResponseMessage(MessageType.ERROR, "Display name must be 256 characters or fewer.");
            return response;
        }

        user.DisplayName = normalized;
        await _dbContext.SaveChangesAsync(cancellationToken);

        response.SetSuccess(new MeDto(user.Id, ResolveDisplayEmail(user), user.DisplayName));
        return response;
    }

    /// <summary>
    /// Email to surface on the caller's own profile: the stored (verified) email when present,
    /// otherwise the caller's raw token email claim. Display-only — never feeds invitation matching.
    /// </summary>
    private string? ResolveDisplayEmail(User user)
    {
        if (!string.IsNullOrEmpty(user.Email))
            return user.Email;

        var principal = _httpContextAccessor.HttpContext?.User;
        return principal is null ? null : ResolveEmailClaim(principal);
    }

    /// <summary>
    /// Loads the authenticated caller's tracked <c>User</c> row, adding an error message to the
    /// response when the caller is unauthenticated, missing the subject claim, or has not yet been
    /// provisioned (bootstrap runs at login, so a missing row is an exceptional state).
    /// </summary>
    private async Task<User?> ResolveCurrentUserAsync<T>(BaseEntityResponse<T> response, CancellationToken cancellationToken)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        var principal = httpContext?.User;
        if (httpContext is null || principal?.Identity?.IsAuthenticated != true)
        {
            response.AddResponseMessage(MessageType.ERROR, "Not authenticated.");
            return null;
        }

        var externalId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? principal.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(externalId))
        {
            response.AddResponseMessage(MessageType.ERROR, "Authenticated user is missing a required identifier claim.");
            return null;
        }

        var user = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.ExternalId == externalId, cancellationToken);
        if (user is null)
        {
            response.AddResponseMessage(MessageType.ERROR, "User has not been provisioned.");
            return null;
        }

        // Pre-seed the resolved id so the auditing interceptor doesn't issue a lazy lookup against
        // this same DbContext mid-SaveChanges (this route isn't tenant-scoped, so nothing else
        // populates the cache first). Mirrors the brand-new-user path in BootstrapAsync.
        httpContext.Items[HttpContextCurrentUserContext.CacheKey] = (Guid?)user.Id;
        return user;
    }

    private async Task<int> ClaimInvitationsAsync(Guid userId, string email, CancellationToken cancellationToken)
    {
        // Invitations are matched by email across every tenant (claim runs before any tenant is
        // resolved), so drop only the tenant filter; soft-delete stays enforced.
        var pending = await _dbContext.Invitations
            .IgnoreQueryFilters([GathersteadDbContext.TenantFilter])
            .Where(i => i.Email == email && i.Status == InvitationStatus.Pending && !i.IsDeleted)
            .ToListAsync(cancellationToken);

        if (pending.Count == 0) return 0;

        var now = DateTimeOffset.UtcNow;
        foreach (var invite in pending)
        {
            await MembershipGrant.GrantAsync(
                _dbContext, invite.TenantId, userId, invite.Role,
                invite.HouseholdId, invite.HouseholdRole, cancellationToken);

            invite.Status = InvitationStatus.Accepted;
            invite.AcceptedByUserId = userId;
            invite.AcceptedAt = now;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        // Evict any cached "no membership" role result for this user in each granted tenant so the
        // newly-claimed access is visible on the user's next request rather than after the TTL.
        foreach (var invite in pending)
        {
            await _authCache.InvalidateTenantUserAsync(invite.TenantId, userId, cancellationToken);
            await _authCache.InvalidateHouseholdUsersAsync(invite.TenantId, userId, cancellationToken);
        }

        // Emit attribution events after the grant is durable. Logged separately so a logging failure
        // can never roll back a membership grant. Invitee email is omitted (PII) — the invitation row
        // referenced by id already carries it.
        foreach (var invite in pending)
            await LogInvitationAcceptedAsync(invite, userId, cancellationToken);

        return pending.Count;
    }

    private Task LogInvitationAcceptedAsync(Invitation invite, Guid acceptedByUserId, CancellationToken cancellationToken)
    {
        // A self-grant (acceptor invited themselves) is the abuse vector worth surfacing for review.
        var selfGrant = invite.InvitedByUserId == acceptedByUserId;
        return _securityEventLogger.LogAsync(
            SecurityEventType.InvitationAccepted,
            selfGrant ? SecurityEventSeverity.Warning : SecurityEventSeverity.Info,
            resource: $"Invitation:{invite.Id}",
            detail: $"{{\"role\":\"{invite.Role}\",\"invitedByUserId\":{JsonId(invite.InvitedByUserId)},\"selfGrant\":{(selfGrant ? "true" : "false")}}}",
            tenantId: invite.TenantId,
            userId: acceptedByUserId,
            cancellationToken: cancellationToken);
    }

    private static string JsonId(Guid? id) => id is null ? "null" : $"\"{id}\"";

    /// <summary>
    /// Resolves the caller's email for invitation matching. Auto-claiming grants real tenant
    /// membership, so the address must be trustworthy. The API validates the token issuer
    /// (<c>ValidateIssuer</c>/<c>ValidIssuer</c>, see Program.cs), so every principal here was issued
    /// by our single configured Entra External ID tenant, which owns the account's email — verified
    /// via email OTP at self-service sign-up, or asserted by the federated social IdP — and is not
    /// self-assertable. We therefore trust the issuer's email and block only when the IdP
    /// <em>explicitly</em> marks it unverified (see <see cref="IsEmailExplicitlyUnverified"/>),
    /// returning null then so the user is still provisioned, just without auto-claim.
    /// </summary>
    private static string? ResolveEmail(ClaimsPrincipal principal)
    {
        if (IsEmailExplicitlyUnverified(principal))
            return null;

        return ResolveEmailClaim(principal);
    }

    /// <summary>
    /// Reads the caller's email claim (normalized) with no verification gate. Prefers the dedicated
    /// email claims, then falls back to <c>preferred_username</c> only when it is email-shaped —
    /// Entra External ID email accounts carry the address there, and the frontend session uses the
    /// same fallback. Safe for display of the caller's own profile; invitation matching stays gated
    /// behind <see cref="ResolveEmail"/>.
    /// </summary>
    private static string? ResolveEmailClaim(ClaimsPrincipal principal)
    {
        var email = principal.FindFirst(ClaimTypes.Email)?.Value
            ?? principal.FindFirst("email")?.Value
            ?? principal.FindFirst("emails")?.Value;

        if (string.IsNullOrWhiteSpace(email))
        {
            // preferred_username is not guaranteed to be an email, so only accept it when it looks like one.
            var preferredUsername = principal.FindFirst("preferred_username")?.Value;
            if (!string.IsNullOrWhiteSpace(preferredUsername) && preferredUsername.Contains('@'))
                email = preferredUsername;
        }

        return string.IsNullOrWhiteSpace(email) ? null : email.Trim().ToLowerInvariant();
    }

    /// <summary>
    /// Resolves the caller's display name from the token "name" claim (the Entra user-flow
    /// "Display Name" attribute). Used only to seed <see cref="User.DisplayName"/> at first login;
    /// returns null when absent so the seeded value stays empty rather than blank-padded.
    /// </summary>
    private static string? ResolveDisplayName(ClaimsPrincipal principal)
    {
        var name = principal.FindFirst("name")?.Value
            ?? principal.FindFirst(ClaimTypes.Name)?.Value;
        return string.IsNullOrWhiteSpace(name) ? null : name.Trim();
    }

    /// <summary>
    /// True only when the IdP <em>explicitly</em> asserts the email is not verified — a verification
    /// claim is present and equal to <c>false</c>. Every present claim is checked, so a negative signal
    /// can't be masked by another claim taking precedence. An absent claim is NOT treated as unverified:
    /// the token issuer is validated upstream, so we trust the issuer's email rather than blocking every
    /// login whose token omits the claim. Entra External ID doesn't emit these natively today; they are
    /// honored for a generic-OIDC / custom-claims-provider signal or a future federated IdP.
    /// <para>
    /// <c>xms_edov</c> is intentionally NOT consulted: it reports email <em>domain-owner</em>
    /// verification, which is <c>false</c> for consumer-domain email+password accounts even when the
    /// mailbox was OTP-verified at sign-up — gating on it would reject legitimate users. Revisit if a
    /// federated/social IdP is added, where <c>xms_edov</c> becomes a meaningful discriminator.
    /// </para>
    /// </summary>
    private static bool IsEmailExplicitlyUnverified(ClaimsPrincipal principal)
    {
        var verificationClaims = principal.FindAll("email_verified")
            .Concat(principal.FindAll("verified_email"));
        return verificationClaims.Any(
            c => string.Equals(c.Value, "false", StringComparison.OrdinalIgnoreCase));
    }
}
