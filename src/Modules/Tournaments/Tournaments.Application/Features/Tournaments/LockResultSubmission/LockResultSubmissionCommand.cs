using MediatR;
using Tournaments.Contracts.Tournaments;

namespace Tournaments.Application.Features.Tournaments.LockResultSubmission;

public record LockResultSubmissionCommand(
    Guid TournamentId,
    string? Reason,
    string AdminId
) : IRequest<TournamentDto>;
