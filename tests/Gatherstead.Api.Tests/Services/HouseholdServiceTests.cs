using Gatherstead.Api.Contracts.Responses;
using Gatherstead.Api.Services.Authorization;
using Gatherstead.Api.Services.Households;
using Gatherstead.Api.Tests.Fixtures;
using Gatherstead.Data;
using Gatherstead.Data.Entities;
using Moq;

namespace Gatherstead.Api.Tests.Services;

/// <summary>Covers the tenant household read (<see cref="HouseholdService.ListAsync"/>).</summary>
public class HouseholdServiceTests : IAsyncLifetime
{
    private GathersteadDbContext _dbContext = null!;
    private readonly Guid _tenantId = Guid.NewGuid();

    public async ValueTask InitializeAsync()
    {
        _dbContext = TestDbContextFactory.Create(tenantId: _tenantId);
        _dbContext.Tenants.Add(new Tenant { Id = _tenantId, Name = "Test Tenant" });
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        _dbContext.Dispose();
        return ValueTask.CompletedTask;
    }

    private HouseholdService CreateService(TenantRole? callerRole = null, CallerHouseholdRoles? householdRoles = null)
    {
        // ListAsync resolves per-household roles from this lookup; a bare mock would return null, so
        // default to Empty (a caller who belongs to no household).
        var roles = householdRoles ?? CallerHouseholdRoles.Empty;
        var auth = Mock.Of<IMemberAuthorizationService>(a =>
            a.GetCallerTenantRoleAsync(_tenantId, It.IsAny<CancellationToken>()) == Task.FromResult(callerRole) &&
            a.GetCallerHouseholdRolesAsync(_tenantId, It.IsAny<CancellationToken>()) == Task.FromResult(roles));
        return new HouseholdService(
            _dbContext,
            Mock.Of<ICurrentTenantContext>(c => c.TenantId == _tenantId),
            auth,
            Mock.Of<IAuditVisibilityContext>());
    }

    [Fact]
    public async Task ListAsync_ReturnsHouseholdsForTenant()
    {
        // Guard: this List already materializes before mapping; keep it exercised under SQLite so it
        // cannot regress into an untranslatable instance-method projection.
        _dbContext.Households.Add(new Household { Id = Guid.NewGuid(), TenantId = _tenantId, Name = "Smith Household" });
        _dbContext.Households.Add(new Household { Id = Guid.NewGuid(), TenantId = _tenantId, Name = "Jones Household" });
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await CreateService().ListAsync(_tenantId, null, TestContext.Current.CancellationToken);

        Assert.True(result.Successful);
        Assert.Equal(2, result.Entity!.Count);
    }

    [Fact]
    public async Task ListAsync_OrdersByNameCaseInsensitive()
    {
        _dbContext.Households.Add(new Household { Id = Guid.NewGuid(), TenantId = _tenantId, Name = "Zephyr" });
        _dbContext.Households.Add(new Household { Id = Guid.NewGuid(), TenantId = _tenantId, Name = "alpha" });
        _dbContext.Households.Add(new Household { Id = Guid.NewGuid(), TenantId = _tenantId, Name = "Mason" });
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await CreateService().ListAsync(_tenantId, null, TestContext.Current.CancellationToken);

        Assert.True(result.Successful);
        Assert.Equal(["alpha", "Mason", "Zephyr"], result.Entity!.Select(h => h.Name));
    }

    [Fact]
    public async Task ListAsync_HouseholdScopedAttributes_VisibleOnlyForMemberHousehold()
    {
        // A household-scoped attribute (visible only via HouseholdRole, not TenantRole) must appear on
        // the household the caller belongs to and be hidden on one they don't. Exercises the per-row
        // household-role map, and that a household absent from the map resolves to null (not the
        // default enum value, HouseholdRole.Manager, which would leak the attribute).
        var memberHouseholdId = Guid.NewGuid();
        var otherHouseholdId = Guid.NewGuid();
        _dbContext.Households.Add(new Household { Id = memberHouseholdId, TenantId = _tenantId, Name = "Aaa Member Household" });
        _dbContext.Households.Add(new Household { Id = otherHouseholdId, TenantId = _tenantId, Name = "Bbb Other Household" });
        // TenantMinRole = Owner so the tenant clause never grants visibility to our non-Owner caller;
        // visibility hinges solely on the household role.
        _dbContext.HouseholdAttributes.AddRange(
            new HouseholdAttribute
            {
                Id = Guid.NewGuid(), TenantId = _tenantId, HouseholdId = memberHouseholdId,
                Key = "gateCode", Value = "1234",
                TenantMinRole = (byte)TenantRole.Owner, HouseholdMinRole = (byte)HouseholdRole.Member,
            },
            new HouseholdAttribute
            {
                Id = Guid.NewGuid(), TenantId = _tenantId, HouseholdId = otherHouseholdId,
                Key = "gateCode", Value = "9999",
                TenantMinRole = (byte)TenantRole.Owner, HouseholdMinRole = (byte)HouseholdRole.Member,
            });
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Caller: no tenant role, Member of only the first household.
        var service = CreateService(householdRoles: new CallerHouseholdRoles(new Dictionary<Guid, HouseholdRole>
        {
            [memberHouseholdId] = HouseholdRole.Member,
        }));

        var result = await service.ListAsync(_tenantId, null, TestContext.Current.CancellationToken);

        Assert.True(result.Successful);
        var member = result.Entity!.Single(h => h.Id == memberHouseholdId);
        var other = result.Entity!.Single(h => h.Id == otherHouseholdId);
        Assert.Equal("gateCode", Assert.Single(member.Attributes).Key);
        Assert.Empty(other.Attributes);
    }
}
