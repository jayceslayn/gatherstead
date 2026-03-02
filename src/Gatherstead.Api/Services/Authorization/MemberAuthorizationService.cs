using Gatherstead.Data;
using Gatherstead.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Gatherstead.Api.Services.Authorization;

public class MemberAuthorizationService : IMemberAuthorizationService
{
    private readonly GathersteadDbContext _dbContext;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IHttpContextAccessor _httpContextAccessor;

    private const string CacheKey_TenantRole = "MemberAuth_TenantRole";
    private const string CacheKey_LinkedMembers = "MemberAuth_LinkedMembers";

    public MemberAuthorizationService(
        GathersteadDbContext dbContext,
        ICurrentUserContext currentUserContext,
        IHttpContextAccessor httpContextAccessor)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _currentUserContext = currentUserContext ?? throw new ArgumentNullException(nameof(currentUserContext));
        _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
    }

    public async Task<bool> CanEditMemberAsync(Guid tenantId, Guid householdId, Guid memberId, CancellationToken ct = default)
    {
        var userId = _currentUserContext.UserId;
        if (!userId.HasValue) return false;

        // 1. Tenant Owner/Manager can edit anything
        var role = await GetTenantRoleAsync(tenantId, userId.Value, ct);
        if (role.HasValue && role.Value <= TenantRole.Manager)
            return true;

        // 2-4. Check Self, Household Admin, and Guardian
        var linkedMembers = await GetLinkedMembersAsync(tenantId, userId.Value, ct);
        if (linkedMembers.Count == 0)
            return false;

        // Self check
        if (linkedMembers.Any(m => m.Id == memberId))
            return true;

        // Household Admin check
        if (linkedMembers.Any(m => m.HouseholdId == householdId && m.HouseholdRole == HouseholdRole.Admin))
            return true;

        // Guardian check: does any of the user's linked members have a Parent/Guardian relationship to the target?
        var linkedMemberIds = linkedMembers.Select(m => m.Id).ToList();
        var isGuardian = await _dbContext.MemberRelationships
            .AsNoTracking()
            .AnyAsync(r =>
                linkedMemberIds.Contains(r.HouseholdMemberId) &&
                r.RelatedMemberId == memberId &&
                (r.RelationshipType == RelationshipType.Parent || r.RelationshipType == RelationshipType.Guardian),
                ct);

        return isGuardian;
    }

    public async Task<bool> CanManageHouseholdAsync(Guid tenantId, Guid householdId, CancellationToken ct = default)
    {
        var userId = _currentUserContext.UserId;
        if (!userId.HasValue) return false;

        // Tenant Owner/Manager can manage any household
        var role = await GetTenantRoleAsync(tenantId, userId.Value, ct);
        if (role.HasValue && role.Value <= TenantRole.Manager)
            return true;

        // Household Admin check
        var linkedMembers = await GetLinkedMembersAsync(tenantId, userId.Value, ct);
        return linkedMembers.Any(m => m.HouseholdId == householdId && m.HouseholdRole == HouseholdRole.Admin);
    }

    private async Task<TenantRole?> GetTenantRoleAsync(Guid tenantId, Guid userId, CancellationToken ct)
    {
        var items = _httpContextAccessor.HttpContext?.Items;
        if (items != null && items.TryGetValue(CacheKey_TenantRole, out var cached))
            return (TenantRole?)cached;

        var tenantUser = await _dbContext.TenantUsers
            .AsNoTracking()
            .Where(tu => tu.TenantId == tenantId && tu.UserId == userId)
            .Select(tu => (TenantRole?)tu.Role)
            .FirstOrDefaultAsync(ct);

        items?.TryAdd(CacheKey_TenantRole, tenantUser);
        return tenantUser;
    }

    private async Task<List<LinkedMemberInfo>> GetLinkedMembersAsync(Guid tenantId, Guid userId, CancellationToken ct)
    {
        var items = _httpContextAccessor.HttpContext?.Items;
        if (items != null && items.TryGetValue(CacheKey_LinkedMembers, out var cached))
            return (List<LinkedMemberInfo>)cached!;

        var members = await _dbContext.HouseholdMembers
            .AsNoTracking()
            .Where(hm => hm.TenantId == tenantId && hm.UserId == userId)
            .Select(hm => new LinkedMemberInfo(hm.Id, hm.HouseholdId, hm.HouseholdRole))
            .ToListAsync(ct);

        items?.TryAdd(CacheKey_LinkedMembers, members);
        return members;
    }

    private sealed record LinkedMemberInfo(Guid Id, Guid HouseholdId, HouseholdRole HouseholdRole);
}
