using Gatherstead.Api.Contracts.Attributes;
using Gatherstead.Api.Contracts.Properties;
using Gatherstead.Api.Contracts.Responses;
using Gatherstead.Api.Services.Attributes;
using Gatherstead.Api.Services.Authorization;
using Gatherstead.Api.Services.Validation;
using Gatherstead.Data;
using Gatherstead.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Gatherstead.Api.Services.Properties;

public class PropertyService : IPropertyService
{
    private const string EntityDisplayName = "Property";

    private readonly GathersteadDbContext _dbContext;
    private readonly ICurrentTenantContext _currentTenantContext;
    private readonly IMemberAuthorizationService _memberAuthorizationService;
    private readonly IAuditVisibilityContext _auditVisibility;

    public PropertyService(
        GathersteadDbContext dbContext,
        ICurrentTenantContext currentTenantContext,
        IMemberAuthorizationService memberAuthorizationService,
        IAuditVisibilityContext auditVisibility)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _currentTenantContext = currentTenantContext ?? throw new ArgumentNullException(nameof(currentTenantContext));
        _memberAuthorizationService = memberAuthorizationService ?? throw new ArgumentNullException(nameof(memberAuthorizationService));
        _auditVisibility = auditVisibility ?? throw new ArgumentNullException(nameof(auditVisibility));
    }

    public async Task<BaseEntityResponse<IReadOnlyCollection<PropertyDto>>> ListAsync(
        Guid tenantId,
        IEnumerable<Guid>? ids = null,
        CancellationToken cancellationToken = default)
    {
        var response = new BaseEntityResponse<IReadOnlyCollection<PropertyDto>>();

        if (!ServiceValidationHelper.ValidateTenantContext(tenantId, _currentTenantContext, response))
            return response;

        // Attributes ride along on lists (mirrors GetAsync) so list-sourced edits don't wipe them.
        // A caller with no tenant role sees none, so skip loading them entirely.
        var callerRole = await _memberAuthorizationService.GetCallerTenantRoleAsync(tenantId, cancellationToken);

        var query = _dbContext.Properties
            .AsNoTracking()
            .Where(p => p.TenantId == tenantId);
        if (callerRole is not null)
            query = query.Include(p => p.Attributes);

        if (ids is not null)
        {
            var idList = ids.ToList();
            if (idList.Count > 0)
                query = query.Where(p => idList.Contains(p.Id));
        }

        var properties = await query.ToListAsync(cancellationToken);

        return BaseEntityResponse<IReadOnlyCollection<PropertyDto>>.SuccessfulResponse(
            properties.Select(p => MapToDto(p, AttributeVisibilityHelper.Visible(p.Attributes, callerRole))).ToList());
    }

    public async Task<PropertyResponse> GetAsync(
        Guid tenantId,
        Guid propertyId,
        CancellationToken cancellationToken = default)
    {
        var response = new PropertyResponse();

        if (!ServiceValidationHelper.ValidateTenantContext(tenantId, _currentTenantContext, response))
            return response;

        var property = await ServiceGuards.LoadOrNotFoundAsync(
            response,
            _dbContext.Properties.AsNoTracking()
                .Include(p => p.Attributes)
                .Where(p => p.TenantId == tenantId && p.Id == propertyId),
            EntityDisplayName,
            cancellationToken);

        if (property is null) return response;

        var callerRole = await _memberAuthorizationService.GetCallerTenantRoleAsync(tenantId, cancellationToken);
        var attrs = AttributeVisibilityHelper.Visible(property.Attributes, callerRole);

        response.SetSuccess(MapToDto(property, attrs));
        return response;
    }

    public async Task<PropertyResponse> CreateAsync(
        Guid tenantId,
        CreatePropertyRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = new PropertyResponse();

        if (!ServiceValidationHelper.ValidateTenantContext(tenantId, _currentTenantContext, response))
            return response;
        if (!ServiceGuards.RequireRequest(request, "create property", response))
            return response;
        if (!await ServiceGuards.AuthorizeTenantManageAsync(response, _memberAuthorizationService, tenantId, cancellationToken))
            return response;

        ServiceValidationHelper.TryNormalizeString(request.Name, "Property name", response, out string normalizedName);
        if (ServiceValidationHelper.HasErrors(response))
            return response;

        var duplicateExists = await _dbContext.Properties
            .AsNoTracking()
            .AnyAsync(p => p.TenantId == tenantId && p.Name == normalizedName, cancellationToken);

        if (duplicateExists)
        {
            response.AddResponseMessage(MessageType.ERROR, $"A property named '{normalizedName}' already exists.");
            return response;
        }

        var property = new Property
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = normalizedName,
            Notes = request.Notes?.Trim(),
        };

        _dbContext.Properties.Add(property);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var callerRole = await _memberAuthorizationService.GetCallerTenantRoleAsync(tenantId, cancellationToken);
        List<AttributeDto> attrs = [];

        if (request.Attributes is { Count: > 0 })
        {
            await AttributeSyncHelper.SyncAsync(
                _dbContext.PropertyAttributes.Where(a => a.PropertyId == property.Id),
                _dbContext.PropertyAttributes,
                request.Attributes,
                a => AttributeVisibilityHelper.IsVisible(a, callerRole),
                tenantId,
                () => new PropertyAttribute { TenantId = tenantId, PropertyId = property.Id },
                applyExtra: null,
                cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);

            var savedAttrs = await _dbContext.PropertyAttributes.AsNoTracking()
                .Where(a => a.PropertyId == property.Id).ToListAsync(cancellationToken);
            attrs = AttributeVisibilityHelper.Visible(savedAttrs, callerRole);
        }

        response.SetSuccess(MapToDto(property, attrs));
        return response;
    }

    public async Task<PropertyResponse> UpdateAsync(
        Guid tenantId,
        Guid propertyId,
        UpdatePropertyRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = new PropertyResponse();

        if (!ServiceValidationHelper.ValidateTenantContext(tenantId, _currentTenantContext, response))
            return response;
        if (!ServiceGuards.RequireRequest(request, "update property", response))
            return response;
        if (!await ServiceGuards.AuthorizeTenantManageAsync(response, _memberAuthorizationService, tenantId, cancellationToken))
            return response;

        ServiceValidationHelper.TryNormalizeString(request.Name, "Property name", response, out string normalizedName);
        if (ServiceValidationHelper.HasErrors(response))
            return response;

        var property = await ServiceGuards.LoadOrNotFoundAsync(
            response,
            _dbContext.Properties.Where(p => p.TenantId == tenantId && p.Id == propertyId),
            EntityDisplayName,
            cancellationToken);

        if (property is null) return response;

        if (!string.Equals(property.Name, normalizedName, StringComparison.Ordinal))
        {
            var duplicateExists = await _dbContext.Properties
                .AsNoTracking()
                .AnyAsync(p => p.TenantId == tenantId && p.Name == normalizedName && p.Id != propertyId, cancellationToken);

            if (duplicateExists)
            {
                response.AddResponseMessage(MessageType.ERROR, $"A property named '{normalizedName}' already exists.");
                return response;
            }
        }

        property.Name = normalizedName;
        property.Notes = request.Notes?.Trim();

        var callerRole = await _memberAuthorizationService.GetCallerTenantRoleAsync(tenantId, cancellationToken);

        if (request.Attributes is not null)
        {
            await AttributeSyncHelper.SyncAsync(
                _dbContext.PropertyAttributes.Where(a => a.PropertyId == propertyId),
                _dbContext.PropertyAttributes,
                request.Attributes,
                a => AttributeVisibilityHelper.IsVisible(a, callerRole),
                tenantId,
                () => new PropertyAttribute { TenantId = tenantId, PropertyId = propertyId },
                applyExtra: null,
                cancellationToken);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        var savedAttrs = await _dbContext.PropertyAttributes.AsNoTracking()
            .Where(a => a.PropertyId == propertyId).ToListAsync(cancellationToken);
        var attrs = AttributeVisibilityHelper.Visible(savedAttrs, callerRole);

        response.SetSuccess(MapToDto(property, attrs));
        return response;
    }

    public async Task<PropertyResponse> DeleteAsync(
        Guid tenantId,
        Guid propertyId,
        CancellationToken cancellationToken = default)
    {
        var response = new PropertyResponse();

        if (!ServiceValidationHelper.ValidateTenantContext(tenantId, _currentTenantContext, response))
            return response;
        if (!await ServiceGuards.AuthorizeTenantManageAsync(response, _memberAuthorizationService, tenantId, cancellationToken))
            return response;

        var property = await ServiceGuards.LoadOrNotFoundAsync(
            response,
            _dbContext.Properties.Where(p => p.TenantId == tenantId && p.Id == propertyId),
            EntityDisplayName,
            cancellationToken);

        if (property is null) return response;

        if (property.IsDeleted)
        {
            response.AddResponseMessage(MessageType.WARNING, $"{EntityDisplayName} already deleted.");
            return response;
        }

        property.IsDeleted = true;

        var childAttrs = await _dbContext.PropertyAttributes
            .Where(a => a.PropertyId == propertyId).ToListAsync(cancellationToken);
        foreach (var attr in childAttrs)
            attr.IsDeleted = true;

        await _dbContext.SaveChangesAsync(cancellationToken);

        response.SetSuccess(MapToDto(property, []));
        return response;
    }

    private PropertyDto MapToDto(Property p, IReadOnlyList<AttributeDto> attributes) => new(
        p.Id,
        p.TenantId,
        p.Name,
        p.Notes,
        attributes,
        p.ToAuditInfo(_auditVisibility.IncludeAudit));
}
