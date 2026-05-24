using Gatherstead.Api.Contracts.Attributes;
using Gatherstead.Api.Contracts.Responses;

namespace Gatherstead.Api.Services.Attributes;

public interface IParentScopedAttributeService<TDto, TCreate, TUpdate>
    where TDto : IAttributeDto
{
    Task<BaseEntityResponse<IReadOnlyCollection<TDto>>> ListAsync(
        Guid tenantId,
        Guid parentId,
        IEnumerable<Guid>? ids = null,
        CancellationToken cancellationToken = default);

    Task<BaseEntityResponse<TDto>> GetAsync(
        Guid tenantId,
        Guid parentId,
        Guid attributeId,
        CancellationToken cancellationToken = default);

    Task<BaseEntityResponse<TDto>> CreateAsync(
        Guid tenantId,
        Guid parentId,
        TCreate request,
        CancellationToken cancellationToken = default);

    Task<BaseEntityResponse<TDto>> UpdateAsync(
        Guid tenantId,
        Guid parentId,
        Guid attributeId,
        TUpdate request,
        CancellationToken cancellationToken = default);

    Task<BaseEntityResponse<TDto>> DeleteAsync(
        Guid tenantId,
        Guid parentId,
        Guid attributeId,
        CancellationToken cancellationToken = default);
}
