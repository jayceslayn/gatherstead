namespace Gatherstead.Api.Contracts.Households;

public record HouseholdResponse(
    Guid Id,
    Guid TenantId,
    string Name,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? DeletedAt);
