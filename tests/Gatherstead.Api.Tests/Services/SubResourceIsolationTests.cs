using Gatherstead.Api.Contracts.Addresses;
using Gatherstead.Api.Contracts.ContactMethods;
using Gatherstead.Api.Contracts.MemberRelationships;
using Gatherstead.Api.Contracts.Responses;
using Gatherstead.Api.Security;
using Gatherstead.Api.Services.Addresses;
using Gatherstead.Api.Services.Authorization;
using Gatherstead.Api.Services.ContactMethods;
using Gatherstead.Api.Services.MemberRelationships;
using Gatherstead.Api.Services.Observability;
using Gatherstead.Api.Tests.Fixtures;
using Gatherstead.Data;
using Gatherstead.Data.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;

namespace Gatherstead.Api.Tests.Services;

/// <summary>
/// Regression coverage for the member sub-resource IDOR: the Address / ContactMethod /
/// MemberRelationship services authorize against the route <c>householdId</c> but query rows by
/// <c>memberId</c>. Without binding the member to that household, a Household Manager of household A
/// (which passes the household-manager authorization branch for A) could read/edit/delete a member
/// belonging to household B by supplying <c>householdId = A</c> and a foreign <c>memberId</c>.
///
/// The negative cases here fail against the pre-fix code and pass once the services bind the member
/// to the route household. The positive cases assert the intended access is preserved — a Household
/// Manager retains full access to their own household's members, including login-less children.
/// </summary>
public class SubResourceIsolationTests : IAsyncLifetime
{
    private GathersteadDbContext _dbContext = null!;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();

    // Household A is managed by the acting user. Household B is a different household in the same tenant.
    private readonly Guid _householdA = Guid.NewGuid();
    private readonly Guid _householdB = Guid.NewGuid();

    // memberA is a login-less child in household A (no linked TenantUser). memberB is in household B.
    private readonly Guid _memberA = Guid.NewGuid();
    private readonly Guid _memberB = Guid.NewGuid();

    // Seeded sub-resources belonging to memberB (household B) — the target of the cross-household attack.
    private readonly Guid _addressB = Guid.NewGuid();
    private readonly Guid _contactB = Guid.NewGuid();
    private readonly Guid _relationshipB = Guid.NewGuid();

    public async ValueTask InitializeAsync()
    {
        _dbContext = TestDbContextFactory.Create(tenantId: _tenantId, currentUserId: _userId);

        _dbContext.Tenants.Add(new Tenant { Id = _tenantId, Name = "Test Tenant" });
        _dbContext.Users.Add(new User { Id = _userId, ExternalId = _userId.ToString() });
        _dbContext.Households.Add(new Household { Id = _householdA, TenantId = _tenantId, Name = "Household A" });
        _dbContext.Households.Add(new Household { Id = _householdB, TenantId = _tenantId, Name = "Household B" });

        // memberA: login-less child in A. memberB: member in B.
        _dbContext.HouseholdMembers.Add(new HouseholdMember { Id = _memberA, TenantId = _tenantId, HouseholdId = _householdA, Name = "Child A" });
        _dbContext.HouseholdMembers.Add(new HouseholdMember { Id = _memberB, TenantId = _tenantId, HouseholdId = _householdB, Name = "Member B" });

        // Acting user is a Household Manager of A (and has no tenant-level role → Guest at the tenant tier).
        _dbContext.HouseholdUsers.Add(new HouseholdUser { TenantId = _tenantId, HouseholdId = _householdA, UserId = _userId, Role = HouseholdRole.Manager });

        // Sub-resources owned by memberB in household B.
        _dbContext.Addresses.Add(new Address
        {
            Id = _addressB, TenantId = _tenantId, HouseholdMemberId = _memberB,
            Line1 = "1 B Street", City = "Bville", State = "BS", PostalCode = "00000", Country = "US",
        });
        _dbContext.ContactMethods.Add(new ContactMethod
        {
            Id = _contactB, TenantId = _tenantId, HouseholdMemberId = _memberB,
            Type = ContactMethodType.Email, Value = "b@example.com",
        });
        _dbContext.MemberRelationships.Add(new MemberRelationship
        {
            Id = _relationshipB, TenantId = _tenantId, HouseholdMemberId = _memberB,
            RelatedMemberId = _memberA, RelationshipType = RelationshipType.Other,
        });

        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        _dbContext.Dispose();
        return ValueTask.CompletedTask;
    }

