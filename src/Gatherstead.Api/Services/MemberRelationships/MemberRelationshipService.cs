using Gatherstead.Api.Contracts.MemberRelationships;
using Gatherstead.Api.Observability;
using Gatherstead.Api.Contracts.Responses;
using Gatherstead.Api.Services.Authorization;
using Gatherstead.Api.Services.Validation;
using Gatherstead.Data;
using Gatherstead.Data.Entities;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace Gatherstead.Api.Services.MemberRelationships;

public class MemberRelationshipService : IMemberRelationshipService
{
    private const string EntityDisplayName = "Member relationship";
    private const string AuthorizationDeniedMessage = "You do not have permission to manage relationships in this household.";

    private readonly GathersteadDbContext _dbContext;
    private readonly ICurrentTenantContext _currentTenantContext;
    private readonly IMemberAuthorizationService _memberAuthorizationService;

    private static readonly Expression<Func<MemberRelationship, MemberRelationshipDto>> MapToDtoExpression =
        rel => new MemberRelationshipDto(
            rel.Id,
            rel.TenantId,
            rel.HouseholdMemberId,
            rel.RelatedMemberId,
            rel.RelationshipType,
            rel.Notes,
            rel.CreatedAt,
            rel.UpdatedAt,
            rel.IsDeleted,
            rel.DeletedAt,
            rel.DeletedByUserId);

