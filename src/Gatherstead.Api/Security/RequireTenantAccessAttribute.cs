using Gatherstead.Data;
using Gatherstead.Data.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;

namespace Gatherstead.Api.Security;

/// <summary>
/// Authorization filter that verifies the authenticated user has access to the requested tenant.
/// Optionally enforces a minimum required role within the tenant.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public class RequireTenantAccessAttribute : Attribute, IAsyncAuthorizationFilter
{
    /// <summary>
    /// Gets or sets the minimum role required to access the tenant.
    /// If null, any tenant membership is sufficient.
    /// </summary>
    public TenantRole? MinimumRole { get; set; }

    public RequireTenantAccessAttribute()
    {
    }

    public RequireTenantAccessAttribute(TenantRole minimumRole)
    {
        MinimumRole = minimumRole;
    }

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        // Get services from DI
        var currentUserContext = context.HttpContext.RequestServices.GetRequiredService<ICurrentUserContext>();
        var dbContext = context.HttpContext.RequestServices.GetRequiredService<GathersteadDbContext>();

        // Extract tenantId from route
        var tenantIdValue = context.HttpContext.GetRouteValue("tenantId");
        if (tenantIdValue is null)
        {
            // No tenantId in route - this filter doesn't apply
            // (e.g., /api/tenants endpoint doesn't have {tenantId} parameter)
            return;
        }

        if (!Guid.TryParse(tenantIdValue.ToString(), out var tenantId))
        {
            context.Result = new BadRequestObjectResult(new
            {
                error = "Invalid tenant identifier format."
            });
            return;
        }

        // Get current user
        var userId = currentUserContext.UserId;
        if (!userId.HasValue)
        {
            context.Result = new UnauthorizedObjectResult(new
            {
                error = "Authentication required."
            });
            return;
        }

        // App Admins bypass all tenant membership and role checks
        var appAdminContext = context.HttpContext.RequestServices.GetRequiredService<IAppAdminContext>();
        var isAppAdmin = await appAdminContext.IsAppAdminAsync();
        if (isAppAdmin == true)
        {
            if (string.Equals(context.HttpContext.Request.Query["includeDeleted"], "true", StringComparison.OrdinalIgnoreCase))
            {
                context.HttpContext.Items["IncludeDeletedAuthorized"] = true;
            }
            return;
        }

        // Query tenant membership
        var tenantUser = await dbContext.TenantUsers
            .AsNoTracking()
            .FirstOrDefaultAsync(tu => tu.TenantId == tenantId && tu.UserId == userId.Value);

        if (tenantUser is null)
        {
            context.Result = new ForbidResult();
            return;
        }

        // Check minimum role if specified
        if (MinimumRole.HasValue && !HasRequiredRole(tenantUser.Role, MinimumRole.Value))
        {
            context.Result = new ForbidResult();
            return;
        }

        // If includeDeleted=true is requested and user has Manager+ role, authorize it
        if (string.Equals(context.HttpContext.Request.Query["includeDeleted"], "true", StringComparison.OrdinalIgnoreCase)
            && HasRequiredRole(tenantUser.Role, TenantRole.Manager))
        {
            context.HttpContext.Items["IncludeDeletedAuthorized"] = true;
        }
    }

    /// <summary>
    /// Determines if the user's role meets the minimum required role.
    /// Role hierarchy: Owner > Manager > Member > Guest
    /// </summary>
    private static bool HasRequiredRole(TenantRole userRole, TenantRole requiredRole)
    {
        // Lower numeric value = higher privilege
        // Owner = 0, Manager = 1, Member = 2, Guest = 3
        return userRole <= requiredRole;
    }
}
