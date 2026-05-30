using Gatherstead.Api.Contracts.Reports;
using Gatherstead.Api.Contracts.Responses;
using Gatherstead.Api.Services.Authorization;
using Gatherstead.Api.Services.Validation;
using Gatherstead.Data;
using Gatherstead.Data.Entities;
using Microsoft.EntityFrameworkCore;
using MealAttendanceEntity = Gatherstead.Data.Entities.MealAttendance;

namespace Gatherstead.Api.Services.Reports;

public class EventReportService : IEventReportService
{
    private readonly GathersteadDbContext _dbContext;
    private readonly ICurrentTenantContext _currentTenantContext;
    private readonly IMemberAuthorizationService _memberAuthorizationService;

    public EventReportService(
        GathersteadDbContext dbContext,
        ICurrentTenantContext currentTenantContext,
        IMemberAuthorizationService memberAuthorizationService)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _currentTenantContext = currentTenantContext ?? throw new ArgumentNullException(nameof(currentTenantContext));
        _memberAuthorizationService = memberAuthorizationService ?? throw new ArgumentNullException(nameof(memberAuthorizationService));
    }

    public async Task<EventReportResponse> GetEventMealReportAsync(
        Guid tenantId,
        Guid eventId,
        CancellationToken cancellationToken = default)
    {
        var response = new EventReportResponse();

        if (!ServiceValidationHelper.ValidateTenantContext(tenantId, _currentTenantContext, response))
            return response;

        // Event managers (Coordinator+) may view reports; this also covers sensitive dietary read,
        // since the event-manage role floor (Coordinator) is at or above the sensitive-read floor (Member).
        if (!await ServiceGuards.AuthorizeEventManageAsync(response, _memberAuthorizationService, tenantId, cancellationToken))
            return response;

        var @event = await _dbContext.Events
            .AsNoTracking()
            .Where(e => e.TenantId == tenantId && e.Id == eventId)
            .SingleOrDefaultAsync(cancellationToken);

        if (@event is null)
        {
            response.AddResponseMessage(MessageType.ERROR, "Event not found.");
            return response;
        }

        // Meal templates → plans (non-deleted via global query filters).
        var templateNames = await _dbContext.MealTemplates
            .AsNoTracking()
            .Where(t => t.TenantId == tenantId && t.EventId == eventId)
            .ToDictionaryAsync(t => t.Id, t => t.Name, cancellationToken);

        var templateIds = templateNames.Keys.ToList();

        var plans = await _dbContext.MealPlans
            .AsNoTracking()
            .Where(p => p.TenantId == tenantId && templateIds.Contains(p.MealTemplateId))
            .ToListAsync(cancellationToken);

        var planIds = plans.Select(p => p.Id).ToList();

        var mealAttendances = await _dbContext.MealAttendances
            .AsNoTracking()
            .Where(a => a.TenantId == tenantId && planIds.Contains(a.MealPlanId))
            .ToListAsync(cancellationToken);

        var eventAttendances = await _dbContext.EventAttendances
            .AsNoTracking()
            .Where(a => a.TenantId == tenantId && a.EventId == eventId)
            .ToListAsync(cancellationToken);

        // Members referenced by any attendance, plus their dietary data.
        var memberIds = mealAttendances.Select(a => a.HouseholdMemberId)
            .Concat(eventAttendances.Select(a => a.HouseholdMemberId))
            .Distinct()
            .ToList();

        var members = await _dbContext.HouseholdMembers
            .AsNoTracking()
            .Where(m => m.TenantId == tenantId && memberIds.Contains(m.Id))
            .ToListAsync(cancellationToken);

        var profiles = await _dbContext.DietaryProfiles
            .AsNoTracking()
            .Where(p => p.TenantId == tenantId && memberIds.Contains(p.HouseholdMemberId))
            .ToListAsync(cancellationToken);

        var memberById = members.ToDictionary(m => m.Id);
        var profileByMember = profiles.ToDictionary(p => p.HouseholdMemberId);
        var dietaryByMember = members.ToDictionary(
            m => m.Id,
            m => DietaryLabels(m, profileByMember.GetValueOrDefault(m.Id)));

        var attendancesByPlan = mealAttendances
            .GroupBy(a => a.MealPlanId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var plansByDay = plans
            .GroupBy(p => p.Day)
            .ToDictionary(g => g.Key, g => g.OrderBy(p => p.MealType).ToList());

        var days = new List<EventReportDayDto>();
        for (var day = @event.StartDate; day <= @event.EndDate; day = day.AddDays(1))
        {
            var dayEventAttendance = eventAttendances.Where(a => a.Day == day).ToList();
            var going = dayEventAttendance.Count(a => a.Status == AttendanceStatus.Going);
            var maybe = dayEventAttendance.Count(a => a.Status == AttendanceStatus.Maybe);

            var meals = new List<EventReportMealDto>();
            foreach (var plan in plansByDay.GetValueOrDefault(day, []))
            {
                var planAttendance = attendancesByPlan.GetValueOrDefault(plan.Id, []);
                meals.Add(BuildMeal(plan, templateNames.GetValueOrDefault(plan.MealTemplateId, string.Empty), planAttendance, memberById, dietaryByMember));
            }

            days.Add(new EventReportDayDto(day, going, maybe, meals));
        }

        response.SetSuccess(new EventReportDto(@event.Id, @event.Name, @event.StartDate, @event.EndDate, days));
        return response;
    }

    private static EventReportMealDto BuildMeal(
        MealPlan plan,
        string templateName,
        List<MealAttendanceEntity> attendance,
        IReadOnlyDictionary<Guid, HouseholdMember> memberById,
        IReadOnlyDictionary<Guid, IReadOnlyList<string>> dietaryByMember)
    {
        var going = attendance.Count(a => a.Status == AttendanceStatus.Going);
        var maybe = attendance.Count(a => a.Status == AttendanceStatus.Maybe);
        var notGoing = attendance.Count(a => a.Status == AttendanceStatus.NotGoing);
        var bringOwnFood = attendance.Count(a => a.Status != AttendanceStatus.NotGoing && a.BringOwnFood);

        var attendees = attendance
            .Where(a => a.Status != AttendanceStatus.NotGoing)
            .Select(a => new EventReportAttendeeDto(
                a.HouseholdMemberId,
                memberById.GetValueOrDefault(a.HouseholdMemberId)?.Name ?? string.Empty,
                a.Status,
                a.BringOwnFood,
                dietaryByMember.GetValueOrDefault(a.HouseholdMemberId, [])))
            .OrderBy(a => a.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var dietary = attendees
            .SelectMany(a => a.Dietary)
            .GroupBy(label => label, StringComparer.OrdinalIgnoreCase)
            .Select(g => new DietaryTallyDto(g.Key, g.Count()))
            .OrderByDescending(d => d.Count)
            .ThenBy(d => d.Label, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new EventReportMealDto(
            plan.Id, templateName, plan.MealType,
            going, maybe, notGoing, bringOwnFood,
            dietary, attendees);
    }

    /// <summary>
    /// Combines a member's dietary signals (tags, preferred diet, allergies, restrictions) into a
    /// distinct, normalized label set used for both per-attendee display and per-meal tallies.
    /// </summary>
    private static IReadOnlyList<string> DietaryLabels(HouseholdMember member, DietaryProfile? profile)
    {
        var labels = new List<string>();
        labels.AddRange(member.DietaryTags);
        if (profile is not null)
        {
            if (!string.IsNullOrWhiteSpace(profile.PreferredDiet))
                labels.Add(profile.PreferredDiet);
            labels.AddRange(profile.Allergies);
            labels.AddRange(profile.Restrictions);
        }

        return labels
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .Select(l => l.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
