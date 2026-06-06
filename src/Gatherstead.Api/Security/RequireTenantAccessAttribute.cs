using Gatherstead.Api.Observability;
using Gatherstead.Api.Services.Observability;
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
            // No tenantId in route. A MinimumRole requirement without a {tenantId} route segment
            // is a developer misconfiguration — the check would never run.
            if (MinimumRole.HasValue)
                throw new InvalidOperationException(
                    $"[RequireTenantAccess(MinimumRole={MinimumRole})] is applied to a route that has no {{tenantId}} segment. " +
                    $"The authorization check will never execute. Add {{tenantId}} to the route template or remove the minimum-role requirement.");
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
            if (IsQueryFlagSet(context.HttpContext, "includeDeleted"))
            {
                context.HttpContext.Items["IncludeDeletedAuthorized"] = true;
            }
            if (IsQueryFlagSet(context.HttpContext, "includeAudit"))
            {
                context.HttpContext.Items["IncludeAuditAuthorized"] = true;
            }
            return;
        }

        // Query tenant membership
        var tenantUser = await dbContext.TenantUsers
            .AsNoTracking()
            .FirstOrDefaultAsync(tu => tu.TenantId == tenantId && tu.UserId == userId.Value);

        if (tenantUser is null)
        {
            var logger = context.HttpContext.RequestServices
                .GetRequiredService<ILogger<RequireTenantAccessAttribute>>();
            logger.LogWarning(
                "Tenant access denied: user {UserId} is not a member of tenant {TenantId}",
                userId, tenantId);

            GathersteadMetrics.RecordAuthzDenied("NotTenantMember", tenantId);
            var securityLogger = context.HttpContext.RequestServices.GetService<ISecurityEventLogger>();
            if (securityLogger != null)
                await securityLogger.LogAsync(
                    SecurityEventType.AuthzDenial,
                    SecurityEventSeverity.Warning,
                    resource: $"Tenant:{tenantId}",
                    detail: $"{{\"reason\":\"NotTenantMember\"}}",
                    tenantId: tenantId,
                    userId: userId);

            context.Result = new ForbidResult();
            return;
        }

        // Check minimum role if specified
        if (MinimumRole.HasValue && !HasRequiredRole(tenantUser.Role, MinimumRole.Value))
        {
            var logger = context.HttpContext.RequestServices
                .GetRequiredService<ILogger<RequireTenantAccessAttribute>>();
            logger.LogWarning(
                "Tenant access denied: user {UserId} has role {UserRole} in tenant {TenantId}, required {RequiredRole}",
                userId, tenantUser.Role, tenantId, MinimumRole.Value);

            GathersteadMetrics.RecordAuthzDenied("InsufficientRole", tenantId);
            var securityLogger = context.HttpContext.RequestServices.GetService<ISecurityEventLogger>();
            if (securityLogger != null)
                await securityLogger.LogAsync(
                    SecurityEventType.AuthzDenial,
                    SecurityEventSeverity.Warning,
                    resource: $"Tenant:{tenantId}",
                    detail: $"{{\"reason\":\"InsufficientRole\",\"userRole\":\"{tenantUser.Role}\",\"requiredRole\":\"{MinimumRole.Value}\"}}",
                    tenantId: tenantId,
                    userId: userId);

            context.Result = new ForbidResult();
            return;
        }

        // includeDeleted and includeAudit are sensitive capabilities gated at Manager+.
        var isManagerPlus = HasRequiredRole(tenantUser.Role, TenantRole.Manager);
        if (isManagerPlus && IsQueryFlagSet(context.HttpContext, "includeDeleted"))
        {
            context.HttpContext.Items["IncludeDeletedAuthorized"] = true;
        }
        if (isManagerPlus && IsQueryFlagSet(context.HttpContext, "includeAudit"))
        {
            context.HttpContext.Items["IncludeAuditAuthorized"] = true;
        }
    }

    private static bool IsQueryFlagSet(HttpContext httpContext, string name)
        => string.Equals(httpContext.Request.Query[name], "true", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Determines if the user's role meets the minimum required role.
    /// Role hierarchy: Owner > Manager > Coordinator > Member > Guest
    /// </summary>
    private static bool HasRequiredRole(TenantRole userRole, TenantRole requiredRole)
    {
        // Lower numeric value = higher privilege
        // Owner = 0, Manager = 1, Coordinator = 2, Member = 3, Guest = 4
        return userRole <= requiredRole;
    }
}
