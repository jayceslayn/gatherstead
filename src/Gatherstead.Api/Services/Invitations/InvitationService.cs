using Gatherstead.Api.Contracts.Invitations;
using Gatherstead.Api.Contracts.Responses;
using Gatherstead.Api.Security;
using Gatherstead.Api.Services.Authorization;
using Gatherstead.Api.Services.Membership;
using Gatherstead.Api.Services.Observability;
using Gatherstead.Api.Services.Validation;
using Gatherstead.Data;
using Gatherstead.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Gatherstead.Api.Services.Invitations;

public class InvitationService : IInvitationService
{
    private const string EntityDisplayName = "Invitation";

    private readonly GathersteadDbContext _dbContext;
    private readonly ICurrentTenantContext _currentTenantContext;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IMemberAuthorizationService _memberAuthorizationService;
    private readonly ISecurityEventLogger _securityEventLogger;
    private readonly IAuthCache _authCache;

    public InvitationService(
        GathersteadDbContext dbContext,
        ICurrentTenantContext currentTenantContext,
        ICurrentUserContext currentUserContext,
        IMemberAuthorizationService memberAuthorizationService,
        ISecurityEventLogger securityEventLogger,
        IAuthCache authCache)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _currentTenantContext = currentTenantContext ?? throw new ArgumentNullException(nameof(currentTenantContext));
        _currentUserContext = currentUserContext ?? throw new ArgumentNullException(nameof(currentUserContext));
        _memberAuthorizationService = memberAuthorizationService ?? throw new ArgumentNullException(nameof(memberAuthorizationService));
        _securityEventLogger = securityEventLogger ?? throw new ArgumentNullException(nameof(securityEventLogger));
        _authCache = authCache ?? throw new ArgumentNullException(nameof(authCache));
    }

    public async Task<InvitationResponse> CreateAsync(
        Guid tenantId,
        CreateInvitationRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = new InvitationResponse();

        if (!ServiceValidationHelper.ValidateTenantContext(tenantId, _currentTenantContext, response))
            return response;
        if (!ServiceGuards.RequireRequest(request, "create invitation", response))
            return response;
        if (!await ServiceGuards.AuthorizeTenantManageAsync(response, _memberAuthorizationService, tenantId, cancellationToken))
            return response;

        var email = (request.Email ?? string.Empty).Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(email))
        {
            response.AddResponseMessage(MessageType.ERROR, "An email address is required.");
            return response;
        }

        // A user may not grant a role more privileged than their own (App Admins resolve to null role and bypass).
        var actorRole = await _memberAuthorizationService.GetCallerTenantRoleAsync(tenantId, cancellationToken);
        if (actorRole.HasValue && !ServiceGuards.RequireNonEscalatingRole(response, actorRole.Value, request.Role))
            return response;

        // Validate each requested household grant belongs to the tenant (one set query, not one per
        // grant). Dedupe on householdId so a repeated household resolves to a single grant (last one wins).
        var householdGrants = request.Households
            .GroupBy(h => h.HouseholdId)
            .Select(g => g.Last())
            .ToList();
        if (householdGrants.Count > 0)
        {
            var requestedHouseholdIds = householdGrants.Select(g => g.HouseholdId).ToList();
            var existingHouseholdCount = await _dbContext.Households
                .AsNoTracking()
                .CountAsync(h => h.TenantId == tenantId && requestedHouseholdIds.Contains(h.Id), cancellationToken);
            if (existingHouseholdCount != requestedHouseholdIds.Count)
            {
                response.AddResponseMessage(MessageType.ERROR, "Household not found.");
                return response;
            }
        }

        // Resolve the invitee's account and any outstanding pending invite up front — both feed the
        // link validation (their own prior claim is not a conflict) and the merge-or-create decision.
        // The pending invite loads its grant rows including soft-deleted ones (parent filtered
        // explicitly) so a merge can reactivate a previously-removed grant instead of colliding on
        // the composite primary key.
        var existingUser = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.Email == email, cancellationToken);
        var existingPending = await _dbContext.Invitations
            .IgnoreQueryFilters([GathersteadDbContext.SoftDeleteFilter])
            .Include(i => i.Households)
            .Where(i => i.TenantId == tenantId && i.Email == email && i.Status == InvitationStatus.Pending && !i.IsDeleted)
            .FirstOrDefaultAsync(cancellationToken);

        // Optional member pre-link. Household access (above) stays independent — a link alone grants
        // the invitee self-service scope for that one member. Validate here so a bad link is rejected
        // at invite time rather than silently dropped on accept.
        if (request.LinkedMemberId is Guid linkedMemberId)
        {
            if (!await ServiceGuards.ValidateMemberLinkAsync(
                    response, _memberAuthorizationService, _dbContext, tenantId, linkedMemberId,
                    cancellationToken))
                return response;

            // An invitee who already holds a different link would otherwise have this one silently
            // dropped on accept (grants never overwrite an established link) — reject up front.
            if (existingUser is not null)
            {
                var currentLink = await _dbContext.TenantUsers
                    .AsNoTracking()
                    .Where(tu => tu.TenantId == tenantId && tu.UserId == existingUser.Id)
                    .Select(tu => tu.LinkedMemberId)
                    .FirstOrDefaultAsync(cancellationToken);
                if (currentLink is Guid current && current != linkedMemberId)
                {
                    response.AddResponseMessage(MessageType.ERROR, "The invited user is already linked to a different member in this tenant.");
                    return response;
                }
            }
        }

        // Idempotent: an outstanding pending invite for the same email is updated in place with the
        // newly requested role, link, and grants (last request wins) rather than returned stale —
        // otherwise a re-invite that adds access would report success without ever applying it.
        if (existingPending is not null)
        {
            existingPending.Role = request.Role;
            existingPending.LinkedMemberId = request.LinkedMemberId;
            MergeHouseholdGrants(existingPending, tenantId, householdGrants);
            await _dbContext.SaveChangesAsync(cancellationToken);

            await _securityEventLogger.LogAsync(
                SecurityEventType.InvitationCreated,
                SecurityEventSeverity.Info,
                resource: $"Invitation:{existingPending.Id}",
                detail: $"{{\"role\":\"{existingPending.Role}\",\"householdCount\":{existingPending.Households.Count(h => !h.IsDeleted)},\"updated\":true}}",
                tenantId: tenantId,
                userId: _currentUserContext.UserId,
                cancellationToken: cancellationToken);

            response.SetSuccess(MapToDto(existingPending));
            return response;
        }

        var invitation = new Invitation
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Email = email,
            Role = request.Role,
            LinkedMemberId = request.LinkedMemberId,
            Status = InvitationStatus.Pending,
            InvitedByUserId = _currentUserContext.UserId,
            Households = householdGrants
                .Select(g => new InvitationHouseholdAccess { TenantId = tenantId, HouseholdId = g.HouseholdId, Role = g.Role })
                .ToList(),
        };

        // If a user with this email already exists, accept immediately so the UX is identical
        // whether or not the invitee pre-existed.
        if (existingUser is not null)
        {
            await MembershipGrant.GrantAsync(
                _dbContext, tenantId, existingUser.Id, request.Role,
                householdGrants.Select(g => (g.HouseholdId, g.Role)).ToList(),
                cancellationToken, request.LinkedMemberId);
            invitation.Status = InvitationStatus.Accepted;
            invitation.AcceptedByUserId = existingUser.Id;
            invitation.AcceptedAt = DateTimeOffset.UtcNow;
        }

        _dbContext.Invitations.Add(invitation);
        await _dbContext.SaveChangesAsync(cancellationToken);

        // On immediate auto-accept, evict the granted user's cached role results so their new access
        // is visible on their next request rather than after the TTL.
        if (existingUser is not null)
        {
            await _authCache.InvalidateTenantUserAsync(tenantId, existingUser.Id, cancellationToken);
            await _authCache.InvalidateHouseholdUsersAsync(tenantId, existingUser.Id, cancellationToken);
        }

        // Attribution events emitted after persist so a logging failure can never block the invite.
        // Invitee email is omitted (PII) — the invitation row referenced by id already carries it.
        await _securityEventLogger.LogAsync(
            SecurityEventType.InvitationCreated,
            SecurityEventSeverity.Info,
            resource: $"Invitation:{invitation.Id}",
            detail: $"{{\"role\":\"{invitation.Role}\",\"householdCount\":{invitation.Households.Count}}}",
            tenantId: tenantId,
            userId: _currentUserContext.UserId,
            cancellationToken: cancellationToken);

        // The invite was auto-accepted (the invitee already had an account). Record the acceptance too,
        // flagging a self-grant — an inviter granting a role to their own account — for review.
        if (invitation.Status == InvitationStatus.Accepted)
        {
            var selfGrant = invitation.InvitedByUserId is not null
                && invitation.InvitedByUserId == invitation.AcceptedByUserId;
            await _securityEventLogger.LogAsync(
                SecurityEventType.InvitationAccepted,
                selfGrant ? SecurityEventSeverity.Warning : SecurityEventSeverity.Info,
                resource: $"Invitation:{invitation.Id}",
                detail: $"{{\"role\":\"{invitation.Role}\",\"invitedByUserId\":{JsonId(invitation.InvitedByUserId)},\"selfGrant\":{(selfGrant ? "true" : "false")}}}",
                tenantId: tenantId,
                userId: invitation.AcceptedByUserId,
                cancellationToken: cancellationToken);
        }

        response.SetSuccess(MapToDto(invitation));
        return response;
    }

    public async Task<BaseEntityResponse<IReadOnlyCollection<InvitationDto>>> ListAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var response = new BaseEntityResponse<IReadOnlyCollection<InvitationDto>>();

        if (!ServiceValidationHelper.ValidateTenantContext(tenantId, _currentTenantContext, response))
            return response;
        if (!await ServiceGuards.AuthorizeTenantManageAsync(response, _memberAuthorizationService, tenantId, cancellationToken))
            return response;

        var invitations = await _dbContext.Invitations
            .AsNoTracking()
            .Include(i => i.Households)
            .Where(i => i.TenantId == tenantId)
            .ToListAsync(cancellationToken);

        // Order client-side: SQLite (test provider) can't ORDER BY DateTimeOffset, and the result
        // set per tenant is small enough that in-memory sorting is inconsequential.
        var ordered = invitations.OrderByDescending(i => i.CreatedAt).ToList();

        return BaseEntityResponse<IReadOnlyCollection<InvitationDto>>.SuccessfulResponse(
            ordered.Select(MapToDto).ToList());
    }

    public async Task<InvitationResponse> RevokeAsync(
        Guid tenantId,
        Guid invitationId,
        CancellationToken cancellationToken = default)
    {
        var response = new InvitationResponse();

        if (!ServiceValidationHelper.ValidateTenantContext(tenantId, _currentTenantContext, response))
            return response;
        if (!await ServiceGuards.AuthorizeTenantManageAsync(response, _memberAuthorizationService, tenantId, cancellationToken))
            return response;

        var invitation = await ServiceGuards.LoadOrNotFoundAsync(
            response,
            _dbContext.Invitations.Include(i => i.Households).Where(i => i.TenantId == tenantId && i.Id == invitationId),
            EntityDisplayName,
            cancellationToken);

        if (invitation is null) return response;

        invitation.Status = InvitationStatus.Revoked;
        invitation.IsDeleted = true;
        // Revoking cancels the promised access too — leaving the grant rows active would let a
        // future reader (audit, un-revoke) resurface access the admin believes was cancelled.
        foreach (var grant in invitation.Households)
            grant.IsDeleted = true;
        await _dbContext.SaveChangesAsync(cancellationToken);

        response.SetSuccess(MapToDto(invitation));
        return response;
    }

    /// <summary>
    /// Reconciles a pending invitation's grant rows with the newly requested set: updates roles in
    /// place, reactivates previously-removed rows (the composite PK (InvitationId, HouseholdId)
    /// forbids a fresh insert), soft-deletes rows no longer requested, and adds new ones.
    /// Requires <paramref name="invitation"/>.Households to be loaded including soft-deleted rows.
    /// </summary>
    private static void MergeHouseholdGrants(
        Invitation invitation,
        Guid tenantId,
        IReadOnlyList<InvitationHouseholdGrant> requested)
    {
        var requestedByHousehold = requested.ToDictionary(g => g.HouseholdId, g => g.Role);
        foreach (var row in invitation.Households)
        {
            if (requestedByHousehold.TryGetValue(row.HouseholdId, out var role))
            {
                row.Role = role;
                row.IsDeleted = false;
                row.DeletedAt = null;
                row.DeletedByUserId = null;
                requestedByHousehold.Remove(row.HouseholdId);
            }
            else if (!row.IsDeleted)
            {
                row.IsDeleted = true;
            }
        }

        foreach (var (householdId, role) in requestedByHousehold)
            invitation.Households.Add(new InvitationHouseholdAccess { TenantId = tenantId, HouseholdId = householdId, Role = role });
    }

    // Soft-deleted grant rows can be present in memory (revoke, merge) — never surface them.
    private static InvitationDto MapToDto(Invitation i) => new(
        i.Id, i.TenantId, i.Email, i.Role,
        i.Households.Where(h => !h.IsDeleted).Select(h => new InvitationHouseholdGrant(h.HouseholdId, h.Role)).ToList(),
        i.LinkedMemberId, i.Status, i.CreatedAt, i.AcceptedAt);

    private static string JsonId(Guid? id) => id is null ? "null" : $"\"{id}\"";
}
