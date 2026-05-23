using Gatherstead.Api.Contracts.MemberAttributes;
using Gatherstead.Api.Observability;
using Gatherstead.Api.Contracts.Responses;
using Gatherstead.Api.Services.Authorization;
using Gatherstead.Api.Services.Validation;
using Gatherstead.Data;
using Gatherstead.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Gatherstead.Api.Services.MemberAttributes;

public class HouseholdMemberAttributeService : IHouseholdMemberAttributeService
{
    private const string EntityDisplayName = "Household member attribute";

    private readonly GathersteadDbContext _dbContext;
    private readonly ICurrentTenantContext _currentTenantContext;
    private readonly IMemberAuthorizationService _memberAuthorizationService;

    public HouseholdMemberAttributeService(
        GathersteadDbContext dbContext,
        ICurrentTenantContext currentTenantContext,
        IMemberAuthorizationService memberAuthorizationService)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _currentTenantContext = currentTenantContext ?? throw new ArgumentNullException(nameof(currentTenantContext));
        _memberAuthorizationService = memberAuthorizationService ?? throw new ArgumentNullException(nameof(memberAuthorizationService));
    }

    public async Task<BaseEntityResponse<IReadOnlyCollection<HouseholdMemberAttributeDto>>> ListAsync(
        Guid tenantId,
        Guid householdId,
        Guid memberId,
        IEnumerable<Guid>? ids = null,
        CancellationToken cancellationToken = default)
    {
        var response = new BaseEntityResponse<IReadOnlyCollection<HouseholdMemberAttributeDto>>();

        if (!ServiceValidationHelper.ValidateTenantContext(tenantId, _currentTenantContext, response))
            return response;
        if (!await ServiceGuards.AuthorizeSensitiveReadAsync(response, _memberAuthorizationService, tenantId, householdId, cancellationToken))
            return response;

        var callerTenantRole = await _memberAuthorizationService.GetCallerTenantRoleAsync(tenantId, cancellationToken);
        var callerHouseholdRole = await _memberAuthorizationService.GetCallerHouseholdRoleAsync(tenantId, householdId, cancellationToken);

        var query = _dbContext.HouseholdMemberAttributes
            .AsNoTracking()
            .Where(a => a.TenantId == tenantId && a.HouseholdMemberId == memberId);

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

        return BaseEntityResponse<IReadOnlyCollection<HouseholdMemberAttributeDto>>.SuccessfulResponse(visible);
    }

    public async Task<HouseholdMemberAttributeResponse> GetAsync(
        Guid tenantId,
        Guid householdId,
        Guid memberId,
        Guid attributeId,
        CancellationToken cancellationToken = default)
    {
        var response = new HouseholdMemberAttributeResponse();

        if (!ServiceValidationHelper.ValidateTenantContext(tenantId, _currentTenantContext, response))
            return response;
        if (!await ServiceGuards.AuthorizeSensitiveReadAsync(response, _memberAuthorizationService, tenantId, householdId, cancellationToken))
            return response;

        var callerTenantRole = await _memberAuthorizationService.GetCallerTenantRoleAsync(tenantId, cancellationToken);
        var callerHouseholdRole = await _memberAuthorizationService.GetCallerHouseholdRoleAsync(tenantId, householdId, cancellationToken);

        var attribute = await ServiceGuards.LoadOrNotFoundAsync(
            response,
            _dbContext.HouseholdMemberAttributes
                .AsNoTracking()
                .Where(a => a.TenantId == tenantId && a.HouseholdMemberId == memberId && a.Id == attributeId),
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

    public async Task<HouseholdMemberAttributeResponse> CreateAsync(
        Guid tenantId,
        Guid householdId,
        Guid memberId,
        CreateHouseholdMemberAttributeRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = new HouseholdMemberAttributeResponse();

        if (!ServiceValidationHelper.ValidateTenantContext(tenantId, _currentTenantContext, response))
            return response;
        if (!ServiceGuards.RequireRequest(request, "create household member attribute", response))
            return response;
        if (!await ServiceGuards.AuthorizeMemberEditAsync(response, _memberAuthorizationService, tenantId, householdId, memberId, cancellationToken))
            return response;

        ServiceValidationHelper.TryNormalizeString(request.Key, "Attribute key", response, out string normalizedKey);
        ServiceValidationHelper.TryNormalizeString(request.Value, "Attribute value", response, out string normalizedValue);
        if (ServiceValidationHelper.HasErrors(response))
            return response;
        if (!await ServiceGuards.RequireMemberExistsAsync(response, _dbContext, tenantId, householdId, memberId, cancellationToken))
            return response;

        var duplicateExists = await _dbContext.HouseholdMemberAttributes
            .AsNoTracking()
            .AnyAsync(a => a.TenantId == tenantId && a.HouseholdMemberId == memberId && a.Key == normalizedKey, cancellationToken);

        if (duplicateExists)
        {
            response.AddResponseMessage(MessageType.ERROR, $"An attribute with key '{normalizedKey}' already exists for this member.");
            return response;
        }

        var attribute = new HouseholdMemberAttribute
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            HouseholdMemberId = memberId,
            Key = normalizedKey,
            Value = normalizedValue,
            TenantMinRole = request.TenantMinRole,
            HouseholdMinRole = request.HouseholdMinRole,
        };

        _dbContext.HouseholdMemberAttributes.Add(attribute);
        await _dbContext.SaveChangesAsync(cancellationToken);

        response.SetSuccess(MapToDto(attribute));
        return response;
    }

    public async Task<HouseholdMemberAttributeResponse> UpdateAsync(
        Guid tenantId,
        Guid householdId,
        Guid memberId,
        Guid attributeId,
        UpdateHouseholdMemberAttributeRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = new HouseholdMemberAttributeResponse();

        if (!ServiceValidationHelper.ValidateTenantContext(tenantId, _currentTenantContext, response))
            return response;
        if (!ServiceGuards.RequireRequest(request, "update household member attribute", response))
            return response;
        if (!await ServiceGuards.AuthorizeMemberEditAsync(response, _memberAuthorizationService, tenantId, householdId, memberId, cancellationToken))
            return response;

        ServiceValidationHelper.TryNormalizeString(request.Key, "Attribute key", response, out string normalizedKey);
        ServiceValidationHelper.TryNormalizeString(request.Value, "Attribute value", response, out string normalizedValue);
        if (ServiceValidationHelper.HasErrors(response))
            return response;

        var attribute = await ServiceGuards.LoadOrNotFoundAsync(
            response,
            _dbContext.HouseholdMemberAttributes
                .Where(a => a.TenantId == tenantId && a.HouseholdMemberId == memberId && a.Id == attributeId),
            EntityDisplayName,
            cancellationToken);

        if (attribute is null) return response;

        if (!string.Equals(attribute.Key, normalizedKey, StringComparison.Ordinal))
        {
            var duplicateExists = await _dbContext.HouseholdMemberAttributes
                .AsNoTracking()
                .AnyAsync(a => a.TenantId == tenantId && a.HouseholdMemberId == memberId && a.Key == normalizedKey && a.Id != attributeId, cancellationToken);

            if (duplicateExists)
            {
                response.AddResponseMessage(MessageType.ERROR, $"An attribute with key '{normalizedKey}' already exists for this member.");
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

    public async Task<HouseholdMemberAttributeResponse> DeleteAsync(
        Guid tenantId,
        Guid householdId,
        Guid memberId,
        Guid attributeId,
        CancellationToken cancellationToken = default)
    {
        var response = new HouseholdMemberAttributeResponse();

        if (!ServiceValidationHelper.ValidateTenantContext(tenantId, _currentTenantContext, response))
            return response;
        if (!await ServiceGuards.AuthorizeMemberEditAsync(response, _memberAuthorizationService, tenantId, householdId, memberId, cancellationToken))
            return response;

        var attribute = await ServiceGuards.LoadOrNotFoundAsync(
            response,
            _dbContext.HouseholdMemberAttributes
                .Where(a => a.TenantId == tenantId && a.HouseholdMemberId == memberId && a.Id == attributeId),
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

        GathersteadMetrics.RecordSoftDelete("HouseholdMemberAttribute", tenantId);
        response.SetSuccess(MapToDto(attribute));
        return response;
    }

    private static bool IsVisible(byte tenantMinRole, byte? householdMinRole, TenantRole? callerTenantRole, HouseholdRole? callerHouseholdRole)
        => (callerTenantRole.HasValue && callerTenantRole.Value <= (TenantRole)tenantMinRole)
        || (householdMinRole.HasValue && callerHouseholdRole.HasValue && callerHouseholdRole.Value <= (HouseholdRole)householdMinRole.Value);

    private static HouseholdMemberAttributeDto MapToDto(HouseholdMemberAttribute attr) => new(
        attr.Id,
        attr.TenantId,
        attr.HouseholdMemberId,
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
