namespace Tournaments.Domain.Entities;

public record DraftPlayer(
    Guid Id,
    string Name,
    string? ExternalUserId = null,
    IReadOnlyList<string>? Roles = null,
    string? PreferredRole = null,
    int? RankTier = null,
    string? AvatarUrl = null
)
{
    private DraftPlayer() : this(Guid.Empty, string.Empty, null, null, null, null, null) { }
}
