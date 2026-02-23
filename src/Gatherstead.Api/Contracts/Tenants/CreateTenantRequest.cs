using System.ComponentModel.DataAnnotations;

namespace Gatherstead.Api.Contracts.Tenants;

public class CreateTenantRequest
{
    [Required]
    [StringLength(200)]
    public string Name { get; init; } = string.Empty;
}
