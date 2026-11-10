using BuildingBlocks.Domain;
using MediatR;
using Tournaments.Domain.Repositories;

namespace Tournaments.Application.Features.Tournaments.DeleteTournament;

public class DeleteTournamentHandler : IRequestHandler<DeleteTournamentCommand>
{
    private readonly ITournamentRepository _tournamentRepository;
    private readonly IMatchSeriesRepository _matchRepository;
    private readonly IDraftSessionRepository _draftRepository;

    public DeleteTournamentHandler(
        ITournamentRepository tournamentRepository,
        IMatchSeriesRepository matchRepository,
        IDraftSessionRepository draftRepository)
    {
        _tournamentRepository = tournamentRepository;
        _matchRepository = matchRepository;
        _draftRepository = draftRepository;
    }

    public async Task Handle(DeleteTournamentCommand request, CancellationToken cancellationToken)
    {
        var tournament = await _tournamentRepository.GetByIdAsync(request.TournamentId, cancellationToken)
            ?? throw new DomainException($"Torneio '{request.TournamentId}' não encontrado.");

        // 1. Remove as partidas vinculadas
        var matches = await _matchRepository.GetByTournamentIdAsync(tournament.Id, cancellationToken);
        if (matches.Count > 0)
        {
            await _matchRepository.RemoveRangeAsync(matches, cancellationToken);
        }

        // 2. Remove sessão de draft vinculada (se houver)
        var draftSession = await _draftRepository.GetByTournamentIdAsync(tournament.Id, cancellationToken);
        if (draftSession != null)
        {
            await _draftRepository.DeleteAsync(draftSession, cancellationToken);
        }

        // 3. Remove o torneio
        await _tournamentRepository.DeleteAsync(tournament, cancellationToken);
    }
}

