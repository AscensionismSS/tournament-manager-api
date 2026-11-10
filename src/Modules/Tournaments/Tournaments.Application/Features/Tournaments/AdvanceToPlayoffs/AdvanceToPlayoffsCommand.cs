using MediatR;
using Tournaments.Contracts.Tournaments;

namespace Tournaments.Application.Features.Tournaments.AdvanceToPlayoffs;

public record AdvanceToPlayoffsCommand(Guid TournamentId) : IRequest<TournamentDto>;
