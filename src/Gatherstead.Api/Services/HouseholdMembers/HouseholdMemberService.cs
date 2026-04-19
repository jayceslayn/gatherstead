using Gatherstead.Api.Contracts.HouseholdMembers;
using Gatherstead.Api.Contracts.Responses;
using Gatherstead.Api.Observability;
using Gatherstead.Api.Services.Authorization;
using Gatherstead.Api.Services.Validation;
using Gatherstead.Data;
using Gatherstead.Data.Entities;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace Gatherstead.Api.Services.HouseholdMembers;

public class HouseholdMemberService : IHouseholdMemberService
{
    private readonly GathersteadDbContext _dbContext;
    private readonly ICurrentTenantContext _currentTenantContext;
    private readonly IMemberAuthorizationService _memberAuthorizationService;

    private static readonly Expression<Func<HouseholdMember, HouseholdMemberDto>> MapToDtoExpression =
        member => new HouseholdMemberDto(
            member.Id,
            member.TenantId,
            member.HouseholdId,
            member.Name,
            member.IsAdult,
            member.AgeBand,
            member.BirthDate,
            member.DietaryNotes,
            member.DietaryTags,
            member.CreatedAt,
            member.UpdatedAt,
            member.IsDeleted,
            member.DeletedAt,
            member.DeletedByUserId);

