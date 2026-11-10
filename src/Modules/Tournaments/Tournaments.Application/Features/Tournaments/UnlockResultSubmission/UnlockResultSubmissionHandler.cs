using BuildingBlocks.Domain;
using MediatR;
using Tournaments.Application.Common.Interfaces;
using Tournaments.Contracts.Tournaments;
using Tournaments.Domain.Repositories;

namespace Tournaments.Application.Features.Tournaments.UnlockResultSubmission;

public class UnlockResultSubmissionHandler : IRequestHandler<UnlockResultSubmissionCommand, TournamentDto>
{
    private readonly ITournamentRepository _tournamentRepository;
    private readonly ITournamentRealtimeNotifier? _notifier;

    public UnlockResultSubmissionHandler(
        ITournamentRepository tournamentRepository,
        ITournamentRealtimeNotifier? notifier = null)
    {
        _tournamentRepository = tournamentRepository;
        _notifier = notifier;
    }

    public async Task<TournamentDto> Handle(UnlockResultSubmissionCommand request, CancellationToken cancellationToken)
    {
        var tournament = await _tournamentRepository.GetByIdAsync(request.TournamentId, cancellationToken)
            ?? throw new DomainException($"Torneio '{request.TournamentId}' não encontrado.");

        tournament.UnlockResultSubmission(request.AdminId);
        await _tournamentRepository.UpdateAsync(tournament, cancellationToken);

        if (_notifier != null)
        {
            await _notifier.NotifyTournamentWebhookAsync(new TournamentWebhookEventDto(
                tournament.Id,
                tournament.Name,
                "TournamentResultsUnlocked",
                $"O envio de resultados do campeonato '{tournament.Name}' foi desbloqueado.",
                tournament.Version,
                tournament.Status.ToString(),
                null,
                null,
                null,
                DateTime.UtcNow
            ), cancellationToken);
        }

        return tournament.ToDto();
    }
}
