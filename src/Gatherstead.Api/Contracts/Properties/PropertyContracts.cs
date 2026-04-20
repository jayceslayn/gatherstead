using System.ComponentModel.DataAnnotations;
using Gatherstead.Api.Contracts.Responses;

namespace Gatherstead.Api.Contracts.Properties;

public record PropertyDto(
    Guid Id,
    Guid TenantId,
    string Name,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    bool IsDeleted,
    DateTimeOffset? DeletedAt,
    Guid? DeletedByUserId);

public class PropertyResponse : BaseEntityResponse<PropertyDto> { }

public class CreatePropertyRequest
{
    [Required]
    [StringLength(200)]
    public string Name { get; init; } = string.Empty;
}

public class UpdatePropertyRequest
{
    [Required]
    [StringLength(200)]
    public string Name { get; init; } = string.Empty;
}
