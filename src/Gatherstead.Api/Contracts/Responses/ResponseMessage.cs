namespace Gatherstead.Api.Contracts.Responses;

public class ResponseMessage
{
    public MessageType Type { get; set; }

    public string Message { get; set; } = string.Empty;
}
