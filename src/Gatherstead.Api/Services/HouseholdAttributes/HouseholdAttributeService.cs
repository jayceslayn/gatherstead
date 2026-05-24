using Gatherstead.Api.Contracts.HouseholdAttributes;
using Gatherstead.Api.Contracts.Responses;
using Gatherstead.Api.Services.Attributes;
using Gatherstead.Api.Services.Authorization;
using Gatherstead.Api.Services.Validation;
using Gatherstead.Data;
using Gatherstead.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Gatherstead.Api.Services.HouseholdAttributes;

public class HouseholdAttributeService
    : ParentScopedAttributeServiceBase<HouseholdAttribute, HouseholdAttributeDto, CreateHouseholdAttributeRequest, UpdateHouseholdAttributeRequest>,
      IHouseholdAttributeService
{
    public HouseholdAttributeService(
        GathersteadDbContext dbContext,
        ICurrentTenantContext currentTenantContext,
        IMemberAuthorizationService memberAuthorizationService)
        : base(dbContext, currentTenantContext, memberAuthorizationService)
    {
    }

    protected override DbSet<HouseholdAttribute> Set => Db.HouseholdAttributes;
    protected override string ParentDisplayName => "Household";
    protected override string ParentNoun => "household";

    protected override IQueryable<HouseholdAttribute> ByParent(Guid parentId)
        => Set.Where(a => a.HouseholdId == parentId);

    protected override void SetParentFk(HouseholdAttribute entity, Guid parentId)
        => entity.HouseholdId = parentId;

    protected override Task<bool> ParentExistsAsync(Guid tenantId, Guid parentId, CancellationToken cancellationToken)
        => Db.Households.AsNoTracking().AnyAsync(h => h.Id == parentId, cancellationToken);

    protected override Task<bool> AuthorizeWriteAsync<T>(
        BaseEntityResponse<T> response,
        Guid tenantId,
        Guid parentId,
        CancellationToken cancellationToken)
        => ServiceGuards.AuthorizeHouseholdManageAsync(
            response, Auth, tenantId, parentId,
            "You do not have permission to manage this household.",
            cancellationToken);

    protected override Task<HouseholdRole?> GetCallerHouseholdRoleAsync(
        Guid tenantId, Guid parentId, CancellationToken cancellationToken)
        => Auth.GetCallerHouseholdRoleAsync(tenantId, parentId, cancellationToken);

    protected override bool IsVisible(
        HouseholdAttribute entity, TenantRole? callerTenantRole, HouseholdRole? callerHouseholdRole)
        => base.IsVisible(entity, callerTenantRole, callerHouseholdRole)
        || (entity.HouseholdMinRole.HasValue
            && callerHouseholdRole.HasValue
            && callerHouseholdRole.Value <= (HouseholdRole)entity.HouseholdMinRole.Value);

    protected override void ApplyExtraCreateFields(HouseholdAttribute entity, CreateHouseholdAttributeRequest request)
        => entity.HouseholdMinRole = request.HouseholdMinRole;

    protected override void ApplyExtraUpdateFields(HouseholdAttribute entity, UpdateHouseholdAttributeRequest request)
        => entity.HouseholdMinRole = request.HouseholdMinRole;

    protected override HouseholdAttributeDto MapToDto(HouseholdAttribute attr) => new(
        attr.Id,
        attr.TenantId,
        attr.HouseholdId,
        attr.Key,
        attr.Value,
        attr.TenantMinRole,
        attr.HouseholdMinRole,
        attr.CreatedAt,
        attr.UpdatedAt,
        attr.IsDeleted,
        attr.DeletedAt,
        attr.DeletedByUserId);
}
