using Gatherstead.Api.Contracts.Attributes;
using Gatherstead.Api.Contracts.Responses;

namespace Gatherstead.Api.Contracts.EventAttributes;

public class EventAttributeResponse : BaseEntityResponse<EventAttributeDto>
{
}

public record EventAttributeDto(
    Guid Id,
    Guid TenantId,
    Guid EventId,
    string Key,
    string Value,
    byte TenantMinRole,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    bool IsDeleted,
    DateTimeOffset? DeletedAt,
    Guid? DeletedByUserId) : IAttributeDto;
