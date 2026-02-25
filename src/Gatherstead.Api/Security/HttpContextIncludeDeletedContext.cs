using Gatherstead.Data;

namespace Gatherstead.Api.Security;

public class HttpContextIncludeDeletedContext : IIncludeDeletedContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpContextIncludeDeletedContext(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
    }

    public bool IncludeDeleted
    {
        get
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext?.Items["IncludeDeletedAuthorized"] is true)
            {
                return true;
            }

            return false;
        }
    }
}
