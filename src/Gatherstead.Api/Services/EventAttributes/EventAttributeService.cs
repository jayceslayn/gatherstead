using Gatherstead.Api.Contracts.EventAttributes;
using Gatherstead.Api.Observability;
using Gatherstead.Api.Contracts.Responses;
using Gatherstead.Api.Services.Authorization;
using Gatherstead.Api.Services.Validation;
using Gatherstead.Data;
using Gatherstead.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Gatherstead.Api.Services.EventAttributes;

public class EventAttributeService : IEventAttributeService
{
    private const string EntityDisplayName = "Event attribute";

    private readonly GathersteadDbContext _dbContext;
    private readonly ICurrentTenantContext _currentTenantContext;
    private readonly IMemberAuthorizationService _memberAuthorizationService;

    public EventAttributeService(
        GathersteadDbContext dbContext,
        ICurrentTenantContext currentTenantContext,
        IMemberAuthorizationService memberAuthorizationService)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _currentTenantContext = currentTenantContext ?? throw new ArgumentNullException(nameof(currentTenantContext));
        _memberAuthorizationService = memberAuthorizationService ?? throw new ArgumentNullException(nameof(memberAuthorizationService));
    }

    public async Task<BaseEntityResponse<IReadOnlyCollection<EventAttributeDto>>> ListAsync(
        Guid tenantId,
        Guid eventId,
        IEnumerable<Guid>? ids = null,
        CancellationToken cancellationToken = default)
    {
        var response = new BaseEntityResponse<IReadOnlyCollection<EventAttributeDto>>();

        if (!ServiceValidationHelper.ValidateTenantContext(tenantId, _currentTenantContext, response))
            return response;

        var callerTenantRole = await _memberAuthorizationService.GetCallerTenantRoleAsync(tenantId, cancellationToken);

        var query = _dbContext.EventAttributes
            .AsNoTracking()
            .Where(a => a.TenantId == tenantId && a.EventId == eventId);

        if (ids is not null)
        {
            var idList = ids.ToList();
            if (idList.Count > 0)
                query = query.Where(a => idList.Contains(a.Id));
        }

        var attributes = await query.ToListAsync(cancellationToken);

        var visible = attributes
            .Where(a => IsVisible(a.TenantMinRole, callerTenantRole))
            .Select(MapToDto)
            .ToList();

        return BaseEntityResponse<IReadOnlyCollection<EventAttributeDto>>.SuccessfulResponse(visible);
    }

    public async Task<EventAttributeResponse> GetAsync(
        Guid tenantId,
        Guid eventId,
        Guid attributeId,
        CancellationToken cancellationToken = default)
    {
        var response = new EventAttributeResponse();

        if (!ServiceValidationHelper.ValidateTenantContext(tenantId, _currentTenantContext, response))
            return response;

        var callerTenantRole = await _memberAuthorizationService.GetCallerTenantRoleAsync(tenantId, cancellationToken);

        var attribute = await ServiceGuards.LoadOrNotFoundAsync(
            response,
            _dbContext.EventAttributes
                .AsNoTracking()
                .Where(a => a.TenantId == tenantId && a.EventId == eventId && a.Id == attributeId),
            EntityDisplayName,
            cancellationToken);

        if (attribute is null) return response;

        if (!IsVisible(attribute.TenantMinRole, callerTenantRole))
        {
            response.AddResponseMessage(MessageType.ERROR, $"{EntityDisplayName} not found.");
            return response;
        }

        response.SetSuccess(MapToDto(attribute));
        return response;
    }

    public async Task<EventAttributeResponse> CreateAsync(
        Guid tenantId,
        Guid eventId,
        CreateEventAttributeRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = new EventAttributeResponse();

        if (!ServiceValidationHelper.ValidateTenantContext(tenantId, _currentTenantContext, response))
            return response;
        if (!ServiceGuards.RequireRequest(request, "create event attribute", response))
            return response;
        if (!await ServiceGuards.AuthorizeEventManageAsync(response, _memberAuthorizationService, tenantId, cancellationToken))
            return response;

        ServiceValidationHelper.TryNormalizeString(request.Key, "Attribute key", response, out string normalizedKey);
        ServiceValidationHelper.TryNormalizeString(request.Value, "Attribute value", response, out string normalizedValue);
        if (ServiceValidationHelper.HasErrors(response))
            return response;

        var eventExists = await _dbContext.Events
            .AsNoTracking()
            .AnyAsync(e => e.TenantId == tenantId && e.Id == eventId, cancellationToken);
        if (!eventExists)
        {
            response.AddResponseMessage(MessageType.ERROR, "Event not found.");
            return response;
        }

        var duplicateExists = await _dbContext.EventAttributes
            .AsNoTracking()
            .AnyAsync(a => a.TenantId == tenantId && a.EventId == eventId && a.Key == normalizedKey, cancellationToken);

        if (duplicateExists)
        {
            response.AddResponseMessage(MessageType.ERROR, $"An attribute with key '{normalizedKey}' already exists for this event.");
            return response;
        }

        var attribute = new EventAttribute
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            EventId = eventId,
            Key = normalizedKey,
            Value = normalizedValue,
            TenantMinRole = request.TenantMinRole,
        };

        _dbContext.EventAttributes.Add(attribute);
        await _dbContext.SaveChangesAsync(cancellationToken);

        response.SetSuccess(MapToDto(attribute));
        return response;
    }

    public async Task<EventAttributeResponse> UpdateAsync(
        Guid tenantId,
        Guid eventId,
        Guid attributeId,
        UpdateEventAttributeRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = new EventAttributeResponse();

        if (!ServiceValidationHelper.ValidateTenantContext(tenantId, _currentTenantContext, response))
            return response;
        if (!ServiceGuards.RequireRequest(request, "update event attribute", response))
            return response;
        if (!await ServiceGuards.AuthorizeEventManageAsync(response, _memberAuthorizationService, tenantId, cancellationToken))
            return response;

        ServiceValidationHelper.TryNormalizeString(request.Key, "Attribute key", response, out string normalizedKey);
        ServiceValidationHelper.TryNormalizeString(request.Value, "Attribute value", response, out string normalizedValue);
        if (ServiceValidationHelper.HasErrors(response))
            return response;

        var attribute = await ServiceGuards.LoadOrNotFoundAsync(
            response,
            _dbContext.EventAttributes
                .Where(a => a.TenantId == tenantId && a.EventId == eventId && a.Id == attributeId),
            EntityDisplayName,
            cancellationToken);

        if (attribute is null) return response;

        if (!string.Equals(attribute.Key, normalizedKey, StringComparison.Ordinal))
        {
            var duplicateExists = await _dbContext.EventAttributes
                .AsNoTracking()
                .AnyAsync(a => a.TenantId == tenantId && a.EventId == eventId && a.Key == normalizedKey && a.Id != attributeId, cancellationToken);

            if (duplicateExists)
            {
                response.AddResponseMessage(MessageType.ERROR, $"An attribute with key '{normalizedKey}' already exists for this event.");
                return response;
            }
        }

        attribute.Key = normalizedKey;
        attribute.Value = normalizedValue;
        attribute.TenantMinRole = request.TenantMinRole;

        await _dbContext.SaveChangesAsync(cancellationToken);

        response.SetSuccess(MapToDto(attribute));
        return response;
    }

    public async Task<EventAttributeResponse> DeleteAsync(
        Guid tenantId,
        Guid eventId,
        Guid attributeId,
        CancellationToken cancellationToken = default)
    {
        var response = new EventAttributeResponse();

        if (!ServiceValidationHelper.ValidateTenantContext(tenantId, _currentTenantContext, response))
            return response;
        if (!await ServiceGuards.AuthorizeEventManageAsync(response, _memberAuthorizationService, tenantId, cancellationToken))
            return response;

        var attribute = await ServiceGuards.LoadOrNotFoundAsync(
            response,
            _dbContext.EventAttributes
                .Where(a => a.TenantId == tenantId && a.EventId == eventId && a.Id == attributeId),
            EntityDisplayName,
            cancellationToken);

        if (attribute is null) return response;

        if (attribute.IsDeleted)
        {
            response.AddResponseMessage(MessageType.WARNING, $"{EntityDisplayName} already deleted.");
            return response;
        }

        attribute.IsDeleted = true;

        await _dbContext.SaveChangesAsync(cancellationToken);

        GathersteadMetrics.RecordSoftDelete("EventAttribute", tenantId);
        response.SetSuccess(MapToDto(attribute));
        return response;
    }

    private static bool IsVisible(byte tenantMinRole, TenantRole? callerTenantRole)
        => callerTenantRole.HasValue && callerTenantRole.Value <= (TenantRole)tenantMinRole;

    private static EventAttributeDto MapToDto(EventAttribute attr) => new(
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
