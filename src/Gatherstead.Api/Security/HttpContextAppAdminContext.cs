using Gatherstead.Data;
using Microsoft.EntityFrameworkCore;

namespace Gatherstead.Api.Security;

public class HttpContextAppAdminContext : IAppAdminContext
{
    private const string CacheKey = "AppAdmin_IsAppAdmin";

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly GathersteadDbContext _dbContext;
    private readonly ICurrentUserContext _currentUserContext;

    public HttpContextAppAdminContext(
        IHttpContextAccessor httpContextAccessor,
        GathersteadDbContext dbContext,
        ICurrentUserContext currentUserContext)
    {
        _httpContextAccessor = httpContextAccessor;
        _dbContext = dbContext;
        _currentUserContext = currentUserContext;
    }

    public async Task<bool?> IsAppAdminAsync(CancellationToken ct = default)
    {
        var userId = _currentUserContext.UserId;
        if (!userId.HasValue)
            return null;

        var items = _httpContextAccessor.HttpContext?.Items;
        if (items != null && items.TryGetValue(CacheKey, out var cached))
            return (bool?)cached;

        var isAdmin = await _dbContext.Users
            .AsNoTracking()
            .Where(u => u.Id == userId.Value)
            .Select(u => (bool?)u.IsAppAdmin)
            .FirstOrDefaultAsync(ct);

        items?.TryAdd(CacheKey, isAdmin);
        return isAdmin;
    }
}
