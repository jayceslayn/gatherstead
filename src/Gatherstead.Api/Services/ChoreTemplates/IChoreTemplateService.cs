using Gatherstead.Api.Contracts.ChoreTemplates;
using Gatherstead.Api.Contracts.Responses;

namespace Gatherstead.Api.Services.ChoreTemplates;

public interface IChoreTemplateService
{
    Task<BaseEntityResponse<IReadOnlyCollection<ChoreTemplateDto>>> ListAsync(Guid tenantId, Guid eventId, IEnumerable<Guid>? ids = null, CancellationToken cancellationToken = default);
    Task<ChoreTemplateResponse> GetAsync(Guid tenantId, Guid eventId, Guid templateId, CancellationToken cancellationToken = default);
    Task<ChoreTemplateResponse> CreateAsync(Guid tenantId, Guid eventId, CreateChoreTemplateRequest request, CancellationToken cancellationToken = default);
    Task<ChoreTemplateResponse> UpdateAsync(Guid tenantId, Guid eventId, Guid templateId, UpdateChoreTemplateRequest request, CancellationToken cancellationToken = default);
    Task<ChoreTemplateResponse> DeleteAsync(Guid tenantId, Guid eventId, Guid templateId, CancellationToken cancellationToken = default);
}
