using System;
using Gatherstead.Db.Encryption;

namespace Gatherstead.Db.Entities;

public class Resource
{
    public Guid Id { get; set; }
    public Guid EventId { get; set; }
    public Event? Event { get; set; }
    public string Name { get; set; } = string.Empty;
    public ResourceType Type { get; set; }
    public int? CapacityAdults { get; set; }
    public int? CapacityChildren { get; set; }
    public string? Notes { get; set; }

    public ICollection<StayIntent> StayIntents { get; set; } = new List<StayIntent>();
}
