using MediatR;
using Tournaments.Contracts.Tournaments;
using Tournaments.Domain.Enums;

namespace Tournaments.Application.Features.Tournaments.GetTournaments;

public record GetTournamentsQuery(TournamentStatus? Status = null) : IRequest<IReadOnlyList<TournamentDto>>;
