using Gatherstead.Api.Contracts.Responses;
using Gatherstead.Api.Contracts.ShoppingItems;
using Gatherstead.Api.Services.Authorization;
using Gatherstead.Api.Services.Planning;
using Gatherstead.Api.Services.ShoppingItems;
using Gatherstead.Api.Tests.Fixtures;
using Gatherstead.Data;
using Gatherstead.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace Gatherstead.Api.Tests.Services;

public class ShoppingItemServiceTests : IAsyncLifetime
{
    private GathersteadDbContext _dbContext = null!;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _propertyId = Guid.NewGuid();
    private readonly Guid _eventId = Guid.NewGuid();
    private readonly Guid _templateId = Guid.NewGuid();
    private readonly Guid _planId = Guid.NewGuid();

    private static readonly DateOnly Jun1 = new(2025, 6, 1);
    private static readonly DateOnly Jun2 = new(2025, 6, 2);

    public async ValueTask InitializeAsync()
    {
        _dbContext = TestDbContextFactory.Create(tenantId: _tenantId);
        _dbContext.Tenants.Add(new Tenant { Id = _tenantId, Name = "Test Tenant" });
        _dbContext.Properties.Add(new Property { Id = _propertyId, TenantId = _tenantId, Name = "Camp Nomanisan" });
        _dbContext.Events.Add(new Event
        {
            Id = _eventId, TenantId = _tenantId, PropertyId = _propertyId,
            Name = "Summer Reunion", StartDate = Jun1, EndDate = Jun2,
        });
        _dbContext.MealTemplates.Add(new MealTemplate
        {
            Id = _templateId, TenantId = _tenantId, EventId = _eventId,
            Name = "Dinner", MealTypes = MealTypeFlags.Dinner,
        });
        _dbContext.MealPlans.Add(new MealPlan
        {
            Id = _planId, TenantId = _tenantId, MealTemplateId = _templateId,
            Day = Jun2, MealType = MealType.Dinner,
        });
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        _dbContext.Dispose();
        return ValueTask.CompletedTask;
    }

    private ShoppingItemService CreateService(
        bool canManageTenant = false,
        bool canManageEvent = false,
        bool canEditMenu = false,
        Guid? contextTenantId = null)
    {
        var tenantContext = Mock.Of<ICurrentTenantContext>(c => c.TenantId == (contextTenantId ?? _tenantId));
        var auth = Mock.Of<IMemberAuthorizationService>(a =>
            a.CanManageTenantAsync(_tenantId, It.IsAny<CancellationToken>()) == Task.FromResult(canManageTenant)
            && a.CanManageEventAsync(_tenantId, It.IsAny<CancellationToken>()) == Task.FromResult(canManageEvent)
            && a.CanEditMealPlanMenuAsync(_tenantId, _planId, It.IsAny<CancellationToken>()) == Task.FromResult(canEditMenu)
            && a.GetCallerTenantRoleAsync(_tenantId, It.IsAny<CancellationToken>()) == Task.FromResult((TenantRole?)TenantRole.Owner));
        return new ShoppingItemService(_dbContext, tenantContext, auth, Mock.Of<IAuditVisibilityContext>());
    }

    private async Task<ShoppingItem> AddItemAsync(ShoppingItem item)
    {
        _dbContext.ShoppingItems.Add(item);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        return item;
    }

    // ── Scope validation ─────────────────────────────────────────────────────

    [Fact]
    public async Task CreateAsync_NoScope_ReturnsError()
    {
        var request = new CreateShoppingItemRequest { Name = "Foil" };
        var result = await CreateService(canManageTenant: true)
            .CreateAsync(_tenantId, request, TestContext.Current.CancellationToken);
        Assert.False(result.Successful);
        Assert.Contains(result.Messages, m => m.Type == MessageType.ERROR);
    }

    [Fact]
    public async Task CreateAsync_MultipleScopes_ReturnsError()
    {
        var request = new CreateShoppingItemRequest { Name = "Foil", PropertyId = _propertyId, EventId = _eventId };
        var result = await CreateService(canManageTenant: true, canManageEvent: true)
            .CreateAsync(_tenantId, request, TestContext.Current.CancellationToken);
        Assert.False(result.Successful);
        Assert.Contains(result.Messages, m => m.Type == MessageType.ERROR);
    }

