using MediatR;
using Tournaments.Contracts.Tournaments;

namespace Tournaments.Application.Features.Tournaments.GetTournamentFormatOptions;

public record GetTournamentFormatOptionsQuery(
    int? TeamCount = null
) : IRequest<TournamentConfigurationOptionsDto>;
