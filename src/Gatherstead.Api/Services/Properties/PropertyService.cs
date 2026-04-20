using Gatherstead.Api.Contracts.Properties;
using Gatherstead.Api.Contracts.Responses;
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

    public PropertyService(
        GathersteadDbContext dbContext,
        ICurrentTenantContext currentTenantContext,
        IMemberAuthorizationService memberAuthorizationService)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _currentTenantContext = currentTenantContext ?? throw new ArgumentNullException(nameof(currentTenantContext));
        _memberAuthorizationService = memberAuthorizationService ?? throw new ArgumentNullException(nameof(memberAuthorizationService));
    }

    public async Task<BaseEntityResponse<IReadOnlyCollection<PropertyDto>>> ListAsync(
        Guid tenantId,
        IEnumerable<Guid>? ids = null,
        CancellationToken cancellationToken = default)
    {
        var response = new BaseEntityResponse<IReadOnlyCollection<PropertyDto>>();

        if (!ServiceValidationHelper.ValidateTenantContext(tenantId, _currentTenantContext, response))
            return response;

        var query = _dbContext.Properties
            .AsNoTracking()
            .Where(p => p.TenantId == tenantId);

        if (ids is not null)
        {
            var idList = ids.ToList();
            if (idList.Count > 0)
                query = query.Where(p => idList.Contains(p.Id));
        }

        var properties = await query
            .Select(p => MapToDto(p))
            .ToListAsync(cancellationToken);

        return BaseEntityResponse<IReadOnlyCollection<PropertyDto>>.SuccessfulResponse(properties);
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
            _dbContext.Properties.AsNoTracking().Where(p => p.TenantId == tenantId && p.Id == propertyId),
            EntityDisplayName,
            cancellationToken);

        if (property is null) return response;

        response.SetSuccess(MapToDto(property));
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
        };

        _dbContext.Properties.Add(property);
        await _dbContext.SaveChangesAsync(cancellationToken);

        response.SetSuccess(MapToDto(property));
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
        await _dbContext.SaveChangesAsync(cancellationToken);

        response.SetSuccess(MapToDto(property));
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
        await _dbContext.SaveChangesAsync(cancellationToken);

        response.SetSuccess(MapToDto(property));
        return response;
    }

    private static PropertyDto MapToDto(Property p) => new(
        p.Id,
        p.TenantId,
        p.Name,
        p.CreatedAt,
        p.UpdatedAt,
        p.IsDeleted,
        p.DeletedAt,
        p.DeletedByUserId);
}
