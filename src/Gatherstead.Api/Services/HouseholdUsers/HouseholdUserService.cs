using Gatherstead.Api.Contracts.HouseholdUsers;
using Gatherstead.Api.Contracts.Responses;
using Gatherstead.Api.Security;
using Gatherstead.Api.Services.Authorization;
using Gatherstead.Api.Services.Validation;
using Gatherstead.Data;
using Gatherstead.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Gatherstead.Api.Services.HouseholdUsers;

public class HouseholdUserService : IHouseholdUserService
{
    private readonly GathersteadDbContext _dbContext;
    private readonly ICurrentTenantContext _currentTenantContext;
    private readonly IMemberAuthorizationService _memberAuthorizationService;
    private readonly IAuthCache _authCache;

    public HouseholdUserService(
        GathersteadDbContext dbContext,
        ICurrentTenantContext currentTenantContext,
        IMemberAuthorizationService memberAuthorizationService,
        IAuthCache authCache)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _currentTenantContext = currentTenantContext ?? throw new ArgumentNullException(nameof(currentTenantContext));
        _memberAuthorizationService = memberAuthorizationService ?? throw new ArgumentNullException(nameof(memberAuthorizationService));
        _authCache = authCache ?? throw new ArgumentNullException(nameof(authCache));
    }

    public async Task<BaseEntityResponse<IReadOnlyCollection<HouseholdUserDto>>> ListAsync(
        Guid tenantId,
        Guid householdId,
        CancellationToken cancellationToken = default)
    {
        var response = new BaseEntityResponse<IReadOnlyCollection<HouseholdUserDto>>();
        ServiceValidationHelper.ValidateTenantContext(tenantId, _currentTenantContext, response);

        if (ServiceValidationHelper.HasErrors(response))
            return response;

        if (!await ServiceGuards.AuthorizeHouseholdManageAsync(
                response, _memberAuthorizationService, tenantId, householdId,
                "You do not have permission to manage this household's users.", cancellationToken))
            return response;

        var users = await _dbContext.HouseholdUsers
            .AsNoTracking()
            .Include(hu => hu.User)
            .Where(hu => hu.TenantId == tenantId && hu.HouseholdId == householdId)
            .ToListAsync(cancellationToken);

        return BaseEntityResponse<IReadOnlyCollection<HouseholdUserDto>>.SuccessfulResponse(
            users.Select(MapToDto).ToList());
    }

    public async Task<HouseholdUserResponse> UpsertAsync(
        Guid tenantId,
        Guid householdId,
        Guid userId,
        UpsertHouseholdUserRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = new HouseholdUserResponse();
        ServiceValidationHelper.ValidateTenantContext(tenantId, _currentTenantContext, response);

        if (!ServiceGuards.RequireRequest(request, "upsert household user", response))
            return response;

        if (ServiceValidationHelper.HasErrors(response))
            return response;

        if (!await ServiceGuards.AuthorizeHouseholdManageAsync(
                response, _memberAuthorizationService, tenantId, householdId,
                "You do not have permission to manage this household's users.", cancellationToken))
            return response;

        // Target user must be an active TenantUser
        var isTenantMember = await _dbContext.TenantUsers
            .AsNoTracking()
            .AnyAsync(tu => tu.TenantId == tenantId && tu.UserId == userId, cancellationToken);

        if (!isTenantMember)
        {
            response.AddResponseMessage(MessageType.ERROR, "User is not a member of this tenant.");
            return response;
        }

        // Load including soft-deleted rows so we can restore
        var existing = await _dbContext.HouseholdUsers
            .IgnoreQueryFilters([GathersteadDbContext.SoftDeleteFilter])
            .Include(hu => hu.User)
            .Where(hu => hu.TenantId == tenantId && hu.HouseholdId == householdId && hu.UserId == userId)
            .SingleOrDefaultAsync(cancellationToken);

        if (existing is not null)
        {
            existing.Role = request.Role;
            if (existing.IsDeleted)
            {
                existing.IsDeleted = false;
                existing.DeletedAt = null;
                existing.DeletedByUserId = null;
            }
        }
        else
        {
            existing = new HouseholdUser
            {
                TenantId = tenantId,
                HouseholdId = householdId,
                UserId = userId,
                Role = request.Role,
            };
            _dbContext.HouseholdUsers.Add(existing);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        await _authCache.InvalidateHouseholdUsersAsync(tenantId, userId, cancellationToken);

        // Reload User navigation if it was null on a newly created entity
        if (existing.User is null)
            await _dbContext.Entry(existing).Reference(hu => hu.User).LoadAsync(cancellationToken);

        response.SetSuccess(MapToDto(existing));
        return response;
    }

    public async Task<HouseholdUserResponse> DeleteAsync(
        Guid tenantId,
        Guid householdId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var response = new HouseholdUserResponse();
        ServiceValidationHelper.ValidateTenantContext(tenantId, _currentTenantContext, response);

        if (ServiceValidationHelper.HasErrors(response))
            return response;

        if (!await ServiceGuards.AuthorizeHouseholdManageAsync(
                response, _memberAuthorizationService, tenantId, householdId,
                "You do not have permission to manage this household's users.", cancellationToken))
            return response;

        var householdUser = await _dbContext.HouseholdUsers
            .IgnoreQueryFilters([GathersteadDbContext.SoftDeleteFilter])
            .Include(hu => hu.User)
            .Where(hu => hu.TenantId == tenantId && hu.HouseholdId == householdId && hu.UserId == userId)
            .SingleOrDefaultAsync(cancellationToken);

        if (householdUser is null)
        {
            response.AddResponseMessage(MessageType.ERROR, "Household user not found.");
            return response;
        }

        if (householdUser.IsDeleted)
        {
            response.AddResponseMessage(MessageType.WARNING, "Household user access already removed.");
            return response;
        }

        householdUser.IsDeleted = true;
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _authCache.InvalidateHouseholdUsersAsync(tenantId, userId, cancellationToken);

        response.SetSuccess(MapToDto(householdUser));
        return response;
    }

    private static HouseholdUserDto MapToDto(HouseholdUser hu) =>
        new(hu.UserId, hu.TenantId, hu.HouseholdId, hu.Role, hu.User?.ExternalId ?? string.Empty);
}
