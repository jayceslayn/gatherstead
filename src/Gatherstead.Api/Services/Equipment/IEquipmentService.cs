using Gatherstead.Api.Contracts.Equipment;
using Gatherstead.Api.Contracts.Responses;

namespace Gatherstead.Api.Services.Equipment;

public interface IEquipmentService
{
    Task<BaseEntityResponse<IReadOnlyCollection<EquipmentDto>>> ListAsync(Guid tenantId, IEnumerable<Guid>? ids = null, CancellationToken cancellationToken = default);
    Task<EquipmentResponse> GetAsync(Guid tenantId, Guid equipmentId, CancellationToken cancellationToken = default);
    Task<EquipmentResponse> CreateAsync(Guid tenantId, CreateEquipmentRequest request, CancellationToken cancellationToken = default);
    Task<EquipmentResponse> UpdateAsync(Guid tenantId, Guid equipmentId, UpdateEquipmentRequest request, CancellationToken cancellationToken = default);
    Task<EquipmentResponse> DeleteAsync(Guid tenantId, Guid equipmentId, CancellationToken cancellationToken = default);
}
