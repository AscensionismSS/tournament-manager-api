using MediatR;
using Tournaments.Contracts.Draft;
using Tournaments.Domain.Enums;

namespace Tournaments.Application.Features.Draft.FinalizeCaptainOrder;

public record FinalizeCaptainOrderCommand(
    Guid DraftSessionId,
    PickOrderType OrderType,
    IReadOnlyList<Guid>? ManualOrderIds,
    int ExpectedVersion
) : IRequest<DraftStateSnapshotDto>;
