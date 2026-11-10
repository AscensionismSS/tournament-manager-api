using BuildingBlocks.Domain;
using Tournaments.Domain.Enums;

namespace Tournaments.Domain.Entities;

public class MatchGame : Entity<Guid>
{
    public Guid MatchSeriesId { get; private set; }
    public int GameNumber { get; private set; }
    public ResultSource ResultSource { get; private set; }

    // Colunas relacionais normais para ordenação e desempate rápido
    public Guid WinnerTeamId { get; private set; }
    public long DurationSeconds { get; private set; }
    public int TeamAKills { get; private set; }
    public int TeamBKills { get; private set; }
    public bool IsWalkover { get; private set; }

    public long? DotaMatchId { get; private set; }

    public Guid? FirstPickTeamId { get; private set; }
    public List<int> TeamABanHeroIds { get; private set; } = new();
    public List<int> TeamBBanHeroIds { get; private set; } = new();

    public int TeamARoshanKills { get; private set; }
    public int TeamBRoshanKills { get; private set; }
    public int TeamAAegisStolen { get; private set; }
    public int TeamBAegisStolen { get; private set; }

    // Mapeado via .OwnsMany(x => x.PlayerStats, b => b.ToJson()) no EF Core
    private readonly List<PlayerGameStats> _playerStats = [];
    public IReadOnlyCollection<PlayerGameStats> PlayerStats => _playerStats.AsReadOnly();

    protected MatchGame() { }


    public static MatchGame CreateReplay(
        Guid matchSeriesId,
        int gameNumber,
        Guid winnerTeamId,
        long durationSeconds,
        int teamAKills,
        int teamBKills,
        long? dotaMatchId,
        IEnumerable<PlayerGameStats>? playerStats,
        Guid? firstPickTeamId = null,
        IEnumerable<int>? teamABanHeroIds = null,
        IEnumerable<int>? teamBBanHeroIds = null,
        int teamARoshanKills = 0,
        int teamBRoshanKills = 0,
        int teamAAegisStolen = 0,
        int teamBAegisStolen = 0)
    {
        var game = new MatchGame
        {
            Id = Guid.NewGuid(),
            MatchSeriesId = matchSeriesId,
            GameNumber = gameNumber,
            ResultSource = ResultSource.ReplayParser,
            WinnerTeamId = winnerTeamId,
            DurationSeconds = durationSeconds,
            TeamAKills = teamAKills,
            TeamBKills = teamBKills,
            IsWalkover = false,
            DotaMatchId = dotaMatchId,
            FirstPickTeamId = firstPickTeamId,
            TeamARoshanKills = teamARoshanKills,
            TeamBRoshanKills = teamBRoshanKills,
            TeamAAegisStolen = teamAAegisStolen,
            TeamBAegisStolen = teamBAegisStolen
        };

        if (playerStats != null)
        {
            game._playerStats.AddRange(playerStats);
        }

        if (teamABanHeroIds != null)
        {
            game.TeamABanHeroIds.AddRange(teamABanHeroIds);
        }

        if (teamBBanHeroIds != null)
        {
            game.TeamBBanHeroIds.AddRange(teamBBanHeroIds);
        }

        return game;
    }

    public static MatchGame CreateWalkover(Guid matchSeriesId, int gameNumber, Guid winnerTeamId)
    {
        return new MatchGame
        {
            Id = Guid.NewGuid(),
            MatchSeriesId = matchSeriesId,
            GameNumber = gameNumber,
            ResultSource = ResultSource.Walkover,
            WinnerTeamId = winnerTeamId,
            DurationSeconds = 0,
            TeamAKills = 0,
            TeamBKills = 0,
            IsWalkover = true
        };
    }

