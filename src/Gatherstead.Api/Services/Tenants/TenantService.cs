using Gatherstead.Api.Contracts.Attributes;
using Gatherstead.Api.Contracts.Responses;
using Gatherstead.Api.Contracts.Tenants;
using Gatherstead.Api.Observability;
using Gatherstead.Api.Security;
using Gatherstead.Api.Services.Attributes;
using Gatherstead.Api.Services.Authorization;
using Gatherstead.Api.Services.Observability;
using Gatherstead.Api.Services.Validation;
using Gatherstead.Data;
using Gatherstead.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Gatherstead.Api.Services.Tenants;

public class TenantService : ITenantService
{
    private readonly GathersteadDbContext _dbContext;
    private readonly ICurrentTenantContext _currentTenantContext;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IAppAdminContext _appAdminContext;
    private readonly IMemberAuthorizationService _memberAuthorizationService;
    private readonly IAuditVisibilityContext _auditVisibility;
    private readonly ISecurityEventLogger _securityEventLogger;

    public TenantService(
        GathersteadDbContext dbContext,
        ICurrentTenantContext currentTenantContext,
        ICurrentUserContext currentUserContext,
        IAppAdminContext appAdminContext,
        IMemberAuthorizationService memberAuthorizationService,
        IAuditVisibilityContext auditVisibility,
        ISecurityEventLogger securityEventLogger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _currentTenantContext = currentTenantContext ?? throw new ArgumentNullException(nameof(currentTenantContext));
        _currentUserContext = currentUserContext ?? throw new ArgumentNullException(nameof(currentUserContext));
        _appAdminContext = appAdminContext ?? throw new ArgumentNullException(nameof(appAdminContext));
        _memberAuthorizationService = memberAuthorizationService ?? throw new ArgumentNullException(nameof(memberAuthorizationService));
        _auditVisibility = auditVisibility ?? throw new ArgumentNullException(nameof(auditVisibility));
        _securityEventLogger = securityEventLogger ?? throw new ArgumentNullException(nameof(securityEventLogger));
    }

    public async Task<BaseEntityResponse<IReadOnlyCollection<TenantSummary>>> ListAsync(
        Guid userId,
        IEnumerable<Guid>? ids = null,
        CancellationToken cancellationToken = default)
    {
        var response = new BaseEntityResponse<IReadOnlyCollection<TenantSummary>>();

        if (userId == Guid.Empty)
        {
            response.AddResponseMessage(MessageType.ERROR, "A valid user identifier is required.");
            return response;
        }

        // Not tenant-scoped (no {tenantId} route), so the global tenant filter would resolve to
        // TenantId == null and hide every row. Drop only the tenant filter; soft-delete stays
        // enforced on both TenantUser and the projected Tenant navigation.
        var query = _dbContext.TenantUsers
            .IgnoreQueryFilters([GathersteadDbContext.TenantFilter])
            .AsNoTracking()
            .Where(tu => tu.UserId == userId);

        if (ids is not null)
        {
            var idList = ids.ToList();
            if (idList.Count > 0)
                query = query.Where(tu => idList.Contains(tu.TenantId));
        }

        var tenants = await query
            .Select(tu => new TenantSummary(tu.TenantId, tu.Tenant!.Name, tu.Role))
            .ToListAsync(cancellationToken);

        return BaseEntityResponse<IReadOnlyCollection<TenantSummary>>.SuccessfulResponse(tenants);
    }

    public async Task<BaseEntityResponse<IReadOnlyCollection<TenantSummary>>> ListAllAsync(
        IEnumerable<Guid>? ids = null,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Tenants.AsNoTracking().AsQueryable();

        if (ids is not null)
        {
            var idList = ids.ToList();
            if (idList.Count > 0)
                query = query.Where(t => idList.Contains(t.Id));
        }

        var tenants = await query
            .Select(t => new TenantSummary(t.Id, t.Name))
            .ToListAsync(cancellationToken);

        return BaseEntityResponse<IReadOnlyCollection<TenantSummary>>.SuccessfulResponse(tenants);
    }

    public async Task<TenantResponse> GetAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var response = new TenantResponse();

        if (!ServiceValidationHelper.ValidateTenantContext(tenantId, _currentTenantContext, response))
            return response;

        var tenant = await _dbContext.Tenants
            .AsNoTracking()
            .Include(t => t.Attributes)
            .Where(t => t.Id == tenantId)
            .SingleOrDefaultAsync(cancellationToken);

        if (tenant is null)
        {
            response.AddResponseMessage(MessageType.ERROR, "Tenant not found.");
            return response;
        }