    public HouseholdMemberService(
        GathersteadDbContext dbContext,
        ICurrentTenantContext currentTenantContext,
        IMemberAuthorizationService memberAuthorizationService)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _currentTenantContext = currentTenantContext ?? throw new ArgumentNullException(nameof(currentTenantContext));
        _memberAuthorizationService = memberAuthorizationService ?? throw new ArgumentNullException(nameof(memberAuthorizationService));
    }

    public async Task<BaseEntityResponse<IReadOnlyCollection<HouseholdMemberDto>>> ListAsync(
        Guid tenantId,
        Guid householdId,
        IEnumerable<Guid>? ids = null,
        CancellationToken cancellationToken = default)
    {
        var response = new BaseEntityResponse<IReadOnlyCollection<HouseholdMemberDto>>();
        ServiceValidationHelper.ValidateTenantContext(tenantId, _currentTenantContext, response);

        if (ServiceValidationHelper.HasErrors(response))
        {
            return response;
        }

        var query = _dbContext.HouseholdMembers
            .AsNoTracking()
            .Where(m => m.TenantId == tenantId && m.HouseholdId == householdId);

        if (ids is not null)
        {
            var idList = ids.ToList();
            if (idList.Count > 0)
            {
                query = query.Where(m => idList.Contains(m.Id));
            }
        }

        var members = await query
            .Select(MapToDtoExpression)
            .ToListAsync(cancellationToken);

        return BaseEntityResponse<IReadOnlyCollection<HouseholdMemberDto>>.SuccessfulResponse(members);
    }

    public async Task<HouseholdMemberResponse> GetAsync(
        Guid tenantId,
        Guid householdId,
        Guid memberId,
        CancellationToken cancellationToken = default)
    {
        var response = new HouseholdMemberResponse();
        ServiceValidationHelper.ValidateTenantContext(tenantId, _currentTenantContext, response);

        if (ServiceValidationHelper.HasErrors(response))
        {
            return response;
        }

        var member = await _dbContext.HouseholdMembers
            .AsNoTracking()
            .Where(m => m.TenantId == tenantId && m.HouseholdId == householdId && m.Id == memberId)
            .SingleOrDefaultAsync(cancellationToken);

        if (member is null)
        {
            response.AddResponseMessage(MessageType.ERROR, "Household member not found.");
            return response;
        }

        response.SetSuccess(MapToDto(member));
        return response;
    }

    public async Task<HouseholdMemberResponse> CreateAsync(
        Guid tenantId,
        Guid householdId,
        CreateHouseholdMemberRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = new HouseholdMemberResponse();
        ServiceValidationHelper.ValidateTenantContext(tenantId, _currentTenantContext, response);

        if (request is null)
        {
            response.AddResponseMessage(MessageType.ERROR, "A create household member request is required.");
            return response;
        }

        ServiceValidationHelper.TryNormalizeString(request.Name, "Member name", response, out string normalizedName);

        if (ServiceValidationHelper.HasErrors(response))
        {
            return response;
        }

        if (!await _memberAuthorizationService.CanManageHouseholdAsync(tenantId, householdId, cancellationToken))
        {
            response.AddResponseMessage(MessageType.ERROR, "You do not have permission to add members to this household.");
            return response;
        }

        var householdExists = await _dbContext.Households
            .AsNoTracking()
            .AnyAsync(h => h.TenantId == tenantId && h.Id == householdId, cancellationToken);

        if (!householdExists)
        {
            response.AddResponseMessage(MessageType.ERROR, "Household not found.");
            return response;
        }

        var member = new HouseholdMember
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            HouseholdId = householdId,
            Name = normalizedName,
            IsAdult = request.IsAdult,
            AgeBand = request.AgeBand?.Trim(),
            BirthDate = request.BirthDate,
            DietaryNotes = request.DietaryNotes?.Trim(),
            DietaryTags = request.DietaryTags ?? Array.Empty<string>(),
            UserId = request.UserId,
        };

        _dbContext.HouseholdMembers.Add(member);
        await _dbContext.SaveChangesAsync(cancellationToken);

        GathersteadMetrics.RecordMemberCreated(tenantId, householdId);
        response.SetSuccess(MapToDto(member));
        return response;
    }

    public async Task<HouseholdMemberResponse> UpdateAsync(
        Guid tenantId,
        Guid householdId,
        Guid memberId,
        UpdateHouseholdMemberRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = new HouseholdMemberResponse();
        ServiceValidationHelper.ValidateTenantContext(tenantId, _currentTenantContext, response);

        if (request is null)
        {
            response.AddResponseMessage(MessageType.ERROR, "An update household member request is required.");
            return response;
        }

        ServiceValidationHelper.TryNormalizeString(request.Name, "Member name", response, out string normalizedName);

        if (ServiceValidationHelper.HasErrors(response))
        {
            return response;
        }

        if (!await _memberAuthorizationService.CanEditMemberAsync(tenantId, householdId, memberId, cancellationToken))
        {
            response.AddResponseMessage(MessageType.ERROR, "You do not have permission to edit this member.");
            return response;
        }

        var member = await _dbContext.HouseholdMembers
            .Where(m => m.TenantId == tenantId && m.HouseholdId == householdId && m.Id == memberId)
            .SingleOrDefaultAsync(cancellationToken);

        if (member is null)
        {
            response.AddResponseMessage(MessageType.ERROR, "Household member not found.");
            return response;
        }

        member.Name = normalizedName;
        member.IsAdult = request.IsAdult;
        member.AgeBand = request.AgeBand?.Trim();
        member.BirthDate = request.BirthDate;
        member.DietaryNotes = request.DietaryNotes?.Trim();
        member.DietaryTags = request.DietaryTags ?? Array.Empty<string>();

        // Only Tenant Owner/Manager or Household Admin can link a User to a HouseholdMember
        if (request.UserId != member.UserId)
        {
            if (await _memberAuthorizationService.CanManageHouseholdAsync(tenantId, householdId, cancellationToken))
            {
                member.UserId = request.UserId;
            }
            else if (request.UserId is not null)
            {
                response.AddResponseMessage(MessageType.ERROR, "You do not have permission to link a user to this member.");
                return response;
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        response.SetSuccess(MapToDto(member));
        return response;
    }

    public async Task<HouseholdMemberResponse> DeleteAsync(
        Guid tenantId,
        Guid householdId,
        Guid memberId,
        CancellationToken cancellationToken = default)
    {
        var response = new HouseholdMemberResponse();
        ServiceValidationHelper.ValidateTenantContext(tenantId, _currentTenantContext, response);

        if (ServiceValidationHelper.HasErrors(response))
        {
            return response;
        }

        if (!await _memberAuthorizationService.CanEditMemberAsync(tenantId, householdId, memberId, cancellationToken))
        {
            response.AddResponseMessage(MessageType.ERROR, "You do not have permission to delete this member.");
            return response;
        }

        var member = await _dbContext.HouseholdMembers
            .Where(m => m.TenantId == tenantId && m.HouseholdId == householdId && m.Id == memberId)
            .SingleOrDefaultAsync(cancellationToken);

        if (member is null)
        {
            response.AddResponseMessage(MessageType.ERROR, "Household member not found.");
            return response;
        }

        if (member.IsDeleted)
        {
            response.AddResponseMessage(MessageType.WARNING, "Household member already deleted.");
            return response;
        }

        member.IsDeleted = true;

        await _dbContext.SaveChangesAsync(cancellationToken);

        GathersteadMetrics.RecordSoftDelete("HouseholdMember", tenantId);
        response.SetSuccess(MapToDto(member));
        return response;
    }

    private static HouseholdMemberDto MapToDto(HouseholdMember member) => new(
        member.Id,
        member.TenantId,
        member.HouseholdId,
        member.Name,
        member.IsAdult,
        member.AgeBand,
        member.BirthDate,
        member.DietaryNotes,
        member.DietaryTags,
        member.CreatedAt,
        member.UpdatedAt,
        member.IsDeleted,
        member.DeletedAt,
        member.DeletedByUserId);
}
