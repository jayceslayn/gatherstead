using System.Data;
using Gatherstead.Api.Security;
using Gatherstead.Api.Services.Directory;
using Gatherstead.Api.Services.Observability;
using Gatherstead.Data;
using Gatherstead.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Gatherstead.Api.Services.AccountDeletion;

/// <inheritdoc cref="IAccountDeletionService"/>
public sealed class AccountDeletionService : IAccountDeletionService
{
    private readonly GathersteadDbContext _db;
    private readonly IDirectoryAccountService _directoryAccountService;
    private readonly ITokenRevocationService _tokenRevocationService;
    private readonly ISecurityEventLogger _securityEventLogger;
    private readonly IAuthCache _authCache;
    private readonly ILogger<AccountDeletionService> _logger;

    public AccountDeletionService(
        GathersteadDbContext db,
        IDirectoryAccountService directoryAccountService,
        ITokenRevocationService tokenRevocationService,
        ISecurityEventLogger securityEventLogger,
        IAuthCache authCache,
        ILogger<AccountDeletionService> logger)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _directoryAccountService = directoryAccountService ?? throw new ArgumentNullException(nameof(directoryAccountService));
        _tokenRevocationService = tokenRevocationService ?? throw new ArgumentNullException(nameof(tokenRevocationService));
        _securityEventLogger = securityEventLogger ?? throw new ArgumentNullException(nameof(securityEventLogger));
        _authCache = authCache ?? throw new ArgumentNullException(nameof(authCache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<AccountDeletionResult> DeleteUserAsync(
        Guid userId,
        Guid initiatedByUserId,
        string? callerTokenId = null,
        CancellationToken cancellationToken = default)
    {
        // IgnoreQueryFilters throughout: this operation is intentionally cross-tenant (no tenant
        // context on /api/me) and must also see soft-deleted rows so nothing lingers.
        var user = await _db.Users
            .IgnoreQueryFilters().AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user is null)
            return AccountDeletionResult.NotFound(userId);

        List<Guid> userTenantIds;
        List<Guid> fullPurgeTenantIds;
        List<Guid> membershipRemovalTenantIds;
        List<Guid> memberPurgeIds;

        // Serializable so the memberships read below range-locks: a concurrent join/role change in
        // one of these tenants blocks until commit, so the purge can never run on a stale plan.
        await using (var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken))
        {
            // Soft-deleted memberships included: a group the user was removed from earlier still
            // holds their linked member's PII, which this erasure must sweep too.
            var myMemberships = await _db.TenantUsers
                .IgnoreQueryFilters().AsNoTracking()
                .Where(tu => tu.UserId == userId)
                .Select(tu => new { tu.TenantId, tu.Role, tu.LinkedMemberId, tu.IsDeleted })
                .ToListAsync(cancellationToken);

            userTenantIds = myMemberships.Select(m => m.TenantId).Distinct().ToList();

            var otherMemberships = await _db.TenantUsers
                .IgnoreQueryFilters().AsNoTracking()
                .Where(tu => userTenantIds.Contains(tu.TenantId) && tu.UserId != userId)
                .Select(tu => new { tu.TenantId, tu.Role, tu.IsDeleted })
                .ToListAsync(cancellationToken);

            // Groups the user (as Owner) has already deleted in the app: erasure finalizes those
            // with a hard purge instead of blocking on ownership transfer.
            var deletedTenantIds = (await _db.Tenants
                    .IgnoreQueryFilters().AsNoTracking()
                    .Where(t => userTenantIds.Contains(t.Id) && t.IsDeleted)
                    .Select(t => t.Id)
                    .ToListAsync(cancellationToken))
                .ToHashSet();

            fullPurgeTenantIds = [];
            membershipRemovalTenantIds = [];
            memberPurgeIds = [];
            var blockingTenantIds = new List<Guid>();

            foreach (var tenantId in userTenantIds)
            {
                var mine = myMemberships.Where(m => m.TenantId == tenantId).ToList();
                var mineActive = mine.FirstOrDefault(m => !m.IsDeleted);
                var others = otherMemberships.Where(m => m.TenantId == tenantId).ToList();
                var activeOthers = others.Where(o => !o.IsDeleted).ToList();

                if (others.Count == 0)
                {
                    // No one else has ever belonged — the whole group is the user's personal data.
                    fullPurgeTenantIds.Add(tenantId);
                }
                else if (activeOthers.Count == 0)
                {
                    // Formerly shared; every other membership is soft-deleted. Only the group's own
                    // Owner may dissolve it (erasing the residual ex-member rows with it); anyone
                    // else just leaves, so no data they don't own is destroyed.
                    if (mineActive?.Role == TenantRole.Owner)
                        fullPurgeTenantIds.Add(tenantId);
                    else
                        membershipRemovalTenantIds.Add(tenantId);
                }
                else if (mineActive?.Role == TenantRole.Owner && !activeOthers.Any(o => o.Role == TenantRole.Owner))
                {
                    // Sole Owner of a group with other active members. If the Owner has already
                    // deleted the group itself, erasure finalizes that deletion (hard purge);
                    // otherwise refuse — removing the sole Owner would orphan a live shared group.
                    if (deletedTenantIds.Contains(tenantId))
                        fullPurgeTenantIds.Add(tenantId);
                    else
                        blockingTenantIds.Add(tenantId);
                }
                else
                {
                    membershipRemovalTenantIds.Add(tenantId);
                }

                if (!fullPurgeTenantIds.Contains(tenantId))
                {
                    // Any linked member row (from an active or a soft-deleted membership) is the
                    // user's own PII inside a surviving group; it must be purged individually.
                    // Erasure wins over sharing: other logins still linked to the member (several
                    // logins may share one self-profile) keep their membership but lose the link.
                    memberPurgeIds.AddRange(mine
                        .Where(m => m.LinkedMemberId.HasValue)
                        .Select(m => m.LinkedMemberId!.Value));
                }
            }

            if (blockingTenantIds.Count > 0)
            {
                var blockingNames = await _db.Tenants
                    .IgnoreQueryFilters().AsNoTracking()
                    .Where(t => blockingTenantIds.Contains(t.Id))
                    .OrderBy(t => t.Name)
                    .Select(t => t.Name)
                    .ToListAsync(cancellationToken);
                return AccountDeletionResult.Blocked(userId, blockingTenantIds, blockingNames);
            }

            foreach (var tenantId in fullPurgeTenantIds)
                await PurgeTenantAsync(tenantId, cancellationToken);

            foreach (var memberId in memberPurgeIds.Distinct())
                await PurgeMemberAsync(memberId, cancellationToken);

            // Remove the user's remaining memberships across every tenant. Full-purge tenants' rows are
            // already gone; this clears the membership-removal tenants and any orphaned rows.
            await _db.HouseholdUsers.IgnoreQueryFilters()
                .Where(hu => hu.UserId == userId).ExecuteDeleteAsync(cancellationToken);
            await _db.TenantUsers.IgnoreQueryFilters()
                .Where(tu => tu.UserId == userId).ExecuteDeleteAsync(cancellationToken);

            // Invitations addressed to this person (the invitee email is their PII). Delete the
            // child grant rows first to satisfy the Restrict FK.
            if (!string.IsNullOrEmpty(user.Email))
            {
                var email = user.Email;
                var inviteIds = await _db.Invitations
                    .IgnoreQueryFilters().AsNoTracking()
                    .Where(i => i.Email == email)
                    .Select(i => i.Id)
                    .ToListAsync(cancellationToken);

                if (inviteIds.Count > 0)
                {
                    await _db.InvitationHouseholdAccess.IgnoreQueryFilters()
                        .Where(a => inviteIds.Contains(a.InvitationId)).ExecuteDeleteAsync(cancellationToken);
                    await _db.Invitations.IgnoreQueryFilters()
                        .Where(i => inviteIds.Contains(i.Id)).ExecuteDeleteAsync(cancellationToken);
                }
            }

            // The user's RevokedToken rows are intentionally KEPT: they are PII-free, self-expire,
            // and deleting them would un-revoke any still-live token for the rest of its lifetime.

            // Finally the account record itself.
            await _db.Users.IgnoreQueryFilters()
                .Where(u => u.Id == userId).ExecuteDeleteAsync(cancellationToken);

            // Tombstone (hash only, self-expiring): bootstrap refuses to re-provision this identity
            // while a pre-erasure token could still be valid, so a live session can't resurrect the
            // account. A genuine sign-up after the window provisions normally.
            var now = DateTime.UtcNow;
            _db.ErasedAccounts.Add(new ErasedAccount
            {
                Id = Guid.NewGuid(),
                ExternalIdHash = ErasedAccount.HashExternalId(user.ExternalId),
                ErasedAt = now,
                ExpiresAt = now + ErasedAccount.TombstoneLifetime,
            });
            await _db.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
        }

        // Application data is now durably erased. Kill the initiating session's token server-side
        // (self-service passes its own jti; the admin flow has no access to the target's tokens and
        // relies on the tombstone above). A failure here must not fail the already-durable erasure.
        if (!string.IsNullOrWhiteSpace(callerTokenId))
        {
            try
            {
                await _tokenRevocationService.RevokeTokenAsync(
                    callerTokenId, userId, tenantId: null, reason: "Account erased", cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to revoke the deleting session's token for user {UserId}.", userId);
            }
        }

        // Attempt to remove the external-identity account (never throws); a failure is surfaced for
        // operator follow-up, not rolled back.
        var directoryOutcome = await _directoryAccountService.DeleteUserAsync(user.ExternalId, cancellationToken);
        if (directoryOutcome == DirectoryDeletionOutcome.Failed)
            _logger.LogError("Directory account deletion failed for user {UserId}; manual removal required.", userId);

        // Single cache-owned eviction so no user-keyed dimension (mapping, app-admin flag,
        // tenant/household roles) can be missed here.
        await _authCache.InvalidateAllForUserAsync(user.ExternalId, userId, userTenantIds, cancellationToken);

        // Durable, PII-free record that an erasure occurred.
        await _securityEventLogger.LogAsync(
            SecurityEventType.AccountDeleted,
            SecurityEventSeverity.Info,
            resource: $"User:{userId}",
            detail: $"{{\"initiatedByUserId\":\"{initiatedByUserId}\",\"tenantsPurged\":{fullPurgeTenantIds.Count}," +
                    $"\"membershipsRemoved\":{membershipRemovalTenantIds.Count},\"directory\":\"{directoryOutcome}\"}}",
            tenantId: null,
            userId: initiatedByUserId,
            cancellationToken: cancellationToken);

        return new AccountDeletionResult
        {
            Status = AccountDeletionStatus.Deleted,
            UserId = userId,
            TenantsPurged = fullPurgeTenantIds.Count,
            MembershipsRemoved = membershipRemovalTenantIds.Count,
            DirectoryOutcome = directoryOutcome,
        };
    }

    /// <summary>
    /// Hard-deletes every row belonging to a tenant, in leaf-to-root FK order so the Restrict delete
    /// behaviour is never violated. Each tenant-scoped table appears exactly once; the completeness
    /// guards in <c>AccountDeletionServiceTests</c> fail when a new tenant-scoped or
    /// member-referencing entity is not covered here.
    /// </summary>
    private async Task PurgeTenantAsync(Guid tenantId, CancellationToken ct)
    {
        // Member/plan sign-ups and attendances (reference members, plans, events, accommodations).
        await _db.ShoppingItemIntents.IgnoreQueryFilters().Where(x => x.TenantId == tenantId).ExecuteDeleteAsync(ct);
        await _db.MealIntents.IgnoreQueryFilters().Where(x => x.TenantId == tenantId).ExecuteDeleteAsync(ct);
        await _db.TaskIntents.IgnoreQueryFilters().Where(x => x.TenantId == tenantId).ExecuteDeleteAsync(ct);
        await _db.AccommodationIntents.IgnoreQueryFilters().Where(x => x.TenantId == tenantId).ExecuteDeleteAsync(ct);
        await _db.EventAttendances.IgnoreQueryFilters().Where(x => x.TenantId == tenantId).ExecuteDeleteAsync(ct);
        await _db.MealAttendances.IgnoreQueryFilters().Where(x => x.TenantId == tenantId).ExecuteDeleteAsync(ct);

        // Custom attribute rows (reference their parent entity).
        await _db.ShoppingItemAttributes.IgnoreQueryFilters().Where(x => x.TenantId == tenantId).ExecuteDeleteAsync(ct);
        await _db.HouseholdMemberAttributes.IgnoreQueryFilters().Where(x => x.TenantId == tenantId).ExecuteDeleteAsync(ct);
        await _db.EquipmentAttributes.IgnoreQueryFilters().Where(x => x.TenantId == tenantId).ExecuteDeleteAsync(ct);
        await _db.EventAttributes.IgnoreQueryFilters().Where(x => x.TenantId == tenantId).ExecuteDeleteAsync(ct);
        await _db.MealTemplateAttributes.IgnoreQueryFilters().Where(x => x.TenantId == tenantId).ExecuteDeleteAsync(ct);
        await _db.TaskTemplateAttributes.IgnoreQueryFilters().Where(x => x.TenantId == tenantId).ExecuteDeleteAsync(ct);
        await _db.AccommodationAttributes.IgnoreQueryFilters().Where(x => x.TenantId == tenantId).ExecuteDeleteAsync(ct);
        await _db.PropertyAttributes.IgnoreQueryFilters().Where(x => x.TenantId == tenantId).ExecuteDeleteAsync(ct);
        await _db.HouseholdAttributes.IgnoreQueryFilters().Where(x => x.TenantId == tenantId).ExecuteDeleteAsync(ct);
        await _db.TenantAttributes.IgnoreQueryFilters().Where(x => x.TenantId == tenantId).ExecuteDeleteAsync(ct);

        // Shopping items reference meal plans (Origin = Meal), so they go before the plans; then
        // plans (reference templates), then templates (reference events).
        await _db.ShoppingItems.IgnoreQueryFilters().Where(x => x.TenantId == tenantId).ExecuteDeleteAsync(ct);
        await _db.MealPlans.IgnoreQueryFilters().Where(x => x.TenantId == tenantId).ExecuteDeleteAsync(ct);
        await _db.TaskPlans.IgnoreQueryFilters().Where(x => x.TenantId == tenantId).ExecuteDeleteAsync(ct);
        await _db.MealTemplates.IgnoreQueryFilters().Where(x => x.TenantId == tenantId).ExecuteDeleteAsync(ct);
        await _db.TaskTemplates.IgnoreQueryFilters().Where(x => x.TenantId == tenantId).ExecuteDeleteAsync(ct);

        // Member children (reference members).
        await _db.ContactMethods.IgnoreQueryFilters().Where(x => x.TenantId == tenantId).ExecuteDeleteAsync(ct);
        await _db.Addresses.IgnoreQueryFilters().Where(x => x.TenantId == tenantId).ExecuteDeleteAsync(ct);
        await _db.MemberRelationships.IgnoreQueryFilters().Where(x => x.TenantId == tenantId).ExecuteDeleteAsync(ct);

        // Property tree + events.
        await _db.AccommodationBeds.IgnoreQueryFilters().Where(x => x.TenantId == tenantId).ExecuteDeleteAsync(ct);
        await _db.Accommodations.IgnoreQueryFilters().Where(x => x.TenantId == tenantId).ExecuteDeleteAsync(ct);
        await _db.Equipment.IgnoreQueryFilters().Where(x => x.TenantId == tenantId).ExecuteDeleteAsync(ct);
        await _db.Events.IgnoreQueryFilters().Where(x => x.TenantId == tenantId).ExecuteDeleteAsync(ct);

        // Invitations (grants reference the invitation) and user linkage rows.
        await _db.InvitationHouseholdAccess.IgnoreQueryFilters().Where(x => x.TenantId == tenantId).ExecuteDeleteAsync(ct);
        await _db.Invitations.IgnoreQueryFilters().Where(x => x.TenantId == tenantId).ExecuteDeleteAsync(ct);
        await _db.HouseholdUsers.IgnoreQueryFilters().Where(x => x.TenantId == tenantId).ExecuteDeleteAsync(ct);
        // TenantUser links a member (LinkedMemberId) so it must go before HouseholdMember.
        await _db.TenantUsers.IgnoreQueryFilters().Where(x => x.TenantId == tenantId).ExecuteDeleteAsync(ct);

        // Members, households, properties, then the tenant root.
        await _db.HouseholdMembers.IgnoreQueryFilters().Where(x => x.TenantId == tenantId).ExecuteDeleteAsync(ct);
        await _db.Households.IgnoreQueryFilters().Where(x => x.TenantId == tenantId).ExecuteDeleteAsync(ct);
        await _db.Properties.IgnoreQueryFilters().Where(x => x.TenantId == tenantId).ExecuteDeleteAsync(ct);
        await _db.Tenants.IgnoreQueryFilters().Where(x => x.Id == tenantId).ExecuteDeleteAsync(ct);
    }

    /// <summary>
    /// Hard-deletes a single household member and everything that references it, so the departing
    /// user's own personal data is erased from a shared group they are merely leaving.
    /// </summary>
    private async Task PurgeMemberAsync(Guid memberId, CancellationToken ct)
    {
        // Break the (Restrict) links that point at the member so the member row can be deleted.
        await _db.TenantUsers.IgnoreQueryFilters().Where(tu => tu.LinkedMemberId == memberId)
            .ExecuteUpdateAsync(s => s.SetProperty(tu => tu.LinkedMemberId, (Guid?)null), ct);
        await _db.Invitations.IgnoreQueryFilters().Where(i => i.LinkedMemberId == memberId)
            .ExecuteUpdateAsync(s => s.SetProperty(i => i.LinkedMemberId, (Guid?)null), ct);

        await _db.MemberRelationships.IgnoreQueryFilters()
            .Where(x => x.HouseholdMemberId == memberId || x.RelatedMemberId == memberId).ExecuteDeleteAsync(ct);
        await _db.ContactMethods.IgnoreQueryFilters().Where(x => x.HouseholdMemberId == memberId).ExecuteDeleteAsync(ct);
        await _db.Addresses.IgnoreQueryFilters().Where(x => x.HouseholdMemberId == memberId).ExecuteDeleteAsync(ct);
        await _db.HouseholdMemberAttributes.IgnoreQueryFilters().Where(x => x.HouseholdMemberId == memberId).ExecuteDeleteAsync(ct);
        await _db.EventAttendances.IgnoreQueryFilters().Where(x => x.HouseholdMemberId == memberId).ExecuteDeleteAsync(ct);
        await _db.MealAttendances.IgnoreQueryFilters().Where(x => x.HouseholdMemberId == memberId).ExecuteDeleteAsync(ct);
        await _db.MealIntents.IgnoreQueryFilters().Where(x => x.HouseholdMemberId == memberId).ExecuteDeleteAsync(ct);
        await _db.TaskIntents.IgnoreQueryFilters().Where(x => x.HouseholdMemberId == memberId).ExecuteDeleteAsync(ct);
        await _db.AccommodationIntents.IgnoreQueryFilters().Where(x => x.HouseholdMemberId == memberId).ExecuteDeleteAsync(ct);
        await _db.ShoppingItemIntents.IgnoreQueryFilters().Where(x => x.HouseholdMemberId == memberId).ExecuteDeleteAsync(ct);
        await _db.HouseholdMembers.IgnoreQueryFilters().Where(x => x.Id == memberId).ExecuteDeleteAsync(ct);
    }
}