    // ── Property origin ──────────────────────────────────────────────────────

    [Fact]
    public async Task CreateAsync_PropertyOrigin_RequiresTenantManage()
    {
        var request = new CreateShoppingItemRequest { Name = "Motor oil", PropertyId = _propertyId };
        var denied = await CreateService(canManageTenant: false)
            .CreateAsync(_tenantId, request, TestContext.Current.CancellationToken);
        Assert.False(denied.Successful);

        var allowed = await CreateService(canManageTenant: true)
            .CreateAsync(_tenantId, request, TestContext.Current.CancellationToken);
        Assert.True(allowed.Successful);
        Assert.Equal(ShoppingItemOrigin.Property, allowed.Entity!.Origin);
        Assert.Equal(_propertyId, allowed.Entity.PropertyId);
    }

    // ── Event origin ─────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateAsync_EventOrigin_RequiresEventManage()
    {
        var request = new CreateShoppingItemRequest { Name = "Party balloons", EventId = _eventId };
        var denied = await CreateService(canManageEvent: false)
            .CreateAsync(_tenantId, request, TestContext.Current.CancellationToken);
        Assert.False(denied.Successful);

        var allowed = await CreateService(canManageEvent: true)
            .CreateAsync(_tenantId, request, TestContext.Current.CancellationToken);
        Assert.True(allowed.Successful);
        Assert.Equal(ShoppingItemOrigin.Event, allowed.Entity!.Origin);
        Assert.Equal(_eventId, allowed.Entity.EventId);
    }

    // ── Meal origin ──────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateAsync_MealOrigin_Unauthorized_ReturnsError()
    {
        var request = new CreateShoppingItemRequest { Name = "Potatoes", MealPlanId = _planId };
        var result = await CreateService(canEditMenu: false)
            .CreateAsync(_tenantId, request, TestContext.Current.CancellationToken);
        Assert.False(result.Successful);
    }

    [Fact]
    public async Task CreateAsync_MealOrigin_DerivesEventAndNeededByDate()
    {
        var request = new CreateShoppingItemRequest
        {
            Name = "Potatoes", MealPlanId = _planId, QuantityNeeded = 10m, Unit = "lbs",
        };
        var result = await CreateService(canEditMenu: true)
            .CreateAsync(_tenantId, request, TestContext.Current.CancellationToken);

        Assert.True(result.Successful);
        Assert.Equal(ShoppingItemOrigin.Meal, result.Entity!.Origin);
        Assert.Equal(_planId, result.Entity.MealPlanId);
        // EventId and need date are derived from the meal plan, not supplied by the client.
        Assert.Equal(_eventId, result.Entity.EventId);
        Assert.Equal(Jun2, result.Entity.NeededByDate);
    }

    [Fact]
    public async Task CreateAsync_MealOrigin_MealPlanNotFound_ReturnsError()
    {
        var request = new CreateShoppingItemRequest { Name = "Potatoes", MealPlanId = Guid.NewGuid() };
        var auth = Mock.Of<IMemberAuthorizationService>(a =>
            a.CanEditMealPlanMenuAsync(_tenantId, It.IsAny<Guid>(), It.IsAny<CancellationToken>()) == Task.FromResult(true)
            && a.GetCallerTenantRoleAsync(_tenantId, It.IsAny<CancellationToken>()) == Task.FromResult((TenantRole?)TenantRole.Owner));
        var service = new ShoppingItemService(
            _dbContext, Mock.Of<ICurrentTenantContext>(c => c.TenantId == _tenantId), auth, Mock.Of<IAuditVisibilityContext>());

        var result = await service.CreateAsync(_tenantId, request, TestContext.Current.CancellationToken);
        Assert.False(result.Successful);
    }

    // ── Fulfillment (open to everyone) ───────────────────────────────────────

