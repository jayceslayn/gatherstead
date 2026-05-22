using Gatherstead.Api.Contracts.Responses;
using Gatherstead.Api.Services.Authorization;
using Gatherstead.Data;
using Gatherstead.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Gatherstead.Api.Services.Validation;

/// <summary>
/// Composable, async-aware guard helpers for service methods. Each guard attaches an error
/// message to the response on failure and returns a bool (or nullable entity) so callers can
/// short-circuit with <c>if (!await Guard…) return response;</c>.
/// </summary>
public static class ServiceGuards
{
    public static async Task<bool> AuthorizeSensitiveReadAsync<T>(
        BaseEntityResponse<T> response,
        IMemberAuthorizationService authorizationService,
        Guid tenantId,
        Guid householdId,
        CancellationToken cancellationToken)
    {
        var scope = await authorizationService.GetSensitiveReadScopeAsync(tenantId, cancellationToken);
        if (!scope.CanReadSensitive(householdId))
        {
            response.AddResponseMessage(MessageType.ERROR, "You do not have permission to read sensitive details for this household.");
            return false;
        }
        return true;
    }

    public static async Task<bool> AuthorizeGlobalSensitiveReadAsync<T>(
        BaseEntityResponse<T> response,
        IMemberAuthorizationService authorizationService,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var scope = await authorizationService.GetSensitiveReadScopeAsync(tenantId, cancellationToken);
        if (!scope.IsGlobal)
        {
            response.AddResponseMessage(MessageType.ERROR, "You do not have permission to read sensitive details across this tenant.");
            return false;
        }
        return true;
    }

    /// <summary>
    /// Validates that the acting user's own TenantRole is at least as privileged as the role
    /// they are trying to grant. Prevents privilege escalation in role-assignment operations.
    /// A user may only grant roles at or below their own level (lower numeric value = higher privilege).
    /// </summary>
    public static bool RequireNonEscalatingRole<T>(
        BaseEntityResponse<T> response,
        TenantRole actorRole,
        TenantRole roleBeingGranted)
    {
        if (roleBeingGranted < actorRole)
        {
            response.AddResponseMessage(MessageType.ERROR, "You cannot grant a role more privileged than your own.");
            return false;
        }
        return true;
    }

    public static bool RequireRequest<TRequest, T>(
        TRequest? request,
        string operationDescription,
        BaseEntityResponse<T> response)
        where TRequest : class
    {
        if (request is null)
        {
            response.AddResponseMessage(MessageType.ERROR, $"A {operationDescription} request is required.");
            return false;
        }
        return true;
    }

    public static async Task<bool> AuthorizeMemberEditAsync<T>(
        BaseEntityResponse<T> response,
        IMemberAuthorizationService authorizationService,
        Guid tenantId,
        Guid householdId,
        Guid memberId,
        CancellationToken cancellationToken)
    {
        if (!await authorizationService.CanEditMemberAsync(tenantId, householdId, memberId, cancellationToken))
        {
            response.AddResponseMessage(MessageType.ERROR, "You do not have permission to edit this member.");
            return false;
        }
        return true;
    }

    public static async Task<bool> AuthorizeTenantManageAsync<T>(
        BaseEntityResponse<T> response,
        IMemberAuthorizationService authorizationService,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        if (!await authorizationService.CanManageTenantAsync(tenantId, cancellationToken))
        {
            response.AddResponseMessage(MessageType.ERROR, "You do not have permission to manage this tenant's resources.");
            return false;
        }
        return true;
    }

    public static async Task<bool> AuthorizeEventManageAsync<T>(
        BaseEntityResponse<T> response,
        IMemberAuthorizationService authorizationService,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        if (!await authorizationService.CanManageEventAsync(tenantId, cancellationToken))
        {
            response.AddResponseMessage(MessageType.ERROR, "You do not have permission to manage events for this tenant.");
            return false;
        }
        return true;
    }

    public static async Task<bool> AuthorizeIntentAssignAsync<T>(
        BaseEntityResponse<T> response,
        IMemberAuthorizationService authorizationService,
        Guid tenantId,
        Guid householdId,
        Guid memberId,
        CancellationToken cancellationToken)
    {
        if (!await authorizationService.CanAssignIntentForMemberAsync(tenantId, householdId, memberId, cancellationToken))
        {
            response.AddResponseMessage(MessageType.ERROR, "You do not have permission to assign intents for this member.");
            return false;
        }
        return true;
    }

    public static async Task<bool> AuthorizeHouseholdManageAsync<T>(
        BaseEntityResponse<T> response,
        IMemberAuthorizationService authorizationService,
        Guid tenantId,
        Guid householdId,
        string deniedMessage,
        CancellationToken cancellationToken)
    {
        if (!await authorizationService.CanManageHouseholdAsync(tenantId, householdId, cancellationToken))
        {
            response.AddResponseMessage(MessageType.ERROR, deniedMessage);
            return false;
        }
        return true;
    }

    public static async Task<bool> RequireMemberExistsAsync<T>(
        BaseEntityResponse<T> response,
        GathersteadDbContext dbContext,
        Guid tenantId,
        Guid householdId,
        Guid memberId,
        CancellationToken cancellationToken)
    {
        var exists = await dbContext.HouseholdMembers
            .AsNoTracking()
            .AnyAsync(m => m.TenantId == tenantId && m.HouseholdId == householdId && m.Id == memberId, cancellationToken);

        if (!exists)
        {
            response.AddResponseMessage(MessageType.ERROR, "Household member not found.");
            return false;
        }
        return true;
    }

    /// <summary>
    /// Materializes a single entity from the supplied query. Emits a "{entityDisplayName} not found."
    /// error on the response and returns null when no row matches. Callers should short-circuit on null.
    /// </summary>
    public static async Task<TEntity?> LoadOrNotFoundAsync<TEntity, T>(
        BaseEntityResponse<T> response,
        IQueryable<TEntity> query,
        string entityDisplayName,
        CancellationToken cancellationToken)
        where TEntity : class
    {
        var entity = await query.SingleOrDefaultAsync(cancellationToken);
        if (entity is null)
        {
            response.AddResponseMessage(MessageType.ERROR, $"{entityDisplayName} not found.");
        }
        return entity;
    }
}
