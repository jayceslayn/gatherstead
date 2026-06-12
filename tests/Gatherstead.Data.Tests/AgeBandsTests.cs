using Gatherstead.Data.Entities;

namespace Gatherstead.Data.Tests;

public class AgeBandsTests
{
    private static AgeBand Derive(int year, int month, int day, DateOnly today)
        => AgeBands.DeriveFromBirthDate(new DateOnly(year, month, day), today);

    [Fact]
    public void DeriveFromBirthDate_ReturnsAge0To2_ForInfants()
    {
        var today = new DateOnly(2025, 6, 1);
        Assert.Equal(AgeBand.Age0To2, Derive(2023, 6, 1, today));  // exactly 2
        Assert.Equal(AgeBand.Age0To2, Derive(2025, 1, 1, today));  // 0
    }

    [Fact]
    public void DeriveFromBirthDate_ReturnsAge3To5_AtBoundaries()
    {
        var today = new DateOnly(2025, 6, 1);
        Assert.Equal(AgeBand.Age3To5, Derive(2022, 6, 1, today));  // exactly 3
        Assert.Equal(AgeBand.Age3To5, Derive(2020, 6, 1, today));  // exactly 5
    }

    [Fact]
    public void DeriveFromBirthDate_ReturnsAge6To12_AtBoundaries()
    {
        var today = new DateOnly(2025, 6, 1);
        Assert.Equal(AgeBand.Age6To12, Derive(2019, 6, 1, today));  // exactly 6
        Assert.Equal(AgeBand.Age6To12, Derive(2013, 6, 1, today));  // exactly 12
    }

    [Fact]
    public void DeriveFromBirthDate_ReturnsAge13To17_AtBoundaries()
    {
        var today = new DateOnly(2025, 6, 1);
        Assert.Equal(AgeBand.Age13To17, Derive(2012, 6, 1, today));  // exactly 13
        Assert.Equal(AgeBand.Age13To17, Derive(2008, 6, 1, today));  // exactly 17
    }

    [Fact]
    public void DeriveFromBirthDate_ReturnsAge18To64_AtBoundaries()
    {
        var today = new DateOnly(2025, 6, 1);
        Assert.Equal(AgeBand.Age18To64, Derive(2007, 6, 1, today));  // exactly 18
        Assert.Equal(AgeBand.Age18To64, Derive(1961, 6, 1, today));  // exactly 64
    }

    [Fact]
    public void DeriveFromBirthDate_ReturnsAge65Plus_AtBoundaries()
    {
        var today = new DateOnly(2025, 6, 1);
        Assert.Equal(AgeBand.Age65Plus, Derive(1960, 6, 1, today));  // exactly 65
        Assert.Equal(AgeBand.Age65Plus, Derive(1930, 1, 1, today));  // very old
    }

    [Fact]
    public void DeriveFromBirthDate_HandlesHasBirthdayNotYetThisYear()
    {
        var today = new DateOnly(2025, 3, 1);
        // Born 2020-06-01: turns 5 in June, so currently 4
        Assert.Equal(AgeBand.Age3To5, Derive(2020, 6, 1, today));
    }

    [Fact]
    public void All_ReturnsOptionsInSortOrder()
    {
        var sorted = AgeBands.All.OrderBy(o => o.SortOrder).ToList();
        Assert.Equal(AgeBands.All.Select(o => o.Value), sorted.Select(o => o.Value));
    }

    [Fact]
    public void All_HasSixBands()
    {
        Assert.Equal(6, AgeBands.All.Count);
    }
}
