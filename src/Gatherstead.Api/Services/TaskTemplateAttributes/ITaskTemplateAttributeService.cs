using Gatherstead.Api.Contracts.TaskTemplateAttributes;
using Gatherstead.Api.Contracts.Responses;

namespace Gatherstead.Api.Services.TaskTemplateAttributes;

public interface ITaskTemplateAttributeService
{
    Task<BaseEntityResponse<IReadOnlyCollection<TaskTemplateAttributeDto>>> ListAsync(
        Guid tenantId,
        Guid taskTemplateId,
        IEnumerable<Guid>? ids = null,
        CancellationToken cancellationToken = default);

    Task<TaskTemplateAttributeResponse> GetAsync(
        Guid tenantId,
        Guid taskTemplateId,
        Guid attributeId,
        CancellationToken cancellationToken = default);

    Task<TaskTemplateAttributeResponse> CreateAsync(
        Guid tenantId,
        Guid taskTemplateId,
        CreateTaskTemplateAttributeRequest request,
        CancellationToken cancellationToken = default);

    Task<TaskTemplateAttributeResponse> UpdateAsync(
        Guid tenantId,
        Guid taskTemplateId,
        Guid attributeId,
        UpdateTaskTemplateAttributeRequest request,
        CancellationToken cancellationToken = default);

    Task<TaskTemplateAttributeResponse> DeleteAsync(
        Guid tenantId,
        Guid taskTemplateId,
        Guid attributeId,
        CancellationToken cancellationToken = default);
}
