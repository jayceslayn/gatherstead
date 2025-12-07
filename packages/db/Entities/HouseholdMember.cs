using System;
using System.Collections.Generic;
using Gatherstead.Db.Encryption;

namespace Gatherstead.Db.Entities;

public class HouseholdMember : AuditableEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid HouseholdId { get; set; }
    public Household? Household { get; set; }

    public bool IsAdult { get; set; }
    public string? AgeBand { get; set; }

    // Encrypted fields
    public string Name { get; set; } = string.Empty;
    public DateOnly? BirthDate { get; set; }
    public string? DietaryNotes { get; set; }

    public string[] DietaryTags { get; set; } = Array.Empty<string>();

    public ICollection<MemberRelationship> Relationships { get; set; } = new List<MemberRelationship>();
    public ICollection<ContactMethod> ContactMethods { get; set; } = new List<ContactMethod>();
    public ICollection<Address> Addresses { get; set; } = new List<Address>();
    public ICollection<MemberAttribute> Attributes { get; set; } = new List<MemberAttribute>();
    public DietaryProfile? DietaryProfile { get; set; }

    // conversion config is in DbContext using Encrypted converters
}
