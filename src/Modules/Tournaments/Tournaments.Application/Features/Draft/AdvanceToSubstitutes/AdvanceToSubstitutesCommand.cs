using MediatR;
using Tournaments.Contracts.Draft;

namespace Tournaments.Application.Features.Draft.AdvanceToSubstitutes;

public record AdvanceToSubstitutesCommand(
    Guid DraftSessionId,
    int ExpectedVersion
) : IRequest<DraftStateSnapshotDto>;

