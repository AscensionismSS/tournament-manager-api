using MediatR;
using Tournaments.Contracts.Draft;

namespace Tournaments.Application.Features.Draft.ConfirmSubstitution;

public record ConfirmSubstitutionCommand(
    Guid DraftSessionId,
    Guid NewPlayerId,
    int ExpectedVersion
) : IRequest<DraftStateSnapshotDto>;
