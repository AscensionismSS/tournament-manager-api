using MediatR;
using Tournaments.Contracts.Draft;

namespace Tournaments.Application.Features.Draft.CancelSubstitution;

public record CancelSubstitutionCommand(
    Guid DraftSessionId,
    int ExpectedVersion
) : IRequest<DraftStateSnapshotDto>;
