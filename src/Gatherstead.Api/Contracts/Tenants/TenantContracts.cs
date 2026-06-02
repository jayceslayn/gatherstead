using System.ComponentModel.DataAnnotations;
using Gatherstead.Api.Contracts.Attributes;
using Gatherstead.Api.Contracts.Responses;
using Gatherstead.Data.Entities;

namespace Gatherstead.Api.Contracts.Tenants;

public record TenantDto(
    Guid Id,
    string Name,
    string? Notes,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    bool IsDeleted,
    DateTimeOffset? DeletedAt,
    Guid? DeletedByUserId,
    IReadOnlyList<AttributeDto> Attributes);

public class TenantResponse : BaseEntityResponse<TenantDto> { }

public record TenantSummary(Guid Id, string Name, TenantRole? UserRole = null);

public class CreateTenantRequest
{
    [Required]
    [StringLength(200)]
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// The User ID that will become the tenant's Owner.
    /// </summary>
    [Required]
    public Guid OwnerUserId { get; init; }

    [StringLength(500)]
    public string? Notes { get; init; } = null;
}

public class UpdateTenantRequest
{
    private string _name = string.Empty;

    [Required]
    [StringLength(200)]
    public string Name
    {
        get => _name;
        init => _name = (value ?? string.Empty).Trim();
    }

    [StringLength(500)]
    public string? Notes { get; init; } = null;

    public IReadOnlyList<AttributeWriteEntry>? Attributes { get; init; }
}
