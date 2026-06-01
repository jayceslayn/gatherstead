using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace Gatherstead.Data.Entities;

[Index(nameof(Slug), IsUnique = true)]
[Index(nameof(IsActive))]
public class DietaryTag
{
    public Guid Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string Slug { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string DisplayName { get; set; } = string.Empty;

    public DietaryCategory Category { get; set; }

    public int SortOrder { get; set; }

    public bool IsActive { get; set; } = true;
}
