using MediatR;
using Tournaments.Contracts.Draft;

namespace Tournaments.Application.Features.Draft.RemoveReserve;

public record RemoveReserveCommand(
    Guid DraftSessionId,
    Guid TeamId,
    Guid PlayerId,
    int ExpectedVersion
) : IRequest<DraftStateSnapshotDto>;

