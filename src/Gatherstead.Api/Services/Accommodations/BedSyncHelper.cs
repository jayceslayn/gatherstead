using Gatherstead.Api.Contracts.Accommodations;
using Gatherstead.Data;
using Gatherstead.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Gatherstead.Api.Services.Accommodations;

internal static class BedSyncHelper
{
    /// <summary>
    /// Full-replace sync of an accommodation's bed inventory. Follows the attribute-sync pattern:
    /// loads existing rows including soft-deleted ones to re-activate a size that reappears (avoiding
    /// a unique-index collision on (TenantId, AccommodationId, Size)), updates quantities, and
    /// soft-deletes sizes absent from <paramref name="incoming"/>. Entries with quantity &lt;= 0 are
    /// treated as removals. Duplicate sizes in the incoming list collapse (last quantity wins).
    /// Caller is responsible for SaveChanges. Returns the resulting live rows (revived/updated plus
    /// newly added) so callers can build the response without re-querying.
    /// </summary>
    internal static async Task<List<AccommodationBed>> SyncAsync(
        DbSet<AccommodationBed> dbSet,
        Guid tenantId,
        Guid accommodationId,
        IReadOnlyList<BedWriteEntry> incoming,
        CancellationToken ct)
    {
        var all = await dbSet
            .Where(b => b.AccommodationId == accommodationId)
            .IgnoreQueryFilters([GathersteadDbContext.SoftDeleteFilter])
            .ToListAsync(ct);

        // Collapse duplicate sizes (last wins) and drop non-positive quantities.
        var desired = new Dictionary<BedSize, int>();
        foreach (var entry in incoming)
        {
            if (entry.Quantity > 0)
                desired[entry.Size] = entry.Quantity;
            else
                desired.Remove(entry.Size);
        }

        var live = new List<AccommodationBed>(desired.Count);
        foreach (var (size, quantity) in desired)
        {
            var existing = all.FirstOrDefault(b => b.Size == size);
            if (existing is not null)
            {
                existing.IsDeleted = false;
                existing.DeletedAt = null;
                existing.DeletedByUserId = null;
                existing.Quantity = quantity;
                live.Add(existing);
            }
            else
            {
                var bed = new AccommodationBed
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    AccommodationId = accommodationId,
                    Size = size,
                    Quantity = quantity,
                };
                dbSet.Add(bed);
                live.Add(bed);
            }
        }

        // Full replace: soft-delete live rows whose size is no longer desired.
        foreach (var bed in all.Where(b => !b.IsDeleted && !desired.ContainsKey(b.Size)))
            bed.IsDeleted = true;

        return live;
    }
}
