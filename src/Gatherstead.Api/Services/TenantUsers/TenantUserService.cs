using Gatherstead.Api.Contracts.Responses;
using Gatherstead.Api.Contracts.TenantUsers;
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
    private readonly IMemberAuthorizationService _memberAuthorizationService;

    public TenantUserService(
        GathersteadDbContext dbContext,
        ICurrentTenantContext currentTenantContext,
        IMemberAuthorizationService memberAuthorizationService)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _currentTenantContext = currentTenantContext ?? throw new ArgumentNullException(nameof(currentTenantContext));
        _memberAuthorizationService = memberAuthorizationService ?? throw new ArgumentNullException(nameof(memberAuthorizationService));
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
        response.SetSuccess(MapToDto(tenantUser));
        return response;
    }

    private static TenantUserDto MapToDto(TenantUser tu) =>
        new(tu.UserId, tu.TenantId, tu.Role, tu.LinkedMemberId);
}
