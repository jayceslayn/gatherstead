using Gatherstead.Api.Contracts.Attributes;
using Gatherstead.Api.Contracts.Equipment;
using Gatherstead.Api.Contracts.Responses;
using Gatherstead.Api.Observability;
using Gatherstead.Api.Services.Attributes;
using Gatherstead.Api.Services.Authorization;
using Gatherstead.Api.Services.Validation;
using Gatherstead.Data;
using Gatherstead.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Gatherstead.Api.Services.Equipment;

public class EquipmentService : IEquipmentService
{
    private const string EntityDisplayName = "Equipment";

    private readonly GathersteadDbContext _dbContext;
    private readonly ICurrentTenantContext _currentTenantContext;
    private readonly IMemberAuthorizationService _memberAuthorizationService;
    private readonly IAuditVisibilityContext _auditVisibility;

    public EquipmentService(
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

    public async Task<BaseEntityResponse<IReadOnlyCollection<EquipmentDto>>> ListAsync(
        Guid tenantId,
        IEnumerable<Guid>? ids = null,
        CancellationToken cancellationToken = default)
    {
        var response = new BaseEntityResponse<IReadOnlyCollection<EquipmentDto>>();

        if (!ServiceValidationHelper.ValidateTenantContext(tenantId, _currentTenantContext, response))
            return response;

        var query = _dbContext.Equipment
            .AsNoTracking()
            .Where(e => e.TenantId == tenantId);

        if (ids is not null)
        {
            var idList = ids.ToList();
            if (idList.Count > 0)
                query = query.Where(e => idList.Contains(e.Id));
        }

        var equipment = await query.ToListAsync(cancellationToken);

        return BaseEntityResponse<IReadOnlyCollection<EquipmentDto>>.SuccessfulResponse(
            equipment.Select(e => MapToDto(e, [])).ToList());
    }

    public async Task<EquipmentResponse> GetAsync(
        Guid tenantId,
        Guid equipmentId,
        CancellationToken cancellationToken = default)
    {
        var response = new EquipmentResponse();

        if (!ServiceValidationHelper.ValidateTenantContext(tenantId, _currentTenantContext, response))
            return response;

        var equipment = await ServiceGuards.LoadOrNotFoundAsync(
            response,
            _dbContext.Equipment.AsNoTracking()
                .Include(e => e.Attributes)
                .Where(e => e.TenantId == tenantId && e.Id == equipmentId),
            EntityDisplayName,
            cancellationToken);

        if (equipment is null) return response;

        var callerRole = await _memberAuthorizationService.GetCallerTenantRoleAsync(tenantId, cancellationToken);
        response.SetSuccess(MapToDto(equipment, VisibleAttributes(equipment.Attributes, callerRole)));
        return response;
    }

    public async Task<EquipmentResponse> CreateAsync(
        Guid tenantId,
        CreateEquipmentRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = new EquipmentResponse();

        if (!ServiceValidationHelper.ValidateTenantContext(tenantId, _currentTenantContext, response))
            return response;
        if (!ServiceGuards.RequireRequest(request, "create equipment", response))
            return response;
        if (!await ServiceGuards.AuthorizeTenantManageAsync(response, _memberAuthorizationService, tenantId, cancellationToken))
            return response;

        ServiceValidationHelper.TryNormalizeString(request.Name, "Equipment name", response, out string normalizedName);
        if (ServiceValidationHelper.HasErrors(response))
            return response;

        if (request.PropertyId.HasValue)
        {
            var propertyExists = await _dbContext.Properties
                .AsNoTracking()
                .AnyAsync(p => p.TenantId == tenantId && p.Id == request.PropertyId.Value, cancellationToken);
            if (!propertyExists)
            {
                response.AddResponseMessage(MessageType.ERROR, "The specified property does not exist.");
                return response;
            }
        }

        var duplicateExists = await _dbContext.Equipment
            .AsNoTracking()
            .AnyAsync(e => e.TenantId == tenantId && e.PropertyId == request.PropertyId && e.Name == normalizedName, cancellationToken);

        if (duplicateExists)
        {
            response.AddResponseMessage(MessageType.ERROR, $"Equipment named '{normalizedName}' already exists for this property.");
            return response;
        }

        var equipment = new Gatherstead.Data.Entities.Equipment
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            PropertyId = request.PropertyId,
            Name = normalizedName,
            Notes = request.Notes,
        };

        _dbContext.Equipment.Add(equipment);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var callerRole = await _memberAuthorizationService.GetCallerTenantRoleAsync(tenantId, cancellationToken);
        List<AttributeDto> attrs = [];

        if (request.Attributes is { Count: > 0 })
        {
            await AttributeSyncHelper.SyncAsync(
                _dbContext.EquipmentAttributes.Where(a => a.EquipmentId == equipment.Id),
                _dbContext.EquipmentAttributes,
                request.Attributes,
                a => callerRole.HasValue && callerRole.Value <= (TenantRole)a.TenantMinRole,
                tenantId,
                () => new EquipmentAttribute { TenantId = tenantId, EquipmentId = equipment.Id },
                applyExtra: null,
                cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);

            var savedAttrs = await _dbContext.EquipmentAttributes.AsNoTracking()
                .Where(a => a.EquipmentId == equipment.Id).ToListAsync(cancellationToken);
            attrs = VisibleAttributes(savedAttrs, callerRole);
        }

        response.SetSuccess(MapToDto(equipment, attrs));
        return response;
    }

