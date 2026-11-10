using MediatR;
using Tournaments.Contracts.Draft;

namespace Tournaments.Application.Features.Draft.RemoveCaptain;

public record RemoveCaptainCommand(
    Guid DraftSessionId,
    Guid TeamId,
    int ExpectedVersion
) : IRequest<DraftStateSnapshotDto>;
