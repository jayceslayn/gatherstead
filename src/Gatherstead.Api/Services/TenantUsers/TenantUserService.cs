using Gatherstead.Api.Contracts.HouseholdUsers;
using Gatherstead.Api.Contracts.Responses;
using Gatherstead.Api.Contracts.TenantUsers;
using Gatherstead.Api.Security;
using Gatherstead.Api.Services.Authorization;
using Gatherstead.Api.Services.Observability;
using Gatherstead.Api.Services.Validation;
using Gatherstead.Data;
using Gatherstead.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Gatherstead.Api.Services.TenantUsers;

public class TenantUserService : ITenantUserService
{
    private readonly GathersteadDbContext _dbContext;
    private readonly ICurrentTenantContext _currentTenantContext;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IMemberAuthorizationService _memberAuthorizationService;
    private readonly IAppAdminContext _appAdminContext;
    private readonly IAuthCache _authCache;
    private readonly ISecurityEventLogger _securityEventLogger;

    public TenantUserService(
        GathersteadDbContext dbContext,
        ICurrentTenantContext currentTenantContext,
        ICurrentUserContext currentUserContext,
        IMemberAuthorizationService memberAuthorizationService,
        IAppAdminContext appAdminContext,
        IAuthCache authCache,
        ISecurityEventLogger securityEventLogger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _currentTenantContext = currentTenantContext ?? throw new ArgumentNullException(nameof(currentTenantContext));
        _currentUserContext = currentUserContext ?? throw new ArgumentNullException(nameof(currentUserContext));
        _memberAuthorizationService = memberAuthorizationService ?? throw new ArgumentNullException(nameof(memberAuthorizationService));
        _appAdminContext = appAdminContext ?? throw new ArgumentNullException(nameof(appAdminContext));
        _authCache = authCache ?? throw new ArgumentNullException(nameof(authCache));
        _securityEventLogger = securityEventLogger ?? throw new ArgumentNullException(nameof(securityEventLogger));
    }

    public async Task<BaseEntityResponse<IReadOnlyCollection<TenantUserDto>>> ListAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var response = new BaseEntityResponse<IReadOnlyCollection<TenantUserDto>>();
        ServiceValidationHelper.ValidateTenantContext(tenantId, _currentTenantContext, response);

        if (ServiceValidationHelper.HasErrors(response))
            return response;

        if (!await ServiceGuards.AuthorizeTenantManageAsync(response, _memberAuthorizationService, tenantId, cancellationToken))
            return response;

        var users = await _dbContext.TenantUsers
            .AsNoTracking()
            .Include(tu => tu.User)
            .Where(tu => tu.TenantId == tenantId)
            .ToListAsync(cancellationToken);

