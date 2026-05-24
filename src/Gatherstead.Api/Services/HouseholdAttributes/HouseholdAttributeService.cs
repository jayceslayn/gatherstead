using Gatherstead.Api.Contracts.HouseholdAttributes;
using Gatherstead.Api.Observability;
using Gatherstead.Api.Contracts.Responses;
using Gatherstead.Api.Services.Authorization;
using Gatherstead.Api.Services.Validation;
using Gatherstead.Data;
using Gatherstead.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Gatherstead.Api.Services.HouseholdAttributes;

public class HouseholdAttributeService : IHouseholdAttributeService
{
    private const string EntityDisplayName = "Household attribute";

    private readonly GathersteadDbContext _dbContext;
    private readonly ICurrentTenantContext _currentTenantContext;
    private readonly IMemberAuthorizationService _memberAuthorizationService;

    public HouseholdAttributeService(
        GathersteadDbContext dbContext,
        ICurrentTenantContext currentTenantContext,
        IMemberAuthorizationService memberAuthorizationService)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _currentTenantContext = currentTenantContext ?? throw new ArgumentNullException(nameof(currentTenantContext));
        _memberAuthorizationService = memberAuthorizationService ?? throw new ArgumentNullException(nameof(memberAuthorizationService));
    }

    public async Task<BaseEntityResponse<IReadOnlyCollection<HouseholdAttributeDto>>> ListAsync(
        Guid tenantId,
        Guid householdId,
        IEnumerable<Guid>? ids = null,
        CancellationToken cancellationToken = default)
    {
        var response = new BaseEntityResponse<IReadOnlyCollection<HouseholdAttributeDto>>();

        if (!ServiceValidationHelper.ValidateTenantContext(tenantId, _currentTenantContext, response))
            return response;

        var callerTenantRole = await _memberAuthorizationService.GetCallerTenantRoleAsync(tenantId, cancellationToken);
        var callerHouseholdRole = await _memberAuthorizationService.GetCallerHouseholdRoleAsync(tenantId, householdId, cancellationToken);

        var query = _dbContext.HouseholdAttributes
            .AsNoTracking()
            .Where(a => a.TenantId == tenantId && a.HouseholdId == householdId);

        if (ids is not null)
        {
            var idList = ids.ToList();
            if (idList.Count > 0)
                query = query.Where(a => idList.Contains(a.Id));
        }

        var attributes = await query.ToListAsync(cancellationToken);

        var visible = attributes
            .Where(a => IsVisible(a.TenantMinRole, a.HouseholdMinRole, callerTenantRole, callerHouseholdRole))
            .Select(MapToDto)
            .ToList();

        return BaseEntityResponse<IReadOnlyCollection<HouseholdAttributeDto>>.SuccessfulResponse(visible);
    }

    public async Task<HouseholdAttributeResponse> GetAsync(
        Guid tenantId,
        Guid householdId,
        Guid attributeId,
        CancellationToken cancellationToken = default)
    {
        var response = new HouseholdAttributeResponse();

        if (!ServiceValidationHelper.ValidateTenantContext(tenantId, _currentTenantContext, response))
            return response;

        var callerTenantRole = await _memberAuthorizationService.GetCallerTenantRoleAsync(tenantId, cancellationToken);
        var callerHouseholdRole = await _memberAuthorizationService.GetCallerHouseholdRoleAsync(tenantId, householdId, cancellationToken);

        var attribute = await ServiceGuards.LoadOrNotFoundAsync(
            response,
            _dbContext.HouseholdAttributes
                .AsNoTracking()
                .Where(a => a.TenantId == tenantId && a.HouseholdId == householdId && a.Id == attributeId),
            EntityDisplayName,
            cancellationToken);

        if (attribute is null) return response;

        if (!IsVisible(attribute.TenantMinRole, attribute.HouseholdMinRole, callerTenantRole, callerHouseholdRole))
        {
            response.AddResponseMessage(MessageType.ERROR, $"{EntityDisplayName} not found.");
            return response;
        }

        response.SetSuccess(MapToDto(attribute));
        return response;
    }

    public async Task<HouseholdAttributeResponse> CreateAsync(
        Guid tenantId,
        Guid householdId,
        CreateHouseholdAttributeRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = new HouseholdAttributeResponse();

        if (!ServiceValidationHelper.ValidateTenantContext(tenantId, _currentTenantContext, response))
            return response;
        if (!ServiceGuards.RequireRequest(request, "create household attribute", response))
            return response;
        if (!await ServiceGuards.AuthorizeHouseholdManageAsync(response, _memberAuthorizationService, tenantId, householdId, "You do not have permission to manage this household.", cancellationToken))
            return response;

        var householdExists = await _dbContext.Households
            .AsNoTracking()
            .AnyAsync(h => h.TenantId == tenantId && h.Id == householdId, cancellationToken);
        if (!householdExists)
        {
            response.AddResponseMessage(MessageType.ERROR, "Household not found.");
            return response;
        }

        ServiceValidationHelper.TryNormalizeString(request.Key, "Attribute key", response, out string normalizedKey);
        ServiceValidationHelper.TryNormalizeString(request.Value, "Attribute value", response, out string normalizedValue);
        if (ServiceValidationHelper.HasErrors(response))
            return response;

        var duplicateExists = await _dbContext.HouseholdAttributes
            .AsNoTracking()
            .AnyAsync(a => a.TenantId == tenantId && a.HouseholdId == householdId && a.Key == normalizedKey, cancellationToken);

        if (duplicateExists)
        {
            response.AddResponseMessage(MessageType.ERROR, $"An attribute with key '{normalizedKey}' already exists for this household.");
            return response;
        }

        var attribute = new HouseholdAttribute
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            HouseholdId = householdId,
            Key = normalizedKey,
            Value = normalizedValue,
            TenantMinRole = request.TenantMinRole,
            HouseholdMinRole = request.HouseholdMinRole,
        };

        _dbContext.HouseholdAttributes.Add(attribute);
        await _dbContext.SaveChangesAsync(cancellationToken);

        response.SetSuccess(MapToDto(attribute));
        return response;
    }

    public async Task<HouseholdAttributeResponse> UpdateAsync(
        Guid tenantId,
        Guid householdId,
        Guid attributeId,
        UpdateHouseholdAttributeRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = new HouseholdAttributeResponse();

        if (!ServiceValidationHelper.ValidateTenantContext(tenantId, _currentTenantContext, response))
            return response;
        if (!ServiceGuards.RequireRequest(request, "update household attribute", response))
            return response;
        if (!await ServiceGuards.AuthorizeHouseholdManageAsync(response, _memberAuthorizationService, tenantId, householdId, "You do not have permission to manage this household.", cancellationToken))
            return response;

        ServiceValidationHelper.TryNormalizeString(request.Key, "Attribute key", response, out string normalizedKey);
        ServiceValidationHelper.TryNormalizeString(request.Value, "Attribute value", response, out string normalizedValue);
        if (ServiceValidationHelper.HasErrors(response))
            return response;

        var attribute = await ServiceGuards.LoadOrNotFoundAsync(
            response,
            _dbContext.HouseholdAttributes
                .Where(a => a.TenantId == tenantId && a.HouseholdId == householdId && a.Id == attributeId),
            EntityDisplayName,
            cancellationToken);

        if (attribute is null) return response;

        if (!string.Equals(attribute.Key, normalizedKey, StringComparison.Ordinal))
        {
            var duplicateExists = await _dbContext.HouseholdAttributes
                .AsNoTracking()
                .AnyAsync(a => a.TenantId == tenantId && a.HouseholdId == householdId && a.Key == normalizedKey && a.Id != attributeId, cancellationToken);

            if (duplicateExists)
            {
                response.AddResponseMessage(MessageType.ERROR, $"An attribute with key '{normalizedKey}' already exists for this household.");
                return response;
            }
        }

        attribute.Key = normalizedKey;
        attribute.Value = normalizedValue;
        attribute.TenantMinRole = request.TenantMinRole;
        attribute.HouseholdMinRole = request.HouseholdMinRole;

        await _dbContext.SaveChangesAsync(cancellationToken);

        response.SetSuccess(MapToDto(attribute));
        return response;
    }

    public async Task<HouseholdAttributeResponse> DeleteAsync(
        Guid tenantId,
        Guid householdId,
        Guid attributeId,
        CancellationToken cancellationToken = default)
    {
        var response = new HouseholdAttributeResponse();

        if (!ServiceValidationHelper.ValidateTenantContext(tenantId, _currentTenantContext, response))
            return response;
        if (!await ServiceGuards.AuthorizeHouseholdManageAsync(response, _memberAuthorizationService, tenantId, householdId, "You do not have permission to manage this household.", cancellationToken))
            return response;

        var attribute = await ServiceGuards.LoadOrNotFoundAsync(
            response,
            _dbContext.HouseholdAttributes
                .Where(a => a.TenantId == tenantId && a.HouseholdId == householdId && a.Id == attributeId),
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

        GathersteadMetrics.RecordSoftDelete("HouseholdAttribute", tenantId);
        response.SetSuccess(MapToDto(attribute));
        return response;
    }

    private static bool IsVisible(byte tenantMinRole, byte? householdMinRole, TenantRole? callerTenantRole, HouseholdRole? callerHouseholdRole)
        => (callerTenantRole.HasValue && callerTenantRole.Value <= (TenantRole)tenantMinRole)
        || (householdMinRole.HasValue && callerHouseholdRole.HasValue && callerHouseholdRole.Value <= (HouseholdRole)householdMinRole.Value);

    private static HouseholdAttributeDto MapToDto(HouseholdAttribute attr) => new(
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
