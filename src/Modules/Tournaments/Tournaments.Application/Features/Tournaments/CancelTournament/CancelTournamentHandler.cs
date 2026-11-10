using BuildingBlocks.Domain;
using MediatR;
using Tournaments.Application.Common.Interfaces;
using Tournaments.Contracts.Tournaments;
using Tournaments.Domain.Repositories;

namespace Tournaments.Application.Features.Tournaments.CancelTournament;

public class CancelTournamentHandler : IRequestHandler<CancelTournamentCommand, TournamentDto>
{
    private readonly ITournamentRepository _tournamentRepository;
    private readonly ITournamentRealtimeNotifier? _notifier;

    public CancelTournamentHandler(
        ITournamentRepository tournamentRepository,
        ITournamentRealtimeNotifier? notifier = null)
    {
        _tournamentRepository = tournamentRepository;
        _notifier = notifier;
    }

    public async Task<TournamentDto> Handle(CancelTournamentCommand request, CancellationToken cancellationToken)
    {
        var tournament = await _tournamentRepository.GetByIdAsync(request.TournamentId, cancellationToken)
            ?? throw new DomainException($"Torneio '{request.TournamentId}' não encontrado.");

        tournament.Cancel();
        await _tournamentRepository.UpdateAsync(tournament, cancellationToken);

        if (_notifier != null)
        {
            await _notifier.NotifyTournamentWebhookAsync(new TournamentWebhookEventDto(
                tournament.Id,
                tournament.Name,
                "TournamentCanceled",
                $"O campeonato '{tournament.Name}' foi cancelado.",
                tournament.Version,
                tournament.Status.ToString(),
                null,
                null,
                null,
                DateTime.UtcNow
            ), cancellationToken);
        }

        return TournamentMapper.ToDto(tournament);
    }
}
