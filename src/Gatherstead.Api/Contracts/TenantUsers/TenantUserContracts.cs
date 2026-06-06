using System.ComponentModel.DataAnnotations;
using Gatherstead.Api.Contracts.Responses;
using Gatherstead.Data.Entities;

namespace Gatherstead.Api.Contracts.TenantUsers;

public record TenantUserDto(
    [property: Required] Guid UserId,
    [property: Required] Guid TenantId,
    [property: Required] TenantRole Role,
    Guid? LinkedMemberId,
    [property: Required] string ExternalId);

public record TenantUserMeDto(
    [property: Required] Guid UserId,
    [property: Required] Guid TenantId,
    [property: Required] TenantRole Role,
    Guid? LinkedMemberId,
    Guid? LinkedHouseholdId,
    [property: Required] string ExternalId);

public class TenantUserResponse : BaseEntityResponse<TenantUserDto> { }

public record UpdateTenantUserRoleRequest(TenantRole Role);

public class SetLinkedMemberRequest
{
    /// <summary>Null to unlink; a MemberId to link this user's TenantUser to that HouseholdMember.</summary>
    public Guid? MemberId { get; init; }
}
