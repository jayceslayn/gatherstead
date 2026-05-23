using Gatherstead.Api.Contracts.PropertyAttributes;
using Gatherstead.Api.Observability;
using Gatherstead.Api.Contracts.Responses;
using Gatherstead.Api.Services.Authorization;
using Gatherstead.Api.Services.Validation;
using Gatherstead.Data;
using Gatherstead.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Gatherstead.Api.Services.PropertyAttributes;

public class PropertyAttributeService : IPropertyAttributeService
{
    private const string EntityDisplayName = "Property attribute";

    private readonly GathersteadDbContext _dbContext;
    private readonly ICurrentTenantContext _currentTenantContext;
    private readonly IMemberAuthorizationService _memberAuthorizationService;

    public PropertyAttributeService(
        GathersteadDbContext dbContext,
        ICurrentTenantContext currentTenantContext,
        IMemberAuthorizationService memberAuthorizationService)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _currentTenantContext = currentTenantContext ?? throw new ArgumentNullException(nameof(currentTenantContext));
        _memberAuthorizationService = memberAuthorizationService ?? throw new ArgumentNullException(nameof(memberAuthorizationService));
    }

    public async Task<BaseEntityResponse<IReadOnlyCollection<PropertyAttributeDto>>> ListAsync(
        Guid tenantId,
        Guid propertyId,
        IEnumerable<Guid>? ids = null,
        CancellationToken cancellationToken = default)
    {
        var response = new BaseEntityResponse<IReadOnlyCollection<PropertyAttributeDto>>();

        if (!ServiceValidationHelper.ValidateTenantContext(tenantId, _currentTenantContext, response))
            return response;

        var callerTenantRole = await _memberAuthorizationService.GetCallerTenantRoleAsync(tenantId, cancellationToken);

        var query = _dbContext.PropertyAttributes
            .AsNoTracking()
            .Where(a => a.TenantId == tenantId && a.PropertyId == propertyId);

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

        return BaseEntityResponse<IReadOnlyCollection<PropertyAttributeDto>>.SuccessfulResponse(visible);
    }

    public async Task<PropertyAttributeResponse> GetAsync(
        Guid tenantId,
        Guid propertyId,
        Guid attributeId,
        CancellationToken cancellationToken = default)
    {
        var response = new PropertyAttributeResponse();

        if (!ServiceValidationHelper.ValidateTenantContext(tenantId, _currentTenantContext, response))
            return response;

        var callerTenantRole = await _memberAuthorizationService.GetCallerTenantRoleAsync(tenantId, cancellationToken);

        var attribute = await ServiceGuards.LoadOrNotFoundAsync(
            response,
            _dbContext.PropertyAttributes
                .AsNoTracking()
                .Where(a => a.TenantId == tenantId && a.PropertyId == propertyId && a.Id == attributeId),
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

    public async Task<PropertyAttributeResponse> CreateAsync(
        Guid tenantId,
        Guid propertyId,
        CreatePropertyAttributeRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = new PropertyAttributeResponse();

        if (!ServiceValidationHelper.ValidateTenantContext(tenantId, _currentTenantContext, response))
            return response;
        if (!ServiceGuards.RequireRequest(request, "create property attribute", response))
            return response;
        if (!await ServiceGuards.AuthorizeTenantManageAsync(response, _memberAuthorizationService, tenantId, cancellationToken))
            return response;

        ServiceValidationHelper.TryNormalizeString(request.Key, "Attribute key", response, out string normalizedKey);
        ServiceValidationHelper.TryNormalizeString(request.Value, "Attribute value", response, out string normalizedValue);
        if (ServiceValidationHelper.HasErrors(response))
            return response;

        var duplicateExists = await _dbContext.PropertyAttributes
            .AsNoTracking()
            .AnyAsync(a => a.TenantId == tenantId && a.PropertyId == propertyId && a.Key == normalizedKey, cancellationToken);

        if (duplicateExists)
        {
            response.AddResponseMessage(MessageType.ERROR, $"An attribute with key '{normalizedKey}' already exists for this property.");
            return response;
        }

        var attribute = new PropertyAttribute
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            PropertyId = propertyId,
            Key = normalizedKey,
            Value = normalizedValue,
            TenantMinRole = request.TenantMinRole,
        };

        _dbContext.PropertyAttributes.Add(attribute);
        await _dbContext.SaveChangesAsync(cancellationToken);

        response.SetSuccess(MapToDto(attribute));
        return response;
    }

    public async Task<PropertyAttributeResponse> UpdateAsync(
        Guid tenantId,
        Guid propertyId,
        Guid attributeId,
        UpdatePropertyAttributeRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = new PropertyAttributeResponse();

        if (!ServiceValidationHelper.ValidateTenantContext(tenantId, _currentTenantContext, response))
            return response;
        if (!ServiceGuards.RequireRequest(request, "update property attribute", response))
            return response;
        if (!await ServiceGuards.AuthorizeTenantManageAsync(response, _memberAuthorizationService, tenantId, cancellationToken))
            return response;

        ServiceValidationHelper.TryNormalizeString(request.Key, "Attribute key", response, out string normalizedKey);
        ServiceValidationHelper.TryNormalizeString(request.Value, "Attribute value", response, out string normalizedValue);
        if (ServiceValidationHelper.HasErrors(response))
            return response;

        var attribute = await ServiceGuards.LoadOrNotFoundAsync(
            response,
            _dbContext.PropertyAttributes
                .Where(a => a.TenantId == tenantId && a.PropertyId == propertyId && a.Id == attributeId),
            EntityDisplayName,
            cancellationToken);

        if (attribute is null) return response;

        if (!string.Equals(attribute.Key, normalizedKey, StringComparison.Ordinal))
        {
            var duplicateExists = await _dbContext.PropertyAttributes
                .AsNoTracking()
                .AnyAsync(a => a.TenantId == tenantId && a.PropertyId == propertyId && a.Key == normalizedKey && a.Id != attributeId, cancellationToken);

            if (duplicateExists)
            {
                response.AddResponseMessage(MessageType.ERROR, $"An attribute with key '{normalizedKey}' already exists for this property.");
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

    public async Task<PropertyAttributeResponse> DeleteAsync(
        Guid tenantId,
        Guid propertyId,
        Guid attributeId,
        CancellationToken cancellationToken = default)
    {
        var response = new PropertyAttributeResponse();

        if (!ServiceValidationHelper.ValidateTenantContext(tenantId, _currentTenantContext, response))
            return response;
        if (!await ServiceGuards.AuthorizeTenantManageAsync(response, _memberAuthorizationService, tenantId, cancellationToken))
            return response;

        var attribute = await ServiceGuards.LoadOrNotFoundAsync(
            response,
            _dbContext.PropertyAttributes
                .Where(a => a.TenantId == tenantId && a.PropertyId == propertyId && a.Id == attributeId),
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

        GathersteadMetrics.RecordSoftDelete("PropertyAttribute", tenantId);
        response.SetSuccess(MapToDto(attribute));
        return response;
    }

    private static bool IsVisible(byte tenantMinRole, TenantRole? callerTenantRole)
        => callerTenantRole.HasValue && callerTenantRole.Value <= (TenantRole)tenantMinRole;

    private static PropertyAttributeDto MapToDto(PropertyAttribute attr) => new(
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
