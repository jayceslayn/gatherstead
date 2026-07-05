using Gatherstead.Api.Contracts.Accommodations;
using Gatherstead.Api.Contracts.Attributes;
using Gatherstead.Api.Contracts.Responses;
using Gatherstead.Api.Services.Attributes;
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
    private readonly IAuditVisibilityContext _auditVisibility;

    public AccommodationService(
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

        var accommodations = await query.ToListAsync(cancellationToken);

        // List endpoints omit child collections (beds/attributes) — clients fetch them via single-GET.
        return BaseEntityResponse<IReadOnlyCollection<AccommodationDto>>.SuccessfulResponse(
            accommodations.Select(a => MapToDto(a, [], [])).ToList());
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
                .Include(a => a.Attributes)
                .Include(a => a.Beds)
                .Where(a => a.TenantId == tenantId && a.PropertyId == propertyId && a.Id == accommodationId),
            EntityDisplayName,
            cancellationToken);

        if (accommodation is null) return response;

        var callerRole = await _memberAuthorizationService.GetCallerTenantRoleAsync(tenantId, cancellationToken);
        response.SetSuccess(MapToDto(accommodation, MapBeds(accommodation.Beds), VisibleAttributes(accommodation.Attributes, callerRole)));
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
            response.AddResponseMessage(MessageType.ERROR, ErrorCode.ENTITY_NOT_FOUND,
                "Property not found.",
                new Dictionary<string, string> { ["entity"] = "property" });
            return response;
        }

        var duplicateExists = await _dbContext.Accommodations
            .AsNoTracking()
            .AnyAsync(a => a.TenantId == tenantId && a.PropertyId == propertyId && a.Name == normalizedName, cancellationToken);

        if (duplicateExists)
        {
            response.AddResponseMessage(MessageType.ERROR, ErrorCode.ENTITY_CONFLICT,
                $"An accommodation named '{normalizedName}' already exists in this property.",
                new Dictionary<string, string> { ["entity"] = "accommodation", ["name"] = normalizedName });
            return response;
        }

        var accommodation = new Accommodation
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            PropertyId = propertyId,
            Name = normalizedName,
            Type = request.Type,
            WidthMeters = request.WidthMeters,
            DepthMeters = request.DepthMeters,
            AreaSqMeters = request.AreaSqMeters,
            Notes = request.Notes?.Trim(),
        };

        _dbContext.Accommodations.Add(accommodation);

        // Synced before the first SaveChanges so the accommodation and its beds flush together;
        // the returned rows also serve the response without a re-query.
        IReadOnlyList<BedDto> beds = [];
        if (request.Beds is { Count: > 0 })
        {
            beds = MapBeds(await BedSyncHelper.SyncAsync(_dbContext.AccommodationBeds, tenantId, accommodation.Id, request.Beds, cancellationToken));
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        var callerRole = await _memberAuthorizationService.GetCallerTenantRoleAsync(tenantId, cancellationToken);
        List<AttributeDto> attrs = [];

        if (request.Attributes is { Count: > 0 })
        {
            await AttributeSyncHelper.SyncAsync(
                _dbContext.AccommodationAttributes.Where(a => a.AccommodationId == accommodation.Id),
                _dbContext.AccommodationAttributes,
                request.Attributes,
                a => callerRole.HasValue && callerRole.Value <= (TenantRole)a.TenantMinRole,
                tenantId,
                () => new AccommodationAttribute { TenantId = tenantId, AccommodationId = accommodation.Id },
                applyExtra: null,
                cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);

            var savedAttrs = await _dbContext.AccommodationAttributes.AsNoTracking()
                .Where(a => a.AccommodationId == accommodation.Id).ToListAsync(cancellationToken);
            attrs = VisibleAttributes(savedAttrs, callerRole);
        }

        response.SetSuccess(MapToDto(accommodation, beds, attrs));
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
                response.AddResponseMessage(MessageType.ERROR, ErrorCode.ENTITY_CONFLICT,
                    $"An accommodation named '{normalizedName}' already exists in this property.",
                    new Dictionary<string, string> { ["entity"] = "accommodation", ["name"] = normalizedName });
                return response;
            }
        }

        accommodation.Name = normalizedName;
        accommodation.Type = request.Type;
        accommodation.WidthMeters = request.WidthMeters;
        accommodation.DepthMeters = request.DepthMeters;
        accommodation.AreaSqMeters = request.AreaSqMeters;
        accommodation.Notes = request.Notes?.Trim();

        var callerRole = await _memberAuthorizationService.GetCallerTenantRoleAsync(tenantId, cancellationToken);

        if (request.Attributes is not null)
        {
            await AttributeSyncHelper.SyncAsync(
                _dbContext.AccommodationAttributes.Where(a => a.AccommodationId == accommodationId),
                _dbContext.AccommodationAttributes,
                request.Attributes,
                a => callerRole.HasValue && callerRole.Value <= (TenantRole)a.TenantMinRole,
                tenantId,
                () => new AccommodationAttribute { TenantId = tenantId, AccommodationId = accommodationId },
                applyExtra: null,
                cancellationToken);
        }

        // Beds use full-replace semantics: supplying the array (even empty) replaces the inventory;
        // omitting it leaves beds untouched. The sync result serves the response without a re-query.
        IReadOnlyList<BedDto>? beds = null;
        if (request.Beds is not null)
        {
            beds = MapBeds(await BedSyncHelper.SyncAsync(_dbContext.AccommodationBeds, tenantId, accommodationId, request.Beds, cancellationToken));
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        var savedAttrs = await _dbContext.AccommodationAttributes.AsNoTracking()
            .Where(a => a.AccommodationId == accommodationId).ToListAsync(cancellationToken);
        response.SetSuccess(MapToDto(accommodation, beds ?? await LoadBedsAsync(accommodationId, cancellationToken), VisibleAttributes(savedAttrs, callerRole)));
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

        var childAttrs = await _dbContext.AccommodationAttributes
            .Where(a => a.AccommodationId == accommodationId).ToListAsync(cancellationToken);
        foreach (var attr in childAttrs)
            attr.IsDeleted = true;

        var childBeds = await _dbContext.AccommodationBeds
            .Where(b => b.AccommodationId == accommodationId).ToListAsync(cancellationToken);
        foreach (var bed in childBeds)
            bed.IsDeleted = true;

        await _dbContext.SaveChangesAsync(cancellationToken);

        response.SetSuccess(MapToDto(accommodation, [], []));
        return response;
    }

    private static List<AttributeDto> VisibleAttributes(
        IEnumerable<AccommodationAttribute> attrs, TenantRole? callerRole)
        => attrs
            .Where(a => callerRole.HasValue && callerRole.Value <= (TenantRole)a.TenantMinRole)
            .OrderBy(a => a.Key)
            .Select(a => new AttributeDto(a.Id, a.Key, a.Value, a.TenantMinRole))
            .ToList();

    private async Task<IReadOnlyList<BedDto>> LoadBedsAsync(Guid accommodationId, CancellationToken ct)
    {
        var beds = await _dbContext.AccommodationBeds.AsNoTracking()
            .Where(b => b.AccommodationId == accommodationId)
            .ToListAsync(ct);
        return MapBeds(beds);
    }

    private static IReadOnlyList<BedDto> MapBeds(IEnumerable<AccommodationBed> beds) => beds
        .OrderBy(b => b.Size)
        .Select(b => new BedDto(b.Id, b.Size, b.Quantity))
        .ToList();

    /// <summary>Area override when set, otherwise width × depth. Null when neither is available.</summary>
    private static decimal? EffectiveArea(Accommodation a)
        => a.AreaSqMeters ?? (a.WidthMeters.HasValue && a.DepthMeters.HasValue
            ? a.WidthMeters.Value * a.DepthMeters.Value
            : null);

    private AccommodationDto MapToDto(Accommodation a, IReadOnlyList<BedDto> beds, IReadOnlyList<AttributeDto> attributes) => new(
        a.Id, a.TenantId, a.PropertyId, a.Name, a.Type,
        a.WidthMeters, a.DepthMeters, a.AreaSqMeters, EffectiveArea(a), a.Notes,
        beds,
        attributes,
        a.ToAuditInfo(_auditVisibility.IncludeAudit));
}
