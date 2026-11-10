using BuildingBlocks.Domain;
using MediatR;
using Tournaments.Application.Common.Interfaces;
using Tournaments.Contracts.Draft;
using Tournaments.Domain.Enums;
using Tournaments.Domain.Repositories;

namespace Tournaments.Application.Features.Draft.RemovePlayerFromPool;

public class RemovePlayerFromPoolHandler : IRequestHandler<RemovePlayerFromPoolCommand, DraftStateSnapshotDto>
{
    private readonly IDraftSessionRepository _draftRepository;
    private readonly ITournamentRepository _tournamentRepository;
    private readonly ITournamentRealtimeNotifier _notifier;

    public RemovePlayerFromPoolHandler(
        IDraftSessionRepository draftRepository,
        ITournamentRepository tournamentRepository,
        ITournamentRealtimeNotifier notifier)
    {
        _draftRepository = draftRepository;
        _tournamentRepository = tournamentRepository;
        _notifier = notifier;
    }

    public async Task<DraftStateSnapshotDto> Handle(RemovePlayerFromPoolCommand request, CancellationToken cancellationToken)
    {
        var session = await _draftRepository.GetByIdAsync(request.DraftSessionId, cancellationToken)
            ?? throw new DomainException($"Sessão de Draft '{request.DraftSessionId}' não encontrada.");

        var tournament = await _tournamentRepository.GetByIdAsync(session.TournamentId, cancellationToken);
        if (tournament != null)
        {
            if (tournament.Status != TournamentStatus.RegistrationOpen && tournament.Status != TournamentStatus.Draft)
            {
                throw new DomainException("As inscrições do torneio estão encerradas ou o torneio já iniciou. Não é permitido cancelar a inscrição.");
            }
        }

        session.RemovePlayerFromPool(request.PlayerId, request.ActingUserId, request.IsAdmin, request.ExpectedVersion);
        await _draftRepository.UpdateAsync(session, cancellationToken);

        var snapshot = DraftSnapshotMapper.ToSnapshotDto(session);
        await _notifier.NotifyDraftStateSnapshotAsync(session.TournamentId, snapshot, cancellationToken);

        return snapshot;
    }
}
