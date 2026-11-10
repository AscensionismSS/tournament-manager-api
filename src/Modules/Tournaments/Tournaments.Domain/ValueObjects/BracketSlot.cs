namespace Tournaments.Domain.ValueObjects;

public enum BracketStage
{
    PlayIn = 1,
    Upper = 2,
    Lower = 3,
    GrandFinal = 4
}

public record BracketSlot(
    string Code,
    BracketStage Stage,
    int Round,
    string TeamASource,
    string TeamBSource,
    string? WinnerTargetSlotCode,
    string? LoserTargetSlotCode
)
{
    private BracketSlot() : this(string.Empty, BracketStage.Upper, 1, string.Empty, string.Empty, null, null) { }
}
