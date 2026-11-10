using Tournaments.Contracts.Draft;
using Tournaments.Contracts.Tournaments;
using Tournaments.Domain.Entities;
using Tournaments.Domain.Enums;
using Tournaments.Domain.ValueObjects;

namespace Tournaments.Application.Features.Tournaments;

public static class TournamentMapper
{
    public static TournamentDto ToDto(
        this Tournament tournament,
        int? registeredPlayerCount = null,
        IReadOnlyList<DraftPlayerDto>? draftPlayerPool = null)
    {
        var slots = tournament.GetOrBuildBracketSlots();
        int? minRequired = tournament.DraftSessionId.HasValue ? tournament.FormatConfig.TeamCount * 5 : null;
        bool? canStartDraft = tournament.DraftSessionId.HasValue ? (registeredPlayerCount.HasValue && registeredPlayerCount.Value >= (minRequired ?? 0)) : null;
        bool canStartTournament = (tournament.Status == TournamentStatus.Drafting || tournament.Status == TournamentStatus.Draft) && tournament.Teams.Count == tournament.FormatConfig.TeamCount;

        return new TournamentDto(
            tournament.Id,
            tournament.Name,
            tournament.BannerUrl,
            tournament.Status.ToString(),
            tournament.FormatConfig.TeamCount,
            tournament.FormatConfig.GroupCount,
            tournament.FormatConfig.Type.ToString(),
            tournament.FormatConfig.PlayoffFormat.ToString(),
            tournament.SeriesConfig.GroupStageFormat?.ToString(),
            tournament.SeriesConfig.PlayoffFormat.ToString(),
            tournament.SeriesConfig.GrandFinalFormat.ToString(),
            tournament.DraftSessionId,
            tournament.ChampionId,
            tournament.ChampionName,
            tournament.CreatedAtUtc,
            tournament.Teams.Select(t => new TournamentTeamDto(t.Id, t.Name)).ToList(),
            slots.Select(s => new BracketSlotDto(
                s.Code,
                s.Stage.ToString(),
                s.Round,
                s.TeamASource,
                s.TeamBSource,
                s.WinnerTargetSlotCode,
                s.LoserTargetSlotCode,
                s.Stage == BracketStage.GrandFinal
                    ? tournament.SeriesConfig.GrandFinalFormat.ToString()
                    : tournament.SeriesConfig.PlayoffFormat.ToString()
            )).ToList(),
            tournament.Version,
            tournament.LastUpdatedAtUtc,
            registeredPlayerCount,
            draftPlayerPool,
            minRequired,
            canStartDraft,
            canStartTournament,
            tournament.IsResultSubmissionLocked,
            tournament.ResultLockReason,
            tournament.ResultLockedAtUtc
        );
    }
}
