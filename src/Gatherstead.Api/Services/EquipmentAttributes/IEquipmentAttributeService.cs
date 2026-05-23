using Gatherstead.Api.Contracts.EquipmentAttributes;
using Gatherstead.Api.Contracts.Responses;

namespace Gatherstead.Api.Services.EquipmentAttributes;

public interface IEquipmentAttributeService
{
    Task<BaseEntityResponse<IReadOnlyCollection<EquipmentAttributeDto>>> ListAsync(
        Guid tenantId,
        Guid equipmentId,
        IEnumerable<Guid>? ids = null,
        CancellationToken cancellationToken = default);

    Task<EquipmentAttributeResponse> GetAsync(
        Guid tenantId,
        Guid equipmentId,
        Guid attributeId,
        CancellationToken cancellationToken = default);

    Task<EquipmentAttributeResponse> CreateAsync(
        Guid tenantId,
        Guid equipmentId,
        CreateEquipmentAttributeRequest request,
        CancellationToken cancellationToken = default);

    Task<EquipmentAttributeResponse> UpdateAsync(
        Guid tenantId,
        Guid equipmentId,
        Guid attributeId,
        UpdateEquipmentAttributeRequest request,
        CancellationToken cancellationToken = default);

    Task<EquipmentAttributeResponse> DeleteAsync(
        Guid tenantId,
        Guid equipmentId,
        Guid attributeId,
        CancellationToken cancellationToken = default);
}
