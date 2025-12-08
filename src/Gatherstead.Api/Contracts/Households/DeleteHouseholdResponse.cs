namespace Gatherstead.Api.Contracts.Households;

public record DeleteHouseholdResponse(
    Guid Id,
    Guid TenantId,
    bool IsDeleted,
    DateTimeOffset? DeletedAt,
    Guid? DeletedByUserId);
