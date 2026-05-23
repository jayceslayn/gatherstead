using Gatherstead.Api.Contracts.Equipment;
using Gatherstead.Api.Contracts.Responses;
using Gatherstead.Api.Observability;
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

    public EquipmentService(
        GathersteadDbContext dbContext,
        ICurrentTenantContext currentTenantContext,
        IMemberAuthorizationService memberAuthorizationService)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _currentTenantContext = currentTenantContext ?? throw new ArgumentNullException(nameof(currentTenantContext));
        _memberAuthorizationService = memberAuthorizationService ?? throw new ArgumentNullException(nameof(memberAuthorizationService));
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

        var equipment = await query
            .Select(e => MapToDto(e))
            .ToListAsync(cancellationToken);

        return BaseEntityResponse<IReadOnlyCollection<EquipmentDto>>.SuccessfulResponse(equipment);
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
            _dbContext.Equipment.AsNoTracking().Where(e => e.TenantId == tenantId && e.Id == equipmentId),
            EntityDisplayName,
            cancellationToken);

        if (equipment is null) return response;

        response.SetSuccess(MapToDto(equipment));
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

        response.SetSuccess(MapToDto(equipment));
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

        await _dbContext.SaveChangesAsync(cancellationToken);

        response.SetSuccess(MapToDto(equipment));
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
        await _dbContext.SaveChangesAsync(cancellationToken);

        GathersteadMetrics.RecordSoftDelete("Equipment", tenantId);
        response.SetSuccess(MapToDto(equipment));
        return response;
    }

    private static EquipmentDto MapToDto(Gatherstead.Data.Entities.Equipment e) => new(
        e.Id,
        e.TenantId,
        e.PropertyId,
        e.Name,
        e.Notes,
        e.CreatedAt,
        e.UpdatedAt,
        e.IsDeleted,
        e.DeletedAt,
        e.DeletedByUserId);
}
