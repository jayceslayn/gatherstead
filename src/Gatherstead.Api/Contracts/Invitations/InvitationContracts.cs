using System.ComponentModel.DataAnnotations;
using Gatherstead.Api.Contracts.Responses;
using Gatherstead.Data.Entities;

namespace Gatherstead.Api.Contracts.Invitations;

public record InvitationDto(
    Guid Id,
    Guid TenantId,
    string Email,
    TenantRole Role,
    Guid? HouseholdId,
    HouseholdRole? HouseholdRole,
    InvitationStatus Status,
    DateTimeOffset CreatedAt,
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
