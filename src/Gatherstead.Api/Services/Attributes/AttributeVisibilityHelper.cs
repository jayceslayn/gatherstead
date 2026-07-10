using Gatherstead.Api.Contracts.Attributes;
using Gatherstead.Data.Entities;

namespace Gatherstead.Api.Services.Attributes;

/// Read-side visibility rules for parent-scoped attributes — the single home for the rule that
/// AttributeSyncHelper's isVisible callbacks and every service's DTO mapping apply. List endpoints
/// return visible attributes (mirroring single-GET) so cards and edit modals render/persist them
/// without a per-item single-GET, while attributes above the caller's role stay hidden and are
/// preserved untouched on writes.
internal static class AttributeVisibilityHelper
{
    /// Visible when the caller's tenant role meets the attribute's minimum (lower enum value =
    /// more privileged). Callers with no tenant role (e.g. app admins) see no attributes.
    internal static bool IsVisible(IParentScopedAttribute a, TenantRole? callerRole)
        => callerRole.HasValue && callerRole.Value <= (TenantRole)a.TenantMinRole;

    /// Household-scoped attributes are additionally visible via the caller's role in the owning
    /// household. householdRole must be null when the caller is not a member of that household —
    /// never default(HouseholdRole), which is Manager and would leak household-gated attributes.
    internal static bool IsVisible(IHouseholdScopedAttribute a, TenantRole? tenantRole, HouseholdRole? householdRole)
        => IsVisible(a, tenantRole)
        || (householdRole.HasValue && a.HouseholdMinRole.HasValue && householdRole.Value <= (HouseholdRole)a.HouseholdMinRole.Value);

    internal static List<AttributeDto> Visible<TAttr>(IEnumerable<TAttr> attrs, TenantRole? callerRole)
        where TAttr : IParentScopedAttribute
        => attrs
            .Where(a => IsVisible(a, callerRole))
            .OrderBy(a => a.Key)
            .Select(a => new AttributeDto(a.Id, a.Key, a.Value, a.TenantMinRole))
            .ToList();

    internal static List<AttributeDto> Visible<TAttr>(IEnumerable<TAttr> attrs, TenantRole? tenantRole, HouseholdRole? householdRole)
        where TAttr : IHouseholdScopedAttribute
        => attrs
            .Where(a => IsVisible(a, tenantRole, householdRole))
            .OrderBy(a => a.Key)
            .Select(a => new AttributeDto(a.Id, a.Key, a.Value, a.TenantMinRole, a.HouseholdMinRole))
            .ToList();
}