    public MemberRelationshipService(
        GathersteadDbContext dbContext,
        ICurrentTenantContext currentTenantContext,
        IMemberAuthorizationService memberAuthorizationService)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _currentTenantContext = currentTenantContext ?? throw new ArgumentNullException(nameof(currentTenantContext));
        _memberAuthorizationService = memberAuthorizationService ?? throw new ArgumentNullException(nameof(memberAuthorizationService));
    }

    public async Task<BaseEntityResponse<IReadOnlyCollection<MemberRelationshipDto>>> ListAsync(
        Guid tenantId,
        Guid householdId,
        Guid memberId,
        IEnumerable<Guid>? ids = null,
        CancellationToken cancellationToken = default)
    {
        var response = new BaseEntityResponse<IReadOnlyCollection<MemberRelationshipDto>>();

        if (!ServiceValidationHelper.ValidateTenantContext(tenantId, _currentTenantContext, response))
            return response;

        var query = _dbContext.MemberRelationships
            .AsNoTracking()
            .Where(r => r.TenantId == tenantId && r.HouseholdMemberId == memberId);

        if (ids is not null)
        {
            var idList = ids.ToList();
            if (idList.Count > 0)
            {
                query = query.Where(r => idList.Contains(r.Id));
            }
        }

        var relationships = await query
            .Select(MapToDtoExpression)
            .ToListAsync(cancellationToken);

        return BaseEntityResponse<IReadOnlyCollection<MemberRelationshipDto>>.SuccessfulResponse(relationships);
    }

    public async Task<MemberRelationshipResponse> GetAsync(
        Guid tenantId,
        Guid householdId,
        Guid memberId,
        Guid relationshipId,
        CancellationToken cancellationToken = default)
    {
        var response = new MemberRelationshipResponse();

        if (!ServiceValidationHelper.ValidateTenantContext(tenantId, _currentTenantContext, response))
            return response;

        var relationship = await ServiceGuards.LoadOrNotFoundAsync(
            response,
            _dbContext.MemberRelationships
                .AsNoTracking()
                .Where(r => r.TenantId == tenantId && r.HouseholdMemberId == memberId && r.Id == relationshipId),
            EntityDisplayName,
            cancellationToken);

        if (relationship is null) return response;

        response.SetSuccess(MapToDto(relationship));
        return response;
    }

    public async Task<MemberRelationshipResponse> CreateAsync(
        Guid tenantId,
        Guid householdId,
        Guid memberId,
        CreateMemberRelationshipRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = new MemberRelationshipResponse();

        if (!ServiceValidationHelper.ValidateTenantContext(tenantId, _currentTenantContext, response))
            return response;
        if (!ServiceGuards.RequireRequest(request, "create member relationship", response))
            return response;

        if (!await ServiceGuards.AuthorizeHouseholdManageAsync(response, _memberAuthorizationService, tenantId, householdId, AuthorizationDeniedMessage, cancellationToken))
            return response;

        if (request.RelatedMemberId == memberId)
        {
            response.AddResponseMessage(MessageType.ERROR, "A member cannot have a relationship with themselves.");
            return response;
        }

        if (!await ServiceGuards.RequireMemberExistsAsync(response, _dbContext, tenantId, householdId, memberId, cancellationToken))
            return response;

        var relatedMemberExists = await _dbContext.HouseholdMembers
            .AsNoTracking()
            .AnyAsync(m => m.TenantId == tenantId && m.Id == request.RelatedMemberId, cancellationToken);

        if (!relatedMemberExists)
        {
            response.AddResponseMessage(MessageType.ERROR, "Related member not found.");
            return response;
        }

        var duplicateExists = await _dbContext.MemberRelationships
            .AsNoTracking()
            .AnyAsync(r => r.TenantId == tenantId
                && r.HouseholdMemberId == memberId
                && r.RelatedMemberId == request.RelatedMemberId
                && r.RelationshipType == request.RelationshipType, cancellationToken);

        if (duplicateExists)
        {
            response.AddResponseMessage(MessageType.ERROR, "This relationship already exists between these members.");
            return response;
        }

        var relationship = new MemberRelationship
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            HouseholdMemberId = memberId,
            RelatedMemberId = request.RelatedMemberId,
            RelationshipType = request.RelationshipType,
            Notes = request.Notes?.Trim(),
        };

        _dbContext.MemberRelationships.Add(relationship);
        await _dbContext.SaveChangesAsync(cancellationToken);

        response.SetSuccess(MapToDto(relationship));
        return response;
    }

    public async Task<MemberRelationshipResponse> UpdateAsync(
        Guid tenantId,
        Guid householdId,
        Guid memberId,
        Guid relationshipId,
        UpdateMemberRelationshipRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = new MemberRelationshipResponse();

        if (!ServiceValidationHelper.ValidateTenantContext(tenantId, _currentTenantContext, response))
            return response;
        if (!ServiceGuards.RequireRequest(request, "update member relationship", response))
            return response;

        if (!await ServiceGuards.AuthorizeHouseholdManageAsync(response, _memberAuthorizationService, tenantId, householdId, AuthorizationDeniedMessage, cancellationToken))
            return response;

        var relationship = await ServiceGuards.LoadOrNotFoundAsync(
            response,
            _dbContext.MemberRelationships
                .Where(r => r.TenantId == tenantId && r.HouseholdMemberId == memberId && r.Id == relationshipId),
            EntityDisplayName,
            cancellationToken);

        if (relationship is null) return response;

        if (request.RelationshipType != relationship.RelationshipType)
        {
            var duplicateExists = await _dbContext.MemberRelationships
                .AsNoTracking()
                .AnyAsync(r => r.TenantId == tenantId
                    && r.HouseholdMemberId == memberId
                    && r.RelatedMemberId == relationship.RelatedMemberId
                    && r.RelationshipType == request.RelationshipType
                    && r.Id != relationshipId, cancellationToken);

            if (duplicateExists)
            {
                response.AddResponseMessage(MessageType.ERROR, "This relationship type already exists between these members.");
                return response;
            }
        }

        relationship.RelationshipType = request.RelationshipType;
        relationship.Notes = request.Notes?.Trim();

        await _dbContext.SaveChangesAsync(cancellationToken);

        response.SetSuccess(MapToDto(relationship));
        return response;
    }

    public async Task<MemberRelationshipResponse> DeleteAsync(
        Guid tenantId,
        Guid householdId,
        Guid memberId,
        Guid relationshipId,
        CancellationToken cancellationToken = default)
    {
        var response = new MemberRelationshipResponse();

        if (!ServiceValidationHelper.ValidateTenantContext(tenantId, _currentTenantContext, response))
            return response;
        if (!await ServiceGuards.AuthorizeHouseholdManageAsync(response, _memberAuthorizationService, tenantId, householdId, AuthorizationDeniedMessage, cancellationToken))
            return response;

        var relationship = await ServiceGuards.LoadOrNotFoundAsync(
            response,
            _dbContext.MemberRelationships
                .Where(r => r.TenantId == tenantId && r.HouseholdMemberId == memberId && r.Id == relationshipId),
            EntityDisplayName,
            cancellationToken);

        if (relationship is null) return response;

        if (relationship.IsDeleted)
        {
            response.AddResponseMessage(MessageType.WARNING, $"{EntityDisplayName} already deleted.");
            return response;
        }

        relationship.IsDeleted = true;

        await _dbContext.SaveChangesAsync(cancellationToken);

        GathersteadMetrics.RecordSoftDelete("MemberRelationship", tenantId);
        response.SetSuccess(MapToDto(relationship));
        return response;
    }

    private static MemberRelationshipDto MapToDto(MemberRelationship rel) => new(
        rel.Id,
        rel.TenantId,
        rel.HouseholdMemberId,
        rel.RelatedMemberId,
        rel.RelationshipType,
        rel.Notes,
        rel.CreatedAt,
        rel.UpdatedAt,
        rel.IsDeleted,
        rel.DeletedAt,
        rel.DeletedByUserId);
}
