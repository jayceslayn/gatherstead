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
            response.AddResponseMessage(MessageType.ERROR, ErrorCode.PERMISSION_SENSITIVE_READ,
                "You do not have permission to read sensitive details for this household.");
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
            response.AddResponseMessage(MessageType.ERROR, ErrorCode.PERMISSION_SENSITIVE_READ,
                "You do not have permission to read sensitive details across this tenant.");
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
        TenantRole roleBeingGranted,
        string? message = null)
    {
        if (roleBeingGranted < actorRole)
        {
            response.AddResponseMessage(MessageType.ERROR, ErrorCode.PERMISSION_ROLE_ESCALATION,
                message ?? "You cannot grant a role more privileged than your own.");
            return false;
        }
        return true;
    }

    /// <summary>
    /// Validates that a member link may be granted: the member exists in the tenant, the caller can
    /// manage the member's own household, no other tenant user already claims the member, and no
    /// other pending invitation already promises it. Shared by direct linking
    /// (<c>TenantUserService.SetLinkedMemberAsync</c>) and invitation creation so the rule and its
    /// error messages cannot drift between the two paths.
    /// </summary>
    /// <param name="excludeUserId">
    /// The user being linked, when known — their own existing claim on the member is not a conflict
    /// (re-linking the same member is an idempotent no-op for the caller to handle).
    /// </param>
    /// <param name="excludeInvitationId">
    /// A pending invitation being updated in place, when applicable — its own prior claim on the
    /// member is not a conflict.
    /// </param>
    public static async Task<bool> ValidateMemberLinkAsync<T>(
        BaseEntityResponse<T> response,
        IMemberAuthorizationService authorizationService,
        GathersteadDbContext dbContext,
        Guid tenantId,
        Guid memberId,
        Guid? excludeUserId,
        Guid? excludeInvitationId,
        CancellationToken cancellationToken)
    {
        var memberHouseholdId = await dbContext.HouseholdMembers
            .AsNoTracking()
            .Where(m => m.TenantId == tenantId && m.Id == memberId)
            .Select(m => (Guid?)m.HouseholdId)
            .SingleOrDefaultAsync(cancellationToken);

        if (memberHouseholdId is null)
        {
            response.AddResponseMessage(MessageType.ERROR, "Household member not found.");
            return false;
        }

        // The caller must be able to manage the member's own household to link a user to it.
        if (!await authorizationService.CanManageHouseholdAsync(tenantId, memberHouseholdId.Value, cancellationToken))
        {
            response.AddResponseMessage(MessageType.ERROR, "You do not have permission to link a user to this member.");
            return false;
        }

        var alreadyClaimed = await dbContext.TenantUsers
            .AsNoTracking()
            .AnyAsync(tu => tu.TenantId == tenantId && tu.LinkedMemberId == memberId
                && (excludeUserId == null || tu.UserId != excludeUserId), cancellationToken);

        if (alreadyClaimed)
        {
            response.AddResponseMessage(MessageType.ERROR, "The specified member is already linked to another user in this tenant.");
            return false;
        }

        // A pending invitation is an outstanding promise of the link; without this check two
        // invites (or an invite plus a direct link) could claim the same member and the loser
        // would be silently dropped at accept time.
        var pendingClaim = await dbContext.Invitations
            .AsNoTracking()
            .AnyAsync(i => i.TenantId == tenantId && i.Status == InvitationStatus.Pending
                && i.LinkedMemberId == memberId
                && (excludeInvitationId == null || i.Id != excludeInvitationId), cancellationToken);

        if (pendingClaim)
        {
            response.AddResponseMessage(MessageType.ERROR, "A pending invitation already links this member to an invitee.");
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
            response.AddResponseMessage(MessageType.ERROR, ErrorCode.VALIDATION_REQUIRED,
                $"A {operationDescription} request is required.",
                new Dictionary<string, string> { ["field"] = $"{operationDescription} request" });
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
            response.AddResponseMessage(MessageType.ERROR, ErrorCode.PERMISSION_MEMBER_EDIT,
                "You do not have permission to edit this member.");
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
            response.AddResponseMessage(MessageType.ERROR, ErrorCode.PERMISSION_TENANT_MANAGE,
                "You do not have permission to manage this tenant's resources.");
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
            response.AddResponseMessage(MessageType.ERROR, ErrorCode.PERMISSION_EVENT_MANAGE,
                "You do not have permission to manage events for this tenant.");
            return false;
        }
        return true;
    }

    public static async Task<bool> AuthorizeMealPlanMenuAsync<T>(
        BaseEntityResponse<T> response,
        IMemberAuthorizationService authorizationService,
        Guid tenantId,
        Guid mealPlanId,
        CancellationToken cancellationToken)
    {
        if (!await authorizationService.CanEditMealPlanMenuAsync(tenantId, mealPlanId, cancellationToken))
        {
            response.AddResponseMessage(MessageType.ERROR, ErrorCode.PERMISSION_MEALPLAN_MENU,
                "You do not have permission to edit this meal's menu.");
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
            response.AddResponseMessage(MessageType.ERROR, ErrorCode.PERMISSION_INTENT_ASSIGN,
                "You do not have permission to assign intents for this member.");
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
            response.AddResponseMessage(MessageType.ERROR, ErrorCode.PERMISSION_HOUSEHOLD_MANAGE, deniedMessage);
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
            response.AddResponseMessage(MessageType.ERROR, ErrorCode.ENTITY_NOT_FOUND,
                "Household member not found.",
                new Dictionary<string, string> { ["entity"] = "householdMember" });
            return false;
        }
        return true;
    }

    /// <summary>
    /// Resolves a member by (tenantId, memberId) — independent of any client-supplied
    /// householdId — then authorizes intent assignment against the member's ACTUAL household,
    /// returning the classified <see cref="IntentSource"/>. A member belongs to exactly one
    /// household, so deriving it here removes the fragility of trusting a client householdId that
    /// can be stale or mismatched (which previously surfaced as a spurious "Household member not found.").
    /// Attaches the not-found error when the member does not exist, or the standard permission
    /// error when authorization fails, and returns null in both cases so callers short-circuit.
    /// </summary>
    public static async Task<IntentSource?> ResolveMemberForIntentAsync<T>(
        BaseEntityResponse<T> response,
        IMemberAuthorizationService authorizationService,
        GathersteadDbContext dbContext,
        Guid tenantId,
        Guid memberId,
        CancellationToken cancellationToken)
    {
        var householdId = await dbContext.HouseholdMembers
            .AsNoTracking()
            .Where(m => m.TenantId == tenantId && m.Id == memberId)
            .Select(m => (Guid?)m.HouseholdId)
            .SingleOrDefaultAsync(cancellationToken);

        if (householdId is null)
        {
            response.AddResponseMessage(MessageType.ERROR, ErrorCode.ENTITY_NOT_FOUND,
                "Household member not found.",
                new Dictionary<string, string> { ["entity"] = "householdMember" });
            return null;
        }

        var source = await authorizationService.ClassifyIntentActorAsync(tenantId, householdId.Value, memberId, cancellationToken);
        if (source is null)
        {
            response.AddResponseMessage(MessageType.ERROR, ErrorCode.PERMISSION_INTENT_ASSIGN,
                "You do not have permission to assign intents for this member.");
            return null;
        }

        return source;
    }

    /// <summary>
    /// Bulk counterpart to <see cref="ResolveMemberForIntentAsync{T}"/>: resolves and authorizes a
    /// set of members in one members query plus one authorization check per distinct member. Returns
    /// a per-member outcome — <see cref="MemberIntentOutcome.Error"/> null with a non-null
    /// <see cref="MemberIntentOutcome.Source"/> means authorized; otherwise the error message to report
    /// against each item that references that member. Does not touch a response — callers record per-item errors.
    /// </summary>
    public static async Task<Dictionary<Guid, MemberIntentOutcome>> ResolveMembersForIntentAsync(
        IMemberAuthorizationService authorizationService,
        GathersteadDbContext dbContext,
        Guid tenantId,
        IReadOnlyCollection<Guid> memberIds,
        CancellationToken cancellationToken)
    {
        var outcomes = new Dictionary<Guid, MemberIntentOutcome>();
        if (memberIds.Count == 0) return outcomes;

        var distinct = memberIds.Distinct().ToList();
        var households = await dbContext.HouseholdMembers
            .AsNoTracking()
            .Where(m => m.TenantId == tenantId && distinct.Contains(m.Id))
            .Select(m => new { m.Id, m.HouseholdId })
            .ToDictionaryAsync(m => m.Id, m => m.HouseholdId, cancellationToken);

        foreach (var memberId in distinct)
        {
            if (!households.TryGetValue(memberId, out var householdId))
            {
                outcomes[memberId] = new MemberIntentOutcome("Household member not found.", null);
                continue;
            }

            var source = await authorizationService.ClassifyIntentActorAsync(tenantId, householdId, memberId, cancellationToken);
            outcomes[memberId] = source is null
                ? new MemberIntentOutcome("You do not have permission to assign intents for this member.", null)
                : new MemberIntentOutcome(null, source.Value);
        }

        return outcomes;
    }

    /// <summary>Per-member bulk outcome: an <paramref name="Error"/> to report, or a resolved <paramref name="Source"/>.</summary>
    public sealed record MemberIntentOutcome(string? Error, IntentSource? Source);

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
            response.AddResponseMessage(MessageType.ERROR, ErrorCode.ENTITY_NOT_FOUND,
                $"{entityDisplayName} not found.",
                new Dictionary<string, string> { ["entity"] = entityDisplayName });
        }
        return entity;
    }
}
