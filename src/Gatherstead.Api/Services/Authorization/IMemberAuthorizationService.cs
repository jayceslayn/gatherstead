namespace Gatherstead.Api.Services.Authorization;

public interface IMemberAuthorizationService
{
    /// <summary>
    /// Determines if the current user can edit the specified household member.
    /// Returns true if any of: tenant Owner/Manager, Self, household Admin, or Guardian (Parent/Guardian relationship).
    /// </summary>
    Task<bool> CanEditMemberAsync(Guid tenantId, Guid householdId, Guid memberId, CancellationToken ct = default);

    /// <summary>
    /// Determines if the current user can manage the specified household
    /// (add/remove members, edit household details, delete household).
    /// Returns true if: tenant Owner/Manager, or household Admin.
    /// </summary>
    Task<bool> CanManageHouseholdAsync(Guid tenantId, Guid householdId, CancellationToken ct = default);
}
