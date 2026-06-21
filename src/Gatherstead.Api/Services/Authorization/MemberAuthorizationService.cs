using Gatherstead.Api.Observability;
using Gatherstead.Api.Security;
using Gatherstead.Api.Services.Observability;
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
    private readonly ISecurityEventLogger _securityEventLogger;

    private const string CacheKey_TenantUserInfo = "MemberAuth_TenantUserInfo";
    private const string CacheKey_HouseholdUsers = "MemberAuth_HouseholdUsers";

    public MemberAuthorizationService(
        GathersteadDbContext dbContext,
        ICurrentUserContext currentUserContext,
        IHttpContextAccessor httpContextAccessor,
        IAppAdminContext appAdminContext,
        ILogger<MemberAuthorizationService> logger,
        ISecurityEventLogger securityEventLogger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _currentUserContext = currentUserContext ?? throw new ArgumentNullException(nameof(currentUserContext));
        _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
        _appAdminContext = appAdminContext ?? throw new ArgumentNullException(nameof(appAdminContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _securityEventLogger = securityEventLogger ?? throw new ArgumentNullException(nameof(securityEventLogger));
    }

    public async Task<bool> CanEditMemberAsync(Guid tenantId, Guid householdId, Guid memberId, CancellationToken ct = default)
    {
        var userId = _currentUserContext.UserId;
        if (!userId.HasValue) return false;

        if (await _appAdminContext.IsAppAdminAsync(ct) == true)
            return true;

        var info = await GetTenantUserInfoAsync(tenantId, userId.Value, ct);
        if (info?.Role <= TenantRole.Manager)
            return true;

        // Self check — linked member in this tenant
        if (info?.LinkedMemberId == memberId)
            return true;

        // Household Manager check
        var householdUsers = await GetHouseholdUserRolesAsync(tenantId, userId.Value, ct);
        if (householdUsers.Any(hu => hu.HouseholdId == householdId && hu.Role == HouseholdRole.Manager))
            return true;

        _logger.LogWarning(
            "Member edit denied: insufficient role. TenantId: {TenantId}, MemberId: {MemberId}, UserId: {UserId}",
            tenantId, memberId, userId.Value);
        GathersteadMetrics.RecordAuthzDenied("InsufficientHouseholdRole", tenantId);
        await _securityEventLogger.LogAsync(
            SecurityEventType.AuthzDenial, SecurityEventSeverity.Warning,
            resource: $"HouseholdMember:{memberId}",
            detail: "{\"reason\":\"InsufficientHouseholdRole\"}",
            tenantId: tenantId, userId: userId, cancellationToken: ct);
        return false;
    }

    public async Task<bool> CanAssignIntentForMemberAsync(Guid tenantId, Guid householdId, Guid memberId, CancellationToken ct = default)
    {
        var userId = _currentUserContext.UserId;
        if (!userId.HasValue) return false;

        if (await _appAdminContext.IsAppAdminAsync(ct) == true)
            return true;

        var info = await GetTenantUserInfoAsync(tenantId, userId.Value, ct);
        if (info?.Role <= TenantRole.Coordinator)
            return true;

        // Self check — linked member in this tenant
        if (info?.LinkedMemberId == memberId)
            return true;

        // Household Manager check
        var householdUsers = await GetHouseholdUserRolesAsync(tenantId, userId.Value, ct);
        if (householdUsers.Any(hu => hu.HouseholdId == householdId && hu.Role == HouseholdRole.Manager))
            return true;

        _logger.LogWarning(
            "Intent assign denied: insufficient role. TenantId: {TenantId}, MemberId: {MemberId}, UserId: {UserId}",
            tenantId, memberId, userId.Value);
        GathersteadMetrics.RecordAuthzDenied("InsufficientHouseholdRole", tenantId);
        await _securityEventLogger.LogAsync(
            SecurityEventType.AuthzDenial, SecurityEventSeverity.Warning,
            resource: $"HouseholdMember:{memberId}",
            detail: "{\"reason\":\"InsufficientHouseholdRole\"}",
            tenantId: tenantId, userId: userId, cancellationToken: ct);
        return false;
    }

    public async Task<bool> CanManageHouseholdAsync(Guid tenantId, Guid householdId, CancellationToken ct = default)
    {
        var userId = _currentUserContext.UserId;
        if (!userId.HasValue) return false;

        if (await _appAdminContext.IsAppAdminAsync(ct) == true)
            return true;

        var info = await GetTenantUserInfoAsync(tenantId, userId.Value, ct);
        if (info?.Role <= TenantRole.Manager)
            return true;

        var householdUsers = await GetHouseholdUserRolesAsync(tenantId, userId.Value, ct);
        return householdUsers.Any(hu => hu.HouseholdId == householdId && hu.Role == HouseholdRole.Manager);
    }

    public async Task<bool> CanManageTenantAsync(Guid tenantId, CancellationToken ct = default)
    {
        var userId = _currentUserContext.UserId;
        if (!userId.HasValue) return false;

        if (await _appAdminContext.IsAppAdminAsync(ct) == true)
            return true;

        var info = await GetTenantUserInfoAsync(tenantId, userId.Value, ct);
        return info?.Role <= TenantRole.Manager;
    }

    public async Task<bool> CanManageEventAsync(Guid tenantId, CancellationToken ct = default)
    {
        var userId = _currentUserContext.UserId;
        if (!userId.HasValue) return false;

        if (await _appAdminContext.IsAppAdminAsync(ct) == true)
            return true;

        var info = await GetTenantUserInfoAsync(tenantId, userId.Value, ct);
        return info?.Role <= TenantRole.Coordinator;
    }

    public async Task<bool> CanEditMealPlanMenuAsync(Guid tenantId, Guid mealPlanId, CancellationToken ct = default)
    {
        // Event managers (App Admin / Owner / Manager / Coordinator) can always edit the menu.
        if (await CanManageEventAsync(tenantId, ct))
            return true;

        var userId = _currentUserContext.UserId;
        if (!userId.HasValue) return false;

        var info = await GetTenantUserInfoAsync(tenantId, userId.Value, ct);
        if (info?.LinkedMemberId is not Guid linkedMemberId)
            return false;

        // The volunteer cook for this plan may set its menu. (A member could temporarily volunteer
        // to gain this — an accepted, non-critical loophole.)
        return await _dbContext.MealIntents
            .AsNoTracking()
            .AnyAsync(
                mi => mi.MealPlanId == mealPlanId
                    && mi.HouseholdMemberId == linkedMemberId
                    && mi.Volunteered,
                ct);
    }

    public async Task<SensitiveReadScope> GetSensitiveReadScopeAsync(Guid tenantId, CancellationToken ct = default)
    {
        var userId = _currentUserContext.UserId;
        if (!userId.HasValue) return SensitiveReadScope.None;

        // App Admins can take administrative actions but should not see tenant PII.
        if (await _appAdminContext.IsAppAdminAsync(ct) == true)
            return SensitiveReadScope.None;

        var info = await GetTenantUserInfoAsync(tenantId, userId.Value, ct);
        if (info?.Role <= TenantRole.Member)
            return SensitiveReadScope.Global;

        // Guest: scope to household(s) where the user has any HouseholdUser entry
        var householdUsers = await GetHouseholdUserRolesAsync(tenantId, userId.Value, ct);
        if (householdUsers.Count > 0)
            return SensitiveReadScope.ForHouseholds(householdUsers.Select(hu => hu.HouseholdId));

        return SensitiveReadScope.None;
    }

    public async Task<TenantRole?> GetCallerTenantRoleAsync(Guid tenantId, CancellationToken ct = default)
    {
        var userId = _currentUserContext.UserId;
        if (!userId.HasValue) return null;
        if (await _appAdminContext.IsAppAdminAsync(ct) == true) return null;
        var info = await GetTenantUserInfoAsync(tenantId, userId.Value, ct);
        return info?.Role;
    }

    public async Task<HouseholdRole?> GetCallerHouseholdRoleAsync(Guid tenantId, Guid householdId, CancellationToken ct = default)
    {
        var userId = _currentUserContext.UserId;
        if (!userId.HasValue) return null;
        var householdUsers = await GetHouseholdUserRolesAsync(tenantId, userId.Value, ct);
        return householdUsers.FirstOrDefault(hu => hu.HouseholdId == householdId)?.Role;
    }

    private async Task<TenantUserInfo?> GetTenantUserInfoAsync(Guid tenantId, Guid userId, CancellationToken ct)
    {
        var items = _httpContextAccessor.HttpContext?.Items;
        if (items != null && items.TryGetValue(CacheKey_TenantUserInfo, out var cached))
            return (TenantUserInfo?)cached;

        var info = await _dbContext.TenantUsers
            .AsNoTracking()
            .Where(tu => tu.TenantId == tenantId && tu.UserId == userId)
            .Select(tu => new TenantUserInfo(tu.Role, tu.LinkedMemberId))
            .FirstOrDefaultAsync(ct);

        items?.TryAdd(CacheKey_TenantUserInfo, info);
        return info;
    }

    private async Task<List<HouseholdUserInfo>> GetHouseholdUserRolesAsync(Guid tenantId, Guid userId, CancellationToken ct)
    {
        var items = _httpContextAccessor.HttpContext?.Items;
        if (items != null && items.TryGetValue(CacheKey_HouseholdUsers, out var cached))
            return (List<HouseholdUserInfo>)cached!;

        var householdUsers = await _dbContext.HouseholdUsers
            .AsNoTracking()
            .Where(hu => hu.TenantId == tenantId && hu.UserId == userId)
            .Select(hu => new HouseholdUserInfo(hu.HouseholdId, hu.Role))
            .ToListAsync(ct);

        items?.TryAdd(CacheKey_HouseholdUsers, householdUsers);
        return householdUsers;
    }

    private sealed record TenantUserInfo(TenantRole Role, Guid? LinkedMemberId);
    private sealed record HouseholdUserInfo(Guid HouseholdId, HouseholdRole Role);
}
