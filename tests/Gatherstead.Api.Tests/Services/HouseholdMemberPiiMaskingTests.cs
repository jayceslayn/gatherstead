using Gatherstead.Api.Contracts.HouseholdMembers;
using Gatherstead.Api.Contracts.Responses;
using Gatherstead.Api.Services.Authorization;
using Gatherstead.Api.Services.HouseholdMembers;
using Gatherstead.Api.Tests.Fixtures;
using Gatherstead.Data;
using Gatherstead.Data.Entities;
using Moq;

namespace Gatherstead.Api.Tests.Services;

/// <summary>
/// Enforces the sensitive-field masking contract on <see cref="HouseholdMemberService"/>: BirthDate,
/// DietaryNotes, DietaryTags, and Notes are returned only to a caller whose
/// <see cref="SensitiveReadScope"/> covers the member's household; otherwise they are nulled/emptied
/// while the public fields (Name, AgeBand, derived IsAdult) remain. This guards the privacy boundary
/// for Guests and out-of-scope callers.
/// </summary>
public class HouseholdMemberPiiMaskingTests : IAsyncLifetime
{
    private GathersteadDbContext _dbContext = null!;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _householdId = Guid.NewGuid();
    private readonly Guid _otherHouseholdId = Guid.NewGuid();
    private readonly Guid _memberId = Guid.NewGuid();

    public async ValueTask InitializeAsync()
    {
        _dbContext = TestDbContextFactory.Create(tenantId: _tenantId);
        _dbContext.Tenants.Add(new Tenant { Id = _tenantId, Name = "Test Tenant" });
        _dbContext.Households.Add(new Household { Id = _householdId, TenantId = _tenantId, Name = "Household" });
        _dbContext.HouseholdMembers.Add(new HouseholdMember
        {
            Id = _memberId,
            TenantId = _tenantId,
            HouseholdId = _householdId,
            Name = "Alice",
            BirthDate = new DateOnly(1990, 5, 1),
            DietaryNotes = "Severe peanut allergy",
            DietaryTags = ["vegan"],
            Notes = "private note",
        });
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        _dbContext.Dispose();
        return ValueTask.CompletedTask;
    }

    private HouseholdMemberService CreateService(SensitiveReadScope scope)
    {
        var tenantContext = Mock.Of<ICurrentTenantContext>(c => c.TenantId == _tenantId);
        var auth = Mock.Of<IMemberAuthorizationService>(a =>
            a.GetSensitiveReadScopeAsync(_tenantId, It.IsAny<CancellationToken>()) == Task.FromResult(scope)
            && a.GetCallerTenantRoleAsync(_tenantId, It.IsAny<CancellationToken>()) == Task.FromResult<TenantRole?>(null)
            && a.GetCallerHouseholdRoleAsync(_tenantId, _householdId, It.IsAny<CancellationToken>()) == Task.FromResult<HouseholdRole?>(null));
        return new HouseholdMemberService(_dbContext, tenantContext, auth, Mock.Of<IAuditVisibilityContext>());
    }

    private CancellationToken Ct => TestContext.Current.CancellationToken;

    private static void AssertSensitivePresent(HouseholdMemberDto dto)
    {
        Assert.Equal(new DateOnly(1990, 5, 1), dto.BirthDate);
        Assert.Equal("Severe peanut allergy", dto.DietaryNotes);
        Assert.Equal(["vegan"], dto.DietaryTags);
        Assert.Equal("private note", dto.Notes);
    }

    private static void AssertSensitiveMasked(HouseholdMemberDto dto)
    {
        Assert.Null(dto.BirthDate);
        Assert.Null(dto.DietaryNotes);
        Assert.Empty(dto.DietaryTags);
        Assert.Null(dto.Notes);
        // Public fields still present.
        Assert.Equal("Alice", dto.Name);
        Assert.NotNull(dto.AgeBand); // derived from BirthDate — a non-sensitive coarsening
    }

    [Fact]
    public async Task List_GlobalScope_ReturnsSensitiveFields()
    {
        var result = await CreateService(SensitiveReadScope.Global).ListAsync(_tenantId, _householdId, null, Ct);
        Assert.True(result.Successful);
        AssertSensitivePresent(result.Entity!.Single());
    }

    [Fact]
    public async Task List_NoneScope_MasksSensitiveFields()
    {
        var result = await CreateService(SensitiveReadScope.None).ListAsync(_tenantId, _householdId, null, Ct);
        Assert.True(result.Successful);
        AssertSensitiveMasked(result.Entity!.Single());
    }

    [Fact]
    public async Task List_ScopedToThisHousehold_ReturnsSensitiveFields()
    {
        var scope = SensitiveReadScope.ForHouseholds([_householdId]);
        var result = await CreateService(scope).ListAsync(_tenantId, _householdId, null, Ct);
        Assert.True(result.Successful);
        AssertSensitivePresent(result.Entity!.Single());
    }

    [Fact]
    public async Task List_ScopedToOtherHousehold_MasksSensitiveFields()
    {
        var scope = SensitiveReadScope.ForHouseholds([_otherHouseholdId]);
        var result = await CreateService(scope).ListAsync(_tenantId, _householdId, null, Ct);
        Assert.True(result.Successful);
        AssertSensitiveMasked(result.Entity!.Single());
    }

    [Fact]
    public async Task Get_NoneScope_MasksSensitiveFields()
    {
        var result = await CreateService(SensitiveReadScope.None).GetAsync(_tenantId, _householdId, _memberId, Ct);
        Assert.True(result.Successful);
        AssertSensitiveMasked(result.Entity!);
    }

    [Fact]
    public async Task Get_GlobalScope_ReturnsSensitiveFields()
    {
        var result = await CreateService(SensitiveReadScope.Global).GetAsync(_tenantId, _householdId, _memberId, Ct);
        Assert.True(result.Successful);
        AssertSensitivePresent(result.Entity!);
    }
}
