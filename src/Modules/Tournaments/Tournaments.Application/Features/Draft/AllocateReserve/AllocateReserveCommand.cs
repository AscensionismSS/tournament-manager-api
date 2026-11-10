using MediatR;
using Tournaments.Contracts.Draft;

namespace Tournaments.Application.Features.Draft.AllocateReserve;

public record AllocateReserveCommand(
    Guid DraftSessionId,
    Guid TeamId,
    Guid PlayerId,
    int ExpectedVersion
) : IRequest<DraftStateSnapshotDto>;
