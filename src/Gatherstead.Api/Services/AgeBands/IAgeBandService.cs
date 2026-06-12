using Gatherstead.Api.Contracts.AgeBands;

namespace Gatherstead.Api.Services.AgeBands;

public interface IAgeBandService
{
    IReadOnlyList<AgeBandOptionDto> ListOptions();
}
