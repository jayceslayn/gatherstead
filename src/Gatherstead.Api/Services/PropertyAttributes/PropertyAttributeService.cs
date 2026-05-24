using Gatherstead.Api.Contracts.PropertyAttributes;
using Gatherstead.Api.Contracts.Responses;
using Gatherstead.Api.Services.Attributes;
using Gatherstead.Api.Services.Authorization;
using Gatherstead.Api.Services.Validation;
using Gatherstead.Data;
using Gatherstead.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Gatherstead.Api.Services.PropertyAttributes;

public class PropertyAttributeService
    : ParentScopedAttributeServiceBase<PropertyAttribute, PropertyAttributeDto, CreatePropertyAttributeRequest, UpdatePropertyAttributeRequest>,
      IPropertyAttributeService
{
    public PropertyAttributeService(
        GathersteadDbContext dbContext,
        ICurrentTenantContext currentTenantContext,
        IMemberAuthorizationService memberAuthorizationService)
        : base(dbContext, currentTenantContext, memberAuthorizationService)
    {
    }

    protected override DbSet<PropertyAttribute> Set => Db.PropertyAttributes;
    protected override string ParentDisplayName => "Property";
    protected override string ParentNoun => "property";

    protected override IQueryable<PropertyAttribute> ByParent(Guid parentId)
        => Set.Where(a => a.PropertyId == parentId);

    protected override void SetParentFk(PropertyAttribute entity, Guid parentId)
        => entity.PropertyId = parentId;

    protected override Task<bool> ParentExistsAsync(Guid tenantId, Guid parentId, CancellationToken cancellationToken)
        => Db.Properties.AsNoTracking().AnyAsync(p => p.Id == parentId, cancellationToken);

    protected override Task<bool> AuthorizeWriteAsync<T>(
        BaseEntityResponse<T> response,
        Guid tenantId,
        Guid parentId,
        CancellationToken cancellationToken)
        => ServiceGuards.AuthorizeTenantManageAsync(response, Auth, tenantId, cancellationToken);

    protected override PropertyAttributeDto MapToDto(PropertyAttribute attr) => new(
        attr.Id,
        attr.TenantId,
        attr.PropertyId,
        attr.Key,
        attr.Value,
        attr.TenantMinRole,
        attr.CreatedAt,
        attr.UpdatedAt,
        attr.IsDeleted,
        attr.DeletedAt,
        attr.DeletedByUserId);
}
