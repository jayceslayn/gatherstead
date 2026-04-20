using Gatherstead.Api.Contracts.EventAttendance;
using Gatherstead.Api.Contracts.Responses;
using Gatherstead.Api.Services.Authorization;
using Gatherstead.Api.Services.Validation;
using Gatherstead.Data;
using Gatherstead.Data.Entities;
using Microsoft.EntityFrameworkCore;
using EventAttendanceEntity = Gatherstead.Data.Entities.EventAttendance;

namespace Gatherstead.Api.Services.EventAttendance;

public class EventAttendanceService : IEventAttendanceService
{
    private const string EntityDisplayName = "Event attendance";

    private readonly GathersteadDbContext _dbContext;
    private readonly ICurrentTenantContext _currentTenantContext;
    private readonly IMemberAuthorizationService _memberAuthorizationService;

    public EventAttendanceService(
        GathersteadDbContext dbContext,
        ICurrentTenantContext currentTenantContext,
        IMemberAuthorizationService memberAuthorizationService)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _currentTenantContext = currentTenantContext ?? throw new ArgumentNullException(nameof(currentTenantContext));
        _memberAuthorizationService = memberAuthorizationService ?? throw new ArgumentNullException(nameof(memberAuthorizationService));
    }

    public async Task<BaseEntityResponse<IReadOnlyCollection<EventAttendanceDto>>> ListAsync(
        Guid tenantId,
        Guid eventId,
        IEnumerable<Guid>? memberIds = null,
        CancellationToken cancellationToken = default)
    {
        var response = new BaseEntityResponse<IReadOnlyCollection<EventAttendanceDto>>();

        if (!ServiceValidationHelper.ValidateTenantContext(tenantId, _currentTenantContext, response))
            return response;

        var query = _dbContext.EventAttendances
            .AsNoTracking()
            .Where(a => a.TenantId == tenantId && a.EventId == eventId);

        if (memberIds is not null)
        {
            var memberIdList = memberIds.ToList();
            if (memberIdList.Count > 0)
                query = query.Where(a => memberIdList.Contains(a.HouseholdMemberId));
        }

        var attendances = await query.Select(a => MapToDto(a)).ToListAsync(cancellationToken);

        return BaseEntityResponse<IReadOnlyCollection<EventAttendanceDto>>.SuccessfulResponse(attendances);
    }

    public async Task<EventAttendanceResponse> GetAsync(
        Guid tenantId,
        Guid eventId,
        Guid attendanceId,
        CancellationToken cancellationToken = default)
    {
        var response = new EventAttendanceResponse();

        if (!ServiceValidationHelper.ValidateTenantContext(tenantId, _currentTenantContext, response))
            return response;

        var attendance = await ServiceGuards.LoadOrNotFoundAsync(
            response,
            _dbContext.EventAttendances.AsNoTracking()
                .Where(a => a.TenantId == tenantId && a.EventId == eventId && a.Id == attendanceId),
            EntityDisplayName,
            cancellationToken);

        if (attendance is null) return response;

        response.SetSuccess(MapToDto(attendance));
        return response;
    }

    public async Task<EventAttendanceResponse> UpsertAsync(
        Guid tenantId,
        Guid eventId,
        Guid householdId,
        UpsertEventAttendanceRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = new EventAttendanceResponse();

        if (!ServiceValidationHelper.ValidateTenantContext(tenantId, _currentTenantContext, response))
            return response;
        if (!ServiceGuards.RequireRequest(request, "upsert event attendance", response))
            return response;
        if (!await ServiceGuards.AuthorizeMemberEditAsync(response, _memberAuthorizationService, tenantId, householdId, request.HouseholdMemberId, cancellationToken))
            return response;

        var eventExists = await _dbContext.Events
            .AsNoTracking()
            .AnyAsync(e => e.TenantId == tenantId && e.Id == eventId, cancellationToken);

        if (!eventExists)
        {
            response.AddResponseMessage(MessageType.ERROR, "Event not found.");
            return response;
        }

        var existing = await _dbContext.EventAttendances
            .Where(a => a.TenantId == tenantId && a.EventId == eventId && a.HouseholdMemberId == request.HouseholdMemberId && a.Day == request.Day)
            .SingleOrDefaultAsync(cancellationToken);

        if (existing is null)
        {
            existing = new EventAttendanceEntity
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                EventId = eventId,
                HouseholdMemberId = request.HouseholdMemberId,
                Day = request.Day,
            };
            _dbContext.EventAttendances.Add(existing);
        }
        else if (existing.IsDeleted)
        {
            existing.IsDeleted = false;
        }

        existing.Status = request.Status;
        existing.ArrivalWindowStart = request.ArrivalWindowStart;
        existing.ArrivalWindowEnd = request.ArrivalWindowEnd;
        existing.DepartureWindowStart = request.DepartureWindowStart;
        existing.DepartureWindowEnd = request.DepartureWindowEnd;
        existing.Notes = request.Notes?.Trim();

        await _dbContext.SaveChangesAsync(cancellationToken);

        response.SetSuccess(MapToDto(existing));
        return response;
    }

    public async Task<EventAttendanceResponse> DeleteAsync(
        Guid tenantId,
        Guid eventId,
        Guid attendanceId,
        CancellationToken cancellationToken = default)
    {
        var response = new EventAttendanceResponse();

        if (!ServiceValidationHelper.ValidateTenantContext(tenantId, _currentTenantContext, response))
            return response;

        var attendance = await ServiceGuards.LoadOrNotFoundAsync(
            response,
            _dbContext.EventAttendances
                .Where(a => a.TenantId == tenantId && a.EventId == eventId && a.Id == attendanceId),
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

    private static EventAttendanceDto MapToDto(EventAttendanceEntity a) => new(
        a.Id, a.TenantId, a.EventId, a.HouseholdMemberId, a.Day, a.Status,
        a.ArrivalWindowStart, a.ArrivalWindowEnd, a.DepartureWindowStart, a.DepartureWindowEnd,
        a.Notes, a.CreatedAt, a.UpdatedAt, a.IsDeleted, a.DeletedAt, a.DeletedByUserId);
}
