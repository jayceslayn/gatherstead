using Gatherstead.Api.Contracts.Households;
using Gatherstead.Api.Contracts.Responses;
using Gatherstead.Api.Services.Validation;
using Gatherstead.Db;
using Gatherstead.Db.Entities;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace Gatherstead.Api.Services.Households;

public class HouseholdService : IHouseholdService
{
    private readonly GathersteadDbContext _dbContext;
    private readonly ICurrentTenantContext _currentTenantContext;
    private static readonly Expression<Func<Household, HouseholdDto>> MapToDtoExpression = household => new HouseholdDto(
        household.Id,
        household.TenantId,
        household.Name,
        household.CreatedAt,
        household.UpdatedAt,
        household.IsDeleted,
        household.DeletedAt,
        household.DeletedByUserId);

    public HouseholdService(
        GathersteadDbContext dbContext,
        ICurrentTenantContext currentTenantContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _currentTenantContext = currentTenantContext ?? throw new ArgumentNullException(nameof(currentTenantContext));
    }

    public async Task<BaseEntityResponse<IReadOnlyCollection<HouseholdDto>>> ListAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var response = new BaseEntityResponse<IReadOnlyCollection<HouseholdDto>>();
        ServiceValidationHelper.ValidateTenantContext(tenantId, _currentTenantContext, response);

        if (ServiceValidationHelper.HasErrors(response))
        {
            return response;
        }

        var households = await _dbContext.Households
            .AsNoTracking()
            .Where(household => household.TenantId == tenantId)
            .Select(MapToDtoExpression)
            .ToListAsync(cancellationToken);

        return BaseEntityResponse<IReadOnlyCollection<HouseholdDto>>.SuccessfulResponse(households);
    }

    public async Task<HouseholdResponse> GetAsync(
        Guid tenantId,
        Guid householdId,
        CancellationToken cancellationToken = default)
    {
        var response = new HouseholdResponse();
        ServiceValidationHelper.ValidateTenantContext(tenantId, _currentTenantContext, response);

        if (ServiceValidationHelper.HasErrors(response))
        {
            return response;
        }

        var household = await _dbContext.Households
            .AsNoTracking()
            .Where(h => h.TenantId == tenantId && h.Id == householdId)
            .SingleOrDefaultAsync(cancellationToken);

        if (household is null)
        {
            response.AddResponseMessage(MessageType.ERROR, "Household not found.");
            return response;
        }

        response.SuccessfulResponse(MapToDto(household));
        return response;
    }

    public async Task<HouseholdResponse> CreateAsync(
        Guid tenantId,
        CreateHouseholdRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = new HouseholdResponse();
        ServiceValidationHelper.ValidateTenantContext(tenantId, _currentTenantContext, response);

        string? normalizedName = null;

        if (request is null)
        {
            response.AddResponseMessage(MessageType.ERROR, "A create household request is required.");
        }

        if (request is not null &&
            !ServiceValidationHelper.TryNormalizeString(request.Name, "Household name", response, out normalizedName))
        {
            return response;
        }

        if (ServiceValidationHelper.HasErrors(response))
        {
            return response;
        }

        var household = new Household
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = normalizedName!
        };

        _dbContext.Households.Add(household);
        await _dbContext.SaveChangesAsync(cancellationToken);

        response.SuccessfulResponse(MapToDto(household));
        return response;
    }

    public async Task<HouseholdResponse> UpdateAsync(
        Guid tenantId,
        Guid householdId,
        UpdateHouseholdRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = new HouseholdResponse();
        ServiceValidationHelper.ValidateTenantContext(tenantId, _currentTenantContext, response);

        string? normalizedName = null;

        if (request is null)
        {
            response.AddResponseMessage(MessageType.ERROR, "An update household request is required.");
        }

        if (request is not null &&
            !ServiceValidationHelper.TryNormalizeString(request.Name, "Household name", response, out normalizedName))
        {
            return response;
        }

        if (ServiceValidationHelper.HasErrors(response))
        {
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

        await _dbContext.SaveChangesAsync(cancellationToken);

        response.SuccessfulResponse(MapToDto(household));
        return response;
    }

    public async Task<HouseholdResponse> DeleteAsync(
        Guid tenantId,
        Guid householdId,
        CancellationToken cancellationToken = default)
    {
        var response = new HouseholdResponse();
        ServiceValidationHelper.ValidateTenantContext(tenantId, _currentTenantContext, response);

        if (ServiceValidationHelper.HasErrors(response))
        {
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

        if (!household.IsDeleted)
        {
            household.IsDeleted = true;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        response.SuccessfulResponse(MapToDto(household));
        return response;
    }

    private static HouseholdDto MapToDto(Household household) => new(
        household.Id,
        household.TenantId,
        household.Name,
        household.CreatedAt,
        household.UpdatedAt,
        household.IsDeleted,
        household.DeletedAt,
        household.DeletedByUserId);
}
