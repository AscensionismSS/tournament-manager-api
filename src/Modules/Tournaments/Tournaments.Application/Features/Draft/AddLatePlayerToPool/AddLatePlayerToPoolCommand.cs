using MediatR;
using Tournaments.Contracts.Draft;

namespace Tournaments.Application.Features.Draft.AddLatePlayerToPool;

public record AddLatePlayerToPoolCommand(
    Guid DraftSessionId,
    Guid PlayerId,
    string Name,
    string? ExternalUserId = null,
    IReadOnlyList<string>? Roles = null,
    string? PreferredRole = null,
    int? RankTier = null,
    string? AvatarUrl = null,
    int? ExpectedVersion = null
) : IRequest<DraftStateSnapshotDto>;
