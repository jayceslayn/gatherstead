using Gatherstead.Api.Services;
using Gatherstead.Api.Services.AccountDeletion;
using Gatherstead.Api.Services.Directory;
using Gatherstead.Api.Services.Observability;
using Gatherstead.Api.Tests.Fixtures;
using Gatherstead.Data;
using Gatherstead.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Gatherstead.Api.Tests.Services;

public class AccountDeletionServiceTests : IAsyncLifetime
{
    private GathersteadDbContext _db = null!;
    private FakeDirectoryAccountService _directory = null!;
    private FakeAuthCache _authCache = null!;
    private Mock<ISecurityEventLogger> _logger = null!;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public ValueTask InitializeAsync()
    {
        // tenantId: null so the auditing interceptor's tenant-write validation stays out of the way
        // while we seed multiple tenants; the service uses IgnoreQueryFilters throughout regardless.
        _db = TestDbContextFactory.Create(tenantId: null, currentUserId: Guid.NewGuid());
        _directory = new FakeDirectoryAccountService();
        _authCache = new FakeAuthCache();
        _logger = new Mock<ISecurityEventLogger>();
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        _db.Dispose();
        return ValueTask.CompletedTask;
    }

    private AccountDeletionService CreateService() =>
        new(
            _db,
            _directory,
            new TokenRevocationService(_db, _authCache, NullLogger<TokenRevocationService>.Instance),
            _logger.Object,
            _authCache,
            NullLogger<AccountDeletionService>.Instance);

    // ── Full purge: user is the tenant's only member ────────────────────────────

    [Fact]
    public async Task DeleteUser_SoleMemberTenant_ErasesEverything()
    {
        var user = await SeedUserAsync("solo@test");
        var (tenantId, memberId) = await SeedPersonalTenantAsync(user, ownerRole: TenantRole.Owner, linkMember: true);

        var result = await CreateService().DeleteUserAsync(user.Id, user.Id, cancellationToken: Ct);
        _db.ChangeTracker.Clear();

        Assert.Equal(AccountDeletionStatus.Deleted, result.Status);
        Assert.Equal(1, result.TenantsPurged);
        Assert.Equal(0, result.MembershipsRemoved);

        Assert.False(await AnyForTenantAsync(tenantId));
        Assert.Equal(0, await _db.Users.IgnoreQueryFilters().CountAsync(u => u.Id == user.Id, Ct));
        Assert.Equal(0, await _db.HouseholdMembers.IgnoreQueryFilters().CountAsync(m => m.Id == memberId, Ct));
        Assert.Equal(0, await _db.Tenants.IgnoreQueryFilters().CountAsync(t => t.Id == tenantId, Ct));
    }

    [Fact]
    public async Task DeleteUser_SweepsSoftDeletedRows()
    {
        var user = await SeedUserAsync("soft@test");
        var (tenantId, _) = await SeedPersonalTenantAsync(user, TenantRole.Owner, linkMember: true);

        // Soft-delete a PII row; the erasure must still remove it (IgnoreQueryFilters).
        var contact = await _db.ContactMethods.IgnoreQueryFilters().FirstAsync(Ct);
        contact.IsDeleted = true;
        await _db.SaveChangesAsync(Ct);

        await CreateService().DeleteUserAsync(user.Id, user.Id, cancellationToken: Ct);
        _db.ChangeTracker.Clear();

        Assert.Equal(0, await _db.ContactMethods.IgnoreQueryFilters().CountAsync(Ct));
        Assert.False(await AnyForTenantAsync(tenantId));
    }

