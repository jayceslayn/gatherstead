using System;
using System.Collections.Generic;

namespace Gatherstead.Db.Entities;

public class Property
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Tenant? Tenant { get; set; }
    public string Name { get; set; } = string.Empty;

    public ICollection<Event> Events { get; set; } = new List<Event>();
}
