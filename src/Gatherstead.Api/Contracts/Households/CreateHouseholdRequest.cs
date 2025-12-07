using System.ComponentModel.DataAnnotations;

namespace Gatherstead.Api.Contracts.Households;

public class CreateHouseholdRequest
{
    [Required]
    [StringLength(200)]
    public string Name { get; init; } = string.Empty;
}
