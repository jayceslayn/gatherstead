using System.ComponentModel.DataAnnotations;
using Gatherstead.Data.Entities;

namespace Gatherstead.Api.Contracts.AgeBands;

public record AgeBandOptionDto(
    [property: Required] AgeBand Value,
    [property: Required] string DisplayName,
    [property: Required] int MinAge,
    int? MaxAge,
    [property: Required] int SortOrder);
