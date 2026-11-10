namespace TournamentCreator.WebApi.Controllers.v1.Requests;

public record RecordManualResultRequest(
    Guid WinnerTeamId,
    long DurationSeconds,
    int TeamAKills,
    int TeamBKills,
    Guid? FirstPickTeamId = null,
    IReadOnlyList<int>? TeamABanHeroIds = null,
    IReadOnlyList<int>? TeamBBanHeroIds = null,
    long? DotaMatchId = null,
    int? GameNumber = null
);
