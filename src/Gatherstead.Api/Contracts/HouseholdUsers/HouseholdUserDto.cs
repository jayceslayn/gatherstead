using Gatherstead.Data.Entities;

namespace Gatherstead.Api.Contracts.HouseholdUsers;

public record HouseholdUserDto(
    Guid UserId,
    Guid TenantId,
    Guid HouseholdId,
    HouseholdRole Role,
    string ExternalId);
