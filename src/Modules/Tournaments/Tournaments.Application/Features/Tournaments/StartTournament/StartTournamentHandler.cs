using BuildingBlocks.Domain;
using MediatR;
using Tournaments.Application.Common.Interfaces;
using Tournaments.Contracts.Tournaments;
using Tournaments.Domain.Enums;
using Tournaments.Domain.Repositories;
using Tournaments.Domain.Services;

namespace Tournaments.Application.Features.Tournaments.StartTournament;

public class StartTournamentHandler : IRequestHandler<StartTournamentCommand>
{
    private readonly ITournamentRepository _tournamentRepository;
    private readonly IMatchSeriesRepository _matchRepository;
    private readonly ITournamentRealtimeNotifier? _notifier;
    private readonly ISystemSecuritySettingsRepository _securitySettingsRepository;

    public StartTournamentHandler(
        ITournamentRepository tournamentRepository,
        IMatchSeriesRepository matchRepository,
        ITournamentRealtimeNotifier? notifier = null,
        ISystemSecuritySettingsRepository? securitySettingsRepository = null)
    {
        _tournamentRepository = tournamentRepository;
        _matchRepository = matchRepository;
        _notifier = notifier;
        _securitySettingsRepository = securitySettingsRepository ?? new DefaultSystemSecuritySettingsRepository();
    }

    public async Task Handle(StartTournamentCommand request, CancellationToken cancellationToken)
    {
        var securitySettings = await _securitySettingsRepository.GetAsync(cancellationToken);
        if (securitySettings.MaintenanceMode)
        {
            var detail = !string.IsNullOrWhiteSpace(securitySettings.Message) ? $" Motivo: {securitySettings.Message}" : string.Empty;
            throw new DomainException($"Não é possível iniciar o torneio durante o modo de manutenção do sistema.{detail}");
        }

        var tournament = await _tournamentRepository.GetByIdAsync(request.TournamentId, cancellationToken)
            ?? throw new DomainException("Torneio não encontrado.");

        tournament.Start();

        var existingMatches = await _matchRepository.GetByTournamentIdAsync(tournament.Id, cancellationToken);
        if (existingMatches.Count > 0)
        {
            throw new DomainException("As partidas deste campeonato já foram geradas.");
        }

        if (tournament.FormatConfig.Type == TournamentType.DirectPlayoffs)
        {
            var playoffMatches = PlayoffBracketGenerator.Generate(tournament, tournament.Teams.ToList());
            await _matchRepository.AddRangeAsync(playoffMatches, cancellationToken);
        }
        else
        {
            var groupMatches = GroupStageMatchGenerator.Generate(tournament);
            await _matchRepository.AddRangeAsync(groupMatches, cancellationToken);
        }

        await _tournamentRepository.UpdateAsync(tournament, cancellationToken);

        if (_notifier != null)
        {
            await _notifier.NotifyTournamentWebhookAsync(new TournamentWebhookEventDto(
                tournament.Id,
                tournament.Name,
                "TournamentStarted",
                $"O campeonato '{tournament.Name}' começou! As partidas já estão disponíveis.",
                tournament.Version,
                tournament.Status.ToString(),
                null,
                null,
                null,
                tournament.LastUpdatedAtUtc), cancellationToken);
        }
    }
}
