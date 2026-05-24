using Gatherstead.Api.Contracts.EquipmentAttributes;
using Gatherstead.Api.Services.Attributes;

namespace Gatherstead.Api.Services.EquipmentAttributes;

public interface IEquipmentAttributeService
    : IParentScopedAttributeService<EquipmentAttributeDto, CreateEquipmentAttributeRequest, UpdateEquipmentAttributeRequest>
{
}
