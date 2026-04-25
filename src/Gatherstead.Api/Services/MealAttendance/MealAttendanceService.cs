using Gatherstead.Api.Contracts.MealAttendance;
using Gatherstead.Api.Contracts.Responses;
using Gatherstead.Api.Services.Authorization;
using Gatherstead.Api.Services.Validation;
using Gatherstead.Data;
using Gatherstead.Data.Entities;
using Microsoft.EntityFrameworkCore;
using MealAttendanceEntity = Gatherstead.Data.Entities.MealAttendance;

namespace Gatherstead.Api.Services.MealAttendance;

public class MealAttendanceService : IMealAttendanceService
{
    private const string EntityDisplayName = "Meal attendance";

    private readonly GathersteadDbContext _dbContext;
    private readonly ICurrentTenantContext _currentTenantContext;
    private readonly IMemberAuthorizationService _memberAuthorizationService;

    public MealAttendanceService(
        GathersteadDbContext dbContext,
        ICurrentTenantContext currentTenantContext,
        IMemberAuthorizationService memberAuthorizationService)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _currentTenantContext = currentTenantContext ?? throw new ArgumentNullException(nameof(currentTenantContext));
        _memberAuthorizationService = memberAuthorizationService ?? throw new ArgumentNullException(nameof(memberAuthorizationService));
    }

    public async Task<BaseEntityResponse<IReadOnlyCollection<MealAttendanceDto>>> ListAsync(
        Guid tenantId,
        Guid planId,
        IEnumerable<Guid>? memberIds = null,
        CancellationToken cancellationToken = default)
    {
        var response = new BaseEntityResponse<IReadOnlyCollection<MealAttendanceDto>>();

        if (!ServiceValidationHelper.ValidateTenantContext(tenantId, _currentTenantContext, response))
            return response;

        var query = _dbContext.MealAttendances
            .AsNoTracking()
            .Where(a => a.TenantId == tenantId && a.MealPlanId == planId);

        if (memberIds is not null)
        {
            var memberIdList = memberIds.ToList();
            if (memberIdList.Count > 0)
                query = query.Where(a => memberIdList.Contains(a.HouseholdMemberId));
        }

        var attendances = await query.Select(a => MapToDto(a)).ToListAsync(cancellationToken);

        return BaseEntityResponse<IReadOnlyCollection<MealAttendanceDto>>.SuccessfulResponse(attendances);
    }

    public async Task<MealAttendanceResponse> GetAsync(
        Guid tenantId,
        Guid planId,
        Guid attendanceId,
        CancellationToken cancellationToken = default)
    {
        var response = new MealAttendanceResponse();

        if (!ServiceValidationHelper.ValidateTenantContext(tenantId, _currentTenantContext, response))
            return response;

        var attendance = await ServiceGuards.LoadOrNotFoundAsync(
            response,
            _dbContext.MealAttendances.AsNoTracking()
                .Where(a => a.TenantId == tenantId && a.MealPlanId == planId && a.Id == attendanceId),
            EntityDisplayName,
            cancellationToken);

        if (attendance is null) return response;

        response.SetSuccess(MapToDto(attendance));
        return response;
    }

    public async Task<MealAttendanceResponse> UpsertAsync(
        Guid tenantId,
        Guid planId,
        Guid householdId,
        UpsertMealAttendanceRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = new MealAttendanceResponse();

        if (!ServiceValidationHelper.ValidateTenantContext(tenantId, _currentTenantContext, response))
            return response;
        if (!ServiceGuards.RequireRequest(request, "upsert meal attendance", response))
            return response;
        if (!await ServiceGuards.AuthorizeMemberEditAsync(response, _memberAuthorizationService, tenantId, householdId, request.HouseholdMemberId, cancellationToken))
            return response;

        var planExists = await _dbContext.MealPlans
            .AsNoTracking()
            .AnyAsync(p => p.TenantId == tenantId && p.Id == planId, cancellationToken);

        if (!planExists)
        {
            response.AddResponseMessage(MessageType.ERROR, "Meal plan not found.");
            return response;
        }

        var existing = await _dbContext.MealAttendances
            .Where(a => a.TenantId == tenantId && a.MealPlanId == planId && a.HouseholdMemberId == request.HouseholdMemberId)
            .SingleOrDefaultAsync(cancellationToken);

        if (existing is null)
        {
            existing = new MealAttendanceEntity
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                MealPlanId = planId,
                HouseholdMemberId = request.HouseholdMemberId,
            };
            _dbContext.MealAttendances.Add(existing);
        }
        else if (existing.IsDeleted)
        {
            existing.IsDeleted = false;
        }

        existing.Status = request.Status;
        existing.BringOwnFood = request.BringOwnFood;
        existing.Notes = request.Notes?.Trim();

        await _dbContext.SaveChangesAsync(cancellationToken);

        response.SetSuccess(MapToDto(existing));
        return response;
    }

    public async Task<MealAttendanceResponse> DeleteAsync(
        Guid tenantId,
        Guid planId,
        Guid attendanceId,
        CancellationToken cancellationToken = default)
    {
        var response = new MealAttendanceResponse();

        if (!ServiceValidationHelper.ValidateTenantContext(tenantId, _currentTenantContext, response))
            return response;

        var attendance = await ServiceGuards.LoadOrNotFoundAsync(
            response,
            _dbContext.MealAttendances
                .Where(a => a.TenantId == tenantId && a.MealPlanId == planId && a.Id == attendanceId),
            EntityDisplayName,
            cancellationToken);

        if (attendance is null) return response;

        if (!await ServiceGuards.AuthorizeMemberEditAsync(response, _memberAuthorizationService, tenantId, Guid.Empty, attendance.HouseholdMemberId, cancellationToken))
            return response;

        if (attendance.IsDeleted)
        {
            response.AddResponseMessage(MessageType.WARNING, $"{EntityDisplayName} already deleted.");
            return response;
        }

        attendance.IsDeleted = true;
        await _dbContext.SaveChangesAsync(cancellationToken);

        response.SetSuccess(MapToDto(attendance));
        return response;
    }

    private static MealAttendanceDto MapToDto(MealAttendanceEntity a) => new(
        a.Id, a.TenantId, a.MealPlanId, a.HouseholdMemberId, a.Status, a.BringOwnFood, a.Notes,
        a.CreatedAt, a.UpdatedAt, a.IsDeleted, a.DeletedAt, a.DeletedByUserId);
}
