namespace TournamentCreator.WebApi.Controllers.v1.Requests;

public record UpdateGameResultRequest(
    Guid WinnerTeamId,
    long DurationSeconds,
    int TeamAKills,
    int TeamBKills,
    Guid? FirstPickTeamId = null,
    IReadOnlyList<int>? TeamABanHeroIds = null,
    IReadOnlyList<int>? TeamBBanHeroIds = null,
    long? DotaMatchId = null
);
