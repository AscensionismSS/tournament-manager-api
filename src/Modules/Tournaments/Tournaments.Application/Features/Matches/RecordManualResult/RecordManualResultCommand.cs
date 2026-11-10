using MediatR;

namespace Tournaments.Application.Features.Matches.RecordManualResult;

public record RecordManualResultCommand(
    Guid MatchSeriesId,
    Guid WinnerTeamId,
    long DurationSeconds,
    int TeamAKills,
    int TeamBKills,
    Guid? FirstPickTeamId = null,
    IReadOnlyList<int>? TeamABanHeroIds = null,
    IReadOnlyList<int>? TeamBBanHeroIds = null,
    long? DotaMatchId = null,
    int? GameNumber = null
) : IRequest;
