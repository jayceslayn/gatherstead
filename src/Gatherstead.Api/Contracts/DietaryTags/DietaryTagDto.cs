using Gatherstead.Data.Entities;

namespace Gatherstead.Api.Contracts.DietaryTags;

public record DietaryTagDto(
    Guid Id,
    string Slug,
    string DisplayName,
    DietaryCategory Category,
    int SortOrder);
