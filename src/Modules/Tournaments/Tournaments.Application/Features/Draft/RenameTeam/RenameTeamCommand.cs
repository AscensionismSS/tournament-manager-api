using MediatR;
using Tournaments.Contracts.Draft;

namespace Tournaments.Application.Features.Draft.RenameTeam;

public record RenameTeamCommand(
    Guid DraftSessionId,
    Guid TeamId,
    string? NewName,
    int ExpectedVersion,
    string? ActingUserId = null,
    bool IsAdmin = true
) : IRequest<DraftStateSnapshotDto>;
