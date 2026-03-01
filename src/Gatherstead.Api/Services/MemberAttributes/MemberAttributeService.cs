using Gatherstead.Api.Contracts.MemberAttributes;
using Gatherstead.Api.Contracts.Responses;
using Gatherstead.Api.Services.Validation;
using Gatherstead.Data;
using Gatherstead.Data.Entities;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace Gatherstead.Api.Services.MemberAttributes;

public class MemberAttributeService : IMemberAttributeService
{
    private readonly GathersteadDbContext _dbContext;
    private readonly ICurrentTenantContext _currentTenantContext;

    private static readonly Expression<Func<MemberAttribute, MemberAttributeDto>> MapToDtoExpression =
        attr => new MemberAttributeDto(
            attr.Id,
            attr.TenantId,
            attr.HouseholdMemberId,
            attr.Key,
            attr.Value,
            attr.CreatedAt,
            attr.UpdatedAt,
            attr.IsDeleted,
            attr.DeletedAt,
            attr.DeletedByUserId);

    public MemberAttributeService(
        GathersteadDbContext dbContext,
        ICurrentTenantContext currentTenantContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _currentTenantContext = currentTenantContext ?? throw new ArgumentNullException(nameof(currentTenantContext));
    }

    public async Task<BaseEntityResponse<IReadOnlyCollection<MemberAttributeDto>>> ListAsync(
        Guid tenantId,
        Guid householdId,
        Guid memberId,
        IEnumerable<Guid>? ids = null,
        CancellationToken cancellationToken = default)
    {
        var response = new BaseEntityResponse<IReadOnlyCollection<MemberAttributeDto>>();
        ServiceValidationHelper.ValidateTenantContext(tenantId, _currentTenantContext, response);

        if (ServiceValidationHelper.HasErrors(response))
        {
            return response;
        }

        var query = _dbContext.MemberAttributes
            .AsNoTracking()
            .Where(a => a.TenantId == tenantId && a.HouseholdMemberId == memberId);

        if (ids is not null)
        {
            var idList = ids.ToList();
            if (idList.Count > 0)
            {
                query = query.Where(a => idList.Contains(a.Id));
            }
        }

        var attributes = await query
            .Select(MapToDtoExpression)
            .ToListAsync(cancellationToken);

        return BaseEntityResponse<IReadOnlyCollection<MemberAttributeDto>>.SuccessfulResponse(attributes);
    }

    public async Task<MemberAttributeResponse> GetAsync(
        Guid tenantId,
        Guid householdId,
        Guid memberId,
        Guid attributeId,
        CancellationToken cancellationToken = default)
    {
        var response = new MemberAttributeResponse();
        ServiceValidationHelper.ValidateTenantContext(tenantId, _currentTenantContext, response);

        if (ServiceValidationHelper.HasErrors(response))
        {
            return response;
        }

        var attribute = await _dbContext.MemberAttributes
            .AsNoTracking()
            .Where(a => a.TenantId == tenantId && a.HouseholdMemberId == memberId && a.Id == attributeId)
            .SingleOrDefaultAsync(cancellationToken);

        if (attribute is null)
        {
            response.AddResponseMessage(MessageType.ERROR, "Member attribute not found.");
            return response;
        }

        response.SetSuccess(MapToDto(attribute));
        return response;
    }