    [Fact]
    public async Task DeleteUser_InvokesDirectoryDeletionAndAudit()
    {
        var user = await SeedUserAsync("dir@test");
        await SeedPersonalTenantAsync(user, TenantRole.Owner, linkMember: true);
        _directory.Outcome = DirectoryDeletionOutcome.Deleted;

        var result = await CreateService().DeleteUserAsync(user.Id, user.Id, cancellationToken: Ct);

        Assert.Contains(user.ExternalId, _directory.Deleted);
        Assert.Equal(DirectoryDeletionOutcome.Deleted, result.DirectoryOutcome);
        _logger.Verify(l => l.LogAsync(
            SecurityEventType.AccountDeleted, It.IsAny<SecurityEventSeverity>(),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<Guid?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── Session/identity hardening ───────────────────────────────────────────────

    [Fact]
    public async Task DeleteUser_WritesTombstone_RevokesCallerToken_AndKeepsExistingRevocations()
    {
        var user = await SeedUserAsync("session@test");
        await SeedPersonalTenantAsync(user, TenantRole.Owner, linkMember: true);
        _db.RevokedTokens.Add(new RevokedToken
        {
            Id = Guid.NewGuid(), Jti = "previously-revoked", UserId = user.Id,
            RevokedAt = DateTime.UtcNow, ExpiresAt = DateTime.UtcNow.AddHours(12), Reason = "logout",
        });
        await _db.SaveChangesAsync(Ct);

        await CreateService().DeleteUserAsync(user.Id, user.Id, callerTokenId: "current-session", Ct);
        _db.ChangeTracker.Clear();

        // A live tombstone for the identity hash blocks bootstrap re-provisioning.
        var hash = ErasedAccount.HashExternalId(user.ExternalId);
        Assert.True(await _db.ErasedAccounts.AnyAsync(
            t => t.ExternalIdHash == hash && t.ExpiresAt > DateTime.UtcNow, Ct));

        // The deleting session's token is revoked, and prior revocations survive the erasure
        // (deleting them would un-revoke still-live tokens).
        Assert.True(await _db.RevokedTokens.AnyAsync(r => r.Jti == "current-session", Ct));
        Assert.True(await _db.RevokedTokens.AnyAsync(r => r.Jti == "previously-revoked", Ct));
    }

    [Fact]
    public async Task DeleteUser_EvictsEveryUserKeyedCacheEntry()
    {
        var user = await SeedUserAsync("cache@test");
        var (tenantId, _) = await SeedPersonalTenantAsync(user, TenantRole.Owner, linkMember: true);

        await CreateService().DeleteUserAsync(user.Id, user.Id, cancellationToken: Ct);

        Assert.Contains($"user:{user.ExternalId}", _authCache.Invalidations);
        Assert.Contains($"admin:{user.Id}", _authCache.Invalidations);
        Assert.Contains($"tenantuser:{tenantId}:{user.Id}", _authCache.Invalidations);
        Assert.Contains($"hhusers:{tenantId}:{user.Id}", _authCache.Invalidations);
    }

    // ── Guard rail: sole owner of a shared tenant is refused ─────────────────────

    [Fact]
    public async Task DeleteUser_SoleOwnerOfSharedTenant_IsBlockedAndErasesNothing()
    {
        var owner = await SeedUserAsync("owner@test");
        var other = await SeedUserAsync("member@test");
        var (tenantId, _) = await SeedPersonalTenantAsync(owner, TenantRole.Owner, linkMember: false);
        await AddMembershipAsync(tenantId, other.Id, TenantRole.Member);

        var result = await CreateService().DeleteUserAsync(owner.Id, owner.Id, cancellationToken: Ct);
        _db.ChangeTracker.Clear();

        Assert.Equal(AccountDeletionStatus.BlockedByOwnership, result.Status);
        Assert.Contains(tenantId, result.BlockingTenantIds);
        Assert.Contains("Group", result.BlockingTenantNames);
        // Nothing was touched.
        Assert.Equal(1, await _db.Users.IgnoreQueryFilters().CountAsync(u => u.Id == owner.Id, Ct));
        Assert.Equal(2, await _db.TenantUsers.IgnoreQueryFilters().CountAsync(tu => tu.TenantId == tenantId, Ct));
    }

    [Fact]
    public async Task DeleteUser_SoleOwnerOfDeletedSharedTenant_PurgesInsteadOfBlocking()
    {
        var owner = await SeedUserAsync("dissolver@test");
        var other = await SeedUserAsync("bystander@test");
        var (tenantId, _) = await SeedPersonalTenantAsync(owner, TenantRole.Owner, linkMember: true);
        await AddMembershipAsync(tenantId, other.Id, TenantRole.Member);

        // The owner already deleted the group in the app (soft delete); account erasure must
        // finalize that deletion rather than demand an ownership transfer for a dead group.
        var tenant = await _db.Tenants.IgnoreQueryFilters().FirstAsync(t => t.Id == tenantId, Ct);
        tenant.IsDeleted = true;
        await _db.SaveChangesAsync(Ct);

        var result = await CreateService().DeleteUserAsync(owner.Id, owner.Id, cancellationToken: Ct);
        _db.ChangeTracker.Clear();

        Assert.Equal(AccountDeletionStatus.Deleted, result.Status);
        Assert.Equal(1, result.TenantsPurged);
        Assert.False(await AnyForTenantAsync(tenantId));
        Assert.Equal(0, await _db.Tenants.IgnoreQueryFilters().CountAsync(t => t.Id == tenantId, Ct));
        // The other member's account survives; only their rows inside the dissolved group are gone.
        Assert.Equal(1, await _db.Users.IgnoreQueryFilters().CountAsync(u => u.Id == other.Id, Ct));
    }

    // ── Membership removal: non-sole-owner leaves a shared tenant ────────────────

    [Fact]
    public async Task DeleteUser_NonOwnerInSharedTenant_RemovesOwnDataButKeepsTheGroup()
    {
        var owner = await SeedUserAsync("keep-owner@test");
        var leaver = await SeedUserAsync("leaver@test");
        var (tenantId, ownerMemberId) = await SeedPersonalTenantAsync(owner, TenantRole.Owner, linkMember: true);

        // The leaver joins with their own member record (+ a contact method of their own).
        var householdId = await _db.Households.IgnoreQueryFilters().Where(h => h.TenantId == tenantId).Select(h => h.Id).FirstAsync(Ct);
        var leaverMemberId = await AddMemberAsync(tenantId, householdId, "Leaver");
        await AddContactAsync(tenantId, leaverMemberId, "leaver-phone");
        await AddMembershipAsync(tenantId, leaver.Id, TenantRole.Member, leaverMemberId);

        var result = await CreateService().DeleteUserAsync(leaver.Id, leaver.Id, cancellationToken: Ct);
        _db.ChangeTracker.Clear();

        Assert.Equal(AccountDeletionStatus.Deleted, result.Status);
        Assert.Equal(0, result.TenantsPurged);
        Assert.Equal(1, result.MembershipsRemoved);

        // The leaver and their own data are gone.
        Assert.Equal(0, await _db.Users.IgnoreQueryFilters().CountAsync(u => u.Id == leaver.Id, Ct));
        Assert.Equal(0, await _db.TenantUsers.IgnoreQueryFilters().CountAsync(tu => tu.UserId == leaver.Id, Ct));
        Assert.Equal(0, await _db.HouseholdMembers.IgnoreQueryFilters().CountAsync(m => m.Id == leaverMemberId, Ct));
        // The group, its owner and their member survive.
        Assert.Equal(1, await _db.Users.IgnoreQueryFilters().CountAsync(u => u.Id == owner.Id, Ct));
        Assert.Equal(1, await _db.Tenants.IgnoreQueryFilters().CountAsync(t => t.Id == tenantId, Ct));
        Assert.Equal(1, await _db.HouseholdMembers.IgnoreQueryFilters().CountAsync(m => m.Id == ownerMemberId, Ct));
    }

    // ── Soft-deleted memberships: erasure must still sweep the linked member ─────

    [Fact]
    public async Task DeleteUser_ErasesLinkedMemberDataInGroupsTheUserWasRemovedFrom()
    {
        var owner = await SeedUserAsync("host-owner@test");
        var removed = await SeedUserAsync("removed@test");
        var (tenantId, _) = await SeedPersonalTenantAsync(owner, TenantRole.Owner, linkMember: true);

        // The user joined with a linked member (plus contact PII), then was removed from the group:
        // the membership row is soft-deleted but their member data remains, as TenantUserService does.
        var householdId = await _db.Households.IgnoreQueryFilters().Where(h => h.TenantId == tenantId).Select(h => h.Id).FirstAsync(Ct);
        var removedMemberId = await AddMemberAsync(tenantId, householdId, "Removed");
        await AddContactAsync(tenantId, removedMemberId, "removed-phone");
        await AddMembershipAsync(tenantId, removed.Id, TenantRole.Member, removedMemberId);
        var membership = await _db.TenantUsers.IgnoreQueryFilters().FirstAsync(tu => tu.UserId == removed.Id, Ct);
        membership.IsDeleted = true;
        await _db.SaveChangesAsync(Ct);

        var result = await CreateService().DeleteUserAsync(removed.Id, removed.Id, cancellationToken: Ct);
        _db.ChangeTracker.Clear();

        Assert.Equal(AccountDeletionStatus.Deleted, result.Status);
        // Their member row and PII are gone even though the membership was already soft-deleted…
        Assert.Equal(0, await _db.HouseholdMembers.IgnoreQueryFilters().CountAsync(m => m.Id == removedMemberId, Ct));
        Assert.Equal(0, await _db.ContactMethods.IgnoreQueryFilters().CountAsync(c => c.HouseholdMemberId == removedMemberId, Ct));
        Assert.Equal(0, await _db.TenantUsers.IgnoreQueryFilters().CountAsync(tu => tu.UserId == removed.Id, Ct));
        // …while the group itself survives untouched.
        Assert.Equal(1, await _db.Tenants.IgnoreQueryFilters().CountAsync(t => t.Id == tenantId, Ct));
    }

    // ── Formerly shared groups (every other membership soft-deleted) ─────────────

    [Fact]
    public async Task DeleteUser_LastActiveOwner_DissolvesFormerlySharedGroup()
    {
        var owner = await SeedUserAsync("last-owner@test");
        var exMember = await SeedUserAsync("ex-member@test");
        var (tenantId, _) = await SeedPersonalTenantAsync(owner, TenantRole.Owner, linkMember: true);
        await AddMembershipAsync(tenantId, exMember.Id, TenantRole.Member);
        var exMembership = await _db.TenantUsers.IgnoreQueryFilters().FirstAsync(tu => tu.UserId == exMember.Id, Ct);
        exMembership.IsDeleted = true;
        await _db.SaveChangesAsync(Ct);

        var result = await CreateService().DeleteUserAsync(owner.Id, owner.Id, cancellationToken: Ct);
        _db.ChangeTracker.Clear();

        // The owner may dissolve their own group; residual ex-member rows are erased with it.
        Assert.Equal(AccountDeletionStatus.Deleted, result.Status);
        Assert.Equal(1, result.TenantsPurged);
        Assert.False(await AnyForTenantAsync(tenantId));
        Assert.Equal(0, await _db.Tenants.IgnoreQueryFilters().CountAsync(t => t.Id == tenantId, Ct));
        // The ex-member's own account is untouched.
        Assert.Equal(1, await _db.Users.IgnoreQueryFilters().CountAsync(u => u.Id == exMember.Id, Ct));
    }

    [Fact]
    public async Task DeleteUser_LastActiveNonOwner_LeavesFormerlySharedGroupIntact()
    {
        var member = await SeedUserAsync("last-member@test");
        var exOwner = await SeedUserAsync("ex-owner@test");
        var (tenantId, memberMemberId) = await SeedPersonalTenantAsync(member, TenantRole.Member, linkMember: true);
        await AddMembershipAsync(tenantId, exOwner.Id, TenantRole.Owner);
        var exMembership = await _db.TenantUsers.IgnoreQueryFilters().FirstAsync(tu => tu.UserId == exOwner.Id, Ct);
        exMembership.IsDeleted = true;
        await _db.SaveChangesAsync(Ct);

        var result = await CreateService().DeleteUserAsync(member.Id, member.Id, cancellationToken: Ct);
        _db.ChangeTracker.Clear();

        // A non-owner never destroys a group others once belonged to — they just leave.
        Assert.Equal(AccountDeletionStatus.Deleted, result.Status);
        Assert.Equal(0, result.TenantsPurged);
        Assert.Equal(1, result.MembershipsRemoved);
        Assert.Equal(1, await _db.Tenants.IgnoreQueryFilters().CountAsync(t => t.Id == tenantId, Ct));
        // Their own linked member PII is still erased.
        Assert.Equal(0, await _db.HouseholdMembers.IgnoreQueryFilters().CountAsync(m => m.Id == memberMemberId, Ct));
    }

    // ── Invitations addressed to the user's email are swept, even in other tenants ─

    [Fact]
    public async Task DeleteUser_DeletesInvitationsAddressedToTheirEmail()
    {
        var user = await SeedUserAsync("invitee@test");
        await SeedPersonalTenantAsync(user, TenantRole.Owner, linkMember: true);

        // A separate tenant the user does NOT belong to, holding a pending invite to their email.
        var otherTenantId = Guid.NewGuid();
        _db.Tenants.Add(new Tenant { Id = otherTenantId, Name = "Other Group" });
        _db.Invitations.Add(new Invitation
        {
            Id = Guid.NewGuid(),
            TenantId = otherTenantId,
            Email = "invitee@test",
            Role = TenantRole.Member,
            Status = InvitationStatus.Pending,
        });
        await _db.SaveChangesAsync(Ct);

        await CreateService().DeleteUserAsync(user.Id, user.Id, cancellationToken: Ct);
        _db.ChangeTracker.Clear();

        Assert.Equal(0, await _db.Invitations.IgnoreQueryFilters().CountAsync(i => i.Email == "invitee@test", Ct));
        // The unrelated tenant itself is untouched.
        Assert.Equal(1, await _db.Tenants.IgnoreQueryFilters().CountAsync(t => t.Id == otherTenantId, Ct));
    }

    [Fact]
    public async Task DeleteUser_UnknownUser_ReturnsNotFound()
    {
        var result = await CreateService().DeleteUserAsync(Guid.NewGuid(), Guid.NewGuid(), cancellationToken: Ct);
        Assert.Equal(AccountDeletionStatus.NotFound, result.Status);
    }

    // ── Completeness guard: every tenant-scoped entity is covered by PurgeTenant ──

    [Fact]
    public void PurgeTenant_CoversEveryTenantScopedEntity()
    {
        using var db = TestDbContextFactory.Create();

        var tenantScoped = db.Model.GetEntityTypes()
            .Select(e => e.ClrType)
            .Where(t => t.GetProperty("TenantId")?.PropertyType == typeof(Guid))
            .Select(t => t.Name)
            .Distinct()
            .OrderBy(n => n)
            .ToList();

        // Kept in sync with AccountDeletionService.PurgeTenantAsync by hand. If a new tenant-scoped
        // entity is added, this fails until it is deleted there (and listed here) — so PII can never
        // be silently orphaned by a schema change. (Tenant itself is the root and is deleted last.)
        var covered = new[]
        {
            "Accommodation", "AccommodationAttribute", "AccommodationBed", "AccommodationIntent",
            "Address", "ContactMethod", "Equipment", "EquipmentAttribute", "Event", "EventAttendance",
            "EventAttribute", "Household", "HouseholdAttribute", "HouseholdMember",
            "HouseholdMemberAttribute", "HouseholdUser", "Invitation", "InvitationHouseholdAccess",
            "MealAttendance", "MealIntent", "MealPlan", "MealTemplate", "MealTemplateAttribute",
            "MemberRelationship", "Property", "PropertyAttribute", "ShoppingItem",
            "ShoppingItemAttribute", "ShoppingItemIntent", "TaskIntent", "TaskPlan", "TaskTemplate",
            "TaskTemplateAttribute", "TenantAttribute", "TenantUser",
        }.OrderBy(n => n).ToList();

        Assert.Equal(covered, tenantScoped);
    }

    // ── Completeness guard: every FK that references HouseholdMember is covered ───

    [Fact]
    public void PurgeMember_CoversEveryMemberReferencingForeignKey()
    {
        using var db = TestDbContextFactory.Create();

        var memberFks = db.Model.GetEntityTypes()
            .SelectMany(e => e.GetForeignKeys())
            .Where(fk => fk.PrincipalEntityType.ClrType == typeof(HouseholdMember))
            .Select(fk => $"{fk.DeclaringEntityType.ClrType.Name}.{string.Join("+", fk.Properties.Select(p => p.Name))}")
            .Distinct()
            .OrderBy(n => n)
            .ToList();

        // Kept in sync with AccountDeletionService.PurgeMemberAsync by hand. If a new entity gains an
        // FK to HouseholdMember, this fails until PurgeMemberAsync deletes (or nulls) it — otherwise
        // a departing user's PII would linger in a group they left, or the member delete would throw
        // on the Restrict FK. (Invitation.LinkedMemberId is a deliberate plain column with no FK, so
        // it is invisible here; PurgeMemberAsync still nulls it.)
        var covered = new[]
        {
            "AccommodationIntent.HouseholdMemberId",
            "Address.HouseholdMemberId",
            "ContactMethod.HouseholdMemberId",
            "EventAttendance.HouseholdMemberId",
            "HouseholdMemberAttribute.HouseholdMemberId",
            "MealAttendance.HouseholdMemberId",
            "MealIntent.HouseholdMemberId",
            "MemberRelationship.HouseholdMemberId",
            "MemberRelationship.RelatedMemberId",
            "ShoppingItemIntent.HouseholdMemberId",
            "TaskIntent.HouseholdMemberId",
            "TenantUser.LinkedMemberId",
        }.OrderBy(n => n).ToList();

        Assert.Equal(covered, memberFks);
    }

    // ── Seed helpers ─────────────────────────────────────────────────────────────

    private async Task<User> SeedUserAsync(string externalId)
    {
        var user = new User { Id = Guid.NewGuid(), ExternalId = externalId, Email = externalId };
        _db.Users.Add(user);
        await _db.SaveChangesAsync(Ct);
        return user;
    }

    /// <summary>
    /// Seeds a tenant with a household, a member (the user's own when <paramref name="linkMember"/>),
    /// that member's contact/address/attribute/relationship, plus a property, event, attendance, an
    /// invitation with a household grant, and a meal template → plan → meal-origin shopping item
    /// chain — a deep-enough FK graph to exercise the purge ordering (shopping items reference meal
    /// plans, so a wrong order fails these tests).
    /// </summary>
    private async Task<(Guid TenantId, Guid MemberId)> SeedPersonalTenantAsync(User user, TenantRole ownerRole, bool linkMember)
    {
        var tenantId = Guid.NewGuid();
        var householdId = Guid.NewGuid();
        var propertyId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var mealTemplateId = Guid.NewGuid();
        var mealPlanId = Guid.NewGuid();

        _db.Tenants.Add(new Tenant { Id = tenantId, Name = "Group" });
        _db.Households.Add(new Household { Id = householdId, TenantId = tenantId, Name = "Household" });
        _db.Properties.Add(new Property { Id = propertyId, TenantId = tenantId, Name = "Cabin" });
        _db.Events.Add(new Event
        {
            Id = eventId, TenantId = tenantId, PropertyId = propertyId, Name = "Reunion",
            StartDate = new DateOnly(2026, 7, 1), EndDate = new DateOnly(2026, 7, 3),
        });
        await _db.SaveChangesAsync(Ct);

        _db.MealTemplates.Add(new MealTemplate { Id = mealTemplateId, TenantId = tenantId, EventId = eventId, Name = "Dinner" });
        _db.MealPlans.Add(new MealPlan
        {
            Id = mealPlanId, TenantId = tenantId, MealTemplateId = mealTemplateId,
            Day = new DateOnly(2026, 7, 1), MealType = MealType.Dinner,
        });
        await _db.SaveChangesAsync(Ct);
        _db.ShoppingItems.Add(new ShoppingItem
        {
            Id = Guid.NewGuid(), TenantId = tenantId, Origin = ShoppingItemOrigin.Meal,
            MealPlanId = mealPlanId, EventId = eventId, Name = "Groceries",
        });
        await _db.SaveChangesAsync(Ct);

        var memberId = await AddMemberAsync(tenantId, householdId, user.Email ?? "Member");
        var relatedId = await AddMemberAsync(tenantId, householdId, "Relative");
        await AddContactAsync(tenantId, memberId, "primary-phone");
        _db.Addresses.Add(new Address { Id = Guid.NewGuid(), TenantId = tenantId, HouseholdMemberId = memberId, Line1 = "1 Main St" });
        _db.HouseholdMemberAttributes.Add(new HouseholdMemberAttribute { Id = Guid.NewGuid(), TenantId = tenantId, HouseholdMemberId = memberId, Key = "shirt", Value = "L" });
        _db.MemberRelationships.Add(new MemberRelationship { Id = Guid.NewGuid(), TenantId = tenantId, HouseholdMemberId = memberId, RelatedMemberId = relatedId, RelationshipType = RelationshipType.Child });
        _db.EventAttendances.Add(new EventAttendance { Id = Guid.NewGuid(), TenantId = tenantId, EventId = eventId, HouseholdMemberId = memberId, Day = new DateOnly(2026, 7, 1), Status = AttendanceStatus.Going });
        _db.HouseholdUsers.Add(new HouseholdUser { TenantId = tenantId, HouseholdId = householdId, UserId = user.Id, Role = HouseholdRole.Manager });

        var invitationId = Guid.NewGuid();
        _db.Invitations.Add(new Invitation { Id = invitationId, TenantId = tenantId, Email = "guest@test", Role = TenantRole.Member, Status = InvitationStatus.Pending });
        _db.InvitationHouseholdAccess.Add(new InvitationHouseholdAccess { InvitationId = invitationId, TenantId = tenantId, HouseholdId = householdId, Role = HouseholdRole.Member });
        await _db.SaveChangesAsync(Ct);

        await AddMembershipAsync(tenantId, user.Id, ownerRole, linkMember ? memberId : null);
        return (tenantId, memberId);
    }

    private async Task<Guid> AddMemberAsync(Guid tenantId, Guid householdId, string name)
    {
        var id = Guid.NewGuid();
        _db.HouseholdMembers.Add(new HouseholdMember { Id = id, TenantId = tenantId, HouseholdId = householdId, Name = name });
        await _db.SaveChangesAsync(Ct);
        return id;
    }

    private async Task AddContactAsync(Guid tenantId, Guid memberId, string value)
    {
        _db.ContactMethods.Add(new ContactMethod { Id = Guid.NewGuid(), TenantId = tenantId, HouseholdMemberId = memberId, Type = ContactMethodType.Phone, Value = value });
        await _db.SaveChangesAsync(Ct);
    }

    private async Task AddMembershipAsync(Guid tenantId, Guid userId, TenantRole role, Guid? linkedMemberId = null)
    {
        _db.TenantUsers.Add(new TenantUser { TenantId = tenantId, UserId = userId, Role = role, LinkedMemberId = linkedMemberId });
        await _db.SaveChangesAsync(Ct);
    }

    private async Task<bool> AnyForTenantAsync(Guid tenantId)
    {
        return await _db.HouseholdMembers.IgnoreQueryFilters().AnyAsync(x => x.TenantId == tenantId, Ct)
            || await _db.TenantUsers.IgnoreQueryFilters().AnyAsync(x => x.TenantId == tenantId, Ct)
            || await _db.HouseholdUsers.IgnoreQueryFilters().AnyAsync(x => x.TenantId == tenantId, Ct)
            || await _db.Households.IgnoreQueryFilters().AnyAsync(x => x.TenantId == tenantId, Ct)
            || await _db.Properties.IgnoreQueryFilters().AnyAsync(x => x.TenantId == tenantId, Ct)
            || await _db.Events.IgnoreQueryFilters().AnyAsync(x => x.TenantId == tenantId, Ct)
            || await _db.EventAttendances.IgnoreQueryFilters().AnyAsync(x => x.TenantId == tenantId, Ct)
            || await _db.ContactMethods.IgnoreQueryFilters().AnyAsync(x => x.TenantId == tenantId, Ct)
            || await _db.Addresses.IgnoreQueryFilters().AnyAsync(x => x.TenantId == tenantId, Ct)
            || await _db.MemberRelationships.IgnoreQueryFilters().AnyAsync(x => x.TenantId == tenantId, Ct)
            || await _db.HouseholdMemberAttributes.IgnoreQueryFilters().AnyAsync(x => x.TenantId == tenantId, Ct)
            || await _db.Invitations.IgnoreQueryFilters().AnyAsync(x => x.TenantId == tenantId, Ct)
            || await _db.InvitationHouseholdAccess.IgnoreQueryFilters().AnyAsync(x => x.TenantId == tenantId, Ct)
            || await _db.MealTemplates.IgnoreQueryFilters().AnyAsync(x => x.TenantId == tenantId, Ct)
            || await _db.MealPlans.IgnoreQueryFilters().AnyAsync(x => x.TenantId == tenantId, Ct)
            || await _db.ShoppingItems.IgnoreQueryFilters().AnyAsync(x => x.TenantId == tenantId, Ct);
    }

    private sealed class FakeDirectoryAccountService : IDirectoryAccountService
    {
        public List<string> Deleted { get; } = [];
        public DirectoryDeletionOutcome Outcome { get; set; } = DirectoryDeletionOutcome.Skipped;

        public Task<DirectoryDeletionOutcome> DeleteUserAsync(string externalId, CancellationToken cancellationToken = default)
        {
            Deleted.Add(externalId);
            return Task.FromResult(Outcome);
        }
    }
}