    [Fact]
    public async Task UpdateFulfillmentAsync_NoStructuralAuth_PartialThenCovered()
    {
        var item = await AddItemAsync(new ShoppingItem
        {
            Id = Guid.NewGuid(), TenantId = _tenantId, Origin = ShoppingItemOrigin.Meal,
            MealPlanId = _planId, EventId = _eventId, Name = "Potatoes",
            QuantityNeeded = 10m, NeededByDate = Jun2,
        });

        // A member with no management rights provides 5 of 10 lbs — partial fulfillment.
        var partial = await CreateService()
            .UpdateFulfillmentAsync(_tenantId, item.Id, new UpdateFulfillmentRequest
            {
                Status = ShoppingItemStatus.Claimed, QuantityProvided = 5m,
            }, TestContext.Current.CancellationToken);

        Assert.True(partial.Successful);
        Assert.Equal(ShoppingItemStatus.Claimed, partial.Entity!.Status);
        Assert.Equal(5m, partial.Entity.QuantityNeeded!.Value - partial.Entity.QuantityProvided!.Value); // 5 still needed

        // The remaining 5 lbs arrive — covered.
        var covered = await CreateService()
            .UpdateFulfillmentAsync(_tenantId, item.Id, new UpdateFulfillmentRequest
            {
                Status = ShoppingItemStatus.Covered, QuantityProvided = 10m,
            }, TestContext.Current.CancellationToken);
        Assert.Equal(ShoppingItemStatus.Covered, covered.Entity!.Status);
    }

    [Fact]
    public async Task UpdateFulfillmentAsync_ReflagNeeded_ResetsStatus()
    {
        var item = await AddItemAsync(new ShoppingItem
        {
            Id = Guid.NewGuid(), TenantId = _tenantId, Origin = ShoppingItemOrigin.Meal,
            MealPlanId = _planId, EventId = _eventId, Name = "Butter",
            Status = ShoppingItemStatus.Covered, QuantityProvided = 2m,
        });

        // Ran out at breakfast; flag it needed again for dinner.
        var result = await CreateService()
            .UpdateFulfillmentAsync(_tenantId, item.Id, new UpdateFulfillmentRequest
            {
                Status = ShoppingItemStatus.Needed, QuantityProvided = null,
            }, TestContext.Current.CancellationToken);

        Assert.True(result.Successful);
        Assert.Equal(ShoppingItemStatus.Needed, result.Entity!.Status);
        Assert.Null(result.Entity.QuantityProvided);
    }

    // ── Structural update auth by origin ─────────────────────────────────────

    [Fact]
    public async Task UpdateAsync_MealOrigin_RequiresMenuAuth()
    {
        var item = await AddItemAsync(new ShoppingItem
        {
            Id = Guid.NewGuid(), TenantId = _tenantId, Origin = ShoppingItemOrigin.Meal,
            MealPlanId = _planId, EventId = _eventId, Name = "Potatoes",
        });
        var request = new UpdateShoppingItemRequest { Name = "Sweet potatoes" };

        var denied = await CreateService(canEditMenu: false)
            .UpdateAsync(_tenantId, item.Id, request, TestContext.Current.CancellationToken);
        Assert.False(denied.Successful);

        var allowed = await CreateService(canEditMenu: true)
            .UpdateAsync(_tenantId, item.Id, request, TestContext.Current.CancellationToken);
        Assert.True(allowed.Successful);
        Assert.Equal("Sweet potatoes", allowed.Entity!.Name);
    }

    // ── Delete ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteAsync_SoftDeletesItemAndAttributes()
    {
        var item = await AddItemAsync(new ShoppingItem
        {
            Id = Guid.NewGuid(), TenantId = _tenantId, Origin = ShoppingItemOrigin.Property,
            PropertyId = _propertyId, Name = "Foil",
        });
        _dbContext.ShoppingItemAttributes.Add(new ShoppingItemAttribute
        {
            Id = Guid.NewGuid(), TenantId = _tenantId, ShoppingItemId = item.Id, Key = "aisle", Value = "7",
        });
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await CreateService(canManageTenant: true)
            .DeleteAsync(_tenantId, item.Id, TestContext.Current.CancellationToken);

        Assert.True(result.Successful);
        var storedItem = await _dbContext.ShoppingItems.IgnoreQueryFilters()
            .SingleAsync(i => i.Id == item.Id, TestContext.Current.CancellationToken);
        Assert.True(storedItem.IsDeleted);
        var storedAttr = await _dbContext.ShoppingItemAttributes.IgnoreQueryFilters()
            .SingleAsync(a => a.ShoppingItemId == item.Id, TestContext.Current.CancellationToken);
        Assert.True(storedAttr.IsDeleted);
    }

