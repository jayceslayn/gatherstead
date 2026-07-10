using Gatherstead.Api.Contracts.Attributes;
using Gatherstead.Api.Contracts.Households;
using Gatherstead.Api.Contracts.Responses;
using Gatherstead.Api.Observability;
using Gatherstead.Api.Services.Attributes;
using Gatherstead.Api.Services.Authorization;
using Gatherstead.Api.Services.Validation;
using Gatherstead.Data;
using Gatherstead.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Gatherstead.Api.Services.Households;

public class HouseholdService : IHouseholdService
{
    private readonly GathersteadDbContext _dbContext;
    private readonly ICurrentTenantContext _currentTenantContext;
    private readonly IMemberAuthorizationService _memberAuthorizationService;
    private readonly IAuditVisibilityContext _auditVisibility;

    public HouseholdService(
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

    public async Task<BaseEntityResponse<IReadOnlyCollection<HouseholdDto>>> ListAsync(
        Guid tenantId,
        IEnumerable<Guid>? ids = null,
        CancellationToken cancellationToken = default)
    {
        var response = new BaseEntityResponse<IReadOnlyCollection<HouseholdDto>>();

        if (!ServiceValidationHelper.ValidateTenantContext(tenantId, _currentTenantContext, response))
            return response;

        // Attributes ride along on lists (mirrors GetAsync) so list-sourced edits don't wipe them.
        // Visibility is per-row: the caller's tenant role, or their role in that household (all
        // resolved from one cached lookup, no N+1). Skip loading attributes when the caller has
        // neither, since none would be visible.
        var tenantRole = await _memberAuthorizationService.GetCallerTenantRoleAsync(tenantId, cancellationToken);
        var householdRoles = await _memberAuthorizationService.GetCallerHouseholdRolesAsync(tenantId, cancellationToken);

        var query = _dbContext.Households
            .AsNoTracking()
            .Where(h => h.TenantId == tenantId);
        if (tenantRole is not null || householdRoles.Count > 0)
            query = query.Include(h => h.Attributes);

        if (ids is not null)
        {
            var idList = ids.ToList();
            if (idList.Count > 0)
                query = query.Where(h => idList.Contains(h.Id));
        }

        // Ordered by name. Sorted in-memory because Name is an Always Encrypted (PII) column and
        // cannot be ORDER BY'd in SQL.
        var households = (await query.ToListAsync(cancellationToken))
            .OrderBy(h => h.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return BaseEntityResponse<IReadOnlyCollection<HouseholdDto>>.SuccessfulResponse(
            households.Select(h => MapToDto(h, AttributeVisibilityHelper.Visible(
                h.Attributes, tenantRole, householdRoles.RoleFor(h.Id)))).ToList());
    }

    public async Task<HouseholdResponse> GetAsync(
        Guid tenantId,
        Guid householdId,
        CancellationToken cancellationToken = default)
    {
        var response = new HouseholdResponse();

        if (!ServiceValidationHelper.ValidateTenantContext(tenantId, _currentTenantContext, response))
            return response;

        var household = await _dbContext.Households
            .AsNoTracking()
            .Include(h => h.Attributes)
            .Where(h => h.TenantId == tenantId && h.Id == householdId)
            .SingleOrDefaultAsync(cancellationToken);

        if (household is null)
        {
            response.AddResponseMessage(MessageType.ERROR, "Household not found.");
            return response;
        }

        var tenantRole = await _memberAuthorizationService.GetCallerTenantRoleAsync(tenantId, cancellationToken);
        var householdRole = await _memberAuthorizationService.GetCallerHouseholdRoleAsync(tenantId, householdId, cancellationToken);

        response.SetSuccess(MapToDto(household, AttributeVisibilityHelper.Visible(household.Attributes, tenantRole, householdRole)));
        return response;
    }

    public async Task<HouseholdResponse> CreateAsync(
        Guid tenantId,
        CreateHouseholdRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = new HouseholdResponse();

        if (!ServiceValidationHelper.ValidateTenantContext(tenantId, _currentTenantContext, response))
            return response;

        if (request is null)
        {
            response.AddResponseMessage(MessageType.ERROR, "A create household request is required.");
            return response;
        }

        ServiceValidationHelper.TryNormalizeString(request.Name, "Household name", response, out string normalizedName);

        if (ServiceValidationHelper.HasErrors(response))
            return response;

        var household = new Household
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = normalizedName,
            Notes = request.Notes?.Trim(),
        };

        _dbContext.Households.Add(household);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var tenantRole = await _memberAuthorizationService.GetCallerTenantRoleAsync(tenantId, cancellationToken);
        var householdRole = await _memberAuthorizationService.GetCallerHouseholdRoleAsync(tenantId, household.Id, cancellationToken);
        List<AttributeDto> attrs = [];

        if (request.Attributes is { Count: > 0 })
        {
            await AttributeSyncHelper.SyncAsync(
                _dbContext.HouseholdAttributes.Where(a => a.HouseholdId == household.Id),
                _dbContext.HouseholdAttributes,
                request.Attributes,
                a => AttributeVisibilityHelper.IsVisible(a, tenantRole, householdRole),
                tenantId,
                () => new HouseholdAttribute { TenantId = tenantId, HouseholdId = household.Id },
                applyExtra: (attr, entry) => attr.HouseholdMinRole = entry.HouseholdMinRole,
                cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);

            var savedAttrs = await _dbContext.HouseholdAttributes.AsNoTracking()
                .Where(a => a.HouseholdId == household.Id).ToListAsync(cancellationToken);
            attrs = AttributeVisibilityHelper.Visible(savedAttrs, tenantRole, householdRole);
        }

        GathersteadMetrics.RecordHouseholdCreated(tenantId);
        response.SetSuccess(MapToDto(household, attrs));
        return response;
    }

    public async Task<HouseholdResponse> UpdateAsync(
        Guid tenantId,
        Guid householdId,
        UpdateHouseholdRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = new HouseholdResponse();

        if (!ServiceValidationHelper.ValidateTenantContext(tenantId, _currentTenantContext, response))
            return response;

        if (request is null)
        {
            response.AddResponseMessage(MessageType.ERROR, "An update household request is required.");
            return response;
        }

        ServiceValidationHelper.TryNormalizeString(request.Name, "Household name", response, out string normalizedName);

        if (ServiceValidationHelper.HasErrors(response))
            return response;

        if (!await _memberAuthorizationService.CanManageHouseholdAsync(tenantId, householdId, cancellationToken))
        {
            response.AddResponseMessage(MessageType.ERROR, "You do not have permission to manage this household.");
            return response;
        }

        var household = await _dbContext.Households
            .Where(h => h.TenantId == tenantId && h.Id == householdId)
            .SingleOrDefaultAsync(cancellationToken);

        if (household is null)
        {
            response.AddResponseMessage(MessageType.ERROR, "Household not found.");
            return response;
        }

        household.Name = normalizedName;
        household.Notes = request.Notes?.Trim();

        var tenantRole = await _memberAuthorizationService.GetCallerTenantRoleAsync(tenantId, cancellationToken);
        var householdRole = await _memberAuthorizationService.GetCallerHouseholdRoleAsync(tenantId, householdId, cancellationToken);

        if (request.Attributes is not null)
        {
            await AttributeSyncHelper.SyncAsync(
                _dbContext.HouseholdAttributes.Where(a => a.HouseholdId == householdId),
                _dbContext.HouseholdAttributes,
                request.Attributes,
                a => AttributeVisibilityHelper.IsVisible(a, tenantRole, householdRole),
                tenantId,
                () => new HouseholdAttribute { TenantId = tenantId, HouseholdId = householdId },
                applyExtra: (attr, entry) => attr.HouseholdMinRole = entry.HouseholdMinRole,
                cancellationToken);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        var savedAttrs = await _dbContext.HouseholdAttributes.AsNoTracking()
            .Where(a => a.HouseholdId == householdId).ToListAsync(cancellationToken);
        response.SetSuccess(MapToDto(household, AttributeVisibilityHelper.Visible(savedAttrs, tenantRole, householdRole)));
        return response;
    }

    public async Task<HouseholdResponse> DeleteAsync(
        Guid tenantId,
        Guid householdId,
        CancellationToken cancellationToken = default)
    {
        var response = new HouseholdResponse();

        if (!ServiceValidationHelper.ValidateTenantContext(tenantId, _currentTenantContext, response))
            return response;

        if (!await _memberAuthorizationService.CanManageHouseholdAsync(tenantId, householdId, cancellationToken))
        {
            response.AddResponseMessage(MessageType.ERROR, "You do not have permission to manage this household.");
            return response;
        }

        var household = await _dbContext.Households
            .Where(h => h.TenantId == tenantId && h.Id == householdId)
            .SingleOrDefaultAsync(cancellationToken);

        if (household is null)
        {
            response.AddResponseMessage(MessageType.ERROR, "Household not found.");
            return response;
        }

        if (household.IsDeleted)
        {
            response.AddResponseMessage(MessageType.WARNING, "Household already deleted.");
            return response;
        }

        household.IsDeleted = true;

        var childAttrs = await _dbContext.HouseholdAttributes
            .Where(a => a.HouseholdId == householdId).ToListAsync(cancellationToken);
        foreach (var attr in childAttrs)
            attr.IsDeleted = true;

        await _dbContext.SaveChangesAsync(cancellationToken);

        GathersteadMetrics.RecordSoftDelete("Household", tenantId);
        response.SetSuccess(MapToDto(household, []));
        return response;
    }

    private HouseholdDto MapToDto(Household household, IReadOnlyList<AttributeDto> attributes) => new(
        household.Id,
        household.TenantId,
        household.Name,
        household.Notes,
        attributes,
        household.ToAuditInfo(_auditVisibility.IncludeAudit));
}
