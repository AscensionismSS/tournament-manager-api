using BuildingBlocks.Domain;
using Tournaments.Domain.Enums;

namespace Tournaments.Domain.Entities;

public class MatchSeries : AggregateRoot<Guid>
{
    private readonly List<MatchGame> _games = [];

    public Guid TournamentId { get; private set; }
    public string SlotCode { get; private set; } = string.Empty;
    public int Round { get; private set; } = 1;
    public SeriesFormat Format { get; private set; }
    public MatchSeriesStatus Status { get; private set; }

    public Guid? TeamAId { get; private set; }
    public string? TeamAName { get; private set; }
    public int ScoreTeamA { get; private set; }

    public Guid? TeamBId { get; private set; }
    public string? TeamBName { get; private set; }
    public int ScoreTeamB { get; private set; }

    public Guid? WinnerTeamId { get; private set; }
    public Guid? LoserTeamId { get; private set; }

    public IReadOnlyCollection<MatchGame> Games => _games.AsReadOnly();

    protected MatchSeries() { }

    public MatchSeries(Guid tournamentId, string slotCode, SeriesFormat format, int round = 1)
    {
        Id = Guid.NewGuid();
        TournamentId = tournamentId;
        SlotCode = slotCode;
        Format = format;
        Round = round > 0 ? round : 1;
        Status = MatchSeriesStatus.Pending;
    }

    public void AssignTeams(Guid teamAId, string teamAName, Guid teamBId, string teamBName)
    {
        TeamAId = teamAId;
        TeamAName = teamAName;
        TeamBId = teamBId;
        TeamBName = teamBName;
        Status = MatchSeriesStatus.ReadyToPlay;
    }

    public void AssignTeamA(Guid teamId, string teamName)
    {
        TeamAId = teamId;
        TeamAName = teamName;
        CheckIfReady();
    }

    public void AssignTeamB(Guid teamId, string teamName)
    {
        TeamBId = teamId;
        TeamBName = teamName;
        CheckIfReady();
    }

    public void UpdateTeamName(Guid teamId, string newTeamName)
    {
        if (TeamAId == teamId) TeamAName = newTeamName;
        if (TeamBId == teamId) TeamBName = newTeamName;
    }

    public void ClearTeamA()
    {
        TeamAId = null;
        TeamAName = null;
        if (Status == MatchSeriesStatus.ReadyToPlay)
        {
            Status = MatchSeriesStatus.Pending;
        }
    }

    public void ClearTeamB()
    {
        TeamBId = null;
        TeamBName = null;
        if (Status == MatchSeriesStatus.ReadyToPlay)
        {
            Status = MatchSeriesStatus.Pending;
        }
    }

    private void CheckIfReady()
    {
        if (TeamAId.HasValue && TeamBId.HasValue && Status == MatchSeriesStatus.Pending)
        {
            Status = MatchSeriesStatus.ReadyToPlay;
        }
    }


    public void RecordGameReplay(
        Guid winnerTeamId,
        long durationSeconds,
        int teamAKills,
        int teamBKills,
        long? dotaMatchId = null,
        IEnumerable<PlayerGameStats>? playerStats = null,
        Guid? firstPickTeamId = null,
        IEnumerable<int>? teamABanHeroIds = null,
        IEnumerable<int>? teamBBanHeroIds = null,
        int? gameNumber = null,
        int teamARoshanKills = 0,
        int teamBRoshanKills = 0,
        int teamAAegisStolen = 0,
        int teamBAegisStolen = 0)
    {
        ValidateCanRecordGame(winnerTeamId);

        if (gameNumber.HasValue)
        {
            if (gameNumber.Value <= 0)
                throw new DomainException("O número do jogo deve ser maior que zero.");

            if (gameNumber.Value > _games.Count + 1)
                throw new DomainException($"Não é possível registrar o Jogo #{gameNumber.Value} antes de registrar o Jogo #{gameNumber.Value - 1}.");

            if (gameNumber.Value != _games.Count + 1)
                throw new DomainException($"Jogo número {gameNumber.Value} não encontrado na série.");
        }

        int nextGameNumber = _games.Count + 1;
        var game = MatchGame.CreateReplay(
            Id,
            nextGameNumber,
            winnerTeamId,
            durationSeconds,
            teamAKills,
            teamBKills,
            dotaMatchId,
            playerStats,
            firstPickTeamId,
            teamABanHeroIds,
            teamBBanHeroIds,
            teamARoshanKills,
            teamBRoshanKills,
            teamAAegisStolen,
            teamBAegisStolen);
        _games.Add(game);

        RecalculateScoresAndStatus();
    }

