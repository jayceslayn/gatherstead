using System;
using System.Collections.Generic;

namespace Gatherstead.Db.Entities;

public class Household
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Tenant? Tenant { get; set; }
    public string Name { get; set; } = string.Empty;

    public ICollection<HouseholdMember> Members { get; set; } = new List<HouseholdMember>();

    public Guid CreatedByUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public Guid? UpdatedByUserId { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }
    public Guid? DeletedByUserId { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}
