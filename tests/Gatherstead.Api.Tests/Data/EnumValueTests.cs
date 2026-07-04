using Gatherstead.Data.Entities;

namespace Gatherstead.Api.Tests.Data;

/// <summary>
/// Pins the numeric values of persisted enums. These columns are stored numerically, so a value that
/// silently shifts would corrupt existing rows. Integration tests run on SQLite via EnsureCreated (no
/// migration), so these pins — not the migration — are what freeze the DB/wire semantics.
/// </summary>
public class EnumValueTests
{
    [Fact]
    public void SecurityEventType_ValuesAreStable()
    {
        Assert.Equal(0, (int)SecurityEventType.AuthFailure);
        Assert.Equal(1, (int)SecurityEventType.AuthzDenial);
        Assert.Equal(2, (int)SecurityEventType.CrossTenantWriteBlocked);
        Assert.Equal(3, (int)SecurityEventType.TokenRevoked);
        // 4 (SoftDelete) and 5 (Restore) removed; their numbers are reserved.
        Assert.Equal(6, (int)SecurityEventType.AppAdminAction);
        Assert.Equal(7, (int)SecurityEventType.RateLimitBreach);
        Assert.Equal(8, (int)SecurityEventType.InvitationCreated);
        Assert.Equal(9, (int)SecurityEventType.InvitationAccepted);
    }

    [Fact]
    public void AccommodationIntentStatus_ValuesAreStable()
    {
        Assert.Equal(0, (int)AccommodationIntentStatus.Requested);
        Assert.Equal(1, (int)AccommodationIntentStatus.Hold);
        Assert.Equal(2, (int)AccommodationIntentStatus.Confirmed);
        Assert.Equal(3, (int)AccommodationIntentStatus.Declined);
    }

    [Fact]
    public void IntentSource_ValuesAreStable()
    {
        Assert.Equal(0, (int)IntentSource.Volunteered);
        Assert.Equal(1, (int)IntentSource.Assigned);
    }

    [Fact]
    public void BedSize_ValuesAreStable()
    {
        Assert.Equal(0, (int)BedSize.Single);
        Assert.Equal(1, (int)BedSize.Double);
        Assert.Equal(2, (int)BedSize.Queen);
        Assert.Equal(3, (int)BedSize.King);
        Assert.Equal(4, (int)BedSize.Bunk);
        Assert.Equal(5, (int)BedSize.Sofa);
        Assert.Equal(6, (int)BedSize.Crib);
        Assert.Equal(7, (int)BedSize.Other);
    }

    [Theory]
    [InlineData(BedSize.Single, 1)]
    [InlineData(BedSize.Double, 2)]
    [InlineData(BedSize.Queen, 2)]
    [InlineData(BedSize.King, 2)]
    [InlineData(BedSize.Bunk, 2)]
    [InlineData(BedSize.Sofa, 1)]
    [InlineData(BedSize.Crib, 1)]
    [InlineData(BedSize.Other, 1)]
    public void BedSizes_Sleeps_MatchesConvention(BedSize size, int expected)
        => Assert.Equal(expected, BedSizes.Sleeps(size));
}
