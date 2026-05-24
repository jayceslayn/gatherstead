using Gatherstead.Api.Contracts.EventAttributes;
using Gatherstead.Api.Contracts.Responses;
using Gatherstead.Api.Services.Attributes;
using Gatherstead.Api.Services.Authorization;
using Gatherstead.Api.Services.Validation;
using Gatherstead.Data;
using Gatherstead.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Gatherstead.Api.Services.EventAttributes;

public class EventAttributeService
    : ParentScopedAttributeServiceBase<EventAttribute, EventAttributeDto, CreateEventAttributeRequest, UpdateEventAttributeRequest>,
      IEventAttributeService
{
    public EventAttributeService(
        GathersteadDbContext dbContext,
        ICurrentTenantContext currentTenantContext,
        IMemberAuthorizationService memberAuthorizationService)
        : base(dbContext, currentTenantContext, memberAuthorizationService)
    {
    }

    protected override DbSet<EventAttribute> Set => Db.EventAttributes;
    protected override string ParentDisplayName => "Event";
    protected override string ParentNoun => "event";

    protected override IQueryable<EventAttribute> ByParent(Guid parentId)
        => Set.Where(a => a.EventId == parentId);

    protected override void SetParentFk(EventAttribute entity, Guid parentId)
        => entity.EventId = parentId;

    protected override Task<bool> ParentExistsAsync(Guid tenantId, Guid parentId, CancellationToken cancellationToken)
        => Db.Events.AsNoTracking().AnyAsync(e => e.Id == parentId, cancellationToken);

    protected override Task<bool> AuthorizeWriteAsync<T>(
        BaseEntityResponse<T> response,
        Guid tenantId,
        Guid parentId,
        CancellationToken cancellationToken)
        => ServiceGuards.AuthorizeEventManageAsync(response, Auth, tenantId, cancellationToken);

    protected override EventAttributeDto MapToDto(EventAttribute attr) => new(
        attr.Id,
        attr.TenantId,
        attr.EventId,
        attr.Key,
        attr.Value,
        attr.TenantMinRole,
        attr.CreatedAt,
        attr.UpdatedAt,
        attr.IsDeleted,
        attr.DeletedAt,
        attr.DeletedByUserId);
}
