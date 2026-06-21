using Gatherstead.Api.Contracts.Responses;
using Gatherstead.Api.Contracts.ShoppingItems;
using Gatherstead.Data.Entities;

namespace Gatherstead.Api.Services.ShoppingItems;

public interface IShoppingItemService
{
    Task<BaseEntityResponse<IReadOnlyCollection<ShoppingItemDto>>> ListAsync(
        Guid tenantId,
        Guid? eventId,
        Guid? propertyId,
        Guid? mealPlanId,
        ShoppingItemStatus? status,
        CancellationToken cancellationToken = default);

    Task<ShoppingItemResponse> GetAsync(Guid tenantId, Guid itemId, CancellationToken cancellationToken = default);

    Task<ShoppingItemResponse> CreateAsync(Guid tenantId, CreateShoppingItemRequest request, CancellationToken cancellationToken = default);

    Task<ShoppingItemResponse> UpdateAsync(Guid tenantId, Guid itemId, UpdateShoppingItemRequest request, CancellationToken cancellationToken = default);

    Task<ShoppingItemResponse> UpdateFulfillmentAsync(Guid tenantId, Guid itemId, UpdateFulfillmentRequest request, CancellationToken cancellationToken = default);

    Task<ShoppingItemResponse> DeleteAsync(Guid tenantId, Guid itemId, CancellationToken cancellationToken = default);
}
