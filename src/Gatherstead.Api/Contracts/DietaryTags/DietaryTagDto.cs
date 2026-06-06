using System.ComponentModel.DataAnnotations;
using Gatherstead.Data.Entities;

namespace Gatherstead.Api.Contracts.DietaryTags;

public record DietaryTagDto(
    [property: Required] Guid Id,
    [property: Required] string Slug,
    [property: Required] string DisplayName,
    [property: Required] DietaryCategory Category,
    [property: Required] int SortOrder);
