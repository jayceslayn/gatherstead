using Gatherstead.Api.Services.Observability;
using Gatherstead.Data;
using Gatherstead.Data.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Gatherstead.Api.Security;

/// <summary>
/// Authorization filter that restricts an endpoint to App Admins only.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public class RequireAppAdminAttribute : Attribute, IAsyncAuthorizationFilter
{
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var currentUserContext = context.HttpContext.RequestServices
            .GetRequiredService<ICurrentUserContext>();

        if (!currentUserContext.UserId.HasValue)
        {
            context.Result = new UnauthorizedObjectResult(new
            {
                error = "Authentication required."
            });
            return;
        }

        var appAdminContext = context.HttpContext.RequestServices
            .GetRequiredService<IAppAdminContext>();

        var isAdmin = await appAdminContext.IsAppAdminAsync();
        if (isAdmin != true)
        {
            var logger = context.HttpContext.RequestServices
                .GetRequiredService<ILogger<RequireAppAdminAttribute>>();
            logger.LogWarning(
                "App Admin access denied: user {UserId} is not an App Admin",
                currentUserContext.UserId);

            var securityLogger = context.HttpContext.RequestServices.GetService<ISecurityEventLogger>();
            if (securityLogger != null)
                await securityLogger.LogAsync(
                    SecurityEventType.AuthzDenial,
                    SecurityEventSeverity.Warning,
                    resource: "AppAdmin",
                    detail: "{\"reason\":\"NotAppAdmin\"}",
                    userId: currentUserContext.UserId);

            context.Result = new ForbidResult();
        }
    }
}
