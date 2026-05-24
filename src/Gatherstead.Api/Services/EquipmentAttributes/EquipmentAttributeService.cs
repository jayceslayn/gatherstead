using Gatherstead.Api.Contracts.EquipmentAttributes;
using Gatherstead.Api.Observability;
using Gatherstead.Api.Contracts.Responses;
using Gatherstead.Api.Services.Authorization;
using Gatherstead.Api.Services.Validation;
using Gatherstead.Data;
using Gatherstead.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Gatherstead.Api.Services.EquipmentAttributes;

public class EquipmentAttributeService : IEquipmentAttributeService
{
    private const string EntityDisplayName = "Equipment attribute";

    private readonly GathersteadDbContext _dbContext;
    private readonly ICurrentTenantContext _currentTenantContext;
    private readonly IMemberAuthorizationService _memberAuthorizationService;

    public EquipmentAttributeService(
        GathersteadDbContext dbContext,
        ICurrentTenantContext currentTenantContext,
        IMemberAuthorizationService memberAuthorizationService)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _currentTenantContext = currentTenantContext ?? throw new ArgumentNullException(nameof(currentTenantContext));
        _memberAuthorizationService = memberAuthorizationService ?? throw new ArgumentNullException(nameof(memberAuthorizationService));
    }

    public async Task<BaseEntityResponse<IReadOnlyCollection<EquipmentAttributeDto>>> ListAsync(
        Guid tenantId,
        Guid equipmentId,
        IEnumerable<Guid>? ids = null,
        CancellationToken cancellationToken = default)
    {
        var response = new BaseEntityResponse<IReadOnlyCollection<EquipmentAttributeDto>>();

        if (!ServiceValidationHelper.ValidateTenantContext(tenantId, _currentTenantContext, response))
            return response;

        var callerTenantRole = await _memberAuthorizationService.GetCallerTenantRoleAsync(tenantId, cancellationToken);

        var query = _dbContext.EquipmentAttributes
            .AsNoTracking()
            .Where(a => a.TenantId == tenantId && a.EquipmentId == equipmentId);

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

        return BaseEntityResponse<IReadOnlyCollection<EquipmentAttributeDto>>.SuccessfulResponse(visible);
    }

    public async Task<EquipmentAttributeResponse> GetAsync(
        Guid tenantId,
        Guid equipmentId,
        Guid attributeId,
        CancellationToken cancellationToken = default)
    {
        var response = new EquipmentAttributeResponse();

        if (!ServiceValidationHelper.ValidateTenantContext(tenantId, _currentTenantContext, response))
            return response;

        var callerTenantRole = await _memberAuthorizationService.GetCallerTenantRoleAsync(tenantId, cancellationToken);

        var attribute = await ServiceGuards.LoadOrNotFoundAsync(
            response,
            _dbContext.EquipmentAttributes
                .AsNoTracking()
                .Where(a => a.TenantId == tenantId && a.EquipmentId == equipmentId && a.Id == attributeId),
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

    public async Task<EquipmentAttributeResponse> CreateAsync(
        Guid tenantId,
        Guid equipmentId,
        CreateEquipmentAttributeRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = new EquipmentAttributeResponse();

        if (!ServiceValidationHelper.ValidateTenantContext(tenantId, _currentTenantContext, response))
            return response;
        if (!ServiceGuards.RequireRequest(request, "create equipment attribute", response))
            return response;
        if (!await ServiceGuards.AuthorizeTenantManageAsync(response, _memberAuthorizationService, tenantId, cancellationToken))
            return response;

        ServiceValidationHelper.TryNormalizeString(request.Key, "Attribute key", response, out string normalizedKey);
        ServiceValidationHelper.TryNormalizeString(request.Value, "Attribute value", response, out string normalizedValue);
        if (ServiceValidationHelper.HasErrors(response))
            return response;

        var equipmentExists = await _dbContext.Equipment
            .AsNoTracking()
            .AnyAsync(e => e.TenantId == tenantId && e.Id == equipmentId, cancellationToken);
        if (!equipmentExists)
        {
            response.AddResponseMessage(MessageType.ERROR, "Equipment not found.");
            return response;
        }

        var duplicateExists = await _dbContext.EquipmentAttributes
            .AsNoTracking()
            .AnyAsync(a => a.TenantId == tenantId && a.EquipmentId == equipmentId && a.Key == normalizedKey, cancellationToken);

        if (duplicateExists)
        {
            response.AddResponseMessage(MessageType.ERROR, $"An attribute with key '{normalizedKey}' already exists for this equipment.");
            return response;
        }

        var attribute = new EquipmentAttribute
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            EquipmentId = equipmentId,
            Key = normalizedKey,
            Value = normalizedValue,
            TenantMinRole = request.TenantMinRole,
        };

        _dbContext.EquipmentAttributes.Add(attribute);
        await _dbContext.SaveChangesAsync(cancellationToken);

        response.SetSuccess(MapToDto(attribute));
        return response;
    }

    public async Task<EquipmentAttributeResponse> UpdateAsync(
        Guid tenantId,
        Guid equipmentId,
        Guid attributeId,
        UpdateEquipmentAttributeRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = new EquipmentAttributeResponse();

        if (!ServiceValidationHelper.ValidateTenantContext(tenantId, _currentTenantContext, response))
            return response;
        if (!ServiceGuards.RequireRequest(request, "update equipment attribute", response))
            return response;
        if (!await ServiceGuards.AuthorizeTenantManageAsync(response, _memberAuthorizationService, tenantId, cancellationToken))
            return response;

        ServiceValidationHelper.TryNormalizeString(request.Key, "Attribute key", response, out string normalizedKey);
        ServiceValidationHelper.TryNormalizeString(request.Value, "Attribute value", response, out string normalizedValue);
        if (ServiceValidationHelper.HasErrors(response))
            return response;

        var attribute = await ServiceGuards.LoadOrNotFoundAsync(
            response,
            _dbContext.EquipmentAttributes
                .Where(a => a.TenantId == tenantId && a.EquipmentId == equipmentId && a.Id == attributeId),
            EntityDisplayName,
            cancellationToken);

        if (attribute is null) return response;

        if (!string.Equals(attribute.Key, normalizedKey, StringComparison.Ordinal))
        {
            var duplicateExists = await _dbContext.EquipmentAttributes
                .AsNoTracking()
                .AnyAsync(a => a.TenantId == tenantId && a.EquipmentId == equipmentId && a.Key == normalizedKey && a.Id != attributeId, cancellationToken);

            if (duplicateExists)
            {
                response.AddResponseMessage(MessageType.ERROR, $"An attribute with key '{normalizedKey}' already exists for this equipment.");
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

    public async Task<EquipmentAttributeResponse> DeleteAsync(
        Guid tenantId,
        Guid equipmentId,
        Guid attributeId,
        CancellationToken cancellationToken = default)
    {
        var response = new EquipmentAttributeResponse();

        if (!ServiceValidationHelper.ValidateTenantContext(tenantId, _currentTenantContext, response))
            return response;
        if (!await ServiceGuards.AuthorizeTenantManageAsync(response, _memberAuthorizationService, tenantId, cancellationToken))
            return response;

        var attribute = await ServiceGuards.LoadOrNotFoundAsync(
            response,
            _dbContext.EquipmentAttributes
                .Where(a => a.TenantId == tenantId && a.EquipmentId == equipmentId && a.Id == attributeId),
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

        GathersteadMetrics.RecordSoftDelete("EquipmentAttribute", tenantId);
        response.SetSuccess(MapToDto(attribute));
        return response;
    }

    private static bool IsVisible(byte tenantMinRole, TenantRole? callerTenantRole)
        => callerTenantRole.HasValue && callerTenantRole.Value <= (TenantRole)tenantMinRole;

    private static EquipmentAttributeDto MapToDto(EquipmentAttribute attr) => new(
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
