using Gatherstead.Api.Contracts.MealTemplateAttributes;
using Gatherstead.Api.Contracts.Responses;
using Gatherstead.Api.Services.Attributes;
using Gatherstead.Api.Services.Authorization;
using Gatherstead.Api.Services.Validation;
using Gatherstead.Data;
using Gatherstead.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Gatherstead.Api.Services.MealTemplateAttributes;

public class MealTemplateAttributeService
    : ParentScopedAttributeServiceBase<MealTemplateAttribute, MealTemplateAttributeDto, CreateMealTemplateAttributeRequest, UpdateMealTemplateAttributeRequest>,
      IMealTemplateAttributeService
{
    public MealTemplateAttributeService(
        GathersteadDbContext dbContext,
        ICurrentTenantContext currentTenantContext,
        IMemberAuthorizationService memberAuthorizationService)
        : base(dbContext, currentTenantContext, memberAuthorizationService)
    {
    }

    protected override DbSet<MealTemplateAttribute> Set => Db.MealTemplateAttributes;
    protected override string ParentDisplayName => "Meal template";
    protected override string ParentNoun => "meal template";

    protected override IQueryable<MealTemplateAttribute> ByParent(Guid parentId)
        => Set.Where(a => a.MealTemplateId == parentId);

    protected override void SetParentFk(MealTemplateAttribute entity, Guid parentId)
        => entity.MealTemplateId = parentId;

    protected override Task<bool> ParentExistsAsync(Guid tenantId, Guid parentId, CancellationToken cancellationToken)
        => Db.MealTemplates.AsNoTracking().AnyAsync(m => m.Id == parentId, cancellationToken);

    protected override Task<bool> AuthorizeWriteAsync<T>(
        BaseEntityResponse<T> response,
        Guid tenantId,
        Guid parentId,
        CancellationToken cancellationToken)
        => ServiceGuards.AuthorizeEventManageAsync(response, Auth, tenantId, cancellationToken);

    protected override MealTemplateAttributeDto MapToDto(MealTemplateAttribute attr) => new(
        attr.Id,
        attr.TenantId,
        attr.MealTemplateId,
        attr.Key,
        attr.Value,
        attr.TenantMinRole,
        attr.CreatedAt,
        attr.UpdatedAt,
        attr.IsDeleted,
        attr.DeletedAt,
        attr.DeletedByUserId);
}
