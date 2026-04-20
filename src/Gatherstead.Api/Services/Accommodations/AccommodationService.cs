using Gatherstead.Api.Contracts.Accommodations;
using Gatherstead.Api.Contracts.Responses;
using Gatherstead.Api.Services.Authorization;
using Gatherstead.Api.Services.Validation;
using Gatherstead.Data;
using Gatherstead.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Gatherstead.Api.Services.Accommodations;

public class AccommodationService : IAccommodationService
{
    private const string EntityDisplayName = "Accommodation";

    private readonly GathersteadDbContext _dbContext;
    private readonly ICurrentTenantContext _currentTenantContext;
    private readonly IMemberAuthorizationService _memberAuthorizationService;

    public AccommodationService(
        GathersteadDbContext dbContext,
        ICurrentTenantContext currentTenantContext,
        IMemberAuthorizationService memberAuthorizationService)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _currentTenantContext = currentTenantContext ?? throw new ArgumentNullException(nameof(currentTenantContext));
        _memberAuthorizationService = memberAuthorizationService ?? throw new ArgumentNullException(nameof(memberAuthorizationService));
    }

    public async Task<BaseEntityResponse<IReadOnlyCollection<AccommodationDto>>> ListAsync(
        Guid tenantId,
        Guid propertyId,
        IEnumerable<Guid>? ids = null,
        CancellationToken cancellationToken = default)
    {
        var response = new BaseEntityResponse<IReadOnlyCollection<AccommodationDto>>();

        if (!ServiceValidationHelper.ValidateTenantContext(tenantId, _currentTenantContext, response))
            return response;

        var query = _dbContext.Accommodations
            .AsNoTracking()
            .Where(a => a.TenantId == tenantId && a.PropertyId == propertyId);

        if (ids is not null)
        {
            var idList = ids.ToList();
            if (idList.Count > 0)
                query = query.Where(a => idList.Contains(a.Id));
        }

        var accommodations = await query.Select(a => MapToDto(a)).ToListAsync(cancellationToken);

        return BaseEntityResponse<IReadOnlyCollection<AccommodationDto>>.SuccessfulResponse(accommodations);
    }

    public async Task<AccommodationResponse> GetAsync(
        Guid tenantId,
        Guid propertyId,
        Guid accommodationId,
        CancellationToken cancellationToken = default)
    {
        var response = new AccommodationResponse();

        if (!ServiceValidationHelper.ValidateTenantContext(tenantId, _currentTenantContext, response))
            return response;

        var accommodation = await ServiceGuards.LoadOrNotFoundAsync(
            response,
            _dbContext.Accommodations.AsNoTracking()
                .Where(a => a.TenantId == tenantId && a.PropertyId == propertyId && a.Id == accommodationId),
            EntityDisplayName,
            cancellationToken);

        if (accommodation is null) return response;

        response.SetSuccess(MapToDto(accommodation));
        return response;
    }

    public async Task<AccommodationResponse> CreateAsync(
        Guid tenantId,
        Guid propertyId,
        CreateAccommodationRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = new AccommodationResponse();

        if (!ServiceValidationHelper.ValidateTenantContext(tenantId, _currentTenantContext, response))
            return response;
        if (!ServiceGuards.RequireRequest(request, "create accommodation", response))
            return response;
        if (!await ServiceGuards.AuthorizeTenantManageAsync(response, _memberAuthorizationService, tenantId, cancellationToken))
            return response;

        ServiceValidationHelper.TryNormalizeString(request.Name, "Accommodation name", response, out string normalizedName);
        if (ServiceValidationHelper.HasErrors(response))
            return response;

        var propertyExists = await _dbContext.Properties
            .AsNoTracking()
            .AnyAsync(p => p.TenantId == tenantId && p.Id == propertyId, cancellationToken);

        if (!propertyExists)
        {
            response.AddResponseMessage(MessageType.ERROR, "Property not found.");
            return response;
        }

        var duplicateExists = await _dbContext.Accommodations
            .AsNoTracking()
            .AnyAsync(a => a.TenantId == tenantId && a.PropertyId == propertyId && a.Name == normalizedName, cancellationToken);

        if (duplicateExists)
        {
            response.AddResponseMessage(MessageType.ERROR, $"An accommodation named '{normalizedName}' already exists in this property.");
            return response;
        }

        var accommodation = new Accommodation
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            PropertyId = propertyId,
            Name = normalizedName,
            Type = request.Type,
            CapacityAdults = request.CapacityAdults,
            CapacityChildren = request.CapacityChildren,
            Notes = request.Notes?.Trim(),
        };

        _dbContext.Accommodations.Add(accommodation);
        await _dbContext.SaveChangesAsync(cancellationToken);

        response.SetSuccess(MapToDto(accommodation));
        return response;
    }

    public async Task<AccommodationResponse> UpdateAsync(
        Guid tenantId,
        Guid propertyId,
        Guid accommodationId,
        UpdateAccommodationRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = new AccommodationResponse();

        if (!ServiceValidationHelper.ValidateTenantContext(tenantId, _currentTenantContext, response))
            return response;
        if (!ServiceGuards.RequireRequest(request, "update accommodation", response))
            return response;
        if (!await ServiceGuards.AuthorizeTenantManageAsync(response, _memberAuthorizationService, tenantId, cancellationToken))
            return response;

        ServiceValidationHelper.TryNormalizeString(request.Name, "Accommodation name", response, out string normalizedName);
        if (ServiceValidationHelper.HasErrors(response))
            return response;

        var accommodation = await ServiceGuards.LoadOrNotFoundAsync(
            response,
            _dbContext.Accommodations
                .Where(a => a.TenantId == tenantId && a.PropertyId == propertyId && a.Id == accommodationId),
            EntityDisplayName,
            cancellationToken);

        if (accommodation is null) return response;

        if (!string.Equals(accommodation.Name, normalizedName, StringComparison.Ordinal))
        {
            var duplicateExists = await _dbContext.Accommodations
                .AsNoTracking()
                .AnyAsync(a => a.TenantId == tenantId && a.PropertyId == propertyId && a.Name == normalizedName && a.Id != accommodationId, cancellationToken);

            if (duplicateExists)
            {
                response.AddResponseMessage(MessageType.ERROR, $"An accommodation named '{normalizedName}' already exists in this property.");
                return response;
            }
        }

        accommodation.Name = normalizedName;
        accommodation.Type = request.Type;
        accommodation.CapacityAdults = request.CapacityAdults;
        accommodation.CapacityChildren = request.CapacityChildren;
        accommodation.Notes = request.Notes?.Trim();

        await _dbContext.SaveChangesAsync(cancellationToken);

        response.SetSuccess(MapToDto(accommodation));
        return response;
    }

    public async Task<AccommodationResponse> DeleteAsync(
        Guid tenantId,
        Guid propertyId,
        Guid accommodationId,
        CancellationToken cancellationToken = default)
    {
        var response = new AccommodationResponse();

        if (!ServiceValidationHelper.ValidateTenantContext(tenantId, _currentTenantContext, response))
            return response;
        if (!await ServiceGuards.AuthorizeTenantManageAsync(response, _memberAuthorizationService, tenantId, cancellationToken))
            return response;

        var accommodation = await ServiceGuards.LoadOrNotFoundAsync(
            response,
            _dbContext.Accommodations
                .Where(a => a.TenantId == tenantId && a.PropertyId == propertyId && a.Id == accommodationId),
            EntityDisplayName,
            cancellationToken);

        if (accommodation is null) return response;

        if (accommodation.IsDeleted)
        {
            response.AddResponseMessage(MessageType.WARNING, $"{EntityDisplayName} already deleted.");
            return response;
        }

        accommodation.IsDeleted = true;
        await _dbContext.SaveChangesAsync(cancellationToken);

        response.SetSuccess(MapToDto(accommodation));
        return response;
    }

    private static AccommodationDto MapToDto(Accommodation a) => new(
        a.Id, a.TenantId, a.PropertyId, a.Name, a.Type,
        a.CapacityAdults, a.CapacityChildren, a.Notes,
        a.CreatedAt, a.UpdatedAt, a.IsDeleted, a.DeletedAt, a.DeletedByUserId);
}
