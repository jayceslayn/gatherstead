using Gatherstead.Api.Contracts.MealTemplates;
using Gatherstead.Api.Contracts.Responses;

namespace Gatherstead.Api.Services.MealTemplates;

public interface IMealTemplateService
{
    Task<BaseEntityResponse<IReadOnlyCollection<MealTemplateDto>>> ListAsync(Guid tenantId, Guid eventId, IEnumerable<Guid>? ids = null, CancellationToken cancellationToken = default);
    Task<MealTemplateResponse> GetAsync(Guid tenantId, Guid eventId, Guid templateId, CancellationToken cancellationToken = default);
    Task<MealTemplateResponse> CreateAsync(Guid tenantId, Guid eventId, CreateMealTemplateRequest request, CancellationToken cancellationToken = default);
    Task<MealTemplateResponse> UpdateAsync(Guid tenantId, Guid eventId, Guid templateId, UpdateMealTemplateRequest request, CancellationToken cancellationToken = default);
    Task<MealTemplateResponse> DeleteAsync(Guid tenantId, Guid eventId, Guid templateId, CancellationToken cancellationToken = default);
}
