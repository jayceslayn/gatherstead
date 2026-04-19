using Gatherstead.Api.Contracts.ContactMethods;
using Gatherstead.Api.Observability;
using Gatherstead.Api.Contracts.Responses;
using Gatherstead.Api.Services.Authorization;
using Gatherstead.Api.Services.Validation;
using Gatherstead.Data;
using Gatherstead.Data.Entities;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace Gatherstead.Api.Services.ContactMethods;

public class ContactMethodService : IContactMethodService
{
    private readonly GathersteadDbContext _dbContext;
    private readonly ICurrentTenantContext _currentTenantContext;
    private readonly IMemberAuthorizationService _memberAuthorizationService;

    private static readonly Expression<Func<ContactMethod, ContactMethodDto>> MapToDtoExpression =
        contact => new ContactMethodDto(
            contact.Id,
            contact.TenantId,
            contact.HouseholdMemberId,
            contact.Type,
            contact.Value,
            contact.IsPrimary,
            contact.CreatedAt,
            contact.UpdatedAt,
            contact.IsDeleted,
            contact.DeletedAt,
            contact.DeletedByUserId);

    public ContactMethodService(
        GathersteadDbContext dbContext,
        ICurrentTenantContext currentTenantContext,
        IMemberAuthorizationService memberAuthorizationService)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _currentTenantContext = currentTenantContext ?? throw new ArgumentNullException(nameof(currentTenantContext));
        _memberAuthorizationService = memberAuthorizationService ?? throw new ArgumentNullException(nameof(memberAuthorizationService));
    }

    public async Task<BaseEntityResponse<IReadOnlyCollection<ContactMethodDto>>> ListAsync(
        Guid tenantId,
        Guid householdId,
        Guid memberId,
        IEnumerable<Guid>? ids = null,
        CancellationToken cancellationToken = default)
    {
        var response = new BaseEntityResponse<IReadOnlyCollection<ContactMethodDto>>();
        ServiceValidationHelper.ValidateTenantContext(tenantId, _currentTenantContext, response);

        if (ServiceValidationHelper.HasErrors(response))
        {
            return response;
        }

        var query = _dbContext.ContactMethods
            .AsNoTracking()
            .Where(c => c.TenantId == tenantId && c.HouseholdMemberId == memberId);

        if (ids is not null)
        {
            var idList = ids.ToList();
            if (idList.Count > 0)
            {
                query = query.Where(c => idList.Contains(c.Id));
            }
        }

        var contacts = await query
            .Select(MapToDtoExpression)
            .ToListAsync(cancellationToken);

        return BaseEntityResponse<IReadOnlyCollection<ContactMethodDto>>.SuccessfulResponse(contacts);
    }

    public async Task<ContactMethodResponse> GetAsync(
        Guid tenantId,
        Guid householdId,
        Guid memberId,
        Guid contactMethodId,
        CancellationToken cancellationToken = default)
    {
        var response = new ContactMethodResponse();
        ServiceValidationHelper.ValidateTenantContext(tenantId, _currentTenantContext, response);

        if (ServiceValidationHelper.HasErrors(response))
        {
            return response;
        }

        var contact = await _dbContext.ContactMethods
            .AsNoTracking()
            .Where(c => c.TenantId == tenantId && c.HouseholdMemberId == memberId && c.Id == contactMethodId)
            .SingleOrDefaultAsync(cancellationToken);

        if (contact is null)
        {
            response.AddResponseMessage(MessageType.ERROR, "Contact method not found.");
            return response;
        }

        response.SetSuccess(MapToDto(contact));
        return response;
    }

    public async Task<ContactMethodResponse> CreateAsync(
        Guid tenantId,
        Guid householdId,
        Guid memberId,
        CreateContactMethodRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = new ContactMethodResponse();
        ServiceValidationHelper.ValidateTenantContext(tenantId, _currentTenantContext, response);

        if (request is null)
        {
            response.AddResponseMessage(MessageType.ERROR, "A create contact method request is required.");
            return response;
        }

        ServiceValidationHelper.TryNormalizeString(request.Value, "Contact value", response, out string normalizedValue);

        if (ServiceValidationHelper.HasErrors(response))
        {
            return response;
        }

        if (!await _memberAuthorizationService.CanEditMemberAsync(tenantId, householdId, memberId, cancellationToken))
        {
            response.AddResponseMessage(MessageType.ERROR, "You do not have permission to edit this member.");
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

        if (request.IsPrimary)
        {
            await UnsetPrimaryContactsAsync(tenantId, memberId, null, cancellationToken);
        }

        var contact = new ContactMethod
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            HouseholdMemberId = memberId,
            Type = request.Type,
            Value = normalizedValue,
            IsPrimary = request.IsPrimary,
        };

        _dbContext.ContactMethods.Add(contact);
        await _dbContext.SaveChangesAsync(cancellationToken);

        response.SetSuccess(MapToDto(contact));
        return response;
    }

    public async Task<ContactMethodResponse> UpdateAsync(
        Guid tenantId,
        Guid householdId,
        Guid memberId,
        Guid contactMethodId,
        UpdateContactMethodRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = new ContactMethodResponse();
        ServiceValidationHelper.ValidateTenantContext(tenantId, _currentTenantContext, response);

        if (request is null)
        {
            response.AddResponseMessage(MessageType.ERROR, "An update contact method request is required.");
            return response;
        }

        ServiceValidationHelper.TryNormalizeString(request.Value, "Contact value", response, out string normalizedValue);

        if (ServiceValidationHelper.HasErrors(response))
        {
            return response;
        }

        if (!await _memberAuthorizationService.CanEditMemberAsync(tenantId, householdId, memberId, cancellationToken))
        {
            response.AddResponseMessage(MessageType.ERROR, "You do not have permission to edit this member.");
            return response;
        }

        var contact = await _dbContext.ContactMethods
            .Where(c => c.TenantId == tenantId && c.HouseholdMemberId == memberId && c.Id == contactMethodId)
            .SingleOrDefaultAsync(cancellationToken);

        if (contact is null)
        {
            response.AddResponseMessage(MessageType.ERROR, "Contact method not found.");
            return response;
        }

        if (request.IsPrimary)
        {
            await UnsetPrimaryContactsAsync(tenantId, memberId, contactMethodId, cancellationToken);
        }

        contact.Type = request.Type;
        contact.Value = normalizedValue;
        contact.IsPrimary = request.IsPrimary;

        await _dbContext.SaveChangesAsync(cancellationToken);

        response.SetSuccess(MapToDto(contact));
        return response;
    }

    public async Task<ContactMethodResponse> DeleteAsync(
        Guid tenantId,
        Guid householdId,
        Guid memberId,
        Guid contactMethodId,
        CancellationToken cancellationToken = default)
    {
        var response = new ContactMethodResponse();
        ServiceValidationHelper.ValidateTenantContext(tenantId, _currentTenantContext, response);

        if (ServiceValidationHelper.HasErrors(response))
        {
            return response;
        }

        if (!await _memberAuthorizationService.CanEditMemberAsync(tenantId, householdId, memberId, cancellationToken))
        {
            response.AddResponseMessage(MessageType.ERROR, "You do not have permission to edit this member.");
            return response;
        }

        var contact = await _dbContext.ContactMethods
            .Where(c => c.TenantId == tenantId && c.HouseholdMemberId == memberId && c.Id == contactMethodId)
            .SingleOrDefaultAsync(cancellationToken);

        if (contact is null)
        {
            response.AddResponseMessage(MessageType.ERROR, "Contact method not found.");
            return response;
        }

        if (contact.IsDeleted)
        {
            response.AddResponseMessage(MessageType.WARNING, "Contact method already deleted.");
            return response;
        }

        contact.IsDeleted = true;

        await _dbContext.SaveChangesAsync(cancellationToken);

        GathersteadMetrics.RecordSoftDelete("ContactMethod", tenantId);
        response.SetSuccess(MapToDto(contact));
        return response;
    }

    private async Task UnsetPrimaryContactsAsync(Guid tenantId, Guid memberId, Guid? excludeContactId, CancellationToken cancellationToken)
    {
        var existingPrimaries = await _dbContext.ContactMethods
            .Where(c => c.TenantId == tenantId && c.HouseholdMemberId == memberId && c.IsPrimary && c.Id != excludeContactId)
            .ToListAsync(cancellationToken);

        foreach (var existing in existingPrimaries)
        {
            existing.IsPrimary = false;
        }
    }

    private static ContactMethodDto MapToDto(ContactMethod contact) => new(
        contact.Id,
        contact.TenantId,
        contact.HouseholdMemberId,
        contact.Type,
        contact.Value,
        contact.IsPrimary,
        contact.CreatedAt,
        contact.UpdatedAt,
        contact.IsDeleted,
        contact.DeletedAt,
        contact.DeletedByUserId);
}
