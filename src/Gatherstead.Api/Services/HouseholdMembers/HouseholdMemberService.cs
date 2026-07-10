using Gatherstead.Api.Contracts.Attributes;
using Gatherstead.Api.Contracts.HouseholdMembers;
using Gatherstead.Api.Contracts.Responses;
using Gatherstead.Api.Observability;
using Gatherstead.Api.Services.Attributes;
using Gatherstead.Api.Services.Authorization;
using Gatherstead.Api.Services.Validation;
using Gatherstead.Data;
using Gatherstead.Data.Entities;
using Microsoft.EntityFrameworkCore;
using DataAgeBands = Gatherstead.Data.Entities.AgeBands;

namespace Gatherstead.Api.Services.HouseholdMembers;

public class HouseholdMemberService : IHouseholdMemberService
{
    private readonly GathersteadDbContext _dbContext;
    private readonly ICurrentTenantContext _currentTenantContext;
    private readonly IMemberAuthorizationService _memberAuthorizationService;
    private readonly IAuditVisibilityContext _auditVisibility;

    public HouseholdMemberService(
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

    public async Task<BaseEntityResponse<IReadOnlyCollection<HouseholdMemberDto>>> ListAsync(
        Guid tenantId,
        Guid householdId,
        IEnumerable<Guid>? ids = null,
        CancellationToken cancellationToken = default)
    {
        var response = new BaseEntityResponse<IReadOnlyCollection<HouseholdMemberDto>>();

        if (!ServiceValidationHelper.ValidateTenantContext(tenantId, _currentTenantContext, response))
            return response;

        var scope = await _memberAuthorizationService.GetSensitiveReadScopeAsync(tenantId, cancellationToken);

        // Attributes ride along on lists (mirrors GetAsync) so list-sourced edits don't wipe them.
        // The list is scoped to one household, so the household role resolves once. Skip loading
        // attributes when the caller has neither role, since none would be visible.
        var tenantRole = await _memberAuthorizationService.GetCallerTenantRoleAsync(tenantId, cancellationToken);
        var householdRole = await _memberAuthorizationService.GetCallerHouseholdRoleAsync(tenantId, householdId, cancellationToken);

        var query = _dbContext.HouseholdMembers
            .AsNoTracking()
            .Where(m => m.TenantId == tenantId && m.HouseholdId == householdId);
        if (tenantRole is not null || householdRole is not null)
            query = query.Include(m => m.Attributes);

        if (ids is not null)
        {
            var idList = ids.ToList();
            if (idList.Count > 0)
                query = query.Where(m => idList.Contains(m.Id));
        }

        var members = await query.ToListAsync(cancellationToken);
        var canReadSensitive = scope.CanReadSensitive(householdId);

        // Ordered by effective age band (canonical enum order, null last) then name. Sorted on the
        // mapped DTOs because the effective band may be derived from BirthDate (see MapToDto) and
        // Name is an Always Encrypted (PII) column that cannot be ORDER BY'd in SQL.
        var dtos = members
            .Select(m => MapToDto(m, canReadSensitive, AttributeVisibilityHelper.Visible(m.Attributes, tenantRole, householdRole)))
            .OrderBy(d => d.AgeBand.HasValue ? (int)d.AgeBand.Value : int.MaxValue)
            .ThenBy(d => d.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return BaseEntityResponse<IReadOnlyCollection<HouseholdMemberDto>>.SuccessfulResponse(dtos);
    }

    public async Task<HouseholdMemberResponse> GetAsync(
        Guid tenantId,
        Guid householdId,
        Guid memberId,
        CancellationToken cancellationToken = default)
    {
        var response = new HouseholdMemberResponse();

        if (!ServiceValidationHelper.ValidateTenantContext(tenantId, _currentTenantContext, response))
            return response;

        var scope = await _memberAuthorizationService.GetSensitiveReadScopeAsync(tenantId, cancellationToken);

        var member = await _dbContext.HouseholdMembers
            .AsNoTracking()
            .Include(m => m.Attributes)
            .Where(m => m.TenantId == tenantId && m.HouseholdId == householdId && m.Id == memberId)
            .SingleOrDefaultAsync(cancellationToken);

        if (member is null)
        {
            response.AddResponseMessage(MessageType.ERROR, "Household member not found.");
            return response;
        }

        var tenantRole = await _memberAuthorizationService.GetCallerTenantRoleAsync(tenantId, cancellationToken);
        var householdRole = await _memberAuthorizationService.GetCallerHouseholdRoleAsync(tenantId, householdId, cancellationToken);

        response.SetSuccess(MapToDto(member, scope.CanReadSensitive(householdId),
            AttributeVisibilityHelper.Visible(member.Attributes, tenantRole, householdRole)));
        return response;
    }

    public async Task<HouseholdMemberResponse> CreateAsync(
        Guid tenantId,
        Guid householdId,
        CreateHouseholdMemberRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = new HouseholdMemberResponse();

        if (!ServiceValidationHelper.ValidateTenantContext(tenantId, _currentTenantContext, response))
            return response;

        if (request is null)
        {
            response.AddResponseMessage(MessageType.ERROR, "A create household member request is required.");
            return response;
        }

        ServiceValidationHelper.TryNormalizeString(request.Name, "Member name", response, out string normalizedName);

        if (ServiceValidationHelper.HasErrors(response))
            return response;

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
            AgeBand = request.BirthDate is null ? request.AgeBand : null,
            BirthDate = request.BirthDate,
            DietaryNotes = request.DietaryNotes?.Trim(),
            DietaryTags = NormalizeDietaryTags(request.DietaryTags),
            Notes = request.Notes?.Trim(),
        };

        _dbContext.HouseholdMembers.Add(member);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var tenantRole = await _memberAuthorizationService.GetCallerTenantRoleAsync(tenantId, cancellationToken);
        var householdRole = await _memberAuthorizationService.GetCallerHouseholdRoleAsync(tenantId, householdId, cancellationToken);
        List<AttributeDto> attrs = [];

        if (request.Attributes is { Count: > 0 })
        {
            await AttributeSyncHelper.SyncAsync(
                _dbContext.HouseholdMemberAttributes.Where(a => a.HouseholdMemberId == member.Id),
                _dbContext.HouseholdMemberAttributes,
                request.Attributes,
                a => AttributeVisibilityHelper.IsVisible(a, tenantRole, householdRole),
                tenantId,
                () => new HouseholdMemberAttribute { TenantId = tenantId, HouseholdMemberId = member.Id },
                applyExtra: (attr, entry) => attr.HouseholdMinRole = entry.HouseholdMinRole,
                cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);

            var savedAttrs = await _dbContext.HouseholdMemberAttributes.AsNoTracking()
                .Where(a => a.HouseholdMemberId == member.Id).ToListAsync(cancellationToken);
            attrs = AttributeVisibilityHelper.Visible(savedAttrs, tenantRole, householdRole);
        }

        GathersteadMetrics.RecordMemberCreated(tenantId, householdId);
        response.SetSuccess(MapToDto(member, canReadSensitive: true, attrs));
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

        if (!ServiceValidationHelper.ValidateTenantContext(tenantId, _currentTenantContext, response))
            return response;

        if (request is null)
        {
            response.AddResponseMessage(MessageType.ERROR, "An update household member request is required.");
            return response;
        }

        ServiceValidationHelper.TryNormalizeString(request.Name, "Member name", response, out string normalizedName);

        if (ServiceValidationHelper.HasErrors(response))
            return response;

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
        member.AgeBand = request.BirthDate is null ? request.AgeBand : null;
        member.BirthDate = request.BirthDate;
        member.DietaryNotes = request.DietaryNotes?.Trim();
        member.DietaryTags = NormalizeDietaryTags(request.DietaryTags);
        member.Notes = request.Notes?.Trim();

        var tenantRole = await _memberAuthorizationService.GetCallerTenantRoleAsync(tenantId, cancellationToken);
        var householdRole = await _memberAuthorizationService.GetCallerHouseholdRoleAsync(tenantId, householdId, cancellationToken);

        if (request.Attributes is not null)
        {
            await AttributeSyncHelper.SyncAsync(
                _dbContext.HouseholdMemberAttributes.Where(a => a.HouseholdMemberId == memberId),
                _dbContext.HouseholdMemberAttributes,
                request.Attributes,
                a => AttributeVisibilityHelper.IsVisible(a, tenantRole, householdRole),
                tenantId,
                () => new HouseholdMemberAttribute { TenantId = tenantId, HouseholdMemberId = memberId },
                applyExtra: (attr, entry) => attr.HouseholdMinRole = entry.HouseholdMinRole,
                cancellationToken);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        var savedAttrs = await _dbContext.HouseholdMemberAttributes.AsNoTracking()
            .Where(a => a.HouseholdMemberId == memberId).ToListAsync(cancellationToken);
        response.SetSuccess(MapToDto(member, canReadSensitive: true,
            AttributeVisibilityHelper.Visible(savedAttrs, tenantRole, householdRole)));
        return response;
    }

    public async Task<HouseholdMemberResponse> DeleteAsync(
        Guid tenantId,
        Guid householdId,
        Guid memberId,
        CancellationToken cancellationToken = default)
    {
        var response = new HouseholdMemberResponse();

        if (!ServiceValidationHelper.ValidateTenantContext(tenantId, _currentTenantContext, response))
            return response;

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

        var childAttrs = await _dbContext.HouseholdMemberAttributes
            .Where(a => a.HouseholdMemberId == memberId).ToListAsync(cancellationToken);
        foreach (var attr in childAttrs)
            attr.IsDeleted = true;

        await _dbContext.SaveChangesAsync(cancellationToken);

        GathersteadMetrics.RecordSoftDelete("HouseholdMember", tenantId);
        response.SetSuccess(MapToDto(member, canReadSensitive: true, []));
        return response;
    }

    private static string[] NormalizeDietaryTags(string[]? tags)
    {
        if (tags is null or { Length: 0 }) return Array.Empty<string>();
        return tags
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Select(t => t.Trim().ToLowerInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(t => t, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private HouseholdMemberDto MapToDto(HouseholdMember member, bool canReadSensitive, IReadOnlyList<AttributeDto> attributes)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var ageBand = member.BirthDate is DateOnly bd
            ? DataAgeBands.DeriveFromBirthDate(bd, today)
            : member.AgeBand;

        // Adult status is derived from the effective band; null when neither BirthDate nor AgeBand is set.
        bool? isAdult = ageBand is AgeBand band ? DataAgeBands.IsAdult(band) : null;

        return new(
            member.Id,
            member.TenantId,
            member.HouseholdId,
            member.Name,
            isAdult,
            ageBand,
            canReadSensitive ? member.BirthDate : null,
            canReadSensitive ? member.DietaryNotes : null,
            canReadSensitive ? member.DietaryTags : [],
            canReadSensitive ? member.Notes : null,
            attributes,
            member.ToAuditInfo(_auditVisibility.IncludeAudit));
    }
}
