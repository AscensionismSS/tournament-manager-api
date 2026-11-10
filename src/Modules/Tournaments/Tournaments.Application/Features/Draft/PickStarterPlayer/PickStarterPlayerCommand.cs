using MediatR;
using Tournaments.Contracts.Draft;

namespace Tournaments.Application.Features.Draft.PickStarterPlayer;

public record PickStarterPlayerCommand(
    Guid DraftSessionId,
    Guid PlayerId,
    int ExpectedVersion
) : IRequest<DraftStateSnapshotDto>;
