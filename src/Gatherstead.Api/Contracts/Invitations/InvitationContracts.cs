using System.ComponentModel.DataAnnotations;
using Gatherstead.Api.Contracts.Responses;
using Gatherstead.Data.Entities;

namespace Gatherstead.Api.Contracts.Invitations;

public record InvitationDto(
    [property: Required] Guid Id,
    [property: Required] Guid TenantId,
    [property: Required] string Email,
    [property: Required] TenantRole Role,
    Guid? HouseholdId,
    HouseholdRole? HouseholdRole,
    [property: Required] InvitationStatus Status,
    [property: Required] DateTimeOffset CreatedAt,
    DateTimeOffset? AcceptedAt);

public class InvitationResponse : BaseEntityResponse<InvitationDto> { }

public class CreateInvitationRequest
{
    [Required]
    [EmailAddress]
    [StringLength(256)]
    public string Email { get; init; } = string.Empty;

    [Required]
    public TenantRole Role { get; init; }

    public Guid? HouseholdId { get; init; }
    public HouseholdRole? HouseholdRole { get; init; }
}

public record BootstrapTenantDto(Guid TenantId, TenantRole Role);

public record UserBootstrapDto(
    Guid UserId,
    int ClaimedInvitations,
    IReadOnlyList<BootstrapTenantDto> Tenants);

public class UserBootstrapResponse : BaseEntityResponse<UserBootstrapDto> { }
