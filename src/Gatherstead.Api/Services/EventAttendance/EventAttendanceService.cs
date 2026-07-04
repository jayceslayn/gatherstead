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
    private readonly IAuditVisibilityContext _auditVisibility;

    public EventAttendanceService(
        GathersteadDbContext dbContext,
        ICurrentTenantContext currentTenantContext,
        IMemberAuthorizationService memberAuthorizationService,
        IAuditVisibilityContext auditVisibility)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _currentTenantContext = currentTenantContext ?? throw new ArgumentNullException(nameof(currentTenantContext));
        _memberAuthorizationService = memberAuthorizationService ?? throw new ArgumentNullException(nameof(memberAuthorizationService));
        _auditVisibility = auditVisibility ?? throw new ArgumentNullException(nameof(auditVisibility));
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

        var entities = await query.ToListAsync(cancellationToken);
        var attendances = entities.Select(MapToDto).ToList();

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
        // Resolve + authorize the member by its own id; the client-supplied householdId is
        // advisory only (a member has exactly one household), so a stale/mismatched value no
        // longer produces a spurious "Household member not found."
        if (await ServiceGuards.ResolveMemberForIntentAsync(response, _memberAuthorizationService, _dbContext, tenantId, request.HouseholdMemberId, cancellationToken) is null)
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
        existing.Notes = request.Notes?.Trim();

        await _dbContext.SaveChangesAsync(cancellationToken);

        response.SetSuccess(MapToDto(existing));
        return response;
    }

    public async Task<BulkUpsertResponse<EventAttendanceDto>> BulkUpsertAsync(
        Guid tenantId,
        Guid eventId,
        BulkUpsertEventAttendanceRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = new BulkUpsertResponse<EventAttendanceDto>();

        if (!ServiceValidationHelper.ValidateTenantContext(tenantId, _currentTenantContext, response))
            return response;
        if (!ServiceGuards.RequireRequest(request, "bulk upsert event attendance", response))
            return response;

        var items = request.Items;
        if (items.Count == 0)
            return (BulkUpsertResponse<EventAttendanceDto>)response.SetSuccess(Array.Empty<EventAttendanceDto>());

        var eventExists = await _dbContext.Events
            .AsNoTracking()
            .AnyAsync(e => e.TenantId == tenantId && e.Id == eventId, cancellationToken);

        if (!eventExists)
        {
            response.AddResponseMessage(MessageType.ERROR, "Event not found.");
            return response;
        }

        var memberOutcomes = await ServiceGuards.ResolveMembersForIntentAsync(
            _memberAuthorizationService, _dbContext, tenantId,
            items.Select(i => i.HouseholdMemberId).ToList(), cancellationToken);

        var memberIds = items.Select(i => i.HouseholdMemberId).Distinct().ToList();
        var existingByKey = (await _dbContext.EventAttendances
            .Where(a => a.TenantId == tenantId && a.EventId == eventId && memberIds.Contains(a.HouseholdMemberId))
            .ToListAsync(cancellationToken))
            .ToDictionary(a => (a.HouseholdMemberId, a.Day));

        var upserted = new List<EventAttendanceEntity>();
        for (var index = 0; index < items.Count; index++)
        {
            var item = items[index];
            if (memberOutcomes.GetValueOrDefault(item.HouseholdMemberId)?.Error is string error)
            {
                response.ItemErrors.Add(new BulkItemError(index, error));
                continue;
            }

            if (!existingByKey.TryGetValue((item.HouseholdMemberId, item.Day), out var existing))
            {
                existing = new EventAttendanceEntity
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    EventId = eventId,
                    HouseholdMemberId = item.HouseholdMemberId,
                    Day = item.Day,
                };
                _dbContext.EventAttendances.Add(existing);
                existingByKey[(item.HouseholdMemberId, item.Day)] = existing;
            }
            else if (existing.IsDeleted)
            {
                existing.IsDeleted = false;
            }

            existing.Status = item.Status;
            existing.Notes = item.Notes?.Trim();
            upserted.Add(existing);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        response.SetSuccess(upserted.Select(MapToDto).ToList());
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

        if (!await ServiceGuards.AuthorizeIntentAssignAsync(response, _memberAuthorizationService, tenantId, Guid.Empty, attendance.HouseholdMemberId, cancellationToken))
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

    private EventAttendanceDto MapToDto(EventAttendanceEntity a) => new(
        a.Id, a.TenantId, a.EventId, a.HouseholdMemberId, a.Day, a.Status,
        a.Notes, a.ToAuditInfo(_auditVisibility.IncludeAudit));
}
