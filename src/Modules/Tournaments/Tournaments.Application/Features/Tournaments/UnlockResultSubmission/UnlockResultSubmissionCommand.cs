using MediatR;
using Tournaments.Contracts.Tournaments;

namespace Tournaments.Application.Features.Tournaments.UnlockResultSubmission;

public record UnlockResultSubmissionCommand(
    Guid TournamentId,
    string AdminId
) : IRequest<TournamentDto>;
