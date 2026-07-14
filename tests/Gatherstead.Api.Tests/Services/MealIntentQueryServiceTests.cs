using Gatherstead.Api.Contracts.Responses;
using Gatherstead.Api.Services.Authorization;
using Gatherstead.Api.Services.MealIntents;
using Gatherstead.Api.Tests.Fixtures;
using Gatherstead.Data;
using Gatherstead.Data.Entities;
using Moq;

namespace Gatherstead.Api.Tests.Services;

/// <summary>Covers the cross-event "my meals" read (<see cref="MealIntentService.ListForMemberAsync"/>).</summary>
public class MealIntentQueryServiceTests : IAsyncLifetime
{
    private GathersteadDbContext _dbContext = null!;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _propertyId = Guid.NewGuid();
    private readonly Guid _eventId = Guid.NewGuid();
    private readonly Guid _templateId = Guid.NewGuid();
    private readonly Guid _pastPlanId = Guid.NewGuid();
    private readonly Guid _futurePlanId = Guid.NewGuid();
    private readonly Guid _householdId = Guid.NewGuid();
    private readonly Guid _member = Guid.NewGuid();
    private readonly Guid _member2 = Guid.NewGuid();

    private static readonly DateOnly Past = new(2025, 6, 1);
    private static readonly DateOnly Future = new(2025, 6, 10);
    private static readonly DateOnly Cutoff = new(2025, 6, 5);

    public async ValueTask InitializeAsync()
    {
        _dbContext = TestDbContextFactory.Create(tenantId: _tenantId);
        _dbContext.Tenants.Add(new Tenant { Id = _tenantId, Name = "Test Tenant" });
        _dbContext.Properties.Add(new Property { Id = _propertyId, TenantId = _tenantId, Name = "Lake House" });
        _dbContext.Events.Add(new Event
        {
            Id = _eventId, TenantId = _tenantId, PropertyId = _propertyId,
            Name = "Summer Reunion", StartDate = Past, EndDate = Future,
        });
        _dbContext.MealTemplates.Add(new MealTemplate
        {
            Id = _templateId, TenantId = _tenantId, EventId = _eventId, Name = "Camp Dinner",
            MealTypes = MealTypeFlags.Dinner,
        });
        _dbContext.MealPlans.Add(new MealPlan { Id = _pastPlanId, TenantId = _tenantId, MealTemplateId = _templateId, Day = Past, MealType = MealType.Dinner });
        _dbContext.MealPlans.Add(new MealPlan { Id = _futurePlanId, TenantId = _tenantId, MealTemplateId = _templateId, Day = Future, MealType = MealType.Dinner, Notes = "Chili night" });
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

    private MealIntentService CreateService()
    {
        var tenantContext = Mock.Of<ICurrentTenantContext>(c => c.TenantId == _tenantId);
        return new MealIntentService(_dbContext, tenantContext, Mock.Of<IMemberAuthorizationService>(), Mock.Of<IAuditVisibilityContext>());
    }

    private async Task AddIntentAsync(Guid planId, Guid memberId, bool deleted = false)
    {
        _dbContext.MealIntents.Add(new MealIntent
        {
            Id = Guid.NewGuid(), TenantId = _tenantId, MealPlanId = planId,
            HouseholdMemberId = memberId, Source = IntentSource.Volunteered, IsDeleted = deleted,
        });
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task ListForMemberAsync_EnrichesWithMealAndEventContext()
    {
        await AddIntentAsync(_futurePlanId, _member);

        var result = await CreateService().ListForMemberAsync(_tenantId, new[] { _member }, null, TestContext.Current.CancellationToken);

        Assert.True(result.Successful);
        var meal = Assert.Single(result.Entity!);
        Assert.Equal("Camp Dinner", meal.TemplateName);
        Assert.Equal(_templateId, meal.TemplateId);
        Assert.Equal("Summer Reunion", meal.EventName);
        Assert.Equal(_eventId, meal.EventId);
        Assert.Equal(Future, meal.Day);
        Assert.Equal(MealType.Dinner, meal.MealType);
        Assert.Equal("Chili night", meal.Notes);
    }

    [Fact]
    public async Task ListForMemberAsync_ExcludesSoftDeletedIntents()
    {
        // A withdrawn cook sign-up is a soft-deleted row; it must not appear in "my meals".
        await AddIntentAsync(_futurePlanId, _member, deleted: true);

        var result = await CreateService().ListForMemberAsync(_tenantId, new[] { _member }, null, TestContext.Current.CancellationToken);

        Assert.True(result.Successful);
        Assert.Empty(result.Entity!);
    }

    [Fact]
    public async Task ListForMemberAsync_MemberFilter_ExcludesOtherMembers()
    {
        await AddIntentAsync(_futurePlanId, _member2);

        var result = await CreateService().ListForMemberAsync(_tenantId, new[] { _member }, null, TestContext.Current.CancellationToken);

        Assert.True(result.Successful);
        Assert.Empty(result.Entity!);
    }

    [Fact]
    public async Task ListForMemberAsync_FromDay_ExcludesPastPlans()
    {
        await AddIntentAsync(_pastPlanId, _member);
        await AddIntentAsync(_futurePlanId, _member);

        var result = await CreateService().ListForMemberAsync(_tenantId, new[] { _member }, Cutoff, TestContext.Current.CancellationToken);

        Assert.True(result.Successful);
        var meal = Assert.Single(result.Entity!);
        Assert.Equal(Future, meal.Day);
    }
}
