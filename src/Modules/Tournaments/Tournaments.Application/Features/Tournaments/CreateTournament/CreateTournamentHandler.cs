using BuildingBlocks.Domain;
using MediatR;
using Tournaments.Contracts.Tournaments;
using Tournaments.Domain.Entities;
using Tournaments.Domain.Enums;
using Tournaments.Domain.Repositories;
using Tournaments.Domain.ValueObjects;

namespace Tournaments.Application.Features.Tournaments.CreateTournament;

public class CreateTournamentHandler : IRequestHandler<CreateTournamentCommand, TournamentDto>
{
    private readonly ITournamentRepository _tournamentRepository;
    private readonly IDraftSessionRepository _draftSessionRepository;
    private readonly ISystemSecuritySettingsRepository _securitySettingsRepository;

    public CreateTournamentHandler(
        ITournamentRepository tournamentRepository,
        IDraftSessionRepository draftSessionRepository,
        ISystemSecuritySettingsRepository? securitySettingsRepository = null)
    {
        _tournamentRepository = tournamentRepository;
        _draftSessionRepository = draftSessionRepository;
        _securitySettingsRepository = securitySettingsRepository ?? new DefaultSystemSecuritySettingsRepository();
    }

    public async Task<TournamentDto> Handle(CreateTournamentCommand request, CancellationToken cancellationToken)
    {
        var securitySettings = await _securitySettingsRepository.GetAsync(cancellationToken);
        securitySettings.EnsureTournamentCreationAllowed();

        // Regra de Domínio/Produto:
        // - Grupos + Playoffs utiliza obrigatoriamente Eliminatória Dupla (Upper & Lower Bracket).
        // - Mata-Mata Direto utiliza obrigatoriamente Eliminatória Simples (Single Elimination) e 1 grupo.
        var normalizedPlayoffFormat = request.Type == TournamentType.GroupStageAndPlayoffs
            ? PlayoffFormat.DoubleElimination
            : PlayoffFormat.SingleElimination;

        var normalizedGroupCount = (request.Type == TournamentType.DirectPlayoffs || request.TeamCount != 8)
            ? 1
            : request.GroupCount;

        var normalizedGroupStageFormat = request.Type == TournamentType.DirectPlayoffs
            ? (SeriesFormat?)null
            : (request.GroupStageFormat ?? SeriesFormat.Bo1);

        var formatConfigResult = TournamentFormatConfig.Create(
            request.Type,
            normalizedPlayoffFormat,
            request.TeamCount,
            normalizedGroupCount);

        if (formatConfigResult.IsFailure)
            throw new DomainException(formatConfigResult.Error);

        var seriesConfigResult = SeriesConfig.Create(
            normalizedGroupStageFormat,
            request.PlayoffSeriesFormat,
            request.GrandFinalSeriesFormat);

        if (seriesConfigResult.IsFailure)
            throw new DomainException(seriesConfigResult.Error);

        var tournament = new Tournament(
            request.Name,
            formatConfigResult.Value,
            seriesConfigResult.Value,
            null,
            request.BannerUrl);

        if (request.EnableDraftMode)
        {
            var draftSession = new DraftSession(tournament.Id, request.TeamCount);
            tournament.AssignDraftSession(draftSession.Id);

            await _draftSessionRepository.AddAsync(draftSession, cancellationToken);
        }

        await _tournamentRepository.AddAsync(tournament, cancellationToken);

        return tournament.ToDto();
    }
}
