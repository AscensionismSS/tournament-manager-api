using Tournaments.Contracts.Draft;

namespace Tournaments.Contracts.Tournaments;

public record TournamentDto(
    Guid Id,
    string Name,
    string? BannerUrl,
    string Status,
    int TeamCount,
    int GroupCount,
    string FormatType,
    string PlayoffFormat,
    string? GroupStageSeriesFormat,
    string PlayoffSeriesFormat,
    string GrandFinalSeriesFormat,
    Guid? DraftSessionId,
    Guid? ChampionId,
    string? ChampionName,
    DateTime CreatedAtUtc,
    IReadOnlyList<TournamentTeamDto> Teams,
    IReadOnlyList<BracketSlotDto> BracketSlots,
    int Version = 1,
    DateTime? LastUpdatedAtUtc = null,
    int? RegisteredPlayerCount = null,
    IReadOnlyList<DraftPlayerDto>? DraftPlayerPool = null,
    int? MinRequiredPlayers = null,
    bool? CanStartDraft = null,
    bool? CanStartTournament = null,
    bool IsResultSubmissionLocked = false,
    string? ResultLockReason = null,
    DateTime? ResultLockedAtUtc = null
);