    public void RecordGameManual(
        Guid winnerTeamId,
        long durationSeconds,
        int teamAKills,
        int teamBKills,
        Guid? firstPickTeamId = null,
        IEnumerable<int>? teamABanHeroIds = null,
        IEnumerable<int>? teamBBanHeroIds = null,
        long? dotaMatchId = null,
        int? gameNumber = null)
    {
        if (Status == MatchSeriesStatus.Pending || !TeamAId.HasValue || !TeamBId.HasValue)
            throw new DomainException("Ambas as equipes precisam estar definidas na série antes de registrar um resultado.");

        if (Status != MatchSeriesStatus.ReadyToPlay && Status != MatchSeriesStatus.InProgress)
            throw new DomainException("A série não está disponível para receber novos jogos.");

        if (winnerTeamId != TeamAId && winnerTeamId != TeamBId)
            throw new DomainException("O time vencedor do mapa deve ser uma das equipes da série.");

        if (firstPickTeamId.HasValue && firstPickTeamId.Value != Guid.Empty &&
            firstPickTeamId.Value != TeamAId && firstPickTeamId.Value != TeamBId)
        {
            throw new DomainException("O time do First Pick deve ser uma das equipes da série.");
        }

        if (gameNumber.HasValue)
        {
            if (gameNumber.Value <= 0)
                throw new DomainException("O número do jogo deve ser maior que zero.");

            if (gameNumber.Value > _games.Count + 1)
                throw new DomainException($"Não é possível registrar o Jogo #{gameNumber.Value} antes de registrar o Jogo #{gameNumber.Value - 1}.");

            if (gameNumber.Value != _games.Count + 1)
                throw new DomainException($"Jogo número {gameNumber.Value} não encontrado na série.");
        }

        Status = MatchSeriesStatus.InProgress;

        int nextGameNumber = _games.Count + 1;
        var game = MatchGame.CreateManual(
            Id,
            nextGameNumber,
            winnerTeamId,
            durationSeconds,
            teamAKills,
            teamBKills,
            firstPickTeamId,
            teamABanHeroIds,
            teamBBanHeroIds,
            dotaMatchId);

        _games.Add(game);
        RecalculateScoresAndStatus();
    }

    public void RecordWalkover(Guid winnerTeamId)
    {
        if (Status == MatchSeriesStatus.Pending || !TeamAId.HasValue || !TeamBId.HasValue)
            throw new DomainException("Não é possível registrar resultado em uma partida cujas equipes ainda não foram definidas pelos confrontos anteriores.");

        if (Status != MatchSeriesStatus.ReadyToPlay && Status != MatchSeriesStatus.InProgress)
            throw new DomainException("Não é possível aplicar W.O. em série que não está pronta para jogar.");

        if (winnerTeamId != TeamAId && winnerTeamId != TeamBId)
            throw new DomainException("O time vencedor do W.O. deve ser um dos participantes da série.");

        int winsNeeded = GetWinsNeeded();
        while ((winnerTeamId == TeamAId ? ScoreTeamA : ScoreTeamB) < winsNeeded)
        {
            int nextGameNumber = _games.Count + 1;
            _games.Add(MatchGame.CreateWalkover(Id, nextGameNumber, winnerTeamId));
            if (winnerTeamId == TeamAId) ScoreTeamA++; else ScoreTeamB++;
        }

        FinalizeSeries(winnerTeamId);
    }

