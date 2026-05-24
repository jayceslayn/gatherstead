using Gatherstead.Api.Contracts.AccommodationAttributes;
using Gatherstead.Api.Contracts.Responses;
using Gatherstead.Api.Services.Attributes;
using Gatherstead.Api.Services.Authorization;
using Gatherstead.Api.Services.Validation;
using Gatherstead.Data;
using Gatherstead.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Gatherstead.Api.Services.AccommodationAttributes;

public class AccommodationAttributeService
    : ParentScopedAttributeServiceBase<AccommodationAttribute, AccommodationAttributeDto, CreateAccommodationAttributeRequest, UpdateAccommodationAttributeRequest>,
      IAccommodationAttributeService
{
    public AccommodationAttributeService(
        GathersteadDbContext dbContext,
        ICurrentTenantContext currentTenantContext,
        IMemberAuthorizationService memberAuthorizationService)
        : base(dbContext, currentTenantContext, memberAuthorizationService)
    {
    }

    protected override DbSet<AccommodationAttribute> Set => Db.AccommodationAttributes;
    protected override string ParentDisplayName => "Accommodation";
    protected override string ParentNoun => "accommodation";

    protected override IQueryable<AccommodationAttribute> ByParent(Guid parentId)
        => Set.Where(a => a.AccommodationId == parentId);

    protected override void SetParentFk(AccommodationAttribute entity, Guid parentId)
        => entity.AccommodationId = parentId;

    protected override Task<bool> ParentExistsAsync(Guid tenantId, Guid parentId, CancellationToken cancellationToken)
        => Db.Accommodations.AsNoTracking().AnyAsync(a => a.Id == parentId, cancellationToken);

    protected override Task<bool> AuthorizeWriteAsync<T>(
        BaseEntityResponse<T> response,
        Guid tenantId,
        Guid parentId,
        CancellationToken cancellationToken)
        => ServiceGuards.AuthorizeTenantManageAsync(response, Auth, tenantId, cancellationToken);

    protected override AccommodationAttributeDto MapToDto(AccommodationAttribute attr) => new(
        attr.Id,
        attr.TenantId,
        attr.AccommodationId,
        attr.Key,
        attr.Value,
        attr.TenantMinRole,
        attr.CreatedAt,
        attr.UpdatedAt,
        attr.IsDeleted,
        attr.DeletedAt,
        attr.DeletedByUserId);
}
