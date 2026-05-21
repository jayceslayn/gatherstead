using Gatherstead.Api.Contracts.TaskTemplates;
using Gatherstead.Api.Contracts.Responses;

namespace Gatherstead.Api.Services.TaskTemplates;

public interface ITaskTemplateService
{
    Task<BaseEntityResponse<IReadOnlyCollection<TaskTemplateDto>>> ListAsync(Guid tenantId, Guid eventId, IEnumerable<Guid>? ids = null, CancellationToken cancellationToken = default);
    Task<TaskTemplateResponse> GetAsync(Guid tenantId, Guid eventId, Guid templateId, CancellationToken cancellationToken = default);
    Task<TaskTemplateResponse> CreateAsync(Guid tenantId, Guid eventId, CreateTaskTemplateRequest request, CancellationToken cancellationToken = default);
    Task<TaskTemplateResponse> UpdateAsync(Guid tenantId, Guid eventId, Guid templateId, UpdateTaskTemplateRequest request, CancellationToken cancellationToken = default);
    Task<TaskTemplateResponse> DeleteAsync(Guid tenantId, Guid eventId, Guid templateId, CancellationToken cancellationToken = default);
}
