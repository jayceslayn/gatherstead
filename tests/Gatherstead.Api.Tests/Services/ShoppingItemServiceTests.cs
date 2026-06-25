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
    private readonly Guid _householdId = Guid.NewGuid();
    private readonly Guid _aliceId = Guid.NewGuid();
    private readonly Guid _bobId = Guid.NewGuid();

    private static readonly DateOnly Jun1 = new(2025, 6, 1);
    private static readonly DateOnly Jun2 = new(2025, 6, 2);

    public async ValueTask InitializeAsync()
    {
        _dbContext = TestDbContextFactory.Create(tenantId: _tenantId);
        _dbContext.Tenants.Add(new Tenant { Id = _tenantId, Name = "Test Tenant" });
        _dbContext.Properties.Add(new Property { Id = _propertyId, TenantId = _tenantId, Name = "Camp Nomanisan" });
        _dbContext.Households.Add(new Household { Id = _householdId, TenantId = _tenantId, Name = "Parr" });
        _dbContext.HouseholdMembers.Add(new HouseholdMember { Id = _aliceId, TenantId = _tenantId, HouseholdId = _householdId, Name = "Alice" });
        _dbContext.HouseholdMembers.Add(new HouseholdMember { Id = _bobId, TenantId = _tenantId, HouseholdId = _householdId, Name = "Bob" });
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
    public async Task CreateAsync_PropertyOrigin_RequiresEventManage()
    {
        // Property-level lists are editable by Coordinators+ (event-manage), not only Managers (tenant-manage).
        var request = new CreateShoppingItemRequest { Name = "Motor oil", PropertyId = _propertyId };
        var denied = await CreateService(canManageEvent: false, canManageTenant: true)
            .CreateAsync(_tenantId, request, TestContext.Current.CancellationToken);
        Assert.False(denied.Successful);

        var allowed = await CreateService(canManageEvent: true)
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

    // ── Intents / fulfillment (open to everyone) ─────────────────────────────

    [Fact]
    public async Task UpsertIntentAsync_TwoMembersPartialThenProvided_DerivesStatusAndProvidedSum()
    {
        var item = await AddItemAsync(new ShoppingItem
        {
            Id = Guid.NewGuid(), TenantId = _tenantId, Origin = ShoppingItemOrigin.Meal,
            MealPlanId = _planId, EventId = _eventId, Name = "Potatoes",
            QuantityNeeded = 10m, NeededByDate = Jun2,
        });

        // Alice claims 4 lbs (no management rights required) — item is Claimed, nothing provided yet.
        var aliceClaim = await CreateService()
            .UpsertIntentAsync(_tenantId, item.Id, _aliceId, new UpsertShoppingItemIntentRequest
            {
                Quantity = 4m, Status = ShoppingItemIntentStatus.Claimed,
            }, TestContext.Current.CancellationToken);

        Assert.True(aliceClaim.Successful);
        Assert.Equal(ShoppingItemStatus.Claimed, aliceClaim.Entity!.Status);
        Assert.Null(aliceClaim.Entity.QuantityProvided);
        Assert.Single(aliceClaim.Entity.Intents);

        // Bob brings 6 lbs and Alice delivers her 4 — provided sum reaches 10, so the item is Covered.
        await CreateService().UpsertIntentAsync(_tenantId, item.Id, _bobId, new UpsertShoppingItemIntentRequest
        {
            Quantity = 6m, Status = ShoppingItemIntentStatus.Provided,
        }, TestContext.Current.CancellationToken);

        var covered = await CreateService().UpsertIntentAsync(_tenantId, item.Id, _aliceId, new UpsertShoppingItemIntentRequest
        {
            Quantity = 4m, Status = ShoppingItemIntentStatus.Provided,
        }, TestContext.Current.CancellationToken);

        Assert.Equal(ShoppingItemStatus.Covered, covered.Entity!.Status);
        Assert.Equal(10m, covered.Entity.QuantityProvided);
        Assert.Equal(2, covered.Entity.Intents.Count);
    }

    [Fact]
    public async Task UpsertIntentAsync_SameMemberTwice_UpdatesInPlace_NoDuplicateRow()
    {
        var item = await AddItemAsync(new ShoppingItem
        {
            Id = Guid.NewGuid(), TenantId = _tenantId, Origin = ShoppingItemOrigin.Event,
            EventId = _eventId, Name = "Ice", QuantityNeeded = 3m,
        });

        await CreateService().UpsertIntentAsync(_tenantId, item.Id, _aliceId, new UpsertShoppingItemIntentRequest
        {
            Quantity = 1m, Status = ShoppingItemIntentStatus.Claimed,
        }, TestContext.Current.CancellationToken);

        var second = await CreateService().UpsertIntentAsync(_tenantId, item.Id, _aliceId, new UpsertShoppingItemIntentRequest
        {
            Quantity = 3m, Status = ShoppingItemIntentStatus.Provided,
        }, TestContext.Current.CancellationToken);

        Assert.True(second.Successful);
        Assert.Single(second.Entity!.Intents);
        Assert.Equal(ShoppingItemStatus.Covered, second.Entity.Status);

        var rows = await _dbContext.ShoppingItemIntents.IgnoreQueryFilters()
            .CountAsync(x => x.ShoppingItemId == item.Id, TestContext.Current.CancellationToken);
        Assert.Equal(1, rows);
    }

    [Fact]
    public async Task UpsertIntentAsync_UnknownMember_ReturnsError()
    {
        var item = await AddItemAsync(new ShoppingItem
        {
            Id = Guid.NewGuid(), TenantId = _tenantId, Origin = ShoppingItemOrigin.Event,
            EventId = _eventId, Name = "Charcoal",
        });

        var result = await CreateService().UpsertIntentAsync(_tenantId, item.Id, Guid.NewGuid(),
            new UpsertShoppingItemIntentRequest { Status = ShoppingItemIntentStatus.Claimed },
            TestContext.Current.CancellationToken);

        Assert.False(result.Successful);
        Assert.Contains(result.Messages, m => m.Type == MessageType.ERROR);
    }

    [Fact]
    public async Task RemoveIntentAsync_DropsContribution_RevertsStatus_AndReclaimReusesRow()
    {
        var item = await AddItemAsync(new ShoppingItem
        {
            Id = Guid.NewGuid(), TenantId = _tenantId, Origin = ShoppingItemOrigin.Meal,
            MealPlanId = _planId, EventId = _eventId, Name = "Butter", QuantityNeeded = 2m, NeededByDate = Jun2,
        });

        await CreateService().UpsertIntentAsync(_tenantId, item.Id, _aliceId, new UpsertShoppingItemIntentRequest
        {
            Quantity = 2m, Status = ShoppingItemIntentStatus.Provided,
        }, TestContext.Current.CancellationToken);

        // Un-claim: status reverts to Needed and nothing is provided.
        var removed = await CreateService()
            .RemoveIntentAsync(_tenantId, item.Id, _aliceId, TestContext.Current.CancellationToken);
        Assert.True(removed.Successful);
        Assert.Equal(ShoppingItemStatus.Needed, removed.Entity!.Status);
        Assert.Empty(removed.Entity.Intents);

        // Re-claiming reuses the soft-deleted row (unique index spans deleted rows) — no duplicate.
        var reclaim = await CreateService().UpsertIntentAsync(_tenantId, item.Id, _aliceId, new UpsertShoppingItemIntentRequest
        {
            Quantity = 2m, Status = ShoppingItemIntentStatus.Provided,
        }, TestContext.Current.CancellationToken);
        Assert.Equal(ShoppingItemStatus.Covered, reclaim.Entity!.Status);

        var rows = await _dbContext.ShoppingItemIntents.IgnoreQueryFilters()
            .CountAsync(x => x.ShoppingItemId == item.Id, TestContext.Current.CancellationToken);
        Assert.Equal(1, rows);
    }

    [Fact]
    public async Task UpdateAsync_IncreaseQuantityNeeded_RevertsCoveredToClaimed_WithoutAlteringIntents()
    {
        // Core requirement: needing more is just bumping QuantityNeeded — existing intents are untouched.
        var item = await AddItemAsync(new ShoppingItem
        {
            Id = Guid.NewGuid(), TenantId = _tenantId, Origin = ShoppingItemOrigin.Event,
            EventId = _eventId, Name = "Buns", QuantityNeeded = 10m,
        });
        await CreateService().UpsertIntentAsync(_tenantId, item.Id, _aliceId, new UpsertShoppingItemIntentRequest
        {
            Quantity = 10m, Status = ShoppingItemIntentStatus.Provided,
        }, TestContext.Current.CancellationToken);

        var bumped = await CreateService(canManageEvent: true).UpdateAsync(_tenantId, item.Id,
            new UpdateShoppingItemRequest { Name = "Buns", QuantityNeeded = 16m },
            TestContext.Current.CancellationToken);

        Assert.True(bumped.Successful);
        Assert.Equal(16m, bumped.Entity!.QuantityNeeded);
        Assert.Equal(ShoppingItemStatus.Claimed, bumped.Entity.Status); // 10 of 16 provided
        Assert.Equal(10m, bumped.Entity.QuantityProvided);
        Assert.Single(bumped.Entity.Intents); // intent preserved, not revoked
    }

    [Fact]
    public async Task UpsertIntentAsync_NoQuantityNeeded_ProvidedIntentCovers()
    {
        // Unquantified item: a single Provided intent (with or without a quantity) covers it.
        var item = await AddItemAsync(new ShoppingItem
        {
            Id = Guid.NewGuid(), TenantId = _tenantId, Origin = ShoppingItemOrigin.Property,
            PropertyId = _propertyId, Name = "Bug spray",
        });

        var claimed = await CreateService().UpsertIntentAsync(_tenantId, item.Id, _aliceId,
            new UpsertShoppingItemIntentRequest { Status = ShoppingItemIntentStatus.Claimed },
            TestContext.Current.CancellationToken);
        Assert.Equal(ShoppingItemStatus.Claimed, claimed.Entity!.Status);

        var provided = await CreateService().UpsertIntentAsync(_tenantId, item.Id, _aliceId,
            new UpsertShoppingItemIntentRequest { Status = ShoppingItemIntentStatus.Provided },
            TestContext.Current.CancellationToken);
        Assert.Equal(ShoppingItemStatus.Covered, provided.Entity!.Status);
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
    public async Task DeleteAsync_SoftDeletesItemAttributesAndIntents()
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
        await CreateService().UpsertIntentAsync(_tenantId, item.Id, _aliceId,
            new UpsertShoppingItemIntentRequest { Quantity = 1m, Status = ShoppingItemIntentStatus.Claimed },
            TestContext.Current.CancellationToken);

        var result = await CreateService(canManageEvent: true)
            .DeleteAsync(_tenantId, item.Id, TestContext.Current.CancellationToken);

        Assert.True(result.Successful);
        var storedItem = await _dbContext.ShoppingItems.IgnoreQueryFilters()
            .SingleAsync(i => i.Id == item.Id, TestContext.Current.CancellationToken);
        Assert.True(storedItem.IsDeleted);
        var storedAttr = await _dbContext.ShoppingItemAttributes.IgnoreQueryFilters()
            .SingleAsync(a => a.ShoppingItemId == item.Id, TestContext.Current.CancellationToken);
        Assert.True(storedAttr.IsDeleted);
        var storedIntent = await _dbContext.ShoppingItemIntents.IgnoreQueryFilters()
            .SingleAsync(x => x.ShoppingItemId == item.Id, TestContext.Current.CancellationToken);
        Assert.True(storedIntent.IsDeleted);
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
            .ListAsync(_tenantId, _eventId, null, null, null, cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Successful);
        Assert.Equal(2, result.Entity!.Count);
        Assert.DoesNotContain(result.Entity!, i => i.Name == "Foil");
    }

    [Fact]
    public async Task ListAsync_StatusFilter_MatchesDerivedStatus()
    {
        // "Open" has no intents (derives Needed); "Done" is fully provided (derives Covered).
        await AddItemAsync(new ShoppingItem
        {
            Id = Guid.NewGuid(), TenantId = _tenantId, Origin = ShoppingItemOrigin.Event,
            EventId = _eventId, Name = "Open",
        });
        var done = await AddItemAsync(new ShoppingItem
        {
            Id = Guid.NewGuid(), TenantId = _tenantId, Origin = ShoppingItemOrigin.Event,
            EventId = _eventId, Name = "Done",
        });
        await CreateService().UpsertIntentAsync(_tenantId, done.Id, _aliceId,
            new UpsertShoppingItemIntentRequest { Status = ShoppingItemIntentStatus.Provided },
            TestContext.Current.CancellationToken);

        var result = await CreateService()
            .ListAsync(_tenantId, _eventId, null, null, ShoppingItemStatus.Needed, cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Successful);
        Assert.Single(result.Entity!);
        Assert.Equal("Open", result.Entity!.First().Name);
    }

    [Fact]
    public async Task ListAsync_NoScope_ReturnsError()
    {
        var result = await CreateService()
            .ListAsync(_tenantId, null, null, null, null, cancellationToken: TestContext.Current.CancellationToken);
        Assert.False(result.Successful);
    }

    [Fact]
    public async Task ListAsync_ClaimedByMember_ReturnsOnlyItemsTheMemberClaimed()
    {
        var claimed = await AddItemAsync(new ShoppingItem
        {
            Id = Guid.NewGuid(), TenantId = _tenantId, Origin = ShoppingItemOrigin.Event,
            EventId = _eventId, Name = "Claimed by Alice",
        });
        var other = await AddItemAsync(new ShoppingItem
        {
            Id = Guid.NewGuid(), TenantId = _tenantId, Origin = ShoppingItemOrigin.Event,
            EventId = _eventId, Name = "Claimed by Bob",
        });
        await CreateService().UpsertIntentAsync(_tenantId, claimed.Id, _aliceId,
            new UpsertShoppingItemIntentRequest { Status = ShoppingItemIntentStatus.Claimed },
            TestContext.Current.CancellationToken);
        await CreateService().UpsertIntentAsync(_tenantId, other.Id, _bobId,
            new UpsertShoppingItemIntentRequest { Status = ShoppingItemIntentStatus.Claimed },
            TestContext.Current.CancellationToken);

        // claimedByMemberId alone is a valid scope (no event/property/meal needed).
        var result = await CreateService()
            .ListAsync(_tenantId, null, null, null, null, _aliceId, TestContext.Current.CancellationToken);

        Assert.True(result.Successful);
        var item = Assert.Single(result.Entity!);
        Assert.Equal("Claimed by Alice", item.Name);
    }

    [Fact]
    public async Task ListAsync_ClaimedByMember_ExcludesProvidedIntents()
    {
        var item = await AddItemAsync(new ShoppingItem
        {
            Id = Guid.NewGuid(), TenantId = _tenantId, Origin = ShoppingItemOrigin.Event,
            EventId = _eventId, Name = "Already provided",
        });
        await CreateService().UpsertIntentAsync(_tenantId, item.Id, _aliceId,
            new UpsertShoppingItemIntentRequest { Status = ShoppingItemIntentStatus.Provided },
            TestContext.Current.CancellationToken);

        var result = await CreateService()
            .ListAsync(_tenantId, null, null, null, null, _aliceId, TestContext.Current.CancellationToken);

        Assert.True(result.Successful);
        Assert.Empty(result.Entity!);
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
