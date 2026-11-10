using MediatR;
using Tournaments.Contracts.Draft;

namespace Tournaments.Application.Features.Draft.AddPlayerToPool;

public record AddPlayerToPoolCommand(
    Guid DraftSessionId,
    Guid PlayerId,
    string Name,
    string? ExternalUserId = null,
    IReadOnlyList<string>? Roles = null,
    string? PreferredRole = null,
    int? RankTier = null,
    string? AvatarUrl = null
) : IRequest<DraftStateSnapshotDto>;
