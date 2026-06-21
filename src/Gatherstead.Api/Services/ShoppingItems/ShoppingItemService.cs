using Gatherstead.Api.Contracts.Attributes;
using Gatherstead.Api.Contracts.Responses;
using Gatherstead.Api.Contracts.ShoppingItems;
using Gatherstead.Api.Observability;
using Gatherstead.Api.Services.Attributes;
using Gatherstead.Api.Services.Authorization;
using Gatherstead.Api.Services.Validation;
using Gatherstead.Data;
using Gatherstead.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Gatherstead.Api.Services.ShoppingItems;

public class ShoppingItemService : IShoppingItemService
{
    private const string EntityDisplayName = "Shopping item";

    private readonly GathersteadDbContext _dbContext;
    private readonly ICurrentTenantContext _currentTenantContext;
    private readonly IMemberAuthorizationService _memberAuthorizationService;
    private readonly IAuditVisibilityContext _auditVisibility;

    public ShoppingItemService(
        GathersteadDbContext dbContext,
        ICurrentTenantContext currentTenantContext,
        IMemberAuthorizationService memberAuthorizationService,
        IAuditVisibilityContext auditVisibility)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _currentTenantContext = currentTenantContext ?? throw new ArgumentNullException(nameof(currentTenantContext));
        _memberAuthorizationService = memberAuthorizationService ?? throw new ArgumentNullException(nameof(memberAuthorizationService));
        _auditVisibility = auditVisibility ?? throw new ArgumentNullException(nameof(auditVisibility));
    }

    public async Task<BaseEntityResponse<IReadOnlyCollection<ShoppingItemDto>>> ListAsync(
        Guid tenantId,
        Guid? eventId,
        Guid? propertyId,
        Guid? mealPlanId,
        ShoppingItemStatus? status,
        CancellationToken cancellationToken = default)
    {
        var response = new BaseEntityResponse<IReadOnlyCollection<ShoppingItemDto>>();

        if (!ServiceValidationHelper.ValidateTenantContext(tenantId, _currentTenantContext, response))
            return response;

        if (eventId is null && propertyId is null && mealPlanId is null)
        {
            response.AddResponseMessage(MessageType.ERROR, "At least one of eventId, propertyId, or mealPlanId is required.");
            return response;
        }

        var query = _dbContext.ShoppingItems
            .AsNoTracking()
            .Where(i => i.TenantId == tenantId);

        if (eventId is Guid ev) query = query.Where(i => i.EventId == ev);
        if (propertyId is Guid prop) query = query.Where(i => i.PropertyId == prop);
        if (mealPlanId is Guid plan) query = query.Where(i => i.MealPlanId == plan);
        if (status is ShoppingItemStatus st) query = query.Where(i => i.Status == st);

        var items = await query.ToListAsync(cancellationToken);

        return BaseEntityResponse<IReadOnlyCollection<ShoppingItemDto>>.SuccessfulResponse(
            items.Select(i => MapToDto(i, [])).ToList());
    }

    public async Task<ShoppingItemResponse> GetAsync(Guid tenantId, Guid itemId, CancellationToken cancellationToken = default)
    {
        var response = new ShoppingItemResponse();

        if (!ServiceValidationHelper.ValidateTenantContext(tenantId, _currentTenantContext, response))
            return response;

        var item = await ServiceGuards.LoadOrNotFoundAsync(
            response,
            _dbContext.ShoppingItems.AsNoTracking()
                .Include(i => i.Attributes)
                .Where(i => i.TenantId == tenantId && i.Id == itemId),
            EntityDisplayName,
            cancellationToken);

        if (item is null) return response;

        var callerRole = await _memberAuthorizationService.GetCallerTenantRoleAsync(tenantId, cancellationToken);
        response.SetSuccess(MapToDto(item, VisibleAttributes(item.Attributes, callerRole)));
        return response;
    }

    public async Task<ShoppingItemResponse> CreateAsync(Guid tenantId, CreateShoppingItemRequest request, CancellationToken cancellationToken = default)
    {
        var response = new ShoppingItemResponse();

        if (!ServiceValidationHelper.ValidateTenantContext(tenantId, _currentTenantContext, response))
            return response;
        if (!ServiceGuards.RequireRequest(request, "create shopping item", response))
            return response;

        var scopeCount = (request.PropertyId.HasValue ? 1 : 0)
            + (request.EventId.HasValue ? 1 : 0)
            + (request.MealPlanId.HasValue ? 1 : 0);
        if (scopeCount != 1)
        {
            response.AddResponseMessage(MessageType.ERROR, "Exactly one of propertyId, eventId, or mealPlanId must be supplied.");
            return response;
        }

        ServiceValidationHelper.TryNormalizeString(request.Name, "Shopping item name", response, out string normalizedName);
        if (ServiceValidationHelper.HasErrors(response))
            return response;

        var item = new ShoppingItem
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = normalizedName,
            QuantityNeeded = request.QuantityNeeded,
            Unit = request.Unit?.Trim(),
            Status = ShoppingItemStatus.Needed,
            NeededByDate = request.NeededByDate,
            Category = request.Category?.Trim(),
            Notes = request.Notes?.Trim(),
        };

        if (request.PropertyId is Guid propertyId)
        {
            if (!await ServiceGuards.AuthorizeTenantManageAsync(response, _memberAuthorizationService, tenantId, cancellationToken))
                return response;
            var exists = await _dbContext.Properties.AsNoTracking()
                .AnyAsync(p => p.TenantId == tenantId && p.Id == propertyId, cancellationToken);
            if (!exists)
            {
                response.AddResponseMessage(MessageType.ERROR, "The specified property does not exist.");
                return response;
            }
            item.Origin = ShoppingItemOrigin.Property;
            item.PropertyId = propertyId;
        }
        else if (request.EventId is Guid eventId)
        {
            if (!await ServiceGuards.AuthorizeEventManageAsync(response, _memberAuthorizationService, tenantId, cancellationToken))
                return response;
            var exists = await _dbContext.Events.AsNoTracking()
                .AnyAsync(e => e.TenantId == tenantId && e.Id == eventId, cancellationToken);
            if (!exists)
            {
                response.AddResponseMessage(MessageType.ERROR, "The specified event does not exist.");
                return response;
            }
            item.Origin = ShoppingItemOrigin.Event;
            item.EventId = eventId;
        }
        else
        {
            var mealPlanId = request.MealPlanId!.Value;
            if (!await ServiceGuards.AuthorizeMealPlanMenuAsync(response, _memberAuthorizationService, tenantId, mealPlanId, cancellationToken))
                return response;
            var planInfo = await _dbContext.MealPlans.AsNoTracking()
                .Where(p => p.TenantId == tenantId && p.Id == mealPlanId)
                .Select(p => new { p.Day, p.MealTemplate!.EventId })
                .FirstOrDefaultAsync(cancellationToken);
            if (planInfo is null)
            {
                response.AddResponseMessage(MessageType.ERROR, "The specified meal plan does not exist.");
                return response;
            }
            item.Origin = ShoppingItemOrigin.Meal;
            item.MealPlanId = mealPlanId;
            item.EventId = planInfo.EventId;
            // Meal items inherit their plan's day so the merged view can sort/group by need date.
            item.NeededByDate = planInfo.Day;
        }

        _dbContext.ShoppingItems.Add(item);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var callerRole = await _memberAuthorizationService.GetCallerTenantRoleAsync(tenantId, cancellationToken);
        List<AttributeDto> attrs = [];

        if (request.Attributes is { Count: > 0 })
        {
            await SyncAttributesAsync(item.Id, tenantId, request.Attributes, callerRole, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);

            var savedAttrs = await _dbContext.ShoppingItemAttributes.AsNoTracking()
                .Where(a => a.ShoppingItemId == item.Id).ToListAsync(cancellationToken);
            attrs = VisibleAttributes(savedAttrs, callerRole);
        }

        response.SetSuccess(MapToDto(item, attrs));
        return response;
    }

    public async Task<ShoppingItemResponse> UpdateAsync(Guid tenantId, Guid itemId, UpdateShoppingItemRequest request, CancellationToken cancellationToken = default)
    {
        var response = new ShoppingItemResponse();

        if (!ServiceValidationHelper.ValidateTenantContext(tenantId, _currentTenantContext, response))
            return response;
        if (!ServiceGuards.RequireRequest(request, "update shopping item", response))
            return response;

        ServiceValidationHelper.TryNormalizeString(request.Name, "Shopping item name", response, out string normalizedName);
        if (ServiceValidationHelper.HasErrors(response))
            return response;

        var item = await ServiceGuards.LoadOrNotFoundAsync(
            response,
            _dbContext.ShoppingItems.Where(i => i.TenantId == tenantId && i.Id == itemId),
            EntityDisplayName,
            cancellationToken);

        if (item is null) return response;

        if (!await AuthorizeStructuralAsync(response, item, tenantId, cancellationToken))
            return response;

        item.Name = normalizedName;
        item.QuantityNeeded = request.QuantityNeeded;
        item.Unit = request.Unit?.Trim();
        item.Category = request.Category?.Trim();
        item.Notes = request.Notes?.Trim();
        // Meal items keep their plan-derived need date; only manual-scope items accept an override.
        if (item.Origin != ShoppingItemOrigin.Meal)
            item.NeededByDate = request.NeededByDate;

        var callerRole = await _memberAuthorizationService.GetCallerTenantRoleAsync(tenantId, cancellationToken);

        if (request.Attributes is not null)
            await SyncAttributesAsync(itemId, tenantId, request.Attributes, callerRole, cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);

        var savedAttrs = await _dbContext.ShoppingItemAttributes.AsNoTracking()
            .Where(a => a.ShoppingItemId == itemId).ToListAsync(cancellationToken);
        response.SetSuccess(MapToDto(item, VisibleAttributes(savedAttrs, callerRole)));
        return response;
    }

    public async Task<ShoppingItemResponse> UpdateFulfillmentAsync(Guid tenantId, Guid itemId, UpdateFulfillmentRequest request, CancellationToken cancellationToken = default)
    {
        var response = new ShoppingItemResponse();

        if (!ServiceValidationHelper.ValidateTenantContext(tenantId, _currentTenantContext, response))
            return response;
        if (!ServiceGuards.RequireRequest(request, "update shopping item fulfillment", response))
            return response;

        // Fulfillment (claim / covered / re-flag needed) is intentionally open to anyone with tenant
        // access — RequireTenantAccess on the controller is the gate; no extra role is required.
        var item = await ServiceGuards.LoadOrNotFoundAsync(
            response,
            _dbContext.ShoppingItems.Where(i => i.TenantId == tenantId && i.Id == itemId),
            EntityDisplayName,
            cancellationToken);

        if (item is null) return response;

        if (request.ClaimedByMemberId is Guid memberId)
        {
            var memberExists = await _dbContext.HouseholdMembers.AsNoTracking()
                .AnyAsync(m => m.TenantId == tenantId && m.Id == memberId, cancellationToken);
            if (!memberExists)
            {
                response.AddResponseMessage(MessageType.ERROR, "The specified member does not exist.");
                return response;
            }
        }

        item.Status = request.Status;
        item.QuantityProvided = request.QuantityProvided;
        item.ClaimedByMemberId = request.ClaimedByMemberId;

        await _dbContext.SaveChangesAsync(cancellationToken);

        response.SetSuccess(MapToDto(item, []));
        return response;
    }

    public async Task<ShoppingItemResponse> DeleteAsync(Guid tenantId, Guid itemId, CancellationToken cancellationToken = default)
    {
        var response = new ShoppingItemResponse();

        if (!ServiceValidationHelper.ValidateTenantContext(tenantId, _currentTenantContext, response))
            return response;

        var item = await ServiceGuards.LoadOrNotFoundAsync(
            response,
            _dbContext.ShoppingItems.Where(i => i.TenantId == tenantId && i.Id == itemId),
            EntityDisplayName,
            cancellationToken);

        if (item is null) return response;

        if (!await AuthorizeStructuralAsync(response, item, tenantId, cancellationToken))
            return response;

        if (item.IsDeleted)
        {
            response.AddResponseMessage(MessageType.WARNING, $"{EntityDisplayName} already deleted.");
            return response;
        }

        item.IsDeleted = true;

        var childAttrs = await _dbContext.ShoppingItemAttributes
            .Where(a => a.ShoppingItemId == itemId).ToListAsync(cancellationToken);
        foreach (var attr in childAttrs)
            attr.IsDeleted = true;

        await _dbContext.SaveChangesAsync(cancellationToken);

        GathersteadMetrics.RecordSoftDelete("ShoppingItem", tenantId);
        response.SetSuccess(MapToDto(item, []));
        return response;
    }

    /// <summary>Authorizes a structural write against an existing item, branching on its origin scope.</summary>
    private async Task<bool> AuthorizeStructuralAsync<T>(BaseEntityResponse<T> response, ShoppingItem item, Guid tenantId, CancellationToken ct)
        => item.Origin switch
        {
            ShoppingItemOrigin.Meal when item.MealPlanId is Guid planId
                => await ServiceGuards.AuthorizeMealPlanMenuAsync(response, _memberAuthorizationService, tenantId, planId, ct),
            ShoppingItemOrigin.Event
                => await ServiceGuards.AuthorizeEventManageAsync(response, _memberAuthorizationService, tenantId, ct),
            _ => await ServiceGuards.AuthorizeTenantManageAsync(response, _memberAuthorizationService, tenantId, ct),
        };

    private Task SyncAttributesAsync(Guid itemId, Guid tenantId, IReadOnlyList<AttributeWriteEntry> attributes, TenantRole? callerRole, CancellationToken ct)
        => AttributeSyncHelper.SyncAsync(
            _dbContext.ShoppingItemAttributes.Where(a => a.ShoppingItemId == itemId),
            _dbContext.ShoppingItemAttributes,
            attributes,
            a => callerRole.HasValue && callerRole.Value <= (TenantRole)a.TenantMinRole,
            tenantId,
            () => new ShoppingItemAttribute { TenantId = tenantId, ShoppingItemId = itemId },
            applyExtra: null,
            ct);

    private static List<AttributeDto> VisibleAttributes(IEnumerable<ShoppingItemAttribute> attrs, TenantRole? callerRole)
        => attrs
            .Where(a => callerRole.HasValue && callerRole.Value <= (TenantRole)a.TenantMinRole)
            .OrderBy(a => a.Key)
            .Select(a => new AttributeDto(a.Id, a.Key, a.Value, a.TenantMinRole))
            .ToList();

    private ShoppingItemDto MapToDto(ShoppingItem i, IReadOnlyList<AttributeDto> attributes) => new(
        i.Id,
        i.TenantId,
        i.Origin,
        i.PropertyId,
        i.EventId,
        i.MealPlanId,
        i.Name,
        i.QuantityNeeded,
        i.Unit,
        i.QuantityProvided,
        i.Status,
        i.ClaimedByMemberId,
        i.NeededByDate,
        i.Category,
        i.Notes,
        attributes,
        i.ToAuditInfo(_auditVisibility.IncludeAudit));
}