    public static MatchGame CreateManual(
        Guid matchSeriesId,
        int gameNumber,
        Guid winnerTeamId,
        long durationSeconds,
        int teamAKills,
        int teamBKills,
        Guid? firstPickTeamId = null,
        IEnumerable<int>? teamABanHeroIds = null,
        IEnumerable<int>? teamBBanHeroIds = null,
        long? dotaMatchId = null)
    {
        if (durationSeconds <= 0)
            throw new DomainException("A duração da partida deve ser maior que zero.");

        var game = new MatchGame
        {
            Id = Guid.NewGuid(),
            MatchSeriesId = matchSeriesId,
            GameNumber = gameNumber,
            ResultSource = ResultSource.Manual,
            WinnerTeamId = winnerTeamId,
            DurationSeconds = durationSeconds,
            TeamAKills = teamAKills,
            TeamBKills = teamBKills,
            IsWalkover = false,
            FirstPickTeamId = (firstPickTeamId.HasValue && firstPickTeamId.Value != Guid.Empty) ? firstPickTeamId.Value : null,
            DotaMatchId = dotaMatchId
        };

        if (teamABanHeroIds != null)
        {
            game.TeamABanHeroIds.AddRange(teamABanHeroIds);
        }

        if (teamBBanHeroIds != null)
        {
            game.TeamBBanHeroIds.AddRange(teamBBanHeroIds);
        }

        return game;
    }

    public void UpdateFromReplay(
        Guid winnerTeamId,
        long durationSeconds,
        int teamAKills,
        int teamBKills,
        long? dotaMatchId,
        IEnumerable<PlayerGameStats>? playerStats,
        Guid? firstPickTeamId = null,
        IEnumerable<int>? teamABanHeroIds = null,
        IEnumerable<int>? teamBBanHeroIds = null,
        int teamARoshanKills = 0,
        int teamBRoshanKills = 0,
        int teamAAegisStolen = 0,
        int teamBAegisStolen = 0)
    {
        if (durationSeconds <= 0)
            throw new DomainException("A duração da partida deve ser maior que zero.");

        ResultSource = ResultSource.ReplayParser;
        WinnerTeamId = winnerTeamId;
        DurationSeconds = durationSeconds;
        TeamAKills = teamAKills;
        TeamBKills = teamBKills;
        IsWalkover = false;
        DotaMatchId = dotaMatchId;
        FirstPickTeamId = firstPickTeamId;
        TeamARoshanKills = teamARoshanKills;
        TeamBRoshanKills = teamBRoshanKills;
        TeamAAegisStolen = teamAAegisStolen;
        TeamBAegisStolen = teamBAegisStolen;

        TeamABanHeroIds.Clear();
        if (teamABanHeroIds != null)
        {
            TeamABanHeroIds.AddRange(teamABanHeroIds);
        }

        TeamBBanHeroIds.Clear();
        if (teamBBanHeroIds != null)
        {
            TeamBBanHeroIds.AddRange(teamBBanHeroIds);
        }

        _playerStats.Clear();
        if (playerStats != null)
        {
            _playerStats.AddRange(playerStats);
        }
    }

    public void UpdateManual(
        Guid winnerTeamId,
        long durationSeconds,
        int teamAKills,
        int teamBKills,
        Guid? firstPickTeamId = null,
        IEnumerable<int>? teamABanHeroIds = null,
        IEnumerable<int>? teamBBanHeroIds = null,
        long? dotaMatchId = null)
    {
        if (durationSeconds <= 0)
            throw new DomainException("A duração da partida deve ser maior que zero.");

        ResultSource = ResultSource.Manual;
        WinnerTeamId = winnerTeamId;
        DurationSeconds = durationSeconds;
        TeamAKills = teamAKills;
        TeamBKills = teamBKills;
        IsWalkover = false;
        FirstPickTeamId = (firstPickTeamId.HasValue && firstPickTeamId.Value != Guid.Empty) ? firstPickTeamId.Value : null;
        DotaMatchId = dotaMatchId;

        TeamABanHeroIds.Clear();
        if (teamABanHeroIds != null)
        {
            TeamABanHeroIds.AddRange(teamABanHeroIds);
        }

        TeamBBanHeroIds.Clear();
        if (teamBBanHeroIds != null)
        {
            TeamBBanHeroIds.AddRange(teamBBanHeroIds);
        }

        _playerStats.Clear();
    }

    public void UpdateWalkover(Guid winnerTeamId)
    {
        ResultSource = ResultSource.Walkover;
        WinnerTeamId = winnerTeamId;
        DurationSeconds = 0;
        TeamAKills = 0;
        TeamBKills = 0;
        IsWalkover = true;
        DotaMatchId = null;
        FirstPickTeamId = null;
        TeamABanHeroIds.Clear();
        TeamBBanHeroIds.Clear();
        _playerStats.Clear();
    }
}
