using Gatherstead.Api.Contracts.Addresses;
using Gatherstead.Api.Contracts.Responses;
using Gatherstead.Api.Services.Authorization;
using Gatherstead.Api.Services.Validation;
using Gatherstead.Data;
using Gatherstead.Data.Entities;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace Gatherstead.Api.Services.Addresses;

public class AddressService : IAddressService
{
    private readonly GathersteadDbContext _dbContext;
    private readonly ICurrentTenantContext _currentTenantContext;
    private readonly IMemberAuthorizationService _memberAuthorizationService;

    private static readonly Expression<Func<Address, AddressDto>> MapToDtoExpression =
        address => new AddressDto(
            address.Id,
            address.TenantId,
            address.HouseholdMemberId,
            address.Line1,
            address.Line2,
            address.City,
            address.State,
            address.PostalCode,
            address.Country,
            address.IsPrimary,
            address.CreatedAt,
            address.UpdatedAt,
            address.IsDeleted,
            address.DeletedAt,
            address.DeletedByUserId);

    public AddressService(
        GathersteadDbContext dbContext,
        ICurrentTenantContext currentTenantContext,
        IMemberAuthorizationService memberAuthorizationService)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _currentTenantContext = currentTenantContext ?? throw new ArgumentNullException(nameof(currentTenantContext));
        _memberAuthorizationService = memberAuthorizationService ?? throw new ArgumentNullException(nameof(memberAuthorizationService));
    }

    public async Task<BaseEntityResponse<IReadOnlyCollection<AddressDto>>> ListAsync(
        Guid tenantId,
        Guid householdId,
        Guid memberId,
        IEnumerable<Guid>? ids = null,
        CancellationToken cancellationToken = default)
    {
        var response = new BaseEntityResponse<IReadOnlyCollection<AddressDto>>();
        ServiceValidationHelper.ValidateTenantContext(tenantId, _currentTenantContext, response);

        if (ServiceValidationHelper.HasErrors(response))
        {
            return response;
        }

        var query = _dbContext.Addresses
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

        var addresses = await query
            .Select(MapToDtoExpression)
            .ToListAsync(cancellationToken);

        return BaseEntityResponse<IReadOnlyCollection<AddressDto>>.SuccessfulResponse(addresses);
    }

    public async Task<AddressResponse> GetAsync(
        Guid tenantId,
        Guid householdId,
        Guid memberId,
        Guid addressId,
        CancellationToken cancellationToken = default)
    {
        var response = new AddressResponse();
        ServiceValidationHelper.ValidateTenantContext(tenantId, _currentTenantContext, response);

        if (ServiceValidationHelper.HasErrors(response))
        {
            return response;
        }

        var address = await _dbContext.Addresses
            .AsNoTracking()
            .Where(a => a.TenantId == tenantId && a.HouseholdMemberId == memberId && a.Id == addressId)
            .SingleOrDefaultAsync(cancellationToken);

        if (address is null)
        {
            response.AddResponseMessage(MessageType.ERROR, "Address not found.");
            return response;
        }

        response.SetSuccess(MapToDto(address));
        return response;
    }

    public async Task<AddressResponse> CreateAsync(
        Guid tenantId,
        Guid householdId,
        Guid memberId,
        CreateAddressRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = new AddressResponse();
        ServiceValidationHelper.ValidateTenantContext(tenantId, _currentTenantContext, response);

        if (request is null)
        {
            response.AddResponseMessage(MessageType.ERROR, "A create address request is required.");
            return response;
        }

        ServiceValidationHelper.TryNormalizeString(request.Line1, "Address line 1", response, out string normalizedLine1);
        ServiceValidationHelper.TryNormalizeString(request.City, "City", response, out string normalizedCity);
        ServiceValidationHelper.TryNormalizeString(request.State, "State", response, out string normalizedState);
        ServiceValidationHelper.TryNormalizeString(request.PostalCode, "Postal code", response, out string normalizedPostalCode);
        ServiceValidationHelper.TryNormalizeString(request.Country, "Country", response, out string normalizedCountry);

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
            await UnsetPrimaryAddressesAsync(tenantId, memberId, null, cancellationToken);
        }

        var address = new Address
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            HouseholdMemberId = memberId,
            Line1 = normalizedLine1,
            Line2 = request.Line2?.Trim(),
            City = normalizedCity,
            State = normalizedState,
            PostalCode = normalizedPostalCode,
            Country = normalizedCountry,
            IsPrimary = request.IsPrimary,
        };

        _dbContext.Addresses.Add(address);
        await _dbContext.SaveChangesAsync(cancellationToken);

        response.SetSuccess(MapToDto(address));
        return response;
    }

    public async Task<AddressResponse> UpdateAsync(
        Guid tenantId,
        Guid householdId,
        Guid memberId,
        Guid addressId,
        UpdateAddressRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = new AddressResponse();
        ServiceValidationHelper.ValidateTenantContext(tenantId, _currentTenantContext, response);

        if (request is null)
        {
            response.AddResponseMessage(MessageType.ERROR, "An update address request is required.");
            return response;
        }

        ServiceValidationHelper.TryNormalizeString(request.Line1, "Address line 1", response, out string normalizedLine1);
        ServiceValidationHelper.TryNormalizeString(request.City, "City", response, out string normalizedCity);
        ServiceValidationHelper.TryNormalizeString(request.State, "State", response, out string normalizedState);
        ServiceValidationHelper.TryNormalizeString(request.PostalCode, "Postal code", response, out string normalizedPostalCode);
        ServiceValidationHelper.TryNormalizeString(request.Country, "Country", response, out string normalizedCountry);

        if (ServiceValidationHelper.HasErrors(response))
        {
            return response;
        }

        if (!await _memberAuthorizationService.CanEditMemberAsync(tenantId, householdId, memberId, cancellationToken))
        {
            response.AddResponseMessage(MessageType.ERROR, "You do not have permission to edit this member.");
            return response;
        }

        var address = await _dbContext.Addresses
            .Where(a => a.TenantId == tenantId && a.HouseholdMemberId == memberId && a.Id == addressId)
            .SingleOrDefaultAsync(cancellationToken);

        if (address is null)
        {
            response.AddResponseMessage(MessageType.ERROR, "Address not found.");
            return response;
        }

        if (request.IsPrimary)
        {
            await UnsetPrimaryAddressesAsync(tenantId, memberId, addressId, cancellationToken);
        }

        address.Line1 = normalizedLine1;
        address.Line2 = request.Line2?.Trim();
        address.City = normalizedCity;
        address.State = normalizedState;
        address.PostalCode = normalizedPostalCode;
        address.Country = normalizedCountry;
        address.IsPrimary = request.IsPrimary;

        await _dbContext.SaveChangesAsync(cancellationToken);

        response.SetSuccess(MapToDto(address));
        return response;
    }

    public async Task<AddressResponse> DeleteAsync(
        Guid tenantId,
        Guid householdId,
        Guid memberId,
        Guid addressId,
        CancellationToken cancellationToken = default)
    {
        var response = new AddressResponse();
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

        var address = await _dbContext.Addresses
            .Where(a => a.TenantId == tenantId && a.HouseholdMemberId == memberId && a.Id == addressId)
            .SingleOrDefaultAsync(cancellationToken);

        if (address is null)
        {
            response.AddResponseMessage(MessageType.ERROR, "Address not found.");
            return response;
        }

        if (address.IsDeleted)
        {
            response.AddResponseMessage(MessageType.WARNING, "Address already deleted.");
            return response;
        }

        address.IsDeleted = true;

        await _dbContext.SaveChangesAsync(cancellationToken);

        response.SetSuccess(MapToDto(address));
        return response;
    }

    private async Task UnsetPrimaryAddressesAsync(Guid tenantId, Guid memberId, Guid? excludeAddressId, CancellationToken cancellationToken)
    {
        var existingPrimaries = await _dbContext.Addresses
            .Where(a => a.TenantId == tenantId && a.HouseholdMemberId == memberId && a.IsPrimary && a.Id != excludeAddressId)
            .ToListAsync(cancellationToken);

        foreach (var existing in existingPrimaries)
        {
            existing.IsPrimary = false;
        }
    }

    private static AddressDto MapToDto(Address address) => new(
        address.Id,
        address.TenantId,
        address.HouseholdMemberId,
        address.Line1,
        address.Line2,
        address.City,
        address.State,
        address.PostalCode,
        address.Country,
        address.IsPrimary,
        address.CreatedAt,
        address.UpdatedAt,
        address.IsDeleted,
        address.DeletedAt,
        address.DeletedByUserId);
}
