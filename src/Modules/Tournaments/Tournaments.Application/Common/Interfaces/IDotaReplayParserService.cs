using Tournaments.Contracts.Replay;

namespace Tournaments.Application.Common.Interfaces;

public interface IDotaReplayParserService
{
    Task<ParseGoDotaOutputDto> ParseReplayAsync(Stream replayStream, string originalFileName, CancellationToken cancellationToken = default);
}