    public void RollbackResult()
    {
        if (Status != MatchSeriesStatus.Completed && Status != MatchSeriesStatus.InProgress)
            throw new DomainException("Apenas séries concluídas ou em andamento podem ser revertidas (Rollback).");

        _games.Clear();
        ScoreTeamA = 0;
        ScoreTeamB = 0;
        WinnerTeamId = null;
        LoserTeamId = null;
        Status = MatchSeriesStatus.ReadyToPlay;
    }

    public void RemoveLastGame()
    {
        if (_games.Count == 0)
            throw new DomainException("A série não possui jogos para remover.");

        _games.RemoveAt(_games.Count - 1);

        RecalculateScoresAndStatus();
    }

    public void UpdateGameFromReplay(
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
        if (gameNumber <= 0)
            throw new DomainException("O número do jogo deve ser maior que zero.");

        if (Status == MatchSeriesStatus.Pending || !TeamAId.HasValue || !TeamBId.HasValue)
            throw new DomainException("Não é possível registrar resultado em uma partida cujas equipes ainda não foram definidas pelos confrontos anteriores.");

        if (winnerTeamId != TeamAId && winnerTeamId != TeamBId)
            throw new DomainException("O time vencedor do mapa deve ser uma das equipes da série.");

        var game = _games.FirstOrDefault(g => g.GameNumber == gameNumber);
        if (game == null)
        {
            if (gameNumber > _games.Count + 1)
                throw new DomainException($"Não é possível registrar o Jogo #{gameNumber} antes de registrar o Jogo #{gameNumber - 1}.");

            throw new DomainException($"Jogo número {gameNumber} não encontrado na série.");
        }

        game.UpdateFromReplay(
            winnerTeamId,
            durationSeconds,
            teamAKills,
            teamBKills,
            dotaMatchId,
            playerStats,
            firstPickTeamId,
            teamABanHeroIds,
            teamBBanHeroIds,
            teamARoshanKills,
            teamBRoshanKills,
            teamAAegisStolen,
            teamBAegisStolen);

        RecalculateScoresAndStatus();
    }

    public void UpdateGame(
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
        if (gameNumber <= 0)
            throw new DomainException("O número do jogo deve ser maior que zero.");

        if (Status == MatchSeriesStatus.Pending || !TeamAId.HasValue || !TeamBId.HasValue)
            throw new DomainException("Ambas as equipes precisam estar definidas na série antes de registrar um resultado.");

        if (winnerTeamId != TeamAId && winnerTeamId != TeamBId)
            throw new DomainException("O time vencedor do mapa deve ser uma das equipes da série.");

        if (firstPickTeamId.HasValue && firstPickTeamId.Value != Guid.Empty &&
            firstPickTeamId.Value != TeamAId && firstPickTeamId.Value != TeamBId)
        {
            throw new DomainException("O time do First Pick deve ser uma das equipes da série.");
        }

        var game = _games.FirstOrDefault(g => g.GameNumber == gameNumber);
        if (game == null)
        {
            if (gameNumber > _games.Count + 1)
                throw new DomainException($"Não é possível registrar o Jogo #{gameNumber} antes de registrar o Jogo #{gameNumber - 1}.");

            throw new DomainException($"Jogo número {gameNumber} não encontrado na série.");
        }

        game.UpdateManual(
            winnerTeamId,
            durationSeconds,
            teamAKills,
            teamBKills,
            firstPickTeamId,
            teamABanHeroIds,
            teamBBanHeroIds,
            dotaMatchId);

        RecalculateScoresAndStatus();
    }

    public void UpdateGameManual(
        int gameNumber,
        Guid winnerTeamId,
        long durationSeconds,
        int teamAKills,
        int teamBKills,
        Guid? firstPickTeamId = null,
        IEnumerable<int>? teamABanHeroIds = null,
        IEnumerable<int>? teamBBanHeroIds = null,
        long? dotaMatchId = null)
        => UpdateGame(gameNumber, winnerTeamId, durationSeconds, teamAKills, teamBKills, firstPickTeamId, teamABanHeroIds, teamBBanHeroIds, dotaMatchId);

