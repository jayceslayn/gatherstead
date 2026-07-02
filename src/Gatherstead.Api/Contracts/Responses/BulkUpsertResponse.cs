namespace Gatherstead.Api.Contracts.Responses;

/// <summary>A single item that failed within a bulk operation, identified by its index in the
/// submitted list so the client can map the failure back to the row it sent.</summary>
public record BulkItemError(int Index, string Message);

/// <summary>
/// Envelope for bulk upsert operations. <see cref="BaseEntityResponse{T}.Entity"/> holds the
/// successfully upserted rows; <see cref="ItemErrors"/> lists per-item failures (member not
/// found, unauthorized, plan not found). Whole-request failures (e.g. tenant mismatch) still use
/// the inherited <see cref="BaseEntityResponse{T}.Messages"/> and make the request unsuccessful.
/// </summary>
public class BulkUpsertResponse<T> : BaseEntityResponse<IReadOnlyCollection<T>>
{
    public List<BulkItemError> ItemErrors { get; } = new();
}