    // ── Scope filtering ──────────────────────────────────────────────────────

    [Fact]
    public async Task ListAsync_EventScope_IncludesMealItems_ExcludesPropertyItems()
    {
        // Meal items carry EventId, so the event scope returns both event- and meal-origin items.
        await AddItemAsync(new ShoppingItem
        {
            Id = Guid.NewGuid(), TenantId = _tenantId, Origin = ShoppingItemOrigin.Event,
            EventId = _eventId, Name = "Balloons",
        });
        await AddItemAsync(new ShoppingItem
        {
            Id = Guid.NewGuid(), TenantId = _tenantId, Origin = ShoppingItemOrigin.Meal,
            EventId = _eventId, MealPlanId = _planId, Name = "Potatoes",
        });
        await AddItemAsync(new ShoppingItem
        {
            Id = Guid.NewGuid(), TenantId = _tenantId, Origin = ShoppingItemOrigin.Property,
            PropertyId = _propertyId, Name = "Foil",
        });

        var result = await CreateService()
            .ListAsync(_tenantId, _eventId, null, null, null, TestContext.Current.CancellationToken);

        Assert.True(result.Successful);
        Assert.Equal(2, result.Entity!.Count);
        Assert.DoesNotContain(result.Entity!, i => i.Name == "Foil");
    }

    [Fact]
    public async Task ListAsync_StatusFilter_ReturnsOnlyMatching()
    {
        await AddItemAsync(new ShoppingItem
        {
            Id = Guid.NewGuid(), TenantId = _tenantId, Origin = ShoppingItemOrigin.Event,
            EventId = _eventId, Name = "Open", Status = ShoppingItemStatus.Needed,
        });
        await AddItemAsync(new ShoppingItem
        {
            Id = Guid.NewGuid(), TenantId = _tenantId, Origin = ShoppingItemOrigin.Event,
            EventId = _eventId, Name = "Done", Status = ShoppingItemStatus.Covered,
        });

        var result = await CreateService()
            .ListAsync(_tenantId, _eventId, null, null, ShoppingItemStatus.Needed, TestContext.Current.CancellationToken);

        Assert.True(result.Successful);
        Assert.Single(result.Entity!);
        Assert.Equal("Open", result.Entity!.First().Name);
    }

    [Fact]
    public async Task ListAsync_NoScope_ReturnsError()
    {
        var result = await CreateService()
            .ListAsync(_tenantId, null, null, null, null, TestContext.Current.CancellationToken);
        Assert.False(result.Successful);
    }

    // ── Prune cascade (PlanSyncService) ──────────────────────────────────────

    [Fact]
    public async Task PrunedMealPlan_SoftDeletesItsShoppingItems()
    {
        var item = await AddItemAsync(new ShoppingItem
        {
            Id = Guid.NewGuid(), TenantId = _tenantId, Origin = ShoppingItemOrigin.Meal,
            MealPlanId = _planId, EventId = _eventId, Name = "Potatoes", NeededByDate = Jun2,
        });

        // Shrink the template window so the Jun2 plan is pruned.
        var template = await _dbContext.MealTemplates.FindAsync([_templateId], TestContext.Current.CancellationToken);
        await new PlanSyncService(_dbContext).SyncMealPlanAsync(_tenantId, template!, Jun1, Jun1, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var stored = await _dbContext.ShoppingItems.IgnoreQueryFilters()
            .SingleAsync(i => i.Id == item.Id, TestContext.Current.CancellationToken);
        Assert.True(stored.IsDeleted);
    }
}