        var dtos = users.Select(MapToDto).ToList();
        return BaseEntityResponse<IReadOnlyCollection<TenantUserDto>>.SuccessfulResponse(dtos);
    }

    public async Task<TenantUserResponse> UpdateRoleAsync(
        Guid tenantId,
        Guid userId,
        UpdateTenantUserRoleRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = new TenantUserResponse();
        ServiceValidationHelper.ValidateTenantContext(tenantId, _currentTenantContext, response);

        if (!ServiceGuards.RequireRequest(request, "update tenant user role", response))
            return response;

        if (ServiceValidationHelper.HasErrors(response))
            return response;

        if (!await ServiceGuards.AuthorizeTenantManageAsync(response, _memberAuthorizationService, tenantId, cancellationToken))
            return response;

        var isAppAdmin = await _appAdminContext.IsAppAdminAsync(cancellationToken) == true;

        if (!isAppAdmin)
        {
            var actorTenantUser = await ResolveActingManagerAsync(tenantId, response, cancellationToken);
            if (actorTenantUser is null)
                return response;

            if (!ServiceGuards.RequireNonEscalatingRole(response, actorTenantUser.Role, request.Role))
                return response;
        }

        var tenantUser = await _dbContext.TenantUsers
            .Include(tu => tu.User)
            .Where(tu => tu.TenantId == tenantId && tu.UserId == userId)
            .SingleOrDefaultAsync(cancellationToken);

        if (tenantUser is null)
        {
            response.AddResponseMessage(MessageType.ERROR, "User is not a member of this tenant.");
            return response;
        }

        // Protect against demoting the last Owner (App Admins bypass this)
        if (!isAppAdmin && tenantUser.Role == TenantRole.Owner && request.Role != TenantRole.Owner
            && !await RequireOtherOwnerExistsAsync(tenantId, userId, "Cannot demote the last Owner of this tenant.", response, cancellationToken))
        {
            return response;
        }

        tenantUser.Role = request.Role;
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _authCache.InvalidateTenantUserAsync(tenantId, userId, cancellationToken);

        if (isAppAdmin)
            await LogAppAdminActionAsync(tenantId, userId, $"{{\"action\":\"RoleUpdated\",\"newRole\":\"{request.Role}\"}}", cancellationToken);

        response.SetSuccess(MapToDto(tenantUser));
        return response;
    }

    public async Task<TenantUserResponse> SetLinkedMemberAsync(
        Guid tenantId,
        Guid userId,
        SetLinkedMemberRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = new TenantUserResponse();
        ServiceValidationHelper.ValidateTenantContext(tenantId, _currentTenantContext, response);

        if (request is null)
        {
            response.AddResponseMessage(MessageType.ERROR, "A set linked member request is required.");
            return response;
        }

        if (ServiceValidationHelper.HasErrors(response))
            return response;

        var tenantUser = await _dbContext.TenantUsers
            .Include(tu => tu.LinkedMember)
            .Include(tu => tu.User)
            .Where(tu => tu.TenantId == tenantId && tu.UserId == userId)
            .SingleOrDefaultAsync(cancellationToken);

        if (tenantUser is null)
        {
            response.AddResponseMessage(MessageType.ERROR, "User is not a member of this tenant.");
            return response;
        }

        if (request.MemberId.HasValue)
        {
            // No-op if already linked to the same member
            if (tenantUser.LinkedMemberId == request.MemberId)
            {
                response.SetSuccess(MapToDto(tenantUser));
                return response;
            }

            if (!await ServiceGuards.ValidateMemberLinkAsync(
                    response, _memberAuthorizationService, _dbContext, tenantId, request.MemberId.Value,
                    excludeUserId: userId, excludeInvitationId: null, cancellationToken))
                return response;

            tenantUser.LinkedMemberId = request.MemberId;
        }
        else
        {
            // No-op if already unlinked
            if (!tenantUser.LinkedMemberId.HasValue)
            {
                response.SetSuccess(MapToDto(tenantUser));
                return response;
            }

            var currentHouseholdId = tenantUser.LinkedMember!.HouseholdId;
            if (!await _memberAuthorizationService.CanManageHouseholdAsync(tenantId, currentHouseholdId, cancellationToken))
            {
                response.AddResponseMessage(MessageType.ERROR, "You do not have permission to unlink a user from this member.");
                return response;
            }

            tenantUser.LinkedMemberId = null;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        // LinkedMemberId is part of the cached TenantUserInfo (drives the Self check).
        await _authCache.InvalidateTenantUserAsync(tenantId, userId, cancellationToken);

        response.SetSuccess(MapToDto(tenantUser));
        return response;
    }

    public async Task<TenantUserResponse> RemoveAsync(
        Guid tenantId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var response = new TenantUserResponse();
        ServiceValidationHelper.ValidateTenantContext(tenantId, _currentTenantContext, response);

        if (ServiceValidationHelper.HasErrors(response))
            return response;

        if (!await ServiceGuards.AuthorizeTenantManageAsync(response, _memberAuthorizationService, tenantId, cancellationToken))
            return response;

        var isAppAdmin = await _appAdminContext.IsAppAdminAsync(cancellationToken) == true;

        var tenantUser = await _dbContext.TenantUsers
            .Include(tu => tu.User)
            .Where(tu => tu.TenantId == tenantId && tu.UserId == userId)
            .SingleOrDefaultAsync(cancellationToken);

        if (tenantUser is null)
        {
            response.AddResponseMessage(MessageType.ERROR, "User is not a member of this tenant.");
            return response;
        }

        if (!isAppAdmin)
        {
            var actorTenantUser = await ResolveActingManagerAsync(tenantId, response, cancellationToken);
            if (actorTenantUser is null)
                return response;

            // Removing yourself through the admin surface would let a manager lock themselves out
            // mid-session; leaving a tenant is a separate concern out of scope here.
            if (actorTenantUser.UserId == userId)
            {
                response.AddResponseMessage(MessageType.ERROR, "You cannot remove yourself from the tenant.");
                return response;
            }

            // Cannot remove a user more privileged than yourself (lower numeric value = higher privilege).
            if (!ServiceGuards.RequireNonEscalatingRole(response, actorTenantUser.Role, tenantUser.Role,
                    "You cannot remove a user more privileged than yourself."))
                return response;

            // Protect against removing the last Owner (App Admins bypass this).
            if (tenantUser.Role == TenantRole.Owner
                && !await RequireOtherOwnerExistsAsync(tenantId, userId, "Cannot remove the last Owner of this tenant.", response, cancellationToken))
            {
                return response;
            }
        }

        // Clear the member link so the soft-deleted row does not hold the unique filtered index
        // (IX_TenantUser_LinkedMemberId is filtered on LinkedMemberId only, not on IsDeleted),
        // which would otherwise permanently block re-linking that member to anyone.
        tenantUser.LinkedMemberId = null;
        tenantUser.IsDeleted = true;

        // Removing tenant membership also removes household-level access — no orphaned access remains.
        var householdUsers = await _dbContext.HouseholdUsers
            .Where(hu => hu.TenantId == tenantId && hu.UserId == userId)
            .ToListAsync(cancellationToken);
        foreach (var householdUser in householdUsers)
            householdUser.IsDeleted = true;

        await _dbContext.SaveChangesAsync(cancellationToken);

        await _authCache.InvalidateTenantUserAsync(tenantId, userId, cancellationToken);
        await _authCache.InvalidateHouseholdUsersAsync(tenantId, userId, cancellationToken);

        if (isAppAdmin)
            await LogAppAdminActionAsync(tenantId, userId, "{\"action\":\"Removed\"}", cancellationToken);

        response.SetSuccess(MapToDto(tenantUser));
        return response;
    }

    public async Task<BaseEntityResponse<IReadOnlyCollection<HouseholdUserDto>>> ListUserHouseholdAccessAsync(
        Guid tenantId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var response = new BaseEntityResponse<IReadOnlyCollection<HouseholdUserDto>>();
        ServiceValidationHelper.ValidateTenantContext(tenantId, _currentTenantContext, response);

        if (ServiceValidationHelper.HasErrors(response))
            return response;

        if (!await ServiceGuards.AuthorizeTenantManageAsync(response, _memberAuthorizationService, tenantId, cancellationToken))
            return response;

        var householdUsers = await _dbContext.HouseholdUsers
            .AsNoTracking()
            .Include(hu => hu.User)
            .Where(hu => hu.TenantId == tenantId && hu.UserId == userId)
            .ToListAsync(cancellationToken);

        var dtos = householdUsers
            .Select(hu => new HouseholdUserDto(hu.UserId, hu.TenantId, hu.HouseholdId, hu.Role, hu.User?.ExternalId ?? string.Empty))
            .ToList();

        return BaseEntityResponse<IReadOnlyCollection<HouseholdUserDto>>.SuccessfulResponse(dtos);
    }

    public async Task<BaseEntityResponse<TenantUserMeDto>> GetCurrentTenantUserAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var response = new BaseEntityResponse<TenantUserMeDto>();
        ServiceValidationHelper.ValidateTenantContext(tenantId, _currentTenantContext, response);

        if (ServiceValidationHelper.HasErrors(response))
            return response;

        var currentUserId = _currentUserContext.UserId;
        if (!currentUserId.HasValue)
        {
            response.AddResponseMessage(MessageType.ERROR, "Unable to determine current user.");
            return response;
        }

        var tenantUser = await _dbContext.TenantUsers
            .AsNoTracking()
            .Include(tu => tu.User)
            .Include(tu => tu.LinkedMember)
            .Where(tu => tu.TenantId == tenantId && tu.UserId == currentUserId.Value)
            .SingleOrDefaultAsync(cancellationToken);

        if (tenantUser is null)
        {
            response.AddResponseMessage(MessageType.ERROR, "Current user is not a member of this tenant.");
            return response;
        }

        var dto = new TenantUserMeDto(
            tenantUser.UserId,
            tenantUser.TenantId,
            tenantUser.Role,
            tenantUser.LinkedMemberId,
            tenantUser.LinkedMember?.HouseholdId,
            tenantUser.User?.ExternalId ?? string.Empty);

        return BaseEntityResponse<TenantUserMeDto>.SuccessfulResponse(dto);
    }

    /// <summary>
    /// Resolves the non-app-admin actor's own tenant membership for a privileged per-user mutation,
    /// attaching the standard error and returning null when the acting user cannot be determined or
    /// is not a member of the tenant.
    /// </summary>
    private async Task<TenantUser?> ResolveActingManagerAsync(
        Guid tenantId,
        TenantUserResponse response,
        CancellationToken cancellationToken)
    {
        var actorUserId = _currentUserContext.UserId;
        if (!actorUserId.HasValue)
        {
            response.AddResponseMessage(MessageType.ERROR, "Unable to determine acting user.");
            return null;
        }

        var actorTenantUser = await _dbContext.TenantUsers
            .AsNoTracking()
            .Where(tu => tu.TenantId == tenantId && tu.UserId == actorUserId.Value)
            .SingleOrDefaultAsync(cancellationToken);

        if (actorTenantUser is null)
            response.AddResponseMessage(MessageType.ERROR, "Acting user is not a member of this tenant.");

        return actorTenantUser;
    }

    /// <summary>
    /// Guards the "a tenant always retains at least one Owner" invariant: fails with
    /// <paramref name="errorMessage"/> when no Owner other than <paramref name="userId"/> exists.
    /// </summary>
    private async Task<bool> RequireOtherOwnerExistsAsync(
        Guid tenantId,
        Guid userId,
        string errorMessage,
        TenantUserResponse response,
        CancellationToken cancellationToken)
    {
        var otherOwnerExists = await _dbContext.TenantUsers
            .AsNoTracking()
            .AnyAsync(tu => tu.TenantId == tenantId && tu.UserId != userId && tu.Role == TenantRole.Owner, cancellationToken);

        if (!otherOwnerExists)
        {
            response.AddResponseMessage(MessageType.ERROR, errorMessage);
            return false;
        }
        return true;
    }

    // An App Admin bypassing tenant authorization is a privileged action worth auditing.
    private Task LogAppAdminActionAsync(Guid tenantId, Guid targetUserId, string detail, CancellationToken cancellationToken) =>
        _securityEventLogger.LogAsync(
            SecurityEventType.AppAdminAction, SecurityEventSeverity.Info,
            resource: $"TenantUser:{targetUserId}",
            detail: detail,
            tenantId: tenantId, userId: _currentUserContext.UserId, cancellationToken: cancellationToken);

    private static TenantUserDto MapToDto(TenantUser tu) =>
        new(tu.UserId, tu.TenantId, tu.Role, tu.LinkedMemberId, tu.User?.ExternalId ?? string.Empty, tu.User?.Email, tu.User?.DisplayName);
}
