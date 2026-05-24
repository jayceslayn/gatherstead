using Gatherstead.Api.Contracts.TaskTemplateAttributes;
using Gatherstead.Api.Services.Attributes;

namespace Gatherstead.Api.Services.TaskTemplateAttributes;

public interface ITaskTemplateAttributeService
    : IParentScopedAttributeService<TaskTemplateAttributeDto, CreateTaskTemplateAttributeRequest, UpdateTaskTemplateAttributeRequest>
{
}
