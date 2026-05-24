using Gatherstead.Api.Contracts.PropertyAttributes;
using Gatherstead.Api.Services.Attributes;

namespace Gatherstead.Api.Services.PropertyAttributes;

public interface IPropertyAttributeService
    : IParentScopedAttributeService<PropertyAttributeDto, CreatePropertyAttributeRequest, UpdatePropertyAttributeRequest>
{
}
