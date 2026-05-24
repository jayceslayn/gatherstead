using Gatherstead.Api.Contracts.EventAttributes;
using Gatherstead.Api.Services.Attributes;

namespace Gatherstead.Api.Services.EventAttributes;

public interface IEventAttributeService
    : IParentScopedAttributeService<EventAttributeDto, CreateEventAttributeRequest, UpdateEventAttributeRequest>
{
}
