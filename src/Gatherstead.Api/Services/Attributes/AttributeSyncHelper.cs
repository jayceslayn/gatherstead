using Gatherstead.Api.Contracts.Attributes;
using Gatherstead.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Gatherstead.Api.Services.Attributes;

internal static class AttributeSyncHelper
{
    /// Sync visible attributes using full-replace semantics.
    /// Visible attributes absent from <paramref name="incoming"/> are soft-deleted.
    /// Hidden attributes (above caller's role) are preserved untouched.
    /// Soft-deleted attributes whose key reappears in incoming are re-activated.
    internal static async Task SyncAsync<TAttr>(
        IQueryable<TAttr> byParent,
        DbSet<TAttr> dbSet,
        IReadOnlyList<AttributeWriteEntry> incoming,
        Func<TAttr, bool> isVisible,
        Guid tenantId,
        Func<TAttr> newEntity,
        Action<TAttr, AttributeWriteEntry>? applyExtra,
        CancellationToken ct)
        where TAttr : AuditableEntity, IParentScopedAttribute
    {
        // Load all (including soft-deleted) to support key re-activation without
        // violating the unique index on (TenantId, ParentId, Key).
        var all = await byParent.IgnoreQueryFilters().ToListAsync(ct);
        var incomingKeys = incoming.Select(e => e.Key.Trim()).ToHashSet(StringComparer.Ordinal);

        foreach (var entry in incoming)
        {
            var key = entry.Key.Trim();
            var value = entry.Value.Trim();
            var existing = all.FirstOrDefault(a => string.Equals(a.Key, key, StringComparison.Ordinal));

            if (existing is not null)
            {
                existing.IsDeleted = false;
                existing.DeletedAt = null;
                existing.DeletedByUserId = null;
                existing.Value = value;
                existing.TenantMinRole = entry.TenantMinRole;
                applyExtra?.Invoke(existing, entry);
            }
            else
            {
                var fresh = newEntity();
                fresh.Id = Guid.NewGuid();
                fresh.TenantId = tenantId;
                fresh.Key = key;
                fresh.Value = value;
                fresh.TenantMinRole = entry.TenantMinRole;
                applyExtra?.Invoke(fresh, entry);
                dbSet.Add(fresh);
            }
        }

        // Full replace: soft-delete visible attributes absent from the incoming list.
        foreach (var attr in all.Where(a => !a.IsDeleted && isVisible(a) && !incomingKeys.Contains(a.Key)))
            attr.IsDeleted = true;
    }
}
