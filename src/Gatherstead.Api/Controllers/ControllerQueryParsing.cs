namespace Gatherstead.Api.Controllers;

/// <summary>Shared parsing helpers for controller query-string values.</summary>
internal static class ControllerQueryParsing
{
    /// <summary>
    /// Parses an optional comma-separated list of GUIDs. Returns true on success (with
    /// <paramref name="ids"/> null when the input was null/blank, or a populated list otherwise);
    /// returns false with an <paramref name="error"/> message when a segment is not a valid GUID.
    /// </summary>
    public static bool TryParseGuidCsv(string? csv, out IEnumerable<Guid>? ids, out string? error)
    {
        ids = null;
        error = null;

        if (string.IsNullOrWhiteSpace(csv))
            return true;

        var list = new List<Guid>();
        foreach (var segment in csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!Guid.TryParse(segment, out var parsed))
            {
                error = $"Invalid identifier: '{segment}'.";
                return false;
            }
            list.Add(parsed);
        }

        ids = list;
        return true;
    }
}
