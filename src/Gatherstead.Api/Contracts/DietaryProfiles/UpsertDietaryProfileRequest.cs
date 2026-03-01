using System.ComponentModel.DataAnnotations;

namespace Gatherstead.Api.Contracts.DietaryProfiles;

public class UpsertDietaryProfileRequest
{
    [StringLength(200)]
    public string PreferredDiet { get; init; } = string.Empty;

    public string[]? Allergies { get; init; }

    public string[]? Restrictions { get; init; }

    public string? Notes { get; init; }
}
