using System;
using System.Collections.Generic;

namespace Gatherstead.Db.Entities;

public class User
{
    public Guid Id { get; set; }
    public string ExternalId { get; set; } = string.Empty; // Entra ID subject

    public ICollection<TenantUser> Tenants { get; set; } = new List<TenantUser>();
}
