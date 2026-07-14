using Gatherstead.Api.Contracts.Responses;
using Gatherstead.Api.Contracts.ShoppingItems;
using Gatherstead.Api.Security;
using Gatherstead.Api.Services.ShoppingItems;
using Gatherstead.Api.Services.Validation;
using Gatherstead.Data.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Gatherstead.Api.Controllers;

[ApiController]
[Authorize]
[RequireTenantAccess]
[Route("api/tenants/{tenantId:guid}/shopping-items")]
public class ShoppingItemsController : ControllerBase
{
    private readonly IShoppingItemService _shoppingItemService;

    public ShoppingItemsController(IShoppingItemService shoppingItemService)
    {
        _shoppingItemService = shoppingItemService ?? throw new ArgumentNullException(nameof(shoppingItemService));
    }

    [HttpGet]
    public async Task<ActionResult<BaseEntityResponse<IReadOnlyCollection<ShoppingItemDto>>>> GetShoppingItems(
        Guid tenantId,
        [FromQuery] Guid? eventId,
        [FromQuery] Guid? propertyId,
        [FromQuery] Guid? mealPlanId,
        [FromQuery] ShoppingItemStatus? status,
        [FromQuery] Guid? claimedByMemberId,
        CancellationToken cancellationToken)
    {
        var response = await _shoppingItemService.ListAsync(tenantId, eventId, propertyId, mealPlanId, status, claimedByMemberId, cancellationToken);

        if (ServiceValidationHelper.HasErrors(response))
            return this.ToErrorResult(response);

        return Ok(response);
    }

    [HttpGet("{itemId:guid}")]
    public async Task<ActionResult<ShoppingItemResponse>> GetShoppingItem(Guid tenantId, Guid itemId, CancellationToken cancellationToken)
    {
        var response = await _shoppingItemService.GetAsync(tenantId, itemId, cancellationToken);

        if (ServiceValidationHelper.HasErrors(response))
            return this.ToErrorResult(response);

        if (response.Entity is null)
            return NotFound(response);

        return Ok(response);
    }

    [HttpPost]
    public async Task<ActionResult<ShoppingItemResponse>> CreateShoppingItem(Guid tenantId, [FromBody] CreateShoppingItemRequest request, CancellationToken cancellationToken)
    {
        var response = await _shoppingItemService.CreateAsync(tenantId, request, cancellationToken);

        if (ServiceValidationHelper.HasErrors(response))
            return this.ToErrorResult(response);

        return CreatedAtAction(
            nameof(GetShoppingItem),
            new { tenantId, itemId = response.Entity?.Id },
            response);
    }

    [HttpPut("{itemId:guid}")]
    public async Task<ActionResult<ShoppingItemResponse>> UpdateShoppingItem(Guid tenantId, Guid itemId, [FromBody] UpdateShoppingItemRequest request, CancellationToken cancellationToken)
    {
        var response = await _shoppingItemService.UpdateAsync(tenantId, itemId, request, cancellationToken);

        if (ServiceValidationHelper.HasErrors(response))
            return this.ToErrorResult(response);

        if (response.Entity is null)
            return NotFound(response);

        return Ok(response);
    }

    [HttpPut("{itemId:guid}/intents/{memberId:guid}")]
    public async Task<ActionResult<ShoppingItemResponse>> UpsertIntent(Guid tenantId, Guid itemId, Guid memberId, [FromBody] UpsertShoppingItemIntentRequest request, CancellationToken cancellationToken)
    {
        var response = await _shoppingItemService.UpsertIntentAsync(tenantId, itemId, memberId, request, cancellationToken);

        if (ServiceValidationHelper.HasErrors(response))
            return this.ToErrorResult(response);

        if (response.Entity is null)
            return NotFound(response);

        return Ok(response);
    }

    [HttpDelete("{itemId:guid}/intents/{memberId:guid}")]
    public async Task<ActionResult<ShoppingItemResponse>> RemoveIntent(Guid tenantId, Guid itemId, Guid memberId, CancellationToken cancellationToken)
    {
        var response = await _shoppingItemService.RemoveIntentAsync(tenantId, itemId, memberId, cancellationToken);

        if (ServiceValidationHelper.HasErrors(response))
            return this.ToErrorResult(response);

        if (response.Entity is null)
            return NotFound(response);

        return Ok(response);
    }

    [HttpDelete("{itemId:guid}")]
    public async Task<ActionResult<ShoppingItemResponse>> DeleteShoppingItem(Guid tenantId, Guid itemId, CancellationToken cancellationToken)
    {
        var response = await _shoppingItemService.DeleteAsync(tenantId, itemId, cancellationToken);

        if (ServiceValidationHelper.HasErrors(response))
            return this.ToErrorResult(response);

        if (response.Entity is null)
            return NotFound(response);

        return Ok(response);
    }
}
