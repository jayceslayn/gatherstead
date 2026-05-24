using Gatherstead.Api.Contracts.AccommodationAttributes;
using Gatherstead.Api.Services.Attributes;

namespace Gatherstead.Api.Services.AccommodationAttributes;

public interface IAccommodationAttributeService
    : IParentScopedAttributeService<AccommodationAttributeDto, CreateAccommodationAttributeRequest, UpdateAccommodationAttributeRequest>
{
}