    public void UpdateGameWalkover(int gameNumber, Guid winnerTeamId)
    {
        if (gameNumber <= 0)
            throw new DomainException("O número do jogo deve ser maior que zero.");

        if (Status == MatchSeriesStatus.Pending || !TeamAId.HasValue || !TeamBId.HasValue)
            throw new DomainException("Não é possível registrar resultado em uma partida cujas equipes ainda não foram definidas pelos confrontos anteriores.");

        if (winnerTeamId != TeamAId && winnerTeamId != TeamBId)
            throw new DomainException("O time vencedor do W.O. deve ser uma das equipes da série.");

        var game = _games.FirstOrDefault(g => g.GameNumber == gameNumber);
        if (game != null)
        {
            game.UpdateWalkover(winnerTeamId);
        }
        else if (gameNumber == _games.Count + 1)
        {
            if (Status != MatchSeriesStatus.ReadyToPlay && Status != MatchSeriesStatus.InProgress)
                throw new DomainException("A série não está disponível para receber novos jogos.");

            _games.Add(MatchGame.CreateWalkover(Id, gameNumber, winnerTeamId));
        }
        else if (gameNumber > _games.Count + 1)
        {
            throw new DomainException($"Não é possível registrar o Jogo #{gameNumber} antes de registrar o Jogo #{gameNumber - 1}.");
        }
        else
        {
            throw new DomainException($"Jogo número {gameNumber} não encontrado na série.");
        }

        RecalculateScoresAndStatus();
    }

    private void RecalculateScoresAndStatus()
    {
        ScoreTeamA = TeamAId.HasValue ? _games.Count(g => g.WinnerTeamId == TeamAId.Value) : 0;
        ScoreTeamB = TeamBId.HasValue ? _games.Count(g => g.WinnerTeamId == TeamBId.Value) : 0;

        if (Format == SeriesFormat.Bo2)
        {
            if (_games.Count >= 2)
            {
                if (ScoreTeamA > ScoreTeamB) FinalizeSeries(TeamAId!.Value);
                else if (ScoreTeamB > ScoreTeamA) FinalizeSeries(TeamBId!.Value);
                else
                {
                    // Empate 1x1 em MD2 mantém série concluída
                    WinnerTeamId = null;
                    LoserTeamId = null;
                    Status = MatchSeriesStatus.Completed;
                }
            }
            else
            {
                WinnerTeamId = null;
                LoserTeamId = null;
                Status = _games.Count > 0 ? MatchSeriesStatus.InProgress : MatchSeriesStatus.ReadyToPlay;
            }
        }
        else
        {
            int winsNeeded = GetWinsNeeded();
            if (ScoreTeamA >= winsNeeded)
            {
                FinalizeSeries(TeamAId!.Value);
            }
            else if (ScoreTeamB >= winsNeeded)
            {
                FinalizeSeries(TeamBId!.Value);
            }
            else
            {
                WinnerTeamId = null;
                LoserTeamId = null;
                Status = _games.Count > 0 ? MatchSeriesStatus.InProgress : MatchSeriesStatus.ReadyToPlay;
            }
        }
    }

    private void ValidateCanRecordGame(Guid winnerTeamId)
    {
        if (Status == MatchSeriesStatus.Pending || !TeamAId.HasValue || !TeamBId.HasValue)
            throw new DomainException("Não é possível registrar resultado em uma partida cujas equipes ainda não foram definidas pelos confrontos anteriores.");

        if (Status != MatchSeriesStatus.ReadyToPlay && Status != MatchSeriesStatus.InProgress)
            throw new DomainException("A série não está disponível para receber novos jogos.");

        if (winnerTeamId != TeamAId && winnerTeamId != TeamBId)
            throw new DomainException("O time vencedor do mapa deve ser uma das equipes da série.");

        Status = MatchSeriesStatus.InProgress;
    }

    private void FinalizeSeries(Guid winnerId)
    {
        WinnerTeamId = winnerId;
        LoserTeamId = winnerId == TeamAId ? TeamBId : TeamAId;
        Status = MatchSeriesStatus.Completed;
    }

    private int GetWinsNeeded() => Format switch
    {
        SeriesFormat.Bo1 => 1,
        SeriesFormat.Bo2 => 2,
        SeriesFormat.Bo3 => 2,
        SeriesFormat.Bo5 => 3,
        _ => 1
    };
}
