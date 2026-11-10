using MediatR;
using Tournaments.Contracts.Tournaments;

namespace Tournaments.Application.Features.Tournaments.OpenRegistrations;

public record OpenRegistrationsCommand(Guid TournamentId) : IRequest<TournamentDto>;

