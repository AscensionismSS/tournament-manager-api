using Tournaments.Domain.Enums;

namespace TournamentCreator.WebApi.Controllers.v1.Requests;

public record CreateTournamentRequest(
    string Name,
    TournamentType Type,
    PlayoffFormat? PlayoffFormat,
    int TeamCount,
    int GroupCount = 1,
    SeriesFormat? GroupStageFormat = null,
    SeriesFormat PlayoffSeriesFormat = SeriesFormat.Bo3,
    SeriesFormat GrandFinalSeriesFormat = SeriesFormat.Bo5,
    bool EnableDraftMode = true,
    string? BannerUrl = null
);
