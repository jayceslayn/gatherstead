using Gatherstead.Api.Contracts.Attributes;
using Gatherstead.Api.Contracts.Responses;

namespace Gatherstead.Api.Contracts.Equipment;

public class EquipmentResponse : BaseEntityResponse<EquipmentDto>
{
}

public record EquipmentDto(
    Guid Id,
    Guid TenantId,
    Guid? PropertyId,
    string Name,
    string? Notes,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    bool IsDeleted,
    DateTimeOffset? DeletedAt,
    Guid? DeletedByUserId,
    IReadOnlyList<AttributeDto> Attributes);
