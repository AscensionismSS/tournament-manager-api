using MediatR;
using Tournaments.Contracts.Draft;

namespace Tournaments.Application.Features.Draft.RemovePlayerFromPool;

public record RemovePlayerFromPoolCommand(
    Guid DraftSessionId,
    Guid PlayerId,
    string ActingUserId,
    bool IsAdmin,
    int? ExpectedVersion = null
) : IRequest<DraftStateSnapshotDto>;

