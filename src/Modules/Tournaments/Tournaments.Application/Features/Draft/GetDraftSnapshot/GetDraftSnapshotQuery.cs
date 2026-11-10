using MediatR;
using Tournaments.Contracts.Draft;

namespace Tournaments.Application.Features.Draft.GetDraftSnapshot;

public record GetDraftSnapshotQuery(Guid DraftSessionId) : IRequest<DraftStateSnapshotDto>;
