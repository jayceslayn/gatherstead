using Gatherstead.Api.Contracts.HouseholdAttributes;
using Gatherstead.Api.Services.Attributes;

namespace Gatherstead.Api.Services.HouseholdAttributes;

public interface IHouseholdAttributeService
    : IParentScopedAttributeService<HouseholdAttributeDto, CreateHouseholdAttributeRequest, UpdateHouseholdAttributeRequest>
{
}
