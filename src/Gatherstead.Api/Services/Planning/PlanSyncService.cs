using Gatherstead.Data;
using Gatherstead.Data.Entities;
using Gatherstead.Data.Planning;
using Microsoft.EntityFrameworkCore;

namespace Gatherstead.Api.Services.Planning;

public class PlanSyncService
{
    private readonly GathersteadDbContext _dbContext;

    public PlanSyncService(GathersteadDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task SyncEventPlansAsync(Guid tenantId, Event @event, CancellationToken cancellationToken)
    {
        var choreTemplates = await _dbContext.ChoreTemplates
            .AsNoTracking()
            .Where(t => t.TenantId == tenantId && t.EventId == @event.Id)
            .ToListAsync(cancellationToken);

        foreach (var template in choreTemplates)
            await ApplyChorePlanDiffAsync(tenantId, template, @event.StartDate, @event.EndDate, cancellationToken);

        var mealTemplates = await _dbContext.MealTemplates
            .AsNoTracking()
            .Where(t => t.TenantId == tenantId && t.EventId == @event.Id)
            .ToListAsync(cancellationToken);

        foreach (var template in mealTemplates)
            await ApplyMealPlanDiffAsync(tenantId, template, @event.StartDate, @event.EndDate, cancellationToken);
    }

    public async Task SyncChorePlanAsync(Guid tenantId, ChoreTemplate template, DateOnly start, DateOnly end, CancellationToken cancellationToken)
    {
        await ApplyChorePlanDiffAsync(tenantId, template, start, end, cancellationToken);
    }

    public async Task SyncMealPlanAsync(Guid tenantId, MealTemplate template, DateOnly start, DateOnly end, CancellationToken cancellationToken)
    {
        await ApplyMealPlanDiffAsync(tenantId, template, start, end, cancellationToken);
    }

    private async Task ApplyChorePlanDiffAsync(
        Guid tenantId,
        ChoreTemplate template,
        DateOnly start,
        DateOnly end,
        CancellationToken cancellationToken)
    {
        // IgnoreQueryFilters() includes soft-deleted rows so PlanGenerator can detect
        // suppression markers (IsDeleted && IsException) and avoid re-generating them.
        var existing = await _dbContext.ChorePlans
            .IgnoreQueryFilters()
            .Include(p => p.Intents)
            .Where(p => p.TenantId == tenantId && p.TemplateId == template.Id)
            .ToListAsync(cancellationToken);

        var diff = PlanGenerator.DiffChorePlans(template.TimeSlots, start, end, existing);

        foreach (var (day, slot) in diff.ToAdd)
        {
            _dbContext.ChorePlans.Add(new ChorePlan
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                TemplateId = template.Id,
                Day = day,
                TimeSlot = slot,
                Completed = false,
            });
        }

        foreach (var plan in diff.ToRestore)
            plan.IsDeleted = false;

        foreach (var plan in diff.ToPrune)
            plan.IsDeleted = true;
    }

    private async Task ApplyMealPlanDiffAsync(
        Guid tenantId,
        MealTemplate template,
        DateOnly start,
        DateOnly end,
        CancellationToken cancellationToken)
    {
        var existing = await _dbContext.MealPlans
            .IgnoreQueryFilters()
            .Include(p => p.Intents)
            .Where(p => p.TenantId == tenantId && p.MealTemplateId == template.Id)
            .ToListAsync(cancellationToken);

        var diff = PlanGenerator.DiffMealPlans(template.MealTypes, start, end, existing);

        foreach (var (day, mealType) in diff.ToAdd)
        {
            _dbContext.MealPlans.Add(new MealPlan
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                MealTemplateId = template.Id,
                Day = day,
                MealType = mealType,
            });
        }

        foreach (var plan in diff.ToRestore)
            plan.IsDeleted = false;

        foreach (var plan in diff.ToPrune)
            plan.IsDeleted = true;
    }
}
