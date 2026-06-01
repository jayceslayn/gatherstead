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

        // Any tenant Member+ may view the report: members occasionally execute the meal prep behind
        // a TaskPlan, and aggregated dietary needs are allergy-safety information. The global
        // sensitive-read scope (Member+) gates the dietary detail and spans all households, which is
        // exactly what this cross-household aggregate requires.
        if (!await ServiceGuards.AuthorizeGlobalSensitiveReadAsync(response, _memberAuthorizationService, tenantId, cancellationToken))
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

        // Resolve DietaryTag slugs to display names so the report shows human-readable labels.
        var allSlugs = members.SelectMany(m => m.DietaryTags).Distinct().ToList();
        var tagNameBySlug = allSlugs.Count > 0
            ? await _dbContext.DietaryTags
                .AsNoTracking()
                .Where(t => allSlugs.Contains(t.Slug) && t.IsActive)
                .ToDictionaryAsync(t => t.Slug, t => t.DisplayName,
                    StringComparer.OrdinalIgnoreCase, cancellationToken)
            : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var memberById = members.ToDictionary(m => m.Id);
        var dietaryByMember = members.ToDictionary(
            m => m.Id,
            m => (IReadOnlyList<string>)m.DietaryTags
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Select(t => tagNameBySlug.TryGetValue(t.Trim(), out var name) ? name : t.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList());

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
                dietaryByMember.GetValueOrDefault(a.HouseholdMemberId, []),
                memberById.GetValueOrDefault(a.HouseholdMemberId)?.DietaryNotes))
            .OrderBy(a => a.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        // Group attendees by their full sorted tag combination so cooks know how many plates
        // must satisfy each specific combination simultaneously (not independent per-label counts).
        var dietary = attendees
            .Select(a => string.Join(", ", a.Dietary
                .Select(d => d.Trim())
                .Where(d => d.Length > 0)
                .OrderBy(d => d, StringComparer.OrdinalIgnoreCase)))
            .GroupBy(combo => combo, StringComparer.OrdinalIgnoreCase)
            .Select(g => new DietaryTallyDto(
                string.IsNullOrEmpty(g.Key) ? "No dietary restrictions" : g.Key,
                g.Count()))
            .OrderByDescending(d => d.Count)
            .ThenBy(d => d.Label, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new EventReportMealDto(
            plan.Id, templateName, plan.MealType,
            going, maybe, notGoing, bringOwnFood,
            dietary, attendees);
    }

}
