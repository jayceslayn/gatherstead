using Gatherstead.Api.Contracts.Responses;
using Gatherstead.Api.Services.Authorization;
using Gatherstead.Data;
using Microsoft.EntityFrameworkCore;

namespace Gatherstead.Api.Services.Validation;

/// <summary>
/// Composable, async-aware guard helpers for service methods. Each guard attaches an error
/// message to the response on failure and returns a bool (or nullable entity) so callers can
/// short-circuit with <c>if (!await Guard…) return response;</c>.
/// </summary>
public static class ServiceGuards
{
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
