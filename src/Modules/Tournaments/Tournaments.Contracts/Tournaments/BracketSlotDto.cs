namespace Tournaments.Contracts.Tournaments;

public record BracketSlotDto(
    string Code,
    string Stage,
    int Round,
    string TeamASource,
    string TeamBSource,
    string? WinnerTargetSlotCode,
    string? LoserTargetSlotCode,
    string SeriesFormat = "Bo3"
);
