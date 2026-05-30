using System.Security.Claims;
using Gatherstead.Api.Contracts.Invitations;
using Gatherstead.Api.Contracts.Responses;
using Gatherstead.Api.Security;
using Gatherstead.Data;
using Gatherstead.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Gatherstead.Api.Services.Provisioning;

public class UserProvisioningService : IUserProvisioningService
{
    private readonly GathersteadDbContext _dbContext;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public UserProvisioningService(
        GathersteadDbContext dbContext,
        IHttpContextAccessor httpContextAccessor)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
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

            _dbContext.Users.Add(new User { Id = userId, ExternalId = externalId, Email = email });
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
        }

        var claimed = 0;
        if (!string.IsNullOrEmpty(email))
            claimed = await ClaimInvitationsAsync(userId, email, cancellationToken);

        // Bootstrap is not tenant-scoped, so the global tenant filter would hide tenant rows.
        var tenants = await _dbContext.TenantUsers
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(tu => tu.UserId == userId && !tu.IsDeleted)
            .Select(tu => new BootstrapTenantDto(tu.TenantId, tu.Role))
            .ToListAsync(cancellationToken);

        response.SetSuccess(new UserBootstrapDto(userId, claimed, tenants));
        return response;
    }

    private async Task<int> ClaimInvitationsAsync(Guid userId, string email, CancellationToken cancellationToken)
    {
        var pending = await _dbContext.Invitations
            .IgnoreQueryFilters()
            .Where(i => i.Email == email && i.Status == InvitationStatus.Pending && !i.IsDeleted)
            .ToListAsync(cancellationToken);

        if (pending.Count == 0) return 0;

        var now = DateTimeOffset.UtcNow;
        foreach (var invite in pending)
        {
            var hasTenantUser = await _dbContext.TenantUsers
                .IgnoreQueryFilters()
                .AnyAsync(tu => tu.TenantId == invite.TenantId && tu.UserId == userId && !tu.IsDeleted, cancellationToken);
            if (!hasTenantUser)
            {
                _dbContext.TenantUsers.Add(new TenantUser
                {
                    TenantId = invite.TenantId,
                    UserId = userId,
                    Role = invite.Role,
                });
            }

            if (invite.HouseholdId is Guid hid)
            {
                var hasHouseholdUser = await _dbContext.HouseholdUsers
                    .IgnoreQueryFilters()
                    .AnyAsync(hu => hu.HouseholdId == hid && hu.UserId == userId && !hu.IsDeleted, cancellationToken);
                if (!hasHouseholdUser)
                {
                    _dbContext.HouseholdUsers.Add(new HouseholdUser
                    {
                        TenantId = invite.TenantId,
                        HouseholdId = hid,
                        UserId = userId,
                        Role = invite.HouseholdRole ?? HouseholdRole.Member,
                    });
                }
            }

            invite.Status = InvitationStatus.Accepted;
            invite.AcceptedByUserId = userId;
            invite.AcceptedAt = now;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return pending.Count;
    }

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
