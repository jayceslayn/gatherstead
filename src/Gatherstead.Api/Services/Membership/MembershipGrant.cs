using System.Collections.Generic;
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
    /// supplied, a <see cref="HouseholdUser"/>. Existing active memberships are left untouched so
    /// re-running is safe and a user's role is never silently changed; a soft-deleted membership
    /// (e.g. a previously-removed user being re-invited) is reactivated in place and set to the
    /// granted role. When <paramref name="linkedMemberId"/> is supplied it links the tenant user to
    /// that member, but only if the user has no existing link (an established link is never
    /// silently overwritten) and the member is not already linked to someone else. Does not call
    /// <c>SaveChanges</c> — the caller controls persistence so this can compose with other writes.
    /// </summary>
    /// <remarks>
    /// Drops the tenant query filter (via <see cref="GathersteadDbContext.TenantFilter"/>) with an
    /// explicit tenant/user scope so it behaves identically whether or not a tenant context is set
    /// (the bootstrap claim path runs before any tenant is resolved). The soft-delete filter is also
    /// dropped so an existing removed row can be found and reactivated rather than colliding on the
    /// composite primary key with a fresh insert.
    /// </remarks>
    public static async Task GrantAsync(
        GathersteadDbContext dbContext,
        Guid tenantId,
        Guid userId,
        TenantRole role,
        Guid? householdId,
        HouseholdRole? householdRole,
        CancellationToken cancellationToken,
        Guid? linkedMemberId = null)
    {
        // Load including soft-deleted (both filters dropped) so a removed membership is reactivated
        // rather than re-inserted — the composite PK (TenantId, UserId) allows only one row. Check the
        // change tracker first so repeated calls in one save scope (multi-household grant) reuse the
        // pending row instead of adding a duplicate.
        var tenantUser = dbContext.TenantUsers.Local
                .FirstOrDefault(tu => tu.TenantId == tenantId && tu.UserId == userId)
            ?? await dbContext.TenantUsers
                .IgnoreQueryFilters([GathersteadDbContext.TenantFilter, GathersteadDbContext.SoftDeleteFilter])
                .SingleOrDefaultAsync(tu => tu.TenantId == tenantId && tu.UserId == userId, cancellationToken);

        if (tenantUser is null)
        {
            tenantUser = new TenantUser
            {
                TenantId = tenantId,
                UserId = userId,
                Role = role,
            };
            dbContext.TenantUsers.Add(tenantUser);
        }
        else if (tenantUser.IsDeleted)
        {
            // Reactivate a previously-removed membership as a fresh grant at the invited role.
            tenantUser.IsDeleted = false;
            tenantUser.DeletedAt = null;
            tenantUser.DeletedByUserId = null;
            tenantUser.Role = role;
        }
        // else: active membership left untouched (never silently downgraded).

        // Only a user with no existing link may receive one — an established link is never silently
        // overwritten (re-inviting a linked user with a different member must not orphan the old link).
        if (linkedMemberId is Guid memberId && tenantUser.LinkedMemberId is null)
        {
            // Re-validate at grant time: a deferred claim can land days after the invite, by which
            // point the member may already be linked to another user. Skip silently rather than
            // throwing on the unique filtered index — membership/household access still apply.
            var alreadyLinked = await dbContext.TenantUsers
                .IgnoreQueryFilters([GathersteadDbContext.TenantFilter])
                .AnyAsync(tu => tu.TenantId == tenantId && tu.LinkedMemberId == memberId && tu.UserId != userId, cancellationToken);
            if (!alreadyLinked)
                tenantUser.LinkedMemberId = memberId;
        }

        if (householdId is Guid hid)
        {
            // Re-validate at grant time: the household may have been deleted between invite and
            // accept (existence is only checked at invite creation). Skip silently — granting
            // access to a soft-deleted household would create live access rows nothing can see.
            var householdExists = await dbContext.Households
                .IgnoreQueryFilters([GathersteadDbContext.TenantFilter])
                .AnyAsync(h => h.TenantId == tenantId && h.Id == hid, cancellationToken);
            if (!householdExists)
                return;

            var householdUser = dbContext.HouseholdUsers.Local
                    .FirstOrDefault(hu => hu.HouseholdId == hid && hu.UserId == userId)
                ?? await dbContext.HouseholdUsers
                    .IgnoreQueryFilters([GathersteadDbContext.TenantFilter, GathersteadDbContext.SoftDeleteFilter])
                    .SingleOrDefaultAsync(hu => hu.HouseholdId == hid && hu.UserId == userId, cancellationToken);

            if (householdUser is null)
            {
                dbContext.HouseholdUsers.Add(new HouseholdUser
                {
                    TenantId = tenantId,
                    HouseholdId = hid,
                    UserId = userId,
                    Role = householdRole ?? HouseholdRole.Member,
                });
            }
            else if (householdUser.IsDeleted)
            {
                householdUser.IsDeleted = false;
                householdUser.DeletedAt = null;
                householdUser.DeletedByUserId = null;
                householdUser.Role = householdRole ?? HouseholdRole.Member;
            }
            // else: active household access left untouched.
        }
    }

    /// <summary>
    /// Multi-household variant of <see cref="GrantAsync(GathersteadDbContext, Guid, Guid, TenantRole, Guid?, HouseholdRole?, CancellationToken, Guid?)"/>:
    /// ensures the tenant membership (and optional member link) and grants each supplied household in
    /// turn. With no households it still grants the tenant membership so a link-only invite works.
    /// </summary>
    public static async Task GrantAsync(
        GathersteadDbContext dbContext,
        Guid tenantId,
        Guid userId,
        TenantRole role,
        IReadOnlyCollection<(Guid HouseholdId, HouseholdRole Role)> households,
        CancellationToken cancellationToken,
        Guid? linkedMemberId = null)
    {
        if (households.Count == 0)
        {
            await GrantAsync(dbContext, tenantId, userId, role, null, null, cancellationToken, linkedMemberId);
            return;
        }

        var first = true;
        foreach (var (householdId, householdRole) in households)
        {
            // The member link only needs to be applied once; GrantAsync is idempotent for it anyway.
            await GrantAsync(dbContext, tenantId, userId, role, householdId, householdRole, cancellationToken, first ? linkedMemberId : null);
            first = false;
        }
    }
}
