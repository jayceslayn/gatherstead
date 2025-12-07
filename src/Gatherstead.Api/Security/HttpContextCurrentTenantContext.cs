using Gatherstead.Db;
using Microsoft.AspNetCore.Routing;

namespace Gatherstead.Api.Security;

public class HttpContextCurrentTenantContext : ICurrentTenantContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpContextCurrentTenantContext(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
    }

    public Guid? TenantId
    {
        get
        {
            var httpContext = _httpContextAccessor.HttpContext;
            var tenantValue = httpContext?.GetRouteValue("tenantId")?.ToString();
            if (string.IsNullOrWhiteSpace(tenantValue))
            {
                return null;
            }

            if (!Guid.TryParse(tenantValue, out var tenantId))
            {
                throw new InvalidOperationException("Tenant identifier provided in the route is not a valid GUID.");
            }

            return tenantId;
        }
    }
}
