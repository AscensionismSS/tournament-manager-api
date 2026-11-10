using MediatR;
using Tournaments.Contracts.Draft;

namespace Tournaments.Application.Features.Draft.CompleteDraft;

public record CompleteDraftCommand(
    Guid DraftSessionId,
    int ExpectedVersion
) : IRequest<DraftStateSnapshotDto>;
