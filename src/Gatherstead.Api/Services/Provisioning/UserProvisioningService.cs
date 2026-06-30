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

        response.SetSuccess(new MeDto(user.Id, user.Email, user.DisplayName));
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

        response.SetSuccess(new MeDto(user.Id, user.Email, user.DisplayName));
        return response;
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
    /// Resolves the caller's email for invitation matching, but only when the identity provider
    /// asserts it is verified. Auto-claiming an invitation grants real tenant membership, so an
    /// unverified or self-asserted address (or a username-style <c>preferred_username</c>, which is
    /// not guaranteed to be an email) must never drive that flow — doing so would allow a user to
    /// claim an invitation intended for someone else's address and escalate privileges. When the
    /// email cannot be trusted this returns null; the user is still provisioned, just without
    /// auto-claim.
    /// </summary>
    private static string? ResolveEmail(ClaimsPrincipal principal)
    {
        if (!IsEmailVerified(principal))
            return null;

        var email = principal.FindFirst(ClaimTypes.Email)?.Value
            ?? principal.FindFirst("email")?.Value
            ?? principal.FindFirst("emails")?.Value;
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
    /// True only when the IdP explicitly marks the email as verified. Treated as unverified when
    /// the claim is absent so an IdP that omits it cannot silently enable auto-claim.
    /// </summary>
    private static bool IsEmailVerified(ClaimsPrincipal principal)
    {
        var verified = principal.FindFirst("email_verified")?.Value
            ?? principal.FindFirst("verified_email")?.Value;
        return string.Equals(verified, "true", StringComparison.OrdinalIgnoreCase);
    }
}
