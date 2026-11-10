using BuildingBlocks.Domain;
using MediatR;
using Tournaments.Domain.Repositories;

namespace Tournaments.Application.Features.Tournaments.AddTeamToTournament;

public class AddTeamToTournamentHandler : IRequestHandler<AddTeamToTournamentCommand>
{
    private readonly ITournamentRepository _tournamentRepository;
    private readonly ISystemSecuritySettingsRepository _securitySettingsRepository;

    public AddTeamToTournamentHandler(
        ITournamentRepository tournamentRepository,
        ISystemSecuritySettingsRepository? securitySettingsRepository = null)
    {
        _tournamentRepository = tournamentRepository;
        _securitySettingsRepository = securitySettingsRepository ?? new DefaultSystemSecuritySettingsRepository();
    }

    public async Task Handle(AddTeamToTournamentCommand request, CancellationToken cancellationToken)
    {
        var securitySettings = await _securitySettingsRepository.GetAsync(cancellationToken);
        securitySettings.EnsureTournamentRegistrationAllowed();

        var tournament = await _tournamentRepository.GetByIdAsync(request.TournamentId, cancellationToken)
            ?? throw new DomainException("Torneio não encontrado.");

        tournament.AddTeam(request.TeamId, request.TeamName);
        await _tournamentRepository.UpdateAsync(tournament, cancellationToken);
    }
}
