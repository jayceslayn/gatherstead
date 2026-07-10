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

        // Task templates → plans → intents (non-deleted via global query filters).
        var taskTemplates = await _dbContext.TaskTemplates
            .AsNoTracking()
            .Where(t => t.TenantId == tenantId && t.EventId == eventId)
            .ToListAsync(cancellationToken);

        var taskTemplateIds = taskTemplates.Select(t => t.Id).ToList();

        var taskPlans = await _dbContext.TaskPlans
            .AsNoTracking()
            .Where(p => p.TenantId == tenantId && taskTemplateIds.Contains(p.TemplateId))
            .ToListAsync(cancellationToken);

        var taskPlanIds = taskPlans.Select(p => p.Id).ToList();

        var taskIntents = await _dbContext.TaskIntents
            .AsNoTracking()
            .Where(i => i.TenantId == tenantId && taskPlanIds.Contains(i.TaskPlanId))
            .ToListAsync(cancellationToken);

        // Accommodations for the event's property → intents on the event's nights.
        var accommodations = await _dbContext.Accommodations
            .AsNoTracking()
            .Include(a => a.Beds)
            .Where(a => a.TenantId == tenantId && a.PropertyId == @event.PropertyId)
            .ToListAsync(cancellationToken);

        var accommodationIds = accommodations.Select(a => a.Id).ToList();

        // A stay is a [StartNight, EndNight] span; pull every stay whose span overlaps the event so
        // each event day can be filled by date-range overlap below.
        var accommodationIntents = await _dbContext.AccommodationIntents
            .AsNoTracking()
            .Where(i => i.TenantId == tenantId
                && accommodationIds.Contains(i.AccommodationId)
                && i.StartNight <= @event.EndDate
                && i.EndNight >= @event.StartDate)
            .ToListAsync(cancellationToken);

        // Members referenced by any attendance, task intent, or accommodation intent, plus
        // their dietary data — resolved once so assignee/occupant names need no extra query.
        var memberIds = mealAttendances.Select(a => a.HouseholdMemberId)
            .Concat(eventAttendances.Select(a => a.HouseholdMemberId))
            .Concat(taskIntents.Select(i => i.HouseholdMemberId))
            .Concat(accommodationIntents.Select(i => i.HouseholdMemberId))
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

        // Household name per member, so report occupant/attendee lists group same-household people
        // together (ordered by household name, then member name) rather than scattering them.
        var householdIds = members.Select(m => m.HouseholdId).Distinct().ToList();
        var householdNameById = await _dbContext.Households
            .AsNoTracking()
            .Where(h => h.TenantId == tenantId && householdIds.Contains(h.Id))
            .ToDictionaryAsync(h => h.Id, h => h.Name, cancellationToken);
        var householdNameByMemberId = members.ToDictionary(
            m => m.Id,
            m => householdNameById.GetValueOrDefault(m.HouseholdId, string.Empty));

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
            .ToDictionary(g => g.Key, g => g.ToList());

        // Per (template, slot) ordering aggregates — earliest non-exception ("effective")
        // plan day and the count of effective plans — so the report orders meal/task lanes
        // identically to the sign-up and management views.
        var mealOrderKey = plans
            .GroupBy(p => (p.MealTemplateId, p.MealType))
            .ToDictionary(g => g.Key, g => OrderAggregate(g, p => p.IsException, p => p.Day));

        var taskTemplateById = taskTemplates.ToDictionary(t => t.Id);
        var taskPlansByDay = taskPlans
            .GroupBy(p => p.Day)
            .ToDictionary(g => g.Key, g => g.ToList());

        var taskOrderKey = taskPlans
            .GroupBy(p => (p.TemplateId, p.TimeSlot))
            .ToDictionary(g => g.Key, g => OrderAggregate(g, p => p.IsException, p => p.Day));
        var taskIntentsByPlan = taskIntents
            .GroupBy(i => i.TaskPlanId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var accommodationIntentsByAccommodation = accommodationIntents
            .GroupBy(i => i.AccommodationId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var days = new List<EventReportDayDto>();
        for (var day = @event.StartDate; day <= @event.EndDate; day = day.AddDays(1))
        {
            var dayEventAttendance = eventAttendances.Where(a => a.Day == day).ToList();
            var going = dayEventAttendance.Count(a => a.Status == AttendanceStatus.Going);
            var maybe = dayEventAttendance.Count(a => a.Status == AttendanceStatus.Maybe);

            // Who is there that day, grouped for the report by household (household name,
            // then member name — same convention as attendee/occupant lists).
            var dayAttendees = dayEventAttendance
                .Where(a => a.Status != AttendanceStatus.NotGoing)
                .Select(a => new EventReportDayAttendeeDto(
                    a.HouseholdMemberId,
                    memberById.GetValueOrDefault(a.HouseholdMemberId)?.Name ?? string.Empty,
                    a.Status,
                    memberById.GetValueOrDefault(a.HouseholdMemberId)?.HouseholdId ?? Guid.Empty,
                    householdNameByMemberId.GetValueOrDefault(a.HouseholdMemberId, string.Empty)))
                .OrderBy(a => a.HouseholdName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(a => a.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            // Slot order (Breakfast→Lunch→Dinner), then the template with the earliest effective
            // plan, then the template with more effective plans, then name.
            var meals = plansByDay.GetValueOrDefault(day, [])
                .OrderBy(p => p.MealType)
                .ThenBy(p => mealOrderKey[(p.MealTemplateId, p.MealType)].FirstDay)
                .ThenByDescending(p => mealOrderKey[(p.MealTemplateId, p.MealType)].EffectiveCount)
                .ThenBy(p => templateNames.GetValueOrDefault(p.MealTemplateId, string.Empty), StringComparer.OrdinalIgnoreCase)
                .Select(p => BuildMeal(p, templateNames.GetValueOrDefault(p.MealTemplateId, string.Empty), attendancesByPlan.GetValueOrDefault(p.Id, []), memberById, dietaryByMember, householdNameByMemberId))
                .ToList();

            // Timed slots lead in order, then "Anytime"/unslotted; within a slot, the template
            // with the earliest effective plan, then more effective plans, then name.
            var tasks = taskPlansByDay.GetValueOrDefault(day, [])
                .OrderBy(p => TaskSlotRank(p.TimeSlot))
                .ThenBy(p => taskOrderKey[(p.TemplateId, p.TimeSlot)].FirstDay)
                .ThenByDescending(p => taskOrderKey[(p.TemplateId, p.TimeSlot)].EffectiveCount)
                .ThenBy(p => taskTemplateById.GetValueOrDefault(p.TemplateId)?.Name ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .Select(p => BuildTask(p, taskTemplateById.GetValueOrDefault(p.TemplateId), taskIntentsByPlan.GetValueOrDefault(p.Id, []), memberById))
                .ToList();

            // Emit every accommodation each day (occupied 0 when empty) so the occupancy badge
            // renders on all days; occupants are the stays whose span covers this night.
            var dayAccommodations = new List<EventReportAccommodationDto>();
            foreach (var accommodation in accommodations
                .OrderBy(a => a.Type)
                .ThenBy(a => a.Name, StringComparer.OrdinalIgnoreCase))
            {
                var nightIntents = accommodationIntentsByAccommodation.GetValueOrDefault(accommodation.Id, [])
                    .Where(i => i.StartNight <= day && i.EndNight >= day)
                    .ToList();
                dayAccommodations.Add(BuildAccommodation(accommodation, nightIntents, memberById, householdNameByMemberId));
            }

            days.Add(new EventReportDayDto(day, going, maybe, dayAttendees, meals, tasks, dayAccommodations));
        }

        response.SetSuccess(new EventReportDto(@event.Id, @event.Name, @event.StartDate, @event.EndDate, days));
        return response;
    }

    private static EventReportMealDto BuildMeal(
        MealPlan plan,
        string templateName,
        List<MealAttendanceEntity> attendance,
        IReadOnlyDictionary<Guid, HouseholdMember> memberById,
        IReadOnlyDictionary<Guid, IReadOnlyList<string>> dietaryByMember,
        IReadOnlyDictionary<Guid, string> householdNameByMemberId)
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
            .OrderBy(a => householdNameByMemberId.GetValueOrDefault(a.MemberId, string.Empty), StringComparer.OrdinalIgnoreCase)
            .ThenBy(a => a.Name, StringComparer.OrdinalIgnoreCase)
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
            plan.Id, plan.MealTemplateId, templateName, plan.MealType, plan.IsException,
            going, maybe, notGoing, bringOwnFood,
            dietary, attendees);
    }

    // Earliest "effective" (non-exception) plan day and the count of effective plans for a
    // (template, slot) group; falls back to all plans when the group is exception-only so it
    // still sorts deterministically.
    private static (DateOnly FirstDay, int EffectiveCount) OrderAggregate<T>(
        IEnumerable<T> plans, Func<T, bool> isException, Func<T, DateOnly> day)
    {
        var list = plans.ToList();
        var effective = list.Where(p => !isException(p)).ToList();
        var source = effective.Count > 0 ? effective : list;
        return (source.Min(day), effective.Count);
    }

    // Timed slots lead in their natural Morning → Midday → Evening order; "Anytime" (and
    // unslotted) tasks sort last so the day's scheduled work reads top-to-bottom.
    private static int TaskSlotRank(TaskTimeSlot? slot) => slot switch
    {
        null or TaskTimeSlot.Anytime => (int)TaskTimeSlot.Anytime,
        _ => (int)slot,
    };

    private static EventReportTaskDto BuildTask(
        TaskPlan plan,
        TaskTemplate? template,
        List<TaskIntent> intents,
        IReadOnlyDictionary<Guid, HouseholdMember> memberById)
    {
        // An intent row IS the assignment; Volunteered only distinguishes self-signup from
        // manager assignment and is not a coverage signal, so every intent counts toward coverage.
        var assignees = intents
            .Select(i => memberById.GetValueOrDefault(i.HouseholdMemberId)?.Name ?? string.Empty)
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new EventReportTaskDto(
            plan.Id,
            plan.TemplateId,
            template?.Name ?? string.Empty,
            plan.TimeSlot,
            assignees.Count,
            template?.MinimumAssignees,
            plan.Completed,
            plan.IsException,
            plan.ExceptionReason,
            assignees);
    }

    private static EventReportAccommodationDto BuildAccommodation(
        Accommodation accommodation,
        List<AccommodationIntent> nightIntents,
        IReadOnlyDictionary<Guid, HouseholdMember> memberById,
        IReadOnlyDictionary<Guid, string> householdNameByMemberId)
    {
        // A declined request frees the slot, so it neither occupies nor appears as an occupant.
        var occupants = nightIntents
            .Where(i => i.Status != AccommodationIntentStatus.Declined)
            .Select(i => new EventReportOccupantDto(
                i.HouseholdMemberId,
                memberById.GetValueOrDefault(i.HouseholdMemberId)?.Name ?? string.Empty,
                i.Status,
                i.PartyAdults,
                i.PartyChildren))
            .OrderBy(o => householdNameByMemberId.GetValueOrDefault(o.MemberId, string.Empty), StringComparer.OrdinalIgnoreCase)
            .ThenBy(o => o.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        // A party with no adults/children counts still occupies one slot (the requesting member).
        var occupied = occupants.Sum(o => Math.Max((o.PartyAdults ?? 0) + (o.PartyChildren ?? 0), 1));

        // Sleeps capacity from the bed inventory; null when no beds are recorded (unconstrained).
        int? capacity = BedSizes.SleepsCapacity(accommodation.Beds.Select(b => (b.Size, b.Quantity)));

        return new EventReportAccommodationDto(
            accommodation.Id,
            accommodation.Name,
            accommodation.Type,
            capacity,
            accommodation.Notes,
            occupied,
            occupants);
    }

}
