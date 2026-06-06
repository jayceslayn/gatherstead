using System.ComponentModel.DataAnnotations;
using Gatherstead.Api.Contracts.Responses;
using Gatherstead.Data.Entities;

namespace Gatherstead.Api.Contracts.HouseholdUsers;

public record HouseholdUserDto(
    [property: Required] Guid UserId,
    [property: Required] Guid TenantId,
    [property: Required] Guid HouseholdId,
    [property: Required] HouseholdRole Role,
    [property: Required] string ExternalId);

public class HouseholdUserResponse : BaseEntityResponse<HouseholdUserDto> { }

public record UpsertHouseholdUserRequest(HouseholdRole Role);
