using BuildingBlocks.Domain;
using MediatR;
using Tournaments.Contracts.Tournaments;
using Tournaments.Domain.Repositories;

namespace Tournaments.Application.Features.Tournaments.CloseRegistrations;

public class CloseRegistrationsHandler : IRequestHandler<CloseRegistrationsCommand, TournamentDto>
{
    private readonly ITournamentRepository _tournamentRepository;

    public CloseRegistrationsHandler(ITournamentRepository tournamentRepository)
    {
        _tournamentRepository = tournamentRepository;
    }

    public async Task<TournamentDto> Handle(CloseRegistrationsCommand request, CancellationToken cancellationToken)
    {
        var tournament = await _tournamentRepository.GetByIdAsync(request.TournamentId, cancellationToken)
            ?? throw new DomainException($"Torneio '{request.TournamentId}' não encontrado.");

        tournament.CloseRegistrations();
        await _tournamentRepository.UpdateAsync(tournament, cancellationToken);

        return tournament.ToDto();
    }
}
