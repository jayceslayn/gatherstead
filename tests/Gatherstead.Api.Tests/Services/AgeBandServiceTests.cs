using Gatherstead.Api.Services.AgeBands;
using Gatherstead.Data.Entities;

namespace Gatherstead.Api.Tests.Services;

public class AgeBandServiceTests
{
    private readonly AgeBandService _svc = new();

    [Fact]
    public void ListOptions_ReturnsSixBands()
    {
        var options = _svc.ListOptions();
        Assert.Equal(6, options.Count);
    }

    [Fact]
    public void ListOptions_AreSortedBySortOrder()
    {
        var options = _svc.ListOptions();
        var orders = options.Select(o => o.SortOrder).ToList();
        Assert.Equal(orders.OrderBy(x => x), orders);
    }

    [Fact]
    public void ListOptions_DisplayNamesMatchExpected()
    {
        var options = _svc.ListOptions();
        Assert.Equal("0–2",   options.Single(o => o.Value == AgeBand.Age0To2).DisplayName);
        Assert.Equal("65+",      options.Single(o => o.Value == AgeBand.Age65Plus).DisplayName);
    }
}
