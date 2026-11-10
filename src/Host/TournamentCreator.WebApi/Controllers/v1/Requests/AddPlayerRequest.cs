namespace TournamentCreator.WebApi.Controllers.v1.Requests;

public record AddPlayerRequest(
    Guid? PlayerId,
    string Name,
    string? ExternalUserId = null,
    IReadOnlyList<string>? Roles = null,
    string? PreferredRole = null,
    int? RankTier = null,
    string? AvatarUrl = null
);

public record AddLatePlayerRequest(
    Guid? PlayerId,
    string Name,
    string? ExternalUserId = null,
    IReadOnlyList<string>? Roles = null,
    string? PreferredRole = null,
    int? RankTier = null,
    string? AvatarUrl = null,
    int? ExpectedVersion = null
);
