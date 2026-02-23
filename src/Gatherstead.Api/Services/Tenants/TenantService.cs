using Gatherstead.Api.Contracts.Responses;
using Gatherstead.Api.Contracts.Tenants;
using Gatherstead.Api.Services.Validation;
using Gatherstead.Data;
using Gatherstead.Data.Entities;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace Gatherstead.Api.Services.Tenants;

public class TenantService : ITenantService
{
    private readonly GathersteadDbContext _dbContext;
    private readonly ICurrentTenantContext _currentTenantContext;
    private readonly ICurrentUserContext _currentUserContext;

    private static readonly Expression<Func<Tenant, TenantDto>> MapToDtoExpression = tenant => new TenantDto(
        tenant.Id,
        tenant.Name,
        tenant.CreatedAt,
        tenant.UpdatedAt,
        tenant.IsDeleted,
        tenant.DeletedAt,
        tenant.DeletedByUserId);

    public TenantService(
        GathersteadDbContext dbContext,
        ICurrentTenantContext currentTenantContext,
        ICurrentUserContext currentUserContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _currentTenantContext = currentTenantContext ?? throw new ArgumentNullException(nameof(currentTenantContext));
        _currentUserContext = currentUserContext ?? throw new ArgumentNullException(nameof(currentUserContext));
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

        var query = _dbContext.TenantUsers
            .AsNoTracking()
            .Where(tu => tu.UserId == userId);

        if (ids is not null)
        {
            var idList = ids.ToList();
            if (idList.Count > 0)
            {
                query = query.Where(tu => idList.Contains(tu.TenantId));
            }
        }

        var tenants = await query
            .Select(tu => new TenantSummary(tu.TenantId, tu.Tenant!.Name))
            .ToListAsync(cancellationToken);

        return BaseEntityResponse<IReadOnlyCollection<TenantSummary>>.SuccessfulResponse(tenants);
    }

    public async Task<TenantResponse> GetAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var response = new TenantResponse();

        ServiceValidationHelper.ValidateTenantContext(tenantId, _currentTenantContext, response);

        if (ServiceValidationHelper.HasErrors(response))
        {
            return response;
        }

        var tenant = await _dbContext.Tenants
            .AsNoTracking()
            .Where(t => t.Id == tenantId)
            .Select(MapToDtoExpression)
            .SingleOrDefaultAsync(cancellationToken);

        if (tenant is null)
        {
            response.AddResponseMessage(MessageType.ERROR, "Tenant not found.");
            return response;
        }

        response.SetSuccess(tenant);
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

        var userId = _currentUserContext.UserId;
        if (!userId.HasValue)
        {
            response.AddResponseMessage(MessageType.ERROR, "Authentication required.");
            return response;
        }

        ServiceValidationHelper.TryNormalizeString(request.Name, "Tenant name", response, out string normalizedName);

        if (ServiceValidationHelper.HasErrors(response))
        {
            return response;
        }

        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = normalizedName,
        };

        var tenantUser = new TenantUser
        {
            TenantId = tenant.Id,
            UserId = userId.Value,
            Role = TenantRole.Owner,
        };

        _dbContext.Tenants.Add(tenant);
        _dbContext.TenantUsers.Add(tenantUser);
        await _dbContext.SaveChangesAsync(cancellationToken);

        response.SetSuccess(MapToDto(tenant));
        return response;
    }

    public async Task<TenantResponse> UpdateAsync(
        Guid tenantId,
        UpdateTenantRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = new TenantResponse();

        ServiceValidationHelper.ValidateTenantContext(tenantId, _currentTenantContext, response);

        if (request is null)
        {
            response.AddResponseMessage(MessageType.ERROR, "An update tenant request is required.");
            return response;
        }

        ServiceValidationHelper.TryNormalizeString(request.Name, "Tenant name", response, out string normalizedName);

        if (ServiceValidationHelper.HasErrors(response))
        {
            return response;
        }

        var tenant = await _dbContext.Tenants
            .Where(t => t.Id == tenantId)
            .SingleOrDefaultAsync(cancellationToken);

        if (tenant is null)
        {
            response.AddResponseMessage(MessageType.ERROR, "Tenant not found.");
            return response;
        }

        tenant.Name = normalizedName;

        await _dbContext.SaveChangesAsync(cancellationToken);

        response.SetSuccess(MapToDto(tenant));
        return response;
    }

    public async Task<TenantResponse> DeleteAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var response = new TenantResponse();

        ServiceValidationHelper.ValidateTenantContext(tenantId, _currentTenantContext, response);

        if (ServiceValidationHelper.HasErrors(response))
        {
            return response;
        }

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

        await _dbContext.SaveChangesAsync(cancellationToken);

        response.SetSuccess(MapToDto(tenant));
        return response;
    }

    private static TenantDto MapToDto(Tenant tenant) => new(
        tenant.Id,
        tenant.Name,
        tenant.CreatedAt,
        tenant.UpdatedAt,
        tenant.IsDeleted,
        tenant.DeletedAt,
        tenant.DeletedByUserId);
}
