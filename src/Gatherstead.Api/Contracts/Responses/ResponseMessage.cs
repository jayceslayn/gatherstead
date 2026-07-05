using System.Text.Json.Serialization;

namespace Gatherstead.Api.Contracts.Responses;

public class ResponseMessage
{
    public MessageType Type { get; set; }

    /// <summary>
    /// Stable machine code for the error (null for INFORMATION/WARNING messages). The frontend keys
    /// its localized message templates off this; <see cref="Message"/> remains the fallback.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ErrorCode? Code { get; set; }

    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Interpolation values for the localized template (e.g. <c>{ ["name"] = "Lakeside Cabin" }</c>).
    /// Values are literal by default; the reserved key <c>entity</c> carries a stable token (e.g.
    /// "accommodation") the frontend resolves through its own i18n so entity nouns localize too.
    /// May contain user-entered values — returned only to the authenticated caller; never log it.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, string>? Params { get; set; }
}
