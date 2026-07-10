using System.Collections.ObjectModel;
using Gatherstead.Data.Entities;

namespace Gatherstead.Api.Services.Authorization;

/// The caller's HouseholdRole for every household they belong to in a tenant. Absence is typed:
/// RoleFor returns null for households the caller is not a member of — never
/// default(HouseholdRole), which is Manager and would leak household-gated attributes.
public sealed class CallerHouseholdRoles
{
    public static readonly CallerHouseholdRoles Empty = new(ReadOnlyDictionary<Guid, HouseholdRole>.Empty);

    private readonly IReadOnlyDictionary<Guid, HouseholdRole> _rolesByHouseholdId;

    public CallerHouseholdRoles(IReadOnlyDictionary<Guid, HouseholdRole> rolesByHouseholdId)
        => _rolesByHouseholdId = rolesByHouseholdId ?? throw new ArgumentNullException(nameof(rolesByHouseholdId));

    public int Count => _rolesByHouseholdId.Count;

    public HouseholdRole? RoleFor(Guid householdId)
        => _rolesByHouseholdId.TryGetValue(householdId, out var role) ? role : null;
}
