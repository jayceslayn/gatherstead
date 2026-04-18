using Gatherstead.Api.Security;
using Gatherstead.Data;
using Gatherstead.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Gatherstead.Api.Services.Authorization;

public class MemberAuthorizationService : IMemberAuthorizationService
{
    private readonly GathersteadDbContext _dbContext;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAppAdminContext _appAdminContext;
    private readonly ILogger<MemberAuthorizationService> _logger;

    private const string CacheKey_TenantRole = "MemberAuth_TenantRole";
    private const string CacheKey_LinkedMembers = "MemberAuth_LinkedMembers";

    public MemberAuthorizationService(
        GathersteadDbContext dbContext,
        ICurrentUserContext currentUserContext,
        IHttpContextAccessor httpContextAccessor,
        IAppAdminContext appAdminContext,
        ILogger<MemberAuthorizationService> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _currentUserContext = currentUserContext ?? throw new ArgumentNullException(nameof(currentUserContext));
        _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
        _appAdminContext = appAdminContext ?? throw new ArgumentNullException(nameof(appAdminContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<bool> CanEditMemberAsync(Guid tenantId, Guid householdId, Guid memberId, CancellationToken ct = default)
    {
        var userId = _currentUserContext.UserId;
        if (!userId.HasValue) return false;

        // App Admins can edit anything
        if (await _appAdminContext.IsAppAdminAsync(ct) == true)
            return true;

        // 1. Tenant Owner/Manager can edit anything
        var role = await GetTenantRoleAsync(tenantId, userId.Value, ct);
        if (role.HasValue && role.Value <= TenantRole.Manager)
            return true;

        // 2-3. Check Self and Household Admin
        var linkedMembers = await GetLinkedMembersAsync(tenantId, userId.Value, ct);
        if (linkedMembers.Count == 0)
        {
            _logger.LogWarning(
                "Member edit denied: no linked members. TenantId: {TenantId}, MemberId: {MemberId}, UserId: {UserId}",
                tenantId, memberId, userId.Value);
            return false;
        }

        // Self check
        if (linkedMembers.Any(m => m.Id == memberId))
            return true;

        // Household Admin check
        if (linkedMembers.Any(m => m.HouseholdId == householdId && m.HouseholdRole == HouseholdRole.Admin))
            return true;

        _logger.LogWarning(
            "Member edit denied: insufficient role. TenantId: {TenantId}, MemberId: {MemberId}, UserId: {UserId}",
            tenantId, memberId, userId.Value);
        return false;
    }

    public async Task<bool> CanManageHouseholdAsync(Guid tenantId, Guid householdId, CancellationToken ct = default)
    {
        var userId = _currentUserContext.UserId;
        if (!userId.HasValue) return false;

        // App Admins can manage anything
        if (await _appAdminContext.IsAppAdminAsync(ct) == true)
            return true;

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
