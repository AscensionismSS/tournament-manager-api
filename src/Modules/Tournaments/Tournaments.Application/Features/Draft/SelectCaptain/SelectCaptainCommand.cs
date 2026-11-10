using MediatR;
using Tournaments.Contracts.Draft;

namespace Tournaments.Application.Features.Draft.SelectCaptain;

public record SelectCaptainCommand(
    Guid DraftSessionId,
    Guid PlayerId,
    int ExpectedVersion,
    Guid? TeamId = null
) : IRequest<DraftStateSnapshotDto>;
