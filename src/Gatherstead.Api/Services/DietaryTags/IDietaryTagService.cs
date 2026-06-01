using Gatherstead.Api.Contracts.DietaryTags;

namespace Gatherstead.Api.Services.DietaryTags;

public interface IDietaryTagService
{
    Task<IReadOnlyList<DietaryTagDto>> ListActiveAsync(CancellationToken cancellationToken = default);
}
