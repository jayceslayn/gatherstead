using Gatherstead.Api.Contracts.Responses;

namespace Gatherstead.Api.Contracts.Events;

public class EventResponse : BaseEntityResponse<EventDto>
{
}

public record EventDto(
    Guid Id,
    Guid TenantId,
    Guid PropertyId,
    string Name,
    DateOnly StartDate,
    DateOnly EndDate,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    bool IsDeleted,
    DateTimeOffset? DeletedAt,
    Guid? DeletedByUserId);
