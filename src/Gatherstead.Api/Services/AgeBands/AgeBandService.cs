using Gatherstead.Api.Contracts.AgeBands;
using Gatherstead.Data.Entities;
using DataAgeBands = Gatherstead.Data.Entities.AgeBands;

namespace Gatherstead.Api.Services.AgeBands;

public class AgeBandService : IAgeBandService
{
    private static readonly IReadOnlyList<AgeBandOptionDto> _options =
        DataAgeBands.All
            .Select(o => new AgeBandOptionDto(o.Value, o.DisplayName, o.MinAge, o.MaxAge, o.SortOrder))
            .ToList();

    public IReadOnlyList<AgeBandOptionDto> ListOptions() => _options;
}
