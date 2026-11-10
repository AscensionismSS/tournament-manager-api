namespace Tournaments.Domain.Entities;

public record TournamentTeam(Guid Id, string Name)
{
    private TournamentTeam() : this(Guid.Empty, string.Empty) { }
}
