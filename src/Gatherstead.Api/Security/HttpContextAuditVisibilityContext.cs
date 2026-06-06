using Gatherstead.Api.Contracts.Responses;

namespace Gatherstead.Api.Security;

public class HttpContextAuditVisibilityContext : IAuditVisibilityContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpContextAuditVisibilityContext(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
    }

    public bool IncludeAudit
        => _httpContextAccessor.HttpContext?.Items["IncludeAuditAuthorized"] is true;
}
