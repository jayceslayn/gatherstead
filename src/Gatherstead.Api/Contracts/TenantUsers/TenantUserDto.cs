using Gatherstead.Data.Entities;

namespace Gatherstead.Api.Contracts.TenantUsers;

public record TenantUserDto(
    Guid UserId,
    Guid TenantId,
    TenantRole Role,
    Guid? LinkedMemberId,
    string ExternalId);