    // Real authorization service so the tests exercise the genuine household-manager branch rather
    // than a mock — the whole point is that authorization PASSES for household A yet the member is
    // in household B.
    private IMemberAuthorizationService CreateAuthorizationService()
    {
        var userContext = Mock.Of<ICurrentUserContext>(c => c.UserId == _userId);
        var appAdminContext = Mock.Of<IAppAdminContext>(c =>
            c.IsAppAdminAsync(It.IsAny<CancellationToken>()) == Task.FromResult<bool?>(false));
        var httpContextAccessor = new HttpContextAccessor { HttpContext = new DefaultHttpContext() };
        var securityLogger = new Mock<ISecurityEventLogger>();
        securityLogger
            .Setup(s => s.LogAsync(
                It.IsAny<SecurityEventType>(), It.IsAny<SecurityEventSeverity>(),
                It.IsAny<string>(), It.IsAny<string?>(),
                It.IsAny<Guid?>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        return new MemberAuthorizationService(
            _dbContext,
            userContext,
            httpContextAccessor,
            appAdminContext,
            new FakeAuthCache(),
            Mock.Of<ILogger<MemberAuthorizationService>>(),
            securityLogger.Object);
    }

    private ICurrentTenantContext TenantContext => Mock.Of<ICurrentTenantContext>(c => c.TenantId == _tenantId);
    private IAuditVisibilityContext AuditContext => Mock.Of<IAuditVisibilityContext>();

    private AddressService CreateAddressService() =>
        new(_dbContext, TenantContext, CreateAuthorizationService(), AuditContext);
    private ContactMethodService CreateContactMethodService() =>
        new(_dbContext, TenantContext, CreateAuthorizationService(), AuditContext);
    private MemberRelationshipService CreateRelationshipService() =>
        new(_dbContext, TenantContext, CreateAuthorizationService(), AuditContext);

    private static UpdateAddressRequest AddressUpdate() => new()
    {
        Line1 = "hacked", City = "X", State = "X", PostalCode = "11111", Country = "US",
    };
    private static UpdateContactMethodRequest ContactUpdate() => new()
    {
        Type = ContactMethodType.Phone, Value = "555-0000",
    };
    private static UpdateMemberRelationshipRequest RelationshipUpdate() => new()
    {
        RelationshipType = RelationshipType.Sibling,
    };

    private CancellationToken Ct => TestContext.Current.CancellationToken;

    // ── Negative: cross-household access via the managed household is denied ─────────────────────
    // Caller manages household A and supplies householdId = A with a memberId that lives in B.

    [Fact]
    public async Task Address_CrossHousehold_ListReturnsNoData()
    {
        var result = await CreateAddressService().ListAsync(_tenantId, _householdA, _memberB, null, Ct);
        Assert.False(result.Successful);
    }

    [Fact]
    public async Task Address_CrossHousehold_GetReturnsNoData()
    {
        var result = await CreateAddressService().GetAsync(_tenantId, _householdA, _memberB, _addressB, Ct);
        Assert.False(result.Successful);
        Assert.Null(result.Entity);
    }

    [Fact]
    public async Task Address_CrossHousehold_UpdateDenied()
    {
        var result = await CreateAddressService().UpdateAsync(_tenantId, _householdA, _memberB, _addressB, AddressUpdate(), Ct);
        Assert.False(result.Successful);

        var untouched = await _dbContext.Addresses.FindAsync([_addressB], Ct);
        Assert.Equal("1 B Street", untouched!.Line1);
    }

    [Fact]
    public async Task Address_CrossHousehold_DeleteDenied()
    {
        var result = await CreateAddressService().DeleteAsync(_tenantId, _householdA, _memberB, _addressB, Ct);
        Assert.False(result.Successful);

        var untouched = await _dbContext.Addresses.FindAsync([_addressB], Ct);
        Assert.False(untouched!.IsDeleted);
    }

    [Fact]
    public async Task ContactMethod_CrossHousehold_GetReturnsNoData()
    {
        var result = await CreateContactMethodService().GetAsync(_tenantId, _householdA, _memberB, _contactB, Ct);
        Assert.False(result.Successful);
        Assert.Null(result.Entity);
    }

    [Fact]
    public async Task ContactMethod_CrossHousehold_UpdateDenied()
    {
        var result = await CreateContactMethodService().UpdateAsync(_tenantId, _householdA, _memberB, _contactB, ContactUpdate(), Ct);
        Assert.False(result.Successful);

        var untouched = await _dbContext.ContactMethods.FindAsync([_contactB], Ct);
        Assert.Equal("b@example.com", untouched!.Value);
    }

    [Fact]
    public async Task ContactMethod_CrossHousehold_DeleteDenied()
    {
        var result = await CreateContactMethodService().DeleteAsync(_tenantId, _householdA, _memberB, _contactB, Ct);
        Assert.False(result.Successful);

        var untouched = await _dbContext.ContactMethods.FindAsync([_contactB], Ct);
        Assert.False(untouched!.IsDeleted);
    }

    [Fact]
    public async Task MemberRelationship_CrossHousehold_GetReturnsNoData()
    {
        var result = await CreateRelationshipService().GetAsync(_tenantId, _householdA, _memberB, _relationshipB, Ct);
        Assert.False(result.Successful);
        Assert.Null(result.Entity);
    }

    [Fact]
    public async Task MemberRelationship_CrossHousehold_DeleteDenied()
    {
        var result = await CreateRelationshipService().DeleteAsync(_tenantId, _householdA, _memberB, _relationshipB, Ct);
        Assert.False(result.Successful);

        var untouched = await _dbContext.MemberRelationships.FindAsync([_relationshipB], Ct);
        Assert.False(untouched!.IsDeleted);
    }

    // ── Positive: a Household Manager retains full access to their OWN household's members ──────
    // memberA is a login-less child in household A — the common case that must not regress.

    [Fact]
    public async Task Address_SameHousehold_ManagerCanCreateAndReadChildMember()
    {
        var address = new Address
        {
            Id = Guid.NewGuid(), TenantId = _tenantId, HouseholdMemberId = _memberA,
            Line1 = "1 A Street", City = "Aville", State = "AS", PostalCode = "22222", Country = "US",
        };
        _dbContext.Addresses.Add(address);
        await _dbContext.SaveChangesAsync(Ct);

        var list = await CreateAddressService().ListAsync(_tenantId, _householdA, _memberA, null, Ct);
        Assert.True(list.Successful);
        Assert.Single(list.Entity!);

        var get = await CreateAddressService().GetAsync(_tenantId, _householdA, _memberA, address.Id, Ct);
        Assert.True(get.Successful);
        Assert.Equal("1 A Street", get.Entity!.Line1);
    }

    [Fact]
    public async Task ContactMethod_SameHousehold_ManagerCanUpdateChildMember()
    {
        var contact = new ContactMethod
        {
            Id = Guid.NewGuid(), TenantId = _tenantId, HouseholdMemberId = _memberA,
            Type = ContactMethodType.Email, Value = "child@example.com",
        };
        _dbContext.ContactMethods.Add(contact);
        await _dbContext.SaveChangesAsync(Ct);

        var result = await CreateContactMethodService().UpdateAsync(_tenantId, _householdA, _memberA, contact.Id, ContactUpdate(), Ct);
        Assert.True(result.Successful);
        Assert.Equal("555-0000", result.Entity!.Value);
    }

    [Fact]
    public async Task Address_SameHousehold_ManagerCanDeleteChildMemberRecord()
    {
        var address = new Address
        {
            Id = Guid.NewGuid(), TenantId = _tenantId, HouseholdMemberId = _memberA,
            Line1 = "9 A Lane", City = "Aville", State = "AS", PostalCode = "33333", Country = "US",
        };
        _dbContext.Addresses.Add(address);
        await _dbContext.SaveChangesAsync(Ct);

        var result = await CreateAddressService().DeleteAsync(_tenantId, _householdA, _memberA, address.Id, Ct);
        Assert.True(result.Successful);

        var deleted = await _dbContext.Addresses.FindAsync([address.Id], Ct);
        Assert.True(deleted!.IsDeleted);
    }
}
