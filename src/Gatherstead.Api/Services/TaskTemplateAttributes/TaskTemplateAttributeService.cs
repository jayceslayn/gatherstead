using Gatherstead.Api.Contracts.TaskTemplateAttributes;
using Gatherstead.Api.Contracts.Responses;
using Gatherstead.Api.Services.Attributes;
using Gatherstead.Api.Services.Authorization;
using Gatherstead.Api.Services.Validation;
using Gatherstead.Data;
using Gatherstead.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Gatherstead.Api.Services.TaskTemplateAttributes;

public class TaskTemplateAttributeService
    : ParentScopedAttributeServiceBase<TaskTemplateAttribute, TaskTemplateAttributeDto, CreateTaskTemplateAttributeRequest, UpdateTaskTemplateAttributeRequest>,
      ITaskTemplateAttributeService
{
    public TaskTemplateAttributeService(
        GathersteadDbContext dbContext,
        ICurrentTenantContext currentTenantContext,
        IMemberAuthorizationService memberAuthorizationService)
        : base(dbContext, currentTenantContext, memberAuthorizationService)
    {
    }

    protected override DbSet<TaskTemplateAttribute> Set => Db.TaskTemplateAttributes;
    protected override string ParentDisplayName => "Task template";
    protected override string ParentNoun => "task template";

    protected override IQueryable<TaskTemplateAttribute> ByParent(Guid parentId)
        => Set.Where(a => a.TaskTemplateId == parentId);

    protected override void SetParentFk(TaskTemplateAttribute entity, Guid parentId)
        => entity.TaskTemplateId = parentId;

    protected override Task<bool> ParentExistsAsync(Guid tenantId, Guid parentId, CancellationToken cancellationToken)
        => Db.TaskTemplates.AsNoTracking().AnyAsync(t => t.Id == parentId, cancellationToken);

    protected override Task<bool> AuthorizeWriteAsync<T>(
        BaseEntityResponse<T> response,
        Guid tenantId,
        Guid parentId,
        CancellationToken cancellationToken)
        => ServiceGuards.AuthorizeEventManageAsync(response, Auth, tenantId, cancellationToken);

    protected override TaskTemplateAttributeDto MapToDto(TaskTemplateAttribute attr) => new(
        attr.Id,
        attr.TenantId,
        attr.TaskTemplateId,
        attr.Key,
        attr.Value,
        attr.TenantMinRole,
        attr.CreatedAt,
        attr.UpdatedAt,
        attr.IsDeleted,
        attr.DeletedAt,
        attr.DeletedByUserId);
}
