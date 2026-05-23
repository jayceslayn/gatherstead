using Gatherstead.Data.Entities;

namespace Gatherstead.Api.Contracts.TenantUsers;

public record TenantUserMeDto(
    Guid UserId,
    Guid TenantId,
    TenantRole Role,
    Guid? LinkedMemberId,
    Guid? LinkedHouseholdId,
    string ExternalId);
