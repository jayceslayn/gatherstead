using System;
using System.Collections.Generic;
using Gatherstead.Db.Encryption;

namespace Gatherstead.Db.Entities;

public class HouseholdMember
{
    public Guid Id { get; set; }
    public Guid HouseholdId { get; set; }
    public Household? Household { get; set; }

    public bool IsAdult { get; set; }
    public string? AgeBand { get; set; }

    // Encrypted fields
    public string Name { get; set; } = string.Empty;
    public DateOnly? BirthDate { get; set; }
    public string? DietaryNotes { get; set; }

    public string[] DietaryTags { get; set; } = Array.Empty<string>();

    // conversion config is in DbContext using Encrypted converters
}
