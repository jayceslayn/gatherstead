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
