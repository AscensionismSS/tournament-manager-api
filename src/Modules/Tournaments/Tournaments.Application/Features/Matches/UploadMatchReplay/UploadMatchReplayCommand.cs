using MediatR;

namespace Tournaments.Application.Features.Matches.UploadMatchReplay;

public record UploadMatchReplayCommand(
    Guid MatchSeriesId,
    Stream ReplayStream,
    string FileName,
    Guid? RequestUserId = null,
    int? GameNumber = null,
    bool IsAdmin = true
) : IRequest<UploadMatchReplayResult>;

public record UploadMatchReplayResult(
    long DotaMatchId,
    Guid WinnerTeamId,
    string WinnerTeamName,
    int GameNumber,
    long DurationSeconds,
    int ScoreTeamA,
    int ScoreTeamB,
    string Status,
    string Message
);
