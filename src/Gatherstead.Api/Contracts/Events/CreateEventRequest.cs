using System;
using System.ComponentModel.DataAnnotations;

namespace Gatherstead.Api.Contracts.Events;

public class CreateEventRequest
{
    [Required]
    public Guid PropertyId { get; init; }

    [Required]
    [StringLength(200)]
    public string Name { get; init; } = string.Empty;

    [Required]
    public DateOnly StartDate { get; init; }

    [Required]
    public DateOnly EndDate { get; init; }
}
