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
        var taskTemplates = await _dbContext.TaskTemplates
            .AsNoTracking()
            .Where(t => t.TenantId == tenantId && t.EventId == @event.Id)
            .ToListAsync(cancellationToken);

        foreach (var template in taskTemplates)
            await ApplyTaskPlanDiffAsync(tenantId, template, @event.StartDate, @event.EndDate, cancellationToken);

        var mealTemplates = await _dbContext.MealTemplates
            .AsNoTracking()
            .Where(t => t.TenantId == tenantId && t.EventId == @event.Id)
            .ToListAsync(cancellationToken);

        foreach (var template in mealTemplates)
            await ApplyMealPlanDiffAsync(tenantId, template, @event.StartDate, @event.EndDate, cancellationToken);
    }

    public async Task SyncTaskPlanAsync(Guid tenantId, TaskTemplate template, DateOnly start, DateOnly end, CancellationToken cancellationToken)
    {
        await ApplyTaskPlanDiffAsync(tenantId, template, start, end, cancellationToken);
    }

    public async Task SyncMealPlanAsync(Guid tenantId, MealTemplate template, DateOnly start, DateOnly end, CancellationToken cancellationToken)
    {
        await ApplyMealPlanDiffAsync(tenantId, template, start, end, cancellationToken);
    }

    private async Task ApplyTaskPlanDiffAsync(
        Guid tenantId,
        TaskTemplate template,
        DateOnly start,
        DateOnly end,
        CancellationToken cancellationToken)
    {
        // Bypass only the soft-delete filter so PlanGenerator can detect suppression markers
        // (IsDeleted && IsException) and avoid re-generating them. Tenant isolation stays enforced.
        var existing = await _dbContext.TaskPlans
            .IgnoreQueryFilters([GathersteadDbContext.SoftDeleteFilter])
            .Include(p => p.Intents)
            .Where(p => p.TenantId == tenantId && p.TemplateId == template.Id)
            .ToListAsync(cancellationToken);

        var diff = PlanGenerator.DiffTaskPlans(template.TimeSlots, start, end, existing, template.StartDate, template.EndDate);

        foreach (var (day, slot) in diff.ToAdd)
        {
            _dbContext.TaskPlans.Add(new TaskPlan
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
        // Bypass only the soft-delete filter (see ApplyTaskPlanDiffAsync); tenant isolation stays enforced.
        var existing = await _dbContext.MealPlans
            .IgnoreQueryFilters([GathersteadDbContext.SoftDeleteFilter])
            .Include(p => p.Intents)
            .Where(p => p.TenantId == tenantId && p.MealTemplateId == template.Id)
            .ToListAsync(cancellationToken);

        var diff = PlanGenerator.DiffMealPlans(template.MealTypes, start, end, existing, template.StartDate, template.EndDate);

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

        // A pruned meal plan takes its menu's shopping items with it, so the merged list never
        // references a day/meal that no longer exists.
        var prunedPlanIds = diff.ToPrune.Select(p => p.Id).ToList();
        if (prunedPlanIds.Count > 0)
        {
            var orphanedItems = await _dbContext.ShoppingItems
                .Where(i => i.TenantId == tenantId
                    && i.MealPlanId != null
                    && prunedPlanIds.Contains(i.MealPlanId.Value)
                    && !i.IsDeleted)
                .ToListAsync(cancellationToken);
            foreach (var item in orphanedItems)
                item.IsDeleted = true;
        }
    }
}
