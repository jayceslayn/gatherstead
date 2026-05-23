using Gatherstead.Api.Contracts.Responses;

namespace Gatherstead.Api.Contracts.EquipmentAttributes;

public class EquipmentAttributeResponse : BaseEntityResponse<EquipmentAttributeDto>
{
}

public record EquipmentAttributeDto(
    Guid Id,
    Guid TenantId,
    Guid EquipmentId,
    string Key,
    string Value,
    byte TenantMinRole,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    bool IsDeleted,
    DateTimeOffset? DeletedAt,
    Guid? DeletedByUserId);
