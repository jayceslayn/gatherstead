namespace Gatherstead.Api.Services.Authorization;

/// <summary>
/// Encapsulates the caller's sensitive-read permission for household member data.
/// Global = can read sensitive fields for all households in the tenant.
/// ForHouseholds = can read sensitive fields only for the specified household IDs.
/// None = public fields only (Name, AgeBand, derived IsAdult); 403 on sub-entity endpoints.
/// </summary>
public sealed class SensitiveReadScope
{
    public static readonly SensitiveReadScope Global = new(isGlobal: true, null);
    public static readonly SensitiveReadScope None   = new(isGlobal: false, new HashSet<Guid>());

    private readonly IReadOnlySet<Guid>? _householdIds;

    private SensitiveReadScope(bool isGlobal, IReadOnlySet<Guid>? householdIds)
    {
        IsGlobal = isGlobal;
        _householdIds = householdIds;
    }

    public static SensitiveReadScope ForHouseholds(IEnumerable<Guid> householdIds)
        => new(false, householdIds.ToHashSet());

    public bool IsGlobal { get; }

    public bool CanReadSensitive(Guid householdId)
        => IsGlobal || (_householdIds?.Contains(householdId) ?? false);
}
