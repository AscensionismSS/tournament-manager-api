using MediatR;
using Tournaments.Contracts.Tournaments;

namespace Tournaments.Application.Features.Tournaments.RevertToGroupStage;

public record RevertToGroupStageCommand(Guid TournamentId) : IRequest<TournamentDto>;
