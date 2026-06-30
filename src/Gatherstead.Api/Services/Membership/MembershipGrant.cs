using Gatherstead.Data;
using Gatherstead.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Gatherstead.Api.Services.Membership;

/// <summary>
/// Shared, idempotent helper for granting a user tenant membership (and optional household access).
/// Used by both invitation acceptance (tenant-scoped) and the bootstrap claim flow (tenant-less),
/// keeping the "add if absent, never downgrade" semantics in a single place.
/// </summary>
public static class MembershipGrant
{
    /// <summary>
    /// Grants the user a <see cref="TenantUser"/> at the given role and, when a household is
    /// supplied, a <see cref="HouseholdUser"/>. Existing non-deleted memberships are left untouched
    /// so re-running is safe and a user's role is never silently changed. Does not call
    /// <c>SaveChanges</c> — the caller controls persistence so this can compose with other writes.
    /// </summary>
    /// <remarks>
    /// Drops only the tenant query filter (via <see cref="GathersteadDbContext.TenantFilter"/>) with
    /// an explicit tenant/user scope so it behaves identically whether or not a tenant context is set
    /// (the bootstrap claim path runs before any tenant is resolved). Soft-delete stays enforced.
    /// </remarks>
    public static async Task GrantAsync(
        GathersteadDbContext dbContext,
        Guid tenantId,
        Guid userId,
        TenantRole role,
        Guid? householdId,
        HouseholdRole? householdRole,
        CancellationToken cancellationToken)
    {
        var hasTenantUser = await dbContext.TenantUsers
            .IgnoreQueryFilters([GathersteadDbContext.TenantFilter])
            .AnyAsync(tu => tu.TenantId == tenantId && tu.UserId == userId && !tu.IsDeleted, cancellationToken);
        if (!hasTenantUser)
        {
            dbContext.TenantUsers.Add(new TenantUser
            {
                TenantId = tenantId,
                UserId = userId,
                Role = role,
            });
        }

        if (householdId is Guid hid)
        {
            var hasHouseholdUser = await dbContext.HouseholdUsers
                .IgnoreQueryFilters([GathersteadDbContext.TenantFilter])
                .AnyAsync(hu => hu.HouseholdId == hid && hu.UserId == userId && !hu.IsDeleted, cancellationToken);
            if (!hasHouseholdUser)
            {
                dbContext.HouseholdUsers.Add(new HouseholdUser
                {
                    TenantId = tenantId,
                    HouseholdId = hid,
                    UserId = userId,
                    Role = householdRole ?? HouseholdRole.Member,
                });
            }
        }
    }
}
