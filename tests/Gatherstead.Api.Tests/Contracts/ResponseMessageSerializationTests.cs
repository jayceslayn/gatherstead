using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using Gatherstead.Api.Contracts.Responses;

namespace Gatherstead.Api.Tests.Contracts;

/// <summary>
/// Pins the wire shape of the error envelope. The frontend keys localized templates off the string
/// <c>code</c> and interpolates <c>params</c>, so an enum-naming-policy change or a renamed property
/// would silently break every localized error message.
/// </summary>
public class ResponseMessageSerializationTests
{
    // Mirrors the API's JSON config: web defaults (camelCase) + enums as strings (Program.cs).
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    [Fact]
    public void ErrorEnvelope_SerializesCodeAsScreamingSnakeStringWithParams()
    {
        var response = new BaseEntityResponse<string>();
        response.AddResponseMessage(
            MessageType.ERROR,
            ErrorCode.ENTITY_CONFLICT,
            "An accommodation named 'Lakeside Cabin' already exists in this property.",
            new Dictionary<string, string> { ["entity"] = "accommodation", ["name"] = "Lakeside Cabin" });

        var json = JsonSerializer.Serialize(response, Options);

        Assert.Contains("\"successful\":false", json);
        Assert.Contains("\"type\":\"ERROR\"", json);
        Assert.Contains("\"code\":\"ENTITY_CONFLICT\"", json);
        Assert.Contains("\"params\":{", json);
        Assert.Contains("\"entity\":\"accommodation\"", json);
        Assert.Contains("\"name\":\"Lakeside Cabin\"", json);
    }

    [Fact]
    public void PlainMessage_OmitsCodeAndParams()
    {
        var response = new BaseEntityResponse<string>();
        response.AddResponseMessage(MessageType.INFORMATION, "Saved.");

        var json = JsonSerializer.Serialize(response, Options);

        // Code/Params are nullable and default to null → absent from the payload for legacy messages.
        Assert.DoesNotContain("\"code\"", json);
        Assert.DoesNotContain("\"params\"", json);
    }
}
