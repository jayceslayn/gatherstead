using Gatherstead.Api.Contracts.Attributes;
using Gatherstead.Api.Contracts.Responses;
using Gatherstead.Api.Observability;
using Gatherstead.Api.Services.Authorization;
using Gatherstead.Api.Services.Validation;
using Gatherstead.Data;
using Gatherstead.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Gatherstead.Api.Services.Attributes;

public abstract class ParentScopedAttributeServiceBase<TEntity, TDto, TCreate, TUpdate>
    : IParentScopedAttributeService<TDto, TCreate, TUpdate>
    where TEntity : AuditableEntity, IParentScopedAttribute, new()
    where TDto : IAttributeDto
    where TCreate : class, IAttributeWriteRequest
    where TUpdate : class, IAttributeWriteRequest
{
    protected GathersteadDbContext Db { get; }
    protected ICurrentTenantContext TenantContext { get; }
    protected IMemberAuthorizationService Auth { get; }

    protected ParentScopedAttributeServiceBase(
        GathersteadDbContext dbContext,
        ICurrentTenantContext currentTenantContext,
        IMemberAuthorizationService memberAuthorizationService)
    {
        Db = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        TenantContext = currentTenantContext ?? throw new ArgumentNullException(nameof(currentTenantContext));
        Auth = memberAuthorizationService ?? throw new ArgumentNullException(nameof(memberAuthorizationService));
    }

    // ── Subclass hooks ───────────────────────────────────────────────────────

    protected abstract DbSet<TEntity> Set { get; }

    /// Title-cased parent name (e.g. "Property"). Used in "Property attribute" and "Property not found.".
    protected abstract string ParentDisplayName { get; }

    /// Lowercase parent noun (e.g. "property"). Used in "for this property." messages.
    protected abstract string ParentNoun { get; }

    /// Restrict the query to attributes belonging to the given parent (e.g. a.PropertyId == parentId).
    /// Tenant scoping is handled by the global query filter — do not add a TenantId predicate here.
    protected abstract IQueryable<TEntity> ByParent(Guid parentId);

    /// Set the entity's parent FK on a newly-constructed instance.
    protected abstract void SetParentFk(TEntity entity, Guid parentId);

    /// Verify the parent entity exists within the tenant (e.g. via _dbContext.Properties.AnyAsync(...)).
    protected abstract Task<bool> ParentExistsAsync(Guid tenantId, Guid parentId, CancellationToken cancellationToken);

    /// Run the appropriate write-auth gate (CanManageTenantAsync, CanManageHouseholdAsync, etc.).
    protected abstract Task<bool> AuthorizeWriteAsync<T>(
        BaseEntityResponse<T> response,
        Guid tenantId,
        Guid parentId,
        CancellationToken cancellationToken);

    protected abstract TDto MapToDto(TEntity entity);

    /// Apply any extra fields beyond Key/Value/TenantMinRole (e.g. HouseholdMinRole on HouseholdAttribute).
    protected virtual void ApplyExtraCreateFields(TEntity entity, TCreate request) { }
    protected virtual void ApplyExtraUpdateFields(TEntity entity, TUpdate request) { }

    /// Optional read-auth gate (defaults to allow). HouseholdMemberAttribute uses this for sensitive reads;
    /// other services have no read gate.
    protected virtual Task<bool> AuthorizeReadAsync<T>(
        BaseEntityResponse<T> response,
        Guid tenantId,
        Guid parentId,
        CancellationToken cancellationToken) => Task.FromResult(true);

    /// Lookup the caller's HouseholdRole for visibility-bypass purposes. Returns null by default
    /// (most attributes have no household bypass).
    protected virtual Task<HouseholdRole?> GetCallerHouseholdRoleAsync(
        Guid tenantId,
        Guid parentId,
        CancellationToken cancellationToken) => Task.FromResult<HouseholdRole?>(null);

    /// Visibility predicate. Default: caller's TenantRole at or above TenantMinRole.
    /// HouseholdAttribute overrides this to add the HouseholdMinRole bypass.
    protected virtual bool IsVisible(TEntity entity, TenantRole? callerTenantRole, HouseholdRole? callerHouseholdRole)
        => callerTenantRole.HasValue && callerTenantRole.Value <= (TenantRole)entity.TenantMinRole;

    // ── Public API ───────────────────────────────────────────────────────────

    public async Task<BaseEntityResponse<IReadOnlyCollection<TDto>>> ListAsync(
        Guid tenantId,
        Guid parentId,
        IEnumerable<Guid>? ids = null,
        CancellationToken cancellationToken = default)
    {
        var response = new BaseEntityResponse<IReadOnlyCollection<TDto>>();

        if (!ServiceValidationHelper.ValidateTenantContext(tenantId, TenantContext, response))
            return response;
        if (!await AuthorizeReadAsync(response, tenantId, parentId, cancellationToken))
            return response;

        var callerTenantRole = await Auth.GetCallerTenantRoleAsync(tenantId, cancellationToken);
        var callerHouseholdRole = await GetCallerHouseholdRoleAsync(tenantId, parentId, cancellationToken);

        var query = ByParent(parentId).AsNoTracking();

        if (ids is not null)
        {
            var idList = ids.ToList();
            if (idList.Count > 0)
                query = query.Where(a => idList.Contains(a.Id));
        }

        var attributes = await query.ToListAsync(cancellationToken);

        var visible = attributes
            .Where(a => IsVisible(a, callerTenantRole, callerHouseholdRole))
            .Select(MapToDto)
            .ToList();

        return BaseEntityResponse<IReadOnlyCollection<TDto>>.SuccessfulResponse(visible);
    }

    public async Task<BaseEntityResponse<TDto>> GetAsync(
        Guid tenantId,
        Guid parentId,
        Guid attributeId,
        CancellationToken cancellationToken = default)
    {
        var response = new BaseEntityResponse<TDto>();

        if (!ServiceValidationHelper.ValidateTenantContext(tenantId, TenantContext, response))
            return response;
        if (!await AuthorizeReadAsync(response, tenantId, parentId, cancellationToken))
            return response;

        var callerTenantRole = await Auth.GetCallerTenantRoleAsync(tenantId, cancellationToken);
        var callerHouseholdRole = await GetCallerHouseholdRoleAsync(tenantId, parentId, cancellationToken);

        var attribute = await ServiceGuards.LoadOrNotFoundAsync(
            response,
            ByParent(parentId).AsNoTracking().Where(a => a.Id == attributeId),
            DisplayName,
            cancellationToken);

        if (attribute is null) return response;

        if (!IsVisible(attribute, callerTenantRole, callerHouseholdRole))
        {
            response.AddResponseMessage(MessageType.ERROR, $"{DisplayName} not found.");
            return response;
        }

        response.SetSuccess(MapToDto(attribute));
        return response;
    }

    public async Task<BaseEntityResponse<TDto>> CreateAsync(
        Guid tenantId,
        Guid parentId,
        TCreate request,
        CancellationToken cancellationToken = default)
    {
        var response = new BaseEntityResponse<TDto>();

        if (!ServiceValidationHelper.ValidateTenantContext(tenantId, TenantContext, response))
            return response;
        if (!ServiceGuards.RequireRequest(request, $"create {ParentNoun} attribute", response))
            return response;
        if (!await AuthorizeWriteAsync(response, tenantId, parentId, cancellationToken))
            return response;

        ServiceValidationHelper.TryNormalizeString(request.Key, "Attribute key", response, out string normalizedKey);
        ServiceValidationHelper.TryNormalizeString(request.Value, "Attribute value", response, out string normalizedValue);
        if (ServiceValidationHelper.HasErrors(response))
            return response;

        if (!await ParentExistsAsync(tenantId, parentId, cancellationToken))
        {
            response.AddResponseMessage(MessageType.ERROR, $"{ParentDisplayName} not found.");
            return response;
        }

        var duplicateExists = await ByParent(parentId)
            .AsNoTracking()
            .AnyAsync(a => a.Key == normalizedKey, cancellationToken);

        if (duplicateExists)
        {
            response.AddResponseMessage(MessageType.ERROR, $"An attribute with key '{normalizedKey}' already exists for this {ParentNoun}.");
            return response;
        }

        var attribute = new TEntity
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Key = normalizedKey,
            Value = normalizedValue,
            TenantMinRole = request.TenantMinRole,
        };
        SetParentFk(attribute, parentId);
        ApplyExtraCreateFields(attribute, request);

        Set.Add(attribute);
        await Db.SaveChangesAsync(cancellationToken);

        response.SetSuccess(MapToDto(attribute));
        return response;
    }

    public async Task<BaseEntityResponse<TDto>> UpdateAsync(
        Guid tenantId,
        Guid parentId,
        Guid attributeId,
        TUpdate request,
        CancellationToken cancellationToken = default)
    {
        var response = new BaseEntityResponse<TDto>();

        if (!ServiceValidationHelper.ValidateTenantContext(tenantId, TenantContext, response))
            return response;
        if (!ServiceGuards.RequireRequest(request, $"update {ParentNoun} attribute", response))
            return response;
        if (!await AuthorizeWriteAsync(response, tenantId, parentId, cancellationToken))
            return response;

        ServiceValidationHelper.TryNormalizeString(request.Key, "Attribute key", response, out string normalizedKey);
        ServiceValidationHelper.TryNormalizeString(request.Value, "Attribute value", response, out string normalizedValue);
        if (ServiceValidationHelper.HasErrors(response))
            return response;

        var attribute = await ServiceGuards.LoadOrNotFoundAsync(
            response,
            ByParent(parentId).Where(a => a.Id == attributeId),
            DisplayName,
            cancellationToken);

        if (attribute is null) return response;

        if (!string.Equals(attribute.Key, normalizedKey, StringComparison.Ordinal))
        {
            var duplicateExists = await ByParent(parentId)
                .AsNoTracking()
                .AnyAsync(a => a.Key == normalizedKey && a.Id != attributeId, cancellationToken);

            if (duplicateExists)
            {
                response.AddResponseMessage(MessageType.ERROR, $"An attribute with key '{normalizedKey}' already exists for this {ParentNoun}.");
                return response;
            }
        }

        attribute.Key = normalizedKey;
        attribute.Value = normalizedValue;
        attribute.TenantMinRole = request.TenantMinRole;
        ApplyExtraUpdateFields(attribute, request);

        await Db.SaveChangesAsync(cancellationToken);

        response.SetSuccess(MapToDto(attribute));
        return response;
    }

    public async Task<BaseEntityResponse<TDto>> DeleteAsync(
        Guid tenantId,
        Guid parentId,
        Guid attributeId,
        CancellationToken cancellationToken = default)
    {
        var response = new BaseEntityResponse<TDto>();

        if (!ServiceValidationHelper.ValidateTenantContext(tenantId, TenantContext, response))
            return response;
        if (!await AuthorizeWriteAsync(response, tenantId, parentId, cancellationToken))
            return response;

        var attribute = await ServiceGuards.LoadOrNotFoundAsync(
            response,
            ByParent(parentId).Where(a => a.Id == attributeId),
            DisplayName,
            cancellationToken);

        if (attribute is null) return response;

        if (attribute.IsDeleted)
        {
            response.AddResponseMessage(MessageType.WARNING, $"{DisplayName} already deleted.");
            return response;
        }

        attribute.IsDeleted = true;

        await Db.SaveChangesAsync(cancellationToken);

        GathersteadMetrics.RecordSoftDelete(typeof(TEntity).Name, tenantId);
        response.SetSuccess(MapToDto(attribute));
        return response;
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private string DisplayName => $"{ParentDisplayName} attribute";
}
