using Gatherstead.Api.Contracts.EquipmentAttributes;
using Gatherstead.Api.Contracts.Responses;
using Gatherstead.Api.Services.Attributes;
using Gatherstead.Api.Services.Authorization;
using Gatherstead.Api.Services.Validation;
using Gatherstead.Data;
using Gatherstead.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Gatherstead.Api.Services.EquipmentAttributes;

public class EquipmentAttributeService
    : ParentScopedAttributeServiceBase<EquipmentAttribute, EquipmentAttributeDto, CreateEquipmentAttributeRequest, UpdateEquipmentAttributeRequest>,
      IEquipmentAttributeService
{
    public EquipmentAttributeService(
        GathersteadDbContext dbContext,
        ICurrentTenantContext currentTenantContext,
        IMemberAuthorizationService memberAuthorizationService)
        : base(dbContext, currentTenantContext, memberAuthorizationService)
    {
    }

    protected override DbSet<EquipmentAttribute> Set => Db.EquipmentAttributes;
    protected override string ParentDisplayName => "Equipment";
    protected override string ParentNoun => "equipment";

    protected override IQueryable<EquipmentAttribute> ByParent(Guid parentId)
        => Set.Where(a => a.EquipmentId == parentId);

    protected override void SetParentFk(EquipmentAttribute entity, Guid parentId)
        => entity.EquipmentId = parentId;

    protected override Task<bool> ParentExistsAsync(Guid tenantId, Guid parentId, CancellationToken cancellationToken)
        => Db.Equipment.AsNoTracking().AnyAsync(e => e.Id == parentId, cancellationToken);

    protected override Task<bool> AuthorizeWriteAsync<T>(
        BaseEntityResponse<T> response,
        Guid tenantId,
        Guid parentId,
        CancellationToken cancellationToken)
        => ServiceGuards.AuthorizeTenantManageAsync(response, Auth, tenantId, cancellationToken);

    protected override EquipmentAttributeDto MapToDto(EquipmentAttribute attr) => new(
        attr.Id,
        attr.TenantId,
        attr.EquipmentId,
        attr.Key,
        attr.Value,
        attr.TenantMinRole,
        attr.CreatedAt,
        attr.UpdatedAt,
        attr.IsDeleted,
        attr.DeletedAt,
        attr.DeletedByUserId);
}
