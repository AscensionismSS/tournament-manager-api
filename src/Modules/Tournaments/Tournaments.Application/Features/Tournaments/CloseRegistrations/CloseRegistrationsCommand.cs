using MediatR;
using Tournaments.Contracts.Tournaments;

namespace Tournaments.Application.Features.Tournaments.CloseRegistrations;

public record CloseRegistrationsCommand(Guid TournamentId) : IRequest<TournamentDto>;
