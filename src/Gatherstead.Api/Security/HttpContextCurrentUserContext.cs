using Gatherstead.Data;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Gatherstead.Api.Security;

public class HttpContextCurrentUserContext : ICurrentUserContext
{
    private const string CacheKey = "CurrentUser_UserId";

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IServiceProvider _serviceProvider;

    public HttpContextCurrentUserContext(
        IHttpContextAccessor httpContextAccessor,
        IServiceProvider serviceProvider)
    {
        _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    }

    public Guid? UserId
    {
        get
        {
            var httpContext = _httpContextAccessor.HttpContext;
            var user = httpContext?.User;
            if (user?.Identity?.IsAuthenticated != true)
            {
                return null;
            }

            // Check per-request cache
            if (httpContext!.Items.TryGetValue(CacheKey, out var cached))
            {
                return (Guid?)cached;
            }

            var externalId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? user.FindFirst("sub")?.Value;

            if (string.IsNullOrEmpty(externalId))
            {
                throw new InvalidOperationException("Authenticated user is missing a required identifier claim.");
            }

            // Resolve DbContext lazily to break the circular dependency:
            // HttpContextCurrentUserContext → GathersteadDbContext → AuditingSaveChangesInterceptor → ICurrentUserContext
            var dbContext = _serviceProvider.GetRequiredService<GathersteadDbContext>();

            // Look up internal user ID by external identity provider ID
            var userId = dbContext.Users
                .AsNoTracking()
                .Where(u => u.ExternalId == externalId)
                .Select(u => (Guid?)u.Id)
                .FirstOrDefault();

            httpContext.Items[CacheKey] = userId;
            return userId;
        }
    }
}
