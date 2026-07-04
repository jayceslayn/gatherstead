namespace Gatherstead.Data.Entities;

public static class BedSizes
{
    /// <summary>
    /// Approximate sleeping capacity per bed of a given size, used to derive an accommodation's
    /// total "sleeps" capacity. These are rough conventions, not exact — a Bunk is treated as two
    /// spots, a Sofa/Crib as one.
    /// </summary>
    public static int Sleeps(BedSize size) => size switch
    {
        BedSize.Single => 1,
        BedSize.Double => 2,
        BedSize.Queen  => 2,
        BedSize.King   => 2,
        BedSize.Bunk   => 2,
        BedSize.Sofa   => 1,
        BedSize.Crib   => 1,
        BedSize.Other  => 1,
        _              => 1,
    };

    /// <summary>
    /// Total sleeps across a bed inventory; null when no beds are recorded, meaning the
    /// accommodation's capacity is unconstrained (not zero).
    /// </summary>
    public static int? SleepsCapacity(IEnumerable<(BedSize Size, int Quantity)> beds)
    {
        int? total = null;
        foreach (var (size, quantity) in beds)
            total = (total ?? 0) + quantity * Sleeps(size);
        return total;
    }
}
