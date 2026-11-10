using MediatR;
using Tournaments.Contracts.Tournaments;

namespace Tournaments.Application.Features.Tournaments.GetTournamentById;

public record GetTournamentByIdQuery(Guid Id) : IRequest<TournamentDto?>;
