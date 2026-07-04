using Gatherstead.Api.Contracts.Responses;
using Gatherstead.Api.Services.Authorization;
using Gatherstead.Api.Services.HouseholdMembers;
using Gatherstead.Api.Tests.Fixtures;
using Gatherstead.Data;
using Gatherstead.Data.Entities;
using Moq;

namespace Gatherstead.Api.Tests.Services;

/// <summary>Covers the per-household member read (<see cref="HouseholdMemberService.ListAsync"/>).</summary>
public class HouseholdMemberServiceTests : IAsyncLifetime
{
    private GathersteadDbContext _dbContext = null!;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _householdId = Guid.NewGuid();

    public async ValueTask InitializeAsync()
    {
        _dbContext = TestDbContextFactory.Create(tenantId: _tenantId);
        _dbContext.Tenants.Add(new Tenant { Id = _tenantId, Name = "Test Tenant" });
        _dbContext.Households.Add(new Household { Id = _householdId, TenantId = _tenantId, Name = "Smith Household" });
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        _dbContext.Dispose();
        return ValueTask.CompletedTask;
    }

    private HouseholdMemberService CreateService()
    {
        var tenantContext = Mock.Of<ICurrentTenantContext>(c => c.TenantId == _tenantId);
        var auth = Mock.Of<IMemberAuthorizationService>(a =>
            a.GetSensitiveReadScopeAsync(_tenantId, It.IsAny<CancellationToken>())
                == Task.FromResult(SensitiveReadScope.Global));
        return new HouseholdMemberService(_dbContext, tenantContext, auth, Mock.Of<IAuditVisibilityContext>());
    }

    [Fact]
    public async Task ListAsync_ReturnsMembersForHousehold()
    {
        // Guard: this List already materializes before mapping; keep it exercised under SQLite so it
        // cannot regress into an untranslatable instance-method projection.
        _dbContext.HouseholdMembers.Add(new HouseholdMember { Id = Guid.NewGuid(), TenantId = _tenantId, HouseholdId = _householdId, Name = "Alice" });
        _dbContext.HouseholdMembers.Add(new HouseholdMember { Id = Guid.NewGuid(), TenantId = _tenantId, HouseholdId = _householdId, Name = "Bob" });
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await CreateService().ListAsync(_tenantId, _householdId, null, TestContext.Current.CancellationToken);

        Assert.True(result.Successful);
        Assert.Equal(2, result.Entity!.Count);
    }

    [Fact]
    public async Task ListAsync_IsAdult_DerivedFromAgeBand()
    {
        var adultId = Guid.NewGuid();
        var childId = Guid.NewGuid();
        var unknownId = Guid.NewGuid();
        var byBirthDateId = Guid.NewGuid();

        _dbContext.HouseholdMembers.AddRange(
            new HouseholdMember { Id = adultId, TenantId = _tenantId, HouseholdId = _householdId, Name = "Adult", AgeBand = AgeBand.Age18To64 },
            new HouseholdMember { Id = childId, TenantId = _tenantId, HouseholdId = _householdId, Name = "Child", AgeBand = AgeBand.Age6To12 },
            new HouseholdMember { Id = unknownId, TenantId = _tenantId, HouseholdId = _householdId, Name = "Unknown" },
            new HouseholdMember { Id = byBirthDateId, TenantId = _tenantId, HouseholdId = _householdId, Name = "BornAdult", BirthDate = new DateOnly(1990, 1, 1) });
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await CreateService().ListAsync(_tenantId, _householdId, null, TestContext.Current.CancellationToken);

        Assert.True(result.Successful);
        Assert.True(result.Entity!.Single(m => m.Id == adultId).IsAdult);
        Assert.False(result.Entity!.Single(m => m.Id == childId).IsAdult);
        Assert.Null(result.Entity!.Single(m => m.Id == unknownId).IsAdult);
        Assert.True(result.Entity!.Single(m => m.Id == byBirthDateId).IsAdult);
    }
}
