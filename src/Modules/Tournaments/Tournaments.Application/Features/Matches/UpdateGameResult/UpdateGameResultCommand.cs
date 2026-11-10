using MediatR;

namespace Tournaments.Application.Features.Matches.UpdateGameResult;

public record UpdateGameResultCommand(
    Guid MatchSeriesId,
    int GameNumber,
    Guid WinnerTeamId,
    long DurationSeconds,
    int TeamAKills,
    int TeamBKills,
    Guid? FirstPickTeamId = null,
    IReadOnlyList<int>? TeamABanHeroIds = null,
    IReadOnlyList<int>? TeamBBanHeroIds = null,
    long? DotaMatchId = null
) : IRequest;

public record UpdateGameCommand(
    Guid MatchSeriesId,
    int GameNumber,
    Guid WinnerTeamId,
    long DurationSeconds,
    int TeamAKills,
    int TeamBKills,
    Guid? FirstPickTeamId = null,
    IReadOnlyList<int>? TeamABanHeroIds = null,
    IReadOnlyList<int>? TeamBBanHeroIds = null,
    long? DotaMatchId = null
) : UpdateGameResultCommand(MatchSeriesId, GameNumber, WinnerTeamId, DurationSeconds, TeamAKills, TeamBKills, FirstPickTeamId, TeamABanHeroIds, TeamBBanHeroIds, DotaMatchId);
