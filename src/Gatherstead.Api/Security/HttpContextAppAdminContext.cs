using Gatherstead.Data;
using Microsoft.EntityFrameworkCore;

namespace Gatherstead.Api.Security;

public class HttpContextAppAdminContext : IAppAdminContext
{
    private const string CacheKey = "AppAdmin_IsAppAdmin";

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly GathersteadDbContext _dbContext;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IAuthCache _authCache;

    public HttpContextAppAdminContext(
        IHttpContextAccessor httpContextAccessor,
        GathersteadDbContext dbContext,
        ICurrentUserContext currentUserContext,
        IAuthCache authCache)
    {
        _httpContextAccessor = httpContextAccessor;
        _dbContext = dbContext;
        _currentUserContext = currentUserContext;
        _authCache = authCache;
    }

    public async Task<bool?> IsAppAdminAsync(CancellationToken ct = default)
    {
        var userId = _currentUserContext.UserId;
        if (!userId.HasValue)
            return null;

        var items = _httpContextAccessor.HttpContext?.Items;
        if (items != null && items.TryGetValue(CacheKey, out var cached))
            return (bool?)cached;

        // Cross-request cached (moderate TTL): the flag changes rarely. The per-request Items cache
        // still fronts it so repeated checks within one request never re-enter the cache.
        var isAdmin = await _authCache.GetIsAppAdminAsync(
            userId.Value,
            innerCt => _dbContext.Users
                .AsNoTracking()
                .Where(u => u.Id == userId.Value)
                .Select(u => (bool?)u.IsAppAdmin)
                .FirstOrDefaultAsync(innerCt),
            ct);

        items?.TryAdd(CacheKey, isAdmin);
        return isAdmin;
    }
}
