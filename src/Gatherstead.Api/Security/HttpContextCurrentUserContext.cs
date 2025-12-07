using Gatherstead.Db;
using System.Security.Claims;

namespace Gatherstead.Api.Security;

public class HttpContextCurrentUserContext : ICurrentUserContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpContextCurrentUserContext(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
    }

    public Guid? UserId
    {
        get
        {
            var user = _httpContextAccessor.HttpContext?.User;
            if (user?.Identity?.IsAuthenticated != true)
            {
                return null;
            }

            var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier) ?? user.FindFirst("sub");
            if (userIdClaim is null)
            {
                throw new InvalidOperationException("Authenticated user is missing a required identifier claim.");
            }

            if (!Guid.TryParse(userIdClaim.Value, out var userId))
            {
                throw new InvalidOperationException("User identifier claim is not a valid GUID.");
            }

            return userId;
        }
    }
}
