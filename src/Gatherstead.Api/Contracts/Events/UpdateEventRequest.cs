using System.ComponentModel.DataAnnotations;
using Gatherstead.Api.Contracts.Attributes;

namespace Gatherstead.Api.Contracts.Events;

public class UpdateEventRequest
{
    [Required]
    [StringLength(200)]
    public string Name { get; init; } = string.Empty;

    [Required]
    public DateOnly StartDate { get; init; }

    [Required]
    public DateOnly EndDate { get; init; }

    [StringLength(500)]
    public string? Notes { get; init; } = null;

    public IReadOnlyList<AttributeWriteEntry>? Attributes { get; init; }
}
