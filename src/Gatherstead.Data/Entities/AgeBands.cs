namespace Gatherstead.Data.Entities;

public record AgeBandOption(AgeBand Value, string DisplayName, int MinAge, int? MaxAge, int SortOrder);

public static class AgeBands
{
    public static readonly IReadOnlyList<AgeBandOption> All =
    [
        new(AgeBand.Age0To2,   "0–2",   0,  2,  0),
        new(AgeBand.Age3To5,   "3–5",   3,  5,  1),
        new(AgeBand.Age6To12,  "6–12",  6,  12, 2),
        new(AgeBand.Age13To17, "13–17", 13, 17, 3),
        new(AgeBand.Age18To64, "18–64", 18, 64, 4),
        new(AgeBand.Age65Plus, "65+",        65, null, 5),
    ];

    public static AgeBand DeriveFromBirthDate(DateOnly birthDate, DateOnly today)
    {
        var age = today.Year - birthDate.Year;
        if (birthDate > today.AddYears(-age)) age--;

        return age switch
        {
            <= 2  => AgeBand.Age0To2,
            <= 5  => AgeBand.Age3To5,
            <= 12 => AgeBand.Age6To12,
            <= 17 => AgeBand.Age13To17,
            <= 64 => AgeBand.Age18To64,
            _     => AgeBand.Age65Plus,
        };
    }

    /// <summary>An age band counts as adult from the 18–64 band upward.</summary>
    public static bool IsAdult(AgeBand band) => band >= AgeBand.Age18To64;
}
