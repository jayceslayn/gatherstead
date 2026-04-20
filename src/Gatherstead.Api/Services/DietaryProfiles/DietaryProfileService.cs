using Gatherstead.Api.Contracts.DietaryProfiles;
using Gatherstead.Api.Observability;
using Gatherstead.Api.Contracts.Responses;
using Gatherstead.Api.Services.Authorization;
using Gatherstead.Api.Services.Validation;
using Gatherstead.Data;
using Gatherstead.Data.Entities;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace Gatherstead.Api.Services.DietaryProfiles;

public class DietaryProfileService : IDietaryProfileService
{
    private const string EntityDisplayName = "Dietary profile";

    private readonly GathersteadDbContext _dbContext;
    private readonly ICurrentTenantContext _currentTenantContext;
    private readonly IMemberAuthorizationService _memberAuthorizationService;

    private static readonly Expression<Func<DietaryProfile, DietaryProfileDto>> MapToDtoExpression =
        profile => new DietaryProfileDto(
            profile.Id,
            profile.TenantId,
            profile.HouseholdMemberId,
            profile.PreferredDiet,
            profile.Allergies,
            profile.Restrictions,
            profile.Notes,
            profile.CreatedAt,
            profile.UpdatedAt,
            profile.IsDeleted,
            profile.DeletedAt,
            profile.DeletedByUserId);

    public DietaryProfileService(
        GathersteadDbContext dbContext,
        ICurrentTenantContext currentTenantContext,
        IMemberAuthorizationService memberAuthorizationService)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _currentTenantContext = currentTenantContext ?? throw new ArgumentNullException(nameof(currentTenantContext));
        _memberAuthorizationService = memberAuthorizationService ?? throw new ArgumentNullException(nameof(memberAuthorizationService));
    }

    public async Task<BaseEntityResponse<IReadOnlyCollection<DietaryProfileDto>>> ListAsync(
        Guid tenantId,
        IEnumerable<Guid>? memberIds = null,
        CancellationToken cancellationToken = default)
    {
        var response = new BaseEntityResponse<IReadOnlyCollection<DietaryProfileDto>>();

        if (!ServiceValidationHelper.ValidateTenantContext(tenantId, _currentTenantContext, response))
            return response;

        var query = _dbContext.DietaryProfiles
            .AsNoTracking()
            .Where(dp => dp.TenantId == tenantId);

        if (memberIds is not null)
        {
            var memberIdList = memberIds.ToList();
            if (memberIdList.Count > 0)
            {
                query = query.Where(dp => memberIdList.Contains(dp.HouseholdMemberId));
            }
        }

        var profiles = await query
            .Select(MapToDtoExpression)
            .ToListAsync(cancellationToken);

        return BaseEntityResponse<IReadOnlyCollection<DietaryProfileDto>>.SuccessfulResponse(profiles);
    }

    public async Task<DietaryProfileResponse> GetAsync(
        Guid tenantId,
        Guid householdId,
        Guid memberId,
        CancellationToken cancellationToken = default)
    {
        var response = new DietaryProfileResponse();

        if (!ServiceValidationHelper.ValidateTenantContext(tenantId, _currentTenantContext, response))
            return response;

        var profile = await ServiceGuards.LoadOrNotFoundAsync(
            response,
            _dbContext.DietaryProfiles
                .AsNoTracking()
                .Where(dp => dp.TenantId == tenantId && dp.HouseholdMemberId == memberId),
            EntityDisplayName,
            cancellationToken);

        if (profile is null) return response;

        response.SetSuccess(MapToDto(profile));
        return response;
    }

    public async Task<DietaryProfileResponse> UpsertAsync(
        Guid tenantId,
        Guid householdId,
        Guid memberId,
        UpsertDietaryProfileRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = new DietaryProfileResponse();

        if (!ServiceValidationHelper.ValidateTenantContext(tenantId, _currentTenantContext, response))
            return response;
        if (!ServiceGuards.RequireRequest(request, "upsert dietary profile", response))
            return response;

        if (!await ServiceGuards.AuthorizeMemberEditAsync(response, _memberAuthorizationService, tenantId, householdId, memberId, cancellationToken))
            return response;
        if (!await ServiceGuards.RequireMemberExistsAsync(response, _dbContext, tenantId, householdId, memberId, cancellationToken))
            return response;

        var existing = await _dbContext.DietaryProfiles
            .Where(dp => dp.TenantId == tenantId && dp.HouseholdMemberId == memberId)
            .SingleOrDefaultAsync(cancellationToken);

        if (existing is null)
        {
            var profile = new DietaryProfile
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                HouseholdMemberId = memberId,
                PreferredDiet = request.PreferredDiet?.Trim() ?? string.Empty,
                Allergies = request.Allergies ?? Array.Empty<string>(),
                Restrictions = request.Restrictions ?? Array.Empty<string>(),
                Notes = request.Notes?.Trim(),
            };

            _dbContext.DietaryProfiles.Add(profile);
            await _dbContext.SaveChangesAsync(cancellationToken);

            response.SetSuccess(MapToDto(profile));
        }
        else
        {
            if (existing.IsDeleted)
            {
                existing.IsDeleted = false;
            }

            existing.PreferredDiet = request.PreferredDiet?.Trim() ?? string.Empty;
            existing.Allergies = request.Allergies ?? Array.Empty<string>();
            existing.Restrictions = request.Restrictions ?? Array.Empty<string>();
            existing.Notes = request.Notes?.Trim();

            await _dbContext.SaveChangesAsync(cancellationToken);

            response.SetSuccess(MapToDto(existing));
        }

        return response;
    }

    public async Task<DietaryProfileResponse> DeleteAsync(
        Guid tenantId,
        Guid householdId,
        Guid memberId,
        CancellationToken cancellationToken = default)
    {
        var response = new DietaryProfileResponse();

        if (!ServiceValidationHelper.ValidateTenantContext(tenantId, _currentTenantContext, response))
            return response;
        if (!await ServiceGuards.AuthorizeMemberEditAsync(response, _memberAuthorizationService, tenantId, householdId, memberId, cancellationToken))
            return response;

        var profile = await ServiceGuards.LoadOrNotFoundAsync(
            response,
            _dbContext.DietaryProfiles
                .Where(dp => dp.TenantId == tenantId && dp.HouseholdMemberId == memberId),
            EntityDisplayName,
            cancellationToken);

        if (profile is null) return response;

        if (profile.IsDeleted)
        {
            response.AddResponseMessage(MessageType.WARNING, $"{EntityDisplayName} already deleted.");
            return response;
        }

        profile.IsDeleted = true;

        await _dbContext.SaveChangesAsync(cancellationToken);

        GathersteadMetrics.RecordSoftDelete("DietaryProfile", tenantId);
        response.SetSuccess(MapToDto(profile));
        return response;
    }

    private static DietaryProfileDto MapToDto(DietaryProfile profile) => new(
        profile.Id,
        profile.TenantId,
        profile.HouseholdMemberId,
        profile.PreferredDiet,
        profile.Allergies,
        profile.Restrictions,
        profile.Notes,
        profile.CreatedAt,
        profile.UpdatedAt,
        profile.IsDeleted,
        profile.DeletedAt,
        profile.DeletedByUserId);
}
