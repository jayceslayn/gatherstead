using Gatherstead.Api.Contracts.HouseholdUsers;
using Gatherstead.Api.Contracts.Responses;
using Gatherstead.Api.Contracts.TenantUsers;
using Gatherstead.Api.Security;
using Gatherstead.Api.Services.Authorization;
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

    public TenantUserService(
        GathersteadDbContext dbContext,
        ICurrentTenantContext currentTenantContext,
        ICurrentUserContext currentUserContext,
        IMemberAuthorizationService memberAuthorizationService,
        IAppAdminContext appAdminContext,
        IAuthCache authCache)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _currentTenantContext = currentTenantContext ?? throw new ArgumentNullException(nameof(currentTenantContext));
        _currentUserContext = currentUserContext ?? throw new ArgumentNullException(nameof(currentUserContext));
        _memberAuthorizationService = memberAuthorizationService ?? throw new ArgumentNullException(nameof(memberAuthorizationService));
        _appAdminContext = appAdminContext ?? throw new ArgumentNullException(nameof(appAdminContext));
        _authCache = authCache ?? throw new ArgumentNullException(nameof(authCache));
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
            var actorUserId = _currentUserContext.UserId;
            if (!actorUserId.HasValue)
            {
                response.AddResponseMessage(MessageType.ERROR, "Unable to determine acting user.");
                return response;
            }

            var actorTenantUser = await _dbContext.TenantUsers
                .AsNoTracking()
                .Where(tu => tu.TenantId == tenantId && tu.UserId == actorUserId.Value)
                .SingleOrDefaultAsync(cancellationToken);

            if (actorTenantUser is null)
            {
                response.AddResponseMessage(MessageType.ERROR, "Acting user is not a member of this tenant.");
                return response;
            }

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
        if (!isAppAdmin && tenantUser.Role == TenantRole.Owner && request.Role != TenantRole.Owner)
        {
            var otherOwnerExists = await _dbContext.TenantUsers
                .AsNoTracking()
                .AnyAsync(tu => tu.TenantId == tenantId && tu.UserId != userId && tu.Role == TenantRole.Owner, cancellationToken);

            if (!otherOwnerExists)
            {
                response.AddResponseMessage(MessageType.ERROR, "Cannot demote the last Owner of this tenant.");
                return response;
            }
        }

        tenantUser.Role = request.Role;
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _authCache.InvalidateTenantUserAsync(tenantId, userId, cancellationToken);

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

            var targetMember = await _dbContext.HouseholdMembers
                .AsNoTracking()
                .Where(m => m.TenantId == tenantId && m.Id == request.MemberId.Value)
                .SingleOrDefaultAsync(cancellationToken);

            if (targetMember is null)
            {
                response.AddResponseMessage(MessageType.ERROR, "Household member not found.");
                return response;
            }

            if (!await _memberAuthorizationService.CanManageHouseholdAsync(tenantId, targetMember.HouseholdId, cancellationToken))
            {
                response.AddResponseMessage(MessageType.ERROR, "You do not have permission to link a user to this member.");
                return response;
            }

            var alreadyClaimed = await _dbContext.TenantUsers
                .AsNoTracking()
                .AnyAsync(tu => tu.TenantId == tenantId && tu.LinkedMemberId == request.MemberId && tu.UserId != userId, cancellationToken);

            if (alreadyClaimed)
            {
                response.AddResponseMessage(MessageType.ERROR, "The specified member is already linked to another user in this tenant.");
                return response;
            }

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

    private static TenantUserDto MapToDto(TenantUser tu) =>
        new(tu.UserId, tu.TenantId, tu.Role, tu.LinkedMemberId, tu.User?.ExternalId ?? string.Empty, tu.User?.Email, tu.User?.DisplayName);
}