    public async Task<EquipmentResponse> UpdateAsync(
        Guid tenantId,
        Guid equipmentId,
        UpdateEquipmentRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = new EquipmentResponse();

        if (!ServiceValidationHelper.ValidateTenantContext(tenantId, _currentTenantContext, response))
            return response;
        if (!ServiceGuards.RequireRequest(request, "update equipment", response))
            return response;
        if (!await ServiceGuards.AuthorizeTenantManageAsync(response, _memberAuthorizationService, tenantId, cancellationToken))
            return response;

        ServiceValidationHelper.TryNormalizeString(request.Name, "Equipment name", response, out string normalizedName);
        if (ServiceValidationHelper.HasErrors(response))
            return response;

        var equipment = await ServiceGuards.LoadOrNotFoundAsync(
            response,
            _dbContext.Equipment.Where(e => e.TenantId == tenantId && e.Id == equipmentId),
            EntityDisplayName,
            cancellationToken);

        if (equipment is null) return response;

        if (request.PropertyId.HasValue)
        {
            var propertyExists = await _dbContext.Properties
                .AsNoTracking()
                .AnyAsync(p => p.TenantId == tenantId && p.Id == request.PropertyId.Value, cancellationToken);
            if (!propertyExists)
            {
                response.AddResponseMessage(MessageType.ERROR, "The specified property does not exist.");
                return response;
            }
        }

        if (!string.Equals(equipment.Name, normalizedName, StringComparison.Ordinal) || equipment.PropertyId != request.PropertyId)
        {
            var duplicateExists = await _dbContext.Equipment
                .AsNoTracking()
                .AnyAsync(e => e.TenantId == tenantId && e.PropertyId == request.PropertyId && e.Name == normalizedName && e.Id != equipmentId, cancellationToken);

            if (duplicateExists)
            {
                response.AddResponseMessage(MessageType.ERROR, $"Equipment named '{normalizedName}' already exists for this property.");
                return response;
            }
        }

        equipment.PropertyId = request.PropertyId;
        equipment.Name = normalizedName;
        equipment.Notes = request.Notes;

        var callerRole = await _memberAuthorizationService.GetCallerTenantRoleAsync(tenantId, cancellationToken);

        if (request.Attributes is not null)
        {
            await AttributeSyncHelper.SyncAsync(
                _dbContext.EquipmentAttributes.Where(a => a.EquipmentId == equipmentId),
                _dbContext.EquipmentAttributes,
                request.Attributes,
                a => callerRole.HasValue && callerRole.Value <= (TenantRole)a.TenantMinRole,
                tenantId,
                () => new EquipmentAttribute { TenantId = tenantId, EquipmentId = equipmentId },
                applyExtra: null,
                cancellationToken);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        var savedAttrs = await _dbContext.EquipmentAttributes.AsNoTracking()
            .Where(a => a.EquipmentId == equipmentId).ToListAsync(cancellationToken);
        response.SetSuccess(MapToDto(equipment, VisibleAttributes(savedAttrs, callerRole)));
        return response;
    }

    public async Task<EquipmentResponse> DeleteAsync(
        Guid tenantId,
        Guid equipmentId,
        CancellationToken cancellationToken = default)
    {
        var response = new EquipmentResponse();

        if (!ServiceValidationHelper.ValidateTenantContext(tenantId, _currentTenantContext, response))
            return response;
        if (!await ServiceGuards.AuthorizeTenantManageAsync(response, _memberAuthorizationService, tenantId, cancellationToken))
            return response;

        var equipment = await ServiceGuards.LoadOrNotFoundAsync(
            response,
            _dbContext.Equipment.Where(e => e.TenantId == tenantId && e.Id == equipmentId),
            EntityDisplayName,
            cancellationToken);

        if (equipment is null) return response;

        if (equipment.IsDeleted)
        {
            response.AddResponseMessage(MessageType.WARNING, $"{EntityDisplayName} already deleted.");
            return response;
        }

        equipment.IsDeleted = true;

        var childAttrs = await _dbContext.EquipmentAttributes
            .Where(a => a.EquipmentId == equipmentId).ToListAsync(cancellationToken);
        foreach (var attr in childAttrs)
            attr.IsDeleted = true;

        await _dbContext.SaveChangesAsync(cancellationToken);

        GathersteadMetrics.RecordSoftDelete("Equipment", tenantId);
        response.SetSuccess(MapToDto(equipment, []));
        return response;
    }

    private static List<AttributeDto> VisibleAttributes(
        IEnumerable<EquipmentAttribute> attrs, TenantRole? callerRole)
        => attrs
            .Where(a => callerRole.HasValue && callerRole.Value <= (TenantRole)a.TenantMinRole)
            .OrderBy(a => a.Key)
            .Select(a => new AttributeDto(a.Id, a.Key, a.Value, a.TenantMinRole))
            .ToList();

    private EquipmentDto MapToDto(Gatherstead.Data.Entities.Equipment e, IReadOnlyList<AttributeDto> attributes) => new(
        e.Id,
        e.TenantId,
        e.PropertyId,
        e.Name,
        e.Notes,
        attributes,
        e.ToAuditInfo(_auditVisibility.IncludeAudit));
}
