using System.ComponentModel.DataAnnotations;
using Gatherstead.Api.Contracts.Responses;
using Gatherstead.Data.Entities;

namespace Gatherstead.Api.Contracts.Invitations;

/// <summary>A single household-access grant carried by an invitation.</summary>
/// <remarks>
/// <c>Role</c> defaults to <see cref="HouseholdRole.Member"/> so a request that omits it grants
/// least privilege. Without the default, System.Text.Json would bind a missing value to
/// <c>default(HouseholdRole)</c> = <see cref="HouseholdRole.Manager"/> (0) — a silent escalation.
/// </remarks>
public record InvitationHouseholdGrant(
    [property: Required] Guid HouseholdId,
    HouseholdRole Role = HouseholdRole.Member);

public record InvitationDto(
    [property: Required] Guid Id,
    [property: Required] Guid TenantId,
    [property: Required] string Email,
    [property: Required] TenantRole Role,
    [property: Required] IReadOnlyList<InvitationHouseholdGrant> Households,
    Guid? LinkedMemberId,
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

    // Optional household-access grants applied on accept. A user can hold a role in multiple
    // households, so several may be supplied (or none).
    public IReadOnlyList<InvitationHouseholdGrant> Households { get; init; } = [];

    // Optional: link the invitee to this HouseholdMember on accept (a self-service "Self" link).
    // Independent of household access above.
    public Guid? LinkedMemberId { get; init; }
}

public record BootstrapTenantDto(Guid TenantId, TenantRole Role);

public record UserBootstrapDto(
    Guid UserId,
    bool IsAppAdmin,
    int ClaimedInvitations,
    IReadOnlyList<BootstrapTenantDto> Tenants);

public class UserBootstrapResponse : BaseEntityResponse<UserBootstrapDto> { }

public record MeDto(
    [property: Required] Guid UserId,
    string? Email,
    string? DisplayName);

public class MeResponse : BaseEntityResponse<MeDto> { }

public class UpdateMeRequest
{
    [Required]
    [StringLength(256, MinimumLength = 1)]
    public string DisplayName { get; init; } = string.Empty;
}
