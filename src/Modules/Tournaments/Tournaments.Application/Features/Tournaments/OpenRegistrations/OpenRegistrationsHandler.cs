using BuildingBlocks.Domain;
using MediatR;
using Tournaments.Application.Features.Draft;
using Tournaments.Contracts.Tournaments;
using Tournaments.Domain.Repositories;

namespace Tournaments.Application.Features.Tournaments.OpenRegistrations;

public class OpenRegistrationsHandler : IRequestHandler<OpenRegistrationsCommand, TournamentDto>
{
    private readonly ITournamentRepository _tournamentRepository;
    private readonly IDraftSessionRepository? _draftSessionRepository;
    private readonly ISystemSecuritySettingsRepository _securitySettingsRepository;

    public OpenRegistrationsHandler(
        ITournamentRepository tournamentRepository,
        IDraftSessionRepository? draftSessionRepository = null,
        ISystemSecuritySettingsRepository? securitySettingsRepository = null)
    {
        _tournamentRepository = tournamentRepository;
        _draftSessionRepository = draftSessionRepository;
        _securitySettingsRepository = securitySettingsRepository ?? new DefaultSystemSecuritySettingsRepository();
    }

    public async Task<TournamentDto> Handle(OpenRegistrationsCommand request, CancellationToken cancellationToken)
    {
        var securitySettings = await _securitySettingsRepository.GetAsync(cancellationToken);
        securitySettings.EnsureTournamentRegistrationAllowed();

        var tournament = await _tournamentRepository.GetByIdAsync(request.TournamentId, cancellationToken)
            ?? throw new DomainException($"Torneio '{request.TournamentId}' não encontrado.");

        tournament.OpenRegistrations();
        await _tournamentRepository.UpdateAsync(tournament, cancellationToken);

        int? registeredCount = null;
        if (tournament.DraftSessionId.HasValue && _draftSessionRepository != null)
        {
            var draftSession = await _draftSessionRepository.GetByIdAsync(tournament.DraftSessionId.Value, cancellationToken);
            if (draftSession != null)
            {
                registeredCount = draftSession.PlayerPool.Count +
                    draftSession.Teams.Sum(t => (t.CaptainId.HasValue ? 1 : 0) + t.Starters.Count + t.Reserves.Count);
                return tournament.ToDto(registeredCount, draftSession.PlayerPool.Select(p => p.ToDto()).ToList());
            }
        }

        return tournament.ToDto(registeredCount);
    }
}
