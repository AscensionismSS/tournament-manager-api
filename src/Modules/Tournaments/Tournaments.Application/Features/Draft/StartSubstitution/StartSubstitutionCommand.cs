using MediatR;
using Tournaments.Contracts.Draft;

namespace Tournaments.Application.Features.Draft.StartSubstitution;

public record StartSubstitutionCommand(
    Guid DraftSessionId,
    Guid PlayerId,
    Guid TeamId,
    int ExpectedVersion
) : IRequest<DraftStateSnapshotDto>;
