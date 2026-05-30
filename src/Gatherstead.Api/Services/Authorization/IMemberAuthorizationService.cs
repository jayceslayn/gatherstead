using Gatherstead.Data.Entities;

namespace Gatherstead.Api.Services.Authorization;

public interface IMemberAuthorizationService
{
    /// <summary>
    /// Determines if the current user can edit the specified household member's profile data
    /// (name, contact info, addresses, dietary profile, attributes, relationships).
    /// Returns true if any of: App Admin, tenant Owner/Manager, Self, or household Manager.
    /// </summary>
    Task<bool> CanEditMemberAsync(Guid tenantId, Guid householdId, Guid memberId, CancellationToken ct = default);

    /// <summary>
    /// Determines if the current user can assign intent/attendance records for the specified member
    /// (EventAttendance, MealAttendance, MealIntent, TaskIntent, AccommodationIntent, and future EquipmentIntent).
    /// Returns true if any of: App Admin, tenant Owner/Manager/Coordinator, Self, or household Manager.
    /// </summary>
    Task<bool> CanAssignIntentForMemberAsync(Guid tenantId, Guid householdId, Guid memberId, CancellationToken ct = default);

    /// <summary>
    /// Determines if the current user can manage the specified household
    /// (add/remove members, edit household details, delete household).
    /// Returns true if: App Admin, tenant Owner/Manager, or household Manager.
    /// </summary>
    Task<bool> CanManageHouseholdAsync(Guid tenantId, Guid householdId, CancellationToken ct = default);

    /// <summary>
    /// Determines if the current user can manage tenant-level structural resources
    /// (Properties, Accommodations, Households, Users). Excludes Coordinator — use CanManageEventAsync for events.
    /// Returns true if: App Admin, or tenant Owner/Manager.
    /// </summary>
    Task<bool> CanManageTenantAsync(Guid tenantId, CancellationToken ct = default);

    /// <summary>
    /// Determines if the current user can manage event-level resources
    /// (Events, MealTemplates, TaskTemplates, MealPlans, TaskPlans).
    /// Returns true if: App Admin, or tenant Owner/Manager/Coordinator.
    /// </summary>
    Task<bool> CanManageEventAsync(Guid tenantId, CancellationToken ct = default);

    /// <summary>
    /// Returns the caller's sensitive read scope for household member data.
    /// Global  — TenantRole.Member+: can read all sensitive fields across the tenant.
    /// ForHouseholds — TenantRole.Guest with HouseholdUser entries: sensitive fields only for those households.
    /// None    — App Admin, TenantRole.Guest with no HouseholdUser entries: public fields only; 403 on sub-entity endpoints.
    /// </summary>
    Task<SensitiveReadScope> GetSensitiveReadScopeAsync(Guid tenantId, CancellationToken ct = default);

    /// <summary>
    /// Returns the caller's TenantRole within the tenant. Returns null for App Admins (who have no
    /// tenant membership) and for unauthenticated callers. Used for attribute-level visibility filtering.
    /// </summary>
    Task<TenantRole?> GetCallerTenantRoleAsync(Guid tenantId, CancellationToken ct = default);

    /// <summary>
    /// Returns the caller's HouseholdRole within a specific household, or null if they have no
    /// HouseholdUser entry for that household. Used for household-attribute visibility filtering.
    /// </summary>
    Task<HouseholdRole?> GetCallerHouseholdRoleAsync(Guid tenantId, Guid householdId, CancellationToken ct = default);
}
