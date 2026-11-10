namespace Tournaments.Contracts.Tournaments;

public record TournamentFormatOptionDto(
    string Type,
    string Name,
    string Description,
    IReadOnlyList<string> AllowedPlayoffFormats,
    IReadOnlyList<int> AllowedGroupCounts,
    bool SupportsDraftMode
);

public record SeriesFormatOptionsDto(
    IReadOnlyList<string> GroupStageFormats,
    IReadOnlyList<string> PlayoffFormats,
    IReadOnlyList<string> GrandFinalFormats
);

public record TournamentConfigurationOptionsDto(
    int? SelectedTeamCount,
    IReadOnlyList<int> SupportedTeamCounts,
    IReadOnlyList<TournamentFormatOptionDto> AvailableFormats,
    SeriesFormatOptionsDto SeriesFormats
);
