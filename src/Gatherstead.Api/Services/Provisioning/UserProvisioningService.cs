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

    private static string? ResolveEmail(ClaimsPrincipal principal)
    {
        var email = principal.FindFirst(ClaimTypes.Email)?.Value
            ?? principal.FindFirst("email")?.Value
            ?? principal.FindFirst("emails")?.Value
            ?? principal.FindFirst("preferred_username")?.Value;
        return string.IsNullOrWhiteSpace(email) ? null : email.Trim().ToLowerInvariant();
    }
}
