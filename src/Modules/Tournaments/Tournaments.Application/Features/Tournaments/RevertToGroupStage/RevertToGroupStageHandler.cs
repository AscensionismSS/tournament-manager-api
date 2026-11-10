using BuildingBlocks.Domain;
using MediatR;
using Tournaments.Contracts.Tournaments;
using Tournaments.Domain.Enums;
using Tournaments.Domain.Repositories;

namespace Tournaments.Application.Features.Tournaments.RevertToGroupStage;

public class RevertToGroupStageHandler : IRequestHandler<RevertToGroupStageCommand, TournamentDto>
{
    private readonly ITournamentRepository _tournamentRepository;
    private readonly IMatchSeriesRepository _matchRepository;

    public RevertToGroupStageHandler(
        ITournamentRepository tournamentRepository,
        IMatchSeriesRepository matchRepository)
    {
        _tournamentRepository = tournamentRepository;
        _matchRepository = matchRepository;
    }

    public async Task<TournamentDto> Handle(RevertToGroupStageCommand request, CancellationToken cancellationToken)
    {
        var tournament = await _tournamentRepository.GetByIdAsync(request.TournamentId, cancellationToken)
            ?? throw new DomainException($"Torneio '{request.TournamentId}' não encontrado.");

        if (tournament.IsResultSubmissionLocked)
            throw new DomainException($"O envio e alteração de resultados para este campeonato está bloqueado pela administração. Motivo: {tournament.ResultLockReason ?? "Não informado"}.");

        if (tournament.Status != TournamentStatus.Playoffs)
            throw new DomainException("Apenas torneios na fase de Playoffs podem ser revertidos para a Fase de Grupos.");

        if (tournament.FormatConfig.Type == TournamentType.DirectPlayoffs)
            throw new DomainException("Torneios no formato Mata-Mata Direto não possuem Fase de Grupos.");

        var allMatches = await _matchRepository.GetByTournamentIdAsync(request.TournamentId, cancellationToken);
        var playoffSlots = tournament.BracketSlots.Select(s => s.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Identifica as partidas que pertencem aos playoffs
        var playoffMatches = allMatches
            .Where(m => playoffSlots.Contains(m.SlotCode))
            .ToList();

        // Trava de segurança profissional: NENHUM mapa de playoff pode ter sido jogado
        if (playoffMatches.Any(m => m.Games.Count > 0))
        {
            throw new DomainException("Não é possível reverter para a Fase de Grupos pois os Playoffs já possuem partidas disputadas com mapas jogados.");
        }

        // Remove as partidas de playoff do banco
        await _matchRepository.RemoveRangeAsync(playoffMatches, cancellationToken);

        // Reverte o status e limpa as chaves no agregado do torneio
        tournament.RevertToGroupStage();
        await _tournamentRepository.UpdateAsync(tournament, cancellationToken);

        return TournamentMapper.ToDto(tournament);
    }
}
