using Gatherstead.Api.Contracts.Responses;
using Gatherstead.Api.Services.Authorization;
using Gatherstead.Api.Services.MealIntents;
using Gatherstead.Api.Tests.Fixtures;
using Gatherstead.Data;
using Gatherstead.Data.Entities;
using Moq;

namespace Gatherstead.Api.Tests.Services;

/// <summary>Covers the per-plan intent read (<see cref="MealIntentService.ListAsync"/>).</summary>
public class MealIntentServiceTests : IAsyncLifetime
{
    private GathersteadDbContext _dbContext = null!;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _propertyId = Guid.NewGuid();
    private readonly Guid _eventId = Guid.NewGuid();
    private readonly Guid _templateId = Guid.NewGuid();
    private readonly Guid _planId = Guid.NewGuid();
    private readonly Guid _householdId = Guid.NewGuid();
    private readonly Guid _member = Guid.NewGuid();
    private readonly Guid _member2 = Guid.NewGuid();

    private static readonly DateOnly Jun1 = new(2025, 6, 1);

    public async ValueTask InitializeAsync()
    {
        _dbContext = TestDbContextFactory.Create(tenantId: _tenantId);
        _dbContext.Tenants.Add(new Tenant { Id = _tenantId, Name = "Test Tenant" });
        _dbContext.Properties.Add(new Property { Id = _propertyId, TenantId = _tenantId, Name = "Lake House" });
        _dbContext.Events.Add(new Event
        {
            Id = _eventId, TenantId = _tenantId, PropertyId = _propertyId,
            Name = "Summer Reunion", StartDate = Jun1, EndDate = Jun1,
        });
        _dbContext.MealTemplates.Add(new MealTemplate
        {
            Id = _templateId, TenantId = _tenantId, EventId = _eventId,
            Name = "Meals", MealTypes = MealTypeFlags.Breakfast,
        });
        _dbContext.MealPlans.Add(new MealPlan
        {
            Id = _planId, TenantId = _tenantId, MealTemplateId = _templateId,
            Day = Jun1, MealType = MealType.Breakfast,
        });
        _dbContext.Households.Add(new Household { Id = _householdId, TenantId = _tenantId, Name = "Smith Household" });
        _dbContext.HouseholdMembers.Add(new HouseholdMember { Id = _member, TenantId = _tenantId, HouseholdId = _householdId, Name = "Alice" });
        _dbContext.HouseholdMembers.Add(new HouseholdMember { Id = _member2, TenantId = _tenantId, HouseholdId = _householdId, Name = "Bob" });
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        _dbContext.Dispose();
        return ValueTask.CompletedTask;
    }

    private MealIntentService CreateService() =>
        new(_dbContext,
            Mock.Of<ICurrentTenantContext>(c => c.TenantId == _tenantId),
            Mock.Of<IMemberAuthorizationService>(),
            Mock.Of<IAuditVisibilityContext>());

    private async Task AddIntentAsync(Guid memberId)
    {
        _dbContext.MealIntents.Add(new MealIntent
        {
            Id = Guid.NewGuid(), TenantId = _tenantId, MealPlanId = _planId,
            HouseholdMemberId = memberId, Source = IntentSource.Volunteered,
        });
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task ListAsync_ReturnsIntentsForPlan()
    {
        // Regression: the list projection must materialize before mapping, otherwise EF Core rejects
        // the instance MapToDto in the query shaper and the endpoint 500s.
        await AddIntentAsync(_member);

        var result = await CreateService().ListAsync(_tenantId, _planId, null, TestContext.Current.CancellationToken);

        Assert.True(result.Successful);
        var intent = Assert.Single(result.Entity!);
        Assert.Equal(_member, intent.HouseholdMemberId);
    }

    [Fact]
    public async Task ListAsync_MemberFilter_ExcludesOtherMembers()
    {
        await AddIntentAsync(_member2);

        var result = await CreateService().ListAsync(_tenantId, _planId, new[] { _member }, TestContext.Current.CancellationToken);

        Assert.True(result.Successful);
        Assert.Empty(result.Entity!);
    }
}