    public async Task<MemberAttributeResponse> CreateAsync(
        Guid tenantId,
        Guid householdId,
        Guid memberId,
        CreateMemberAttributeRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = new MemberAttributeResponse();
        ServiceValidationHelper.ValidateTenantContext(tenantId, _currentTenantContext, response);

        if (request is null)
        {
            response.AddResponseMessage(MessageType.ERROR, "A create member attribute request is required.");
            return response;
        }

        ServiceValidationHelper.TryNormalizeString(request.Key, "Attribute key", response, out string normalizedKey);
        ServiceValidationHelper.TryNormalizeString(request.Value, "Attribute value", response, out string normalizedValue);

        if (ServiceValidationHelper.HasErrors(response))
        {
            return response;
        }

        var memberExists = await _dbContext.HouseholdMembers
            .AsNoTracking()
            .AnyAsync(m => m.TenantId == tenantId && m.HouseholdId == householdId && m.Id == memberId, cancellationToken);

        if (!memberExists)
        {
            response.AddResponseMessage(MessageType.ERROR, "Household member not found.");
            return response;
        }

        var duplicateExists = await _dbContext.MemberAttributes
            .AsNoTracking()
            .AnyAsync(a => a.TenantId == tenantId && a.HouseholdMemberId == memberId && a.Key == normalizedKey, cancellationToken);

        if (duplicateExists)
        {
            response.AddResponseMessage(MessageType.ERROR, $"An attribute with key '{normalizedKey}' already exists for this member.");
            return response;
        }

        var attribute = new MemberAttribute
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            HouseholdMemberId = memberId,
            Key = normalizedKey,
            Value = normalizedValue,
        };

        _dbContext.MemberAttributes.Add(attribute);
        await _dbContext.SaveChangesAsync(cancellationToken);

        response.SetSuccess(MapToDto(attribute));
        return response;
    }

    public async Task<MemberAttributeResponse> UpdateAsync(
        Guid tenantId,
        Guid householdId,
        Guid memberId,
        Guid attributeId,
        UpdateMemberAttributeRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = new MemberAttributeResponse();
        ServiceValidationHelper.ValidateTenantContext(tenantId, _currentTenantContext, response);

        if (request is null)
        {
            response.AddResponseMessage(MessageType.ERROR, "An update member attribute request is required.");
            return response;
        }

        ServiceValidationHelper.TryNormalizeString(request.Key, "Attribute key", response, out string normalizedKey);
        ServiceValidationHelper.TryNormalizeString(request.Value, "Attribute value", response, out string normalizedValue);

        if (ServiceValidationHelper.HasErrors(response))
        {
            return response;
        }

        var attribute = await _dbContext.MemberAttributes
            .Where(a => a.TenantId == tenantId && a.HouseholdMemberId == memberId && a.Id == attributeId)
            .SingleOrDefaultAsync(cancellationToken);

        if (attribute is null)
        {
            response.AddResponseMessage(MessageType.ERROR, "Member attribute not found.");
            return response;
        }

        if (!string.Equals(attribute.Key, normalizedKey, StringComparison.Ordinal))
        {
            var duplicateExists = await _dbContext.MemberAttributes
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

        await _dbContext.SaveChangesAsync(cancellationToken);

        response.SetSuccess(MapToDto(attribute));
        return response;
    }

    public async Task<MemberAttributeResponse> DeleteAsync(
        Guid tenantId,
        Guid householdId,
        Guid memberId,
        Guid attributeId,
        CancellationToken cancellationToken = default)
    {
        var response = new MemberAttributeResponse();
        ServiceValidationHelper.ValidateTenantContext(tenantId, _currentTenantContext, response);

        if (ServiceValidationHelper.HasErrors(response))
        {
            return response;
        }

        var attribute = await _dbContext.MemberAttributes
            .Where(a => a.TenantId == tenantId && a.HouseholdMemberId == memberId && a.Id == attributeId)
            .SingleOrDefaultAsync(cancellationToken);

        if (attribute is null)
        {
            response.AddResponseMessage(MessageType.ERROR, "Member attribute not found.");
            return response;
        }

        if (attribute.IsDeleted)
        {
            response.AddResponseMessage(MessageType.WARNING, "Member attribute already deleted.");
            return response;
        }

        attribute.IsDeleted = true;

        await _dbContext.SaveChangesAsync(cancellationToken);

        response.SetSuccess(MapToDto(attribute));
        return response;
    }

    private static MemberAttributeDto MapToDto(MemberAttribute attr) => new(
        attr.Id,
        attr.TenantId,
        attr.HouseholdMemberId,
        attr.Key,
        attr.Value,
        attr.CreatedAt,
        attr.UpdatedAt,
        attr.IsDeleted,
        attr.DeletedAt,
        attr.DeletedByUserId);
}
