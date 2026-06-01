using Gatherstead.Api.Contracts.DietaryTags;
using Gatherstead.Data;
using Microsoft.EntityFrameworkCore;

namespace Gatherstead.Api.Services.DietaryTags;

public class DietaryTagService : IDietaryTagService
{
    private readonly GathersteadDbContext _dbContext;

    public DietaryTagService(GathersteadDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<IReadOnlyList<DietaryTagDto>> ListActiveAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.DietaryTags
            .AsNoTracking()
            .Where(t => t.IsActive)
            .OrderBy(t => t.Category)
            .ThenBy(t => t.SortOrder)
            .ThenBy(t => t.DisplayName)
            .Select(t => new DietaryTagDto(t.Id, t.Slug, t.DisplayName, t.Category, t.SortOrder))
            .ToListAsync(cancellationToken);
    }
}
