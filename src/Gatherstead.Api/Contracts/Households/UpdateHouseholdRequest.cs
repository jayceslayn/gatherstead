using System.ComponentModel.DataAnnotations;

namespace Gatherstead.Api.Contracts.Households;

public class UpdateHouseholdRequest
{
    private string _name = string.Empty;

    [Required]
    [StringLength(200)]
    public string Name
    {
        get => _name;
        init => _name = (value ?? string.Empty).Trim();
    }
}
