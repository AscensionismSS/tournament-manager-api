namespace Tournaments.Contracts.Draft;

public record DraftTeamDto(
    Guid Id,
    string Name,
    Guid? CaptainId,
    int PickOrder,
    IReadOnlyList<DraftPlayerDto> Starters,
    IReadOnlyList<DraftPlayerDto> Reserves
);
