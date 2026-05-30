using Gatherstead.Api.Contracts.Invitations;
using Gatherstead.Api.Contracts.Responses;
using Gatherstead.Api.Services.Authorization;
using Gatherstead.Api.Services.Membership;
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

    public InvitationService(
        GathersteadDbContext dbContext,
        ICurrentTenantContext currentTenantContext,
        ICurrentUserContext currentUserContext,
        IMemberAuthorizationService memberAuthorizationService)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _currentTenantContext = currentTenantContext ?? throw new ArgumentNullException(nameof(currentTenantContext));
        _currentUserContext = currentUserContext ?? throw new ArgumentNullException(nameof(currentUserContext));
        _memberAuthorizationService = memberAuthorizationService ?? throw new ArgumentNullException(nameof(memberAuthorizationService));
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

        if (request.HouseholdId is Guid householdId)
        {
            var householdExists = await _dbContext.Households
                .AsNoTracking()
                .AnyAsync(h => h.TenantId == tenantId && h.Id == householdId, cancellationToken);
            if (!householdExists)
            {
                response.AddResponseMessage(MessageType.ERROR, "Household not found.");
                return response;
            }
        }

        // Idempotent: an outstanding pending invite for the same email is returned as-is.
        var existingPending = await _dbContext.Invitations
            .Where(i => i.TenantId == tenantId && i.Email == email && i.Status == InvitationStatus.Pending)
            .FirstOrDefaultAsync(cancellationToken);
        if (existingPending is not null)
        {
            response.SetSuccess(MapToDto(existingPending));
            return response;
        }

        var invitation = new Invitation
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Email = email,
            Role = request.Role,
            HouseholdId = request.HouseholdId,
            HouseholdRole = request.HouseholdRole,
            Status = InvitationStatus.Pending,
            InvitedByUserId = _currentUserContext.UserId,
        };

        // If a user with this email already exists, accept immediately so the UX is identical
        // whether or not the invitee pre-existed.
        var existingUser = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.Email == email, cancellationToken);

        if (existingUser is not null)
        {
            await MembershipGrant.GrantAsync(_dbContext, tenantId, existingUser.Id, request.Role, request.HouseholdId, request.HouseholdRole, cancellationToken);
            invitation.Status = InvitationStatus.Accepted;
            invitation.AcceptedByUserId = existingUser.Id;
            invitation.AcceptedAt = DateTimeOffset.UtcNow;
        }

        _dbContext.Invitations.Add(invitation);
        await _dbContext.SaveChangesAsync(cancellationToken);

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
            _dbContext.Invitations.Where(i => i.TenantId == tenantId && i.Id == invitationId),
            EntityDisplayName,
            cancellationToken);

        if (invitation is null) return response;

        invitation.Status = InvitationStatus.Revoked;
        invitation.IsDeleted = true;
        await _dbContext.SaveChangesAsync(cancellationToken);

        response.SetSuccess(MapToDto(invitation));
        return response;
    }

    private static InvitationDto MapToDto(Invitation i) => new(
        i.Id, i.TenantId, i.Email, i.Role, i.HouseholdId, i.HouseholdRole, i.Status, i.CreatedAt, i.AcceptedAt);
}