        var callerRole = await _memberAuthorizationService.GetCallerTenantRoleAsync(tenantId, cancellationToken);
        response.SetSuccess(MapToDto(tenant, VisibleAttributes(tenant.Attributes, callerRole)));
        return response;
    }

    public async Task<TenantResponse> CreateAsync(
        CreateTenantRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = new TenantResponse();

        if (request is null)
        {
            response.AddResponseMessage(MessageType.ERROR, "A create tenant request is required.");
            return response;
        }

        if (await _appAdminContext.IsAppAdminAsync(cancellationToken) != true)
        {
            response.AddResponseMessage(MessageType.ERROR, "Only App Admins can create tenants.");
            return response;
        }

        ServiceValidationHelper.TryNormalizeString(request.Name, "Tenant name", response, out string normalizedName);

        if (ServiceValidationHelper.HasErrors(response))
            return response;

        var ownerExists = await _dbContext.Users
            .AsNoTracking()
            .AnyAsync(u => u.Id == request.OwnerUserId, cancellationToken);

        if (!ownerExists)
        {
            response.AddResponseMessage(MessageType.ERROR, "The specified owner user was not found.");
            return response;
        }

        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = normalizedName,
            Notes = request.Notes?.Trim(),
        };

        var tenantUser = new TenantUser
        {
            TenantId = tenant.Id,
            UserId = request.OwnerUserId,
            Role = TenantRole.Owner,
        };

        _dbContext.Tenants.Add(tenant);
        _dbContext.TenantUsers.Add(tenantUser);
        await _dbContext.SaveChangesAsync(cancellationToken);

        GathersteadMetrics.RecordTenantCreated();

        // App-admin-gated mutation — record it for the security audit trail.
        await _securityEventLogger.LogAsync(
            SecurityEventType.AppAdminAction, SecurityEventSeverity.Info,
            resource: $"Tenant:{tenant.Id}",
            detail: "{\"action\":\"TenantCreated\"}",
            tenantId: tenant.Id, userId: _currentUserContext.UserId, cancellationToken: cancellationToken);

        response.SetSuccess(MapToDto(tenant, []));
        return response;
    }

    public async Task<TenantResponse> UpdateAsync(
        Guid tenantId,
        UpdateTenantRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = new TenantResponse();

        if (!ServiceValidationHelper.ValidateTenantContext(tenantId, _currentTenantContext, response))
            return response;

        if (request is null)
        {
            response.AddResponseMessage(MessageType.ERROR, "An update tenant request is required.");
            return response;
        }

        ServiceValidationHelper.TryNormalizeString(request.Name, "Tenant name", response, out string normalizedName);

        if (ServiceValidationHelper.HasErrors(response))
            return response;

        var tenant = await _dbContext.Tenants
            .Where(t => t.Id == tenantId)
            .SingleOrDefaultAsync(cancellationToken);

        if (tenant is null)
        {
            response.AddResponseMessage(MessageType.ERROR, "Tenant not found.");
            return response;
        }

        tenant.Name = normalizedName;
        tenant.Notes = request.Notes?.Trim();

        var callerRole = await _memberAuthorizationService.GetCallerTenantRoleAsync(tenantId, cancellationToken);

        if (request.Attributes is not null)
        {
            await AttributeSyncHelper.SyncAsync(
                _dbContext.TenantAttributes.Where(a => a.TenantId == tenantId),
                _dbContext.TenantAttributes,
                request.Attributes,
                a => callerRole.HasValue && callerRole.Value <= (TenantRole)a.TenantMinRole,
                tenantId,
                () => new TenantAttribute { TenantId = tenantId },
                applyExtra: null,
                cancellationToken);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        var savedAttrs = await _dbContext.TenantAttributes.AsNoTracking()
            .Where(a => a.TenantId == tenantId).ToListAsync(cancellationToken);
        response.SetSuccess(MapToDto(tenant, VisibleAttributes(savedAttrs, callerRole)));
        return response;
    }

    public async Task<TenantResponse> DeleteAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var response = new TenantResponse();

        if (!ServiceValidationHelper.ValidateTenantContext(tenantId, _currentTenantContext, response))
            return response;

        var tenant = await _dbContext.Tenants
            .Where(t => t.Id == tenantId)
            .SingleOrDefaultAsync(cancellationToken);

        if (tenant is null)
        {
            response.AddResponseMessage(MessageType.ERROR, "Tenant not found.");
            return response;
        }

        if (tenant.IsDeleted)
        {
            response.AddResponseMessage(MessageType.WARNING, "Tenant already deleted.");
            return response;
        }

        tenant.IsDeleted = true;

        var childAttrs = await _dbContext.TenantAttributes
            .Where(a => a.TenantId == tenantId).ToListAsync(cancellationToken);
        foreach (var attr in childAttrs)
            attr.IsDeleted = true;

        await _dbContext.SaveChangesAsync(cancellationToken);

        GathersteadMetrics.RecordSoftDelete("Tenant", tenantId);
        response.SetSuccess(MapToDto(tenant, []));
        return response;
    }

    private static List<AttributeDto> VisibleAttributes(
        IEnumerable<TenantAttribute> attrs, TenantRole? callerRole)
        => attrs
            .Where(a => callerRole.HasValue && callerRole.Value <= (TenantRole)a.TenantMinRole)
            .OrderBy(a => a.Key)
            .Select(a => new AttributeDto(a.Id, a.Key, a.Value, a.TenantMinRole))
            .ToList();

    private TenantDto MapToDto(Tenant tenant, IReadOnlyList<AttributeDto> attributes) => new(
        tenant.Id,
        tenant.Name,
        tenant.Notes,
        attributes,
        tenant.ToAuditInfo(_auditVisibility.IncludeAudit));
}
