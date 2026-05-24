using Gatherstead.Api.Contracts.Attributes;
using Gatherstead.Api.Contracts.Responses;

namespace Gatherstead.Api.Contracts.TaskTemplateAttributes;

public class TaskTemplateAttributeResponse : BaseEntityResponse<TaskTemplateAttributeDto>
{
}

public record TaskTemplateAttributeDto(
    Guid Id,
    Guid TenantId,
    Guid TaskTemplateId,
    string Key,
    string Value,
    byte TenantMinRole,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    bool IsDeleted,
    DateTimeOffset? DeletedAt,
    Guid? DeletedByUserId) : IAttributeDto;
