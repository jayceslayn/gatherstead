using Gatherstead.Api.Contracts.Responses;
using Gatherstead.Data.Entities;

namespace Gatherstead.Api.Contracts.TenantUsers;

public record TenantUserDto(
    Guid UserId,
    Guid TenantId,
    TenantRole Role,
    Guid? LinkedMemberId,
    string ExternalId);

public record TenantUserMeDto(
    Guid UserId,
    Guid TenantId,
    TenantRole Role,
    Guid? LinkedMemberId,
    Guid? LinkedHouseholdId,
    string ExternalId);

public class TenantUserResponse : BaseEntityResponse<TenantUserDto> { }

public record UpdateTenantUserRoleRequest(TenantRole Role);

public class SetLinkedMemberRequest
{
    /// <summary>Null to unlink; a MemberId to link this user's TenantUser to that HouseholdMember.</summary>
    public Guid? MemberId { get; init; }
}
