namespace Gatherstead.Api.Contracts.Responses;

public class BaseEntityResponse<T>
{
    public T? Entity { get; set; }

    public bool Successful { get; set; } = false;

    public List<ResponseMessage> Messages { get; } = new();

    public BaseEntityResponse<T> AddResponseMessage(MessageType type, string message)
    {
        Messages.Add(new ResponseMessage
        {
            Type = type,
            Message = message
        });

        return this;
    }

    /// <summary>
    /// Attaches a message carrying a stable <see cref="ErrorCode"/> and optional interpolation
    /// <paramref name="params"/>, so the frontend can render a localized template while keeping
    /// <paramref name="message"/> as the human-readable fallback.
    /// </summary>
    public BaseEntityResponse<T> AddResponseMessage(
        MessageType type,
        ErrorCode code,
        string message,
        IReadOnlyDictionary<string, string>? @params = null)
    {
        Messages.Add(new ResponseMessage
        {
            Type = type,
            Code = code,
            Message = message,
            Params = @params
        });

        return this;
    }

    public BaseEntityResponse<T> SetSuccess(T entity)
    {
        Entity = entity;
        Successful = true;
        return this;
    }

    public static BaseEntityResponse<T> SuccessfulResponse(T entity) => new()
    {
        Entity = entity,
        Successful = true
    };
}
