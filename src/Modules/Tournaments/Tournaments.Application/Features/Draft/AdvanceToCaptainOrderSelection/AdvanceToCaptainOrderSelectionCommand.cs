using MediatR;
using Tournaments.Contracts.Draft;

namespace Tournaments.Application.Features.Draft.AdvanceToCaptainOrderSelection;

public record AdvanceToCaptainOrderSelectionCommand(
    Guid DraftSessionId,
    int ExpectedVersion
) : IRequest<DraftStateSnapshotDto>;

