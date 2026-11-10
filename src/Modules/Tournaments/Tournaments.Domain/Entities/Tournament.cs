using BuildingBlocks.Domain;
using Tournaments.Domain.Enums;
using Tournaments.Domain.Services;
using Tournaments.Domain.ValueObjects;

namespace Tournaments.Domain.Entities;
public class Tournament : AggregateRoot<Guid>

{
    private readonly List<TournamentTeam> _teams = [];
    private readonly List<BracketSlot> _bracketSlots = [];

    public string Name { get; private set; } = string.Empty;
    public TournamentStatus Status { get; private set; }
    public TournamentFormatConfig FormatConfig { get; private set; } = default!;
    public SeriesConfig SeriesConfig { get; private set; } = default!;
    public Guid? DraftSessionId { get; private set; }

    public string? BannerUrl { get; private set; }
    public Guid? ChampionId { get; private set; }
    public string? ChampionName { get; private set; }
    public bool IsResultSubmissionLocked { get; private set; }
    public string? ResultLockReason { get; private set; }
    public DateTime? ResultLockedAtUtc { get; private set; }
    public string? ResultLockedBy { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime LastUpdatedAtUtc { get; private set; }
    public int Version { get; private set; }

    public IReadOnlyCollection<TournamentTeam> Teams => _teams.AsReadOnly();
    public IReadOnlyCollection<BracketSlot> BracketSlots => _bracketSlots.AsReadOnly();

    protected Tournament() { }

    public Tournament(
        string name,
        TournamentFormatConfig formatConfig,
        SeriesConfig seriesConfig,
        Guid? draftSessionId = null,
        string? bannerUrl = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("O nome do torneio é obrigatório.");

        Id = Guid.NewGuid();
        Name = name.Trim();
        FormatConfig = formatConfig;
        SeriesConfig = seriesConfig;
        DraftSessionId = draftSessionId;
        BannerUrl = bannerUrl?.Trim();
        Status = TournamentStatus.Draft;
        CreatedAtUtc = DateTime.UtcNow;
        LastUpdatedAtUtc = CreatedAtUtc;
        Version = 1;

        _bracketSlots.AddRange(BuildBracketSlots(formatConfig));
    }

    public void SetBannerUrl(string? bannerUrl)
    {
        BannerUrl = bannerUrl?.Trim();
        Touch();
    }

    public void AssignDraftSession(Guid draftSessionId)
    {
        if (draftSessionId == Guid.Empty)
            throw new DomainException("O identificador da sessão de draft é inválido.");

        DraftSessionId = draftSessionId;
        Touch();
    }

    public void Touch()
    {
        Version++;
        LastUpdatedAtUtc = DateTime.UtcNow;
    }

    public void OpenRegistrations()
    {
        if (Status != TournamentStatus.Draft && Status != TournamentStatus.Drafting)
            throw new DomainException("As inscrições só podem ser abertas na fase inicial ou durante o draft preliminar.");

        Status = TournamentStatus.RegistrationOpen;
        Touch();
    }

    public void CloseRegistrations()
    {
        if (Status != TournamentStatus.RegistrationOpen && Status != TournamentStatus.Draft)
            throw new DomainException("O torneio não está com inscrições abertas.");

        Status = TournamentStatus.Drafting;
        Touch();
    }

    public void AddTeam(Guid teamId, string name)
    {
        if (Status != TournamentStatus.Draft && Status != TournamentStatus.RegistrationOpen && Status != TournamentStatus.Drafting)
            throw new DomainException("Não é possível adicionar equipes neste status.");

        if (_teams.Count >= FormatConfig.TeamCount)
            throw new DomainException($"O torneio já atingiu o limite de {FormatConfig.TeamCount} equipes.");

        if (_teams.Any(t => t.Id == teamId || t.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
            throw new DomainException($"Equipe '{name}' já inscrita no torneio.");

        _teams.Add(new TournamentTeam(teamId, name.Trim()));
        Touch();
    }

    public void RenameTeam(Guid teamId, string newName)
    {
        if (string.IsNullOrWhiteSpace(newName))
            throw new DomainException("O nome da equipe não pode ser vazio.");

        int index = _teams.FindIndex(t => t.Id == teamId);
        if (index == -1)
            throw new DomainException("Equipe não encontrada no torneio.");

        if (_teams.Any(t => t.Id != teamId && t.Name.Equals(newName.Trim(), StringComparison.OrdinalIgnoreCase)))
            throw new DomainException($"Já existe uma equipe com o nome '{newName.Trim()}' no torneio.");

        _teams[index] = new TournamentTeam(teamId, newName.Trim());
        Touch();
    }

    public void Start()
    {
        if (Status == TournamentStatus.GroupStage || Status == TournamentStatus.Playoffs)
            throw new DomainException("O campeonato já foi iniciado.");

        if (Status == TournamentStatus.Completed)
            throw new DomainException("O campeonato já foi finalizado.");

        if (Status == TournamentStatus.Canceled)
            throw new DomainException("Não é possível iniciar um campeonato cancelado.");

        if (_teams.Count != FormatConfig.TeamCount)
            throw new DomainException($"O torneio precisa ter exatamente {FormatConfig.TeamCount} equipes para iniciar (atual: {_teams.Count}).");

        if (FormatConfig.Type == TournamentType.DirectPlayoffs)
        {
            // Gera chaves direto
            GeneratePlayoffs();
            Status = TournamentStatus.Playoffs;
        }
        else
        {
            // Inicia fase de grupos
            Status = TournamentStatus.GroupStage;
        }
        Touch();
    }

    public void Cancel()
    {
        if (Status == TournamentStatus.Completed)
            throw new DomainException("Não é possível cancelar um campeonato já concluído.");

        Status = TournamentStatus.Canceled;
        Touch();
    }

    public void GeneratePlayoffs()
    {
        _bracketSlots.Clear();
        _bracketSlots.AddRange(BuildBracketSlots(FormatConfig));

        Status = TournamentStatus.Playoffs;
        Touch();
    }

    public IReadOnlyList<BracketSlot> GetOrBuildBracketSlots()
    {
        if (_bracketSlots.Count > 0)
            return _bracketSlots;

        return BuildBracketSlots(FormatConfig);
    }

    public static IReadOnlyList<BracketSlot> BuildBracketSlots(TournamentFormatConfig formatConfig)
    {
        if (formatConfig.Type == TournamentType.GroupStageDirectFinal)
        {
            // Final Direta: apenas a Grande Final entre 1º e 2º colocados
            return [new BracketSlot("GF", BracketStage.GrandFinal, 1, "1st", "2nd", null, null)];
        }

        if (formatConfig.PlayoffFormat == PlayoffFormat.SingleElimination)
        {
            return SingleEliminationBracketBuilder.BuildSlots(formatConfig.TeamCount);
        }

        return DoubleEliminationBracketBuilder.BuildSlots(formatConfig.TeamCount);
    }

    public void CrownChampion(Guid teamId, string teamName)
    {
        if (Status != TournamentStatus.Playoffs)
            throw new DomainException("O torneio precisa estar na fase de Playoffs para coroar o campeão.");

        ChampionId = teamId;
        ChampionName = teamName;
        Status = TournamentStatus.Completed;
        Touch();
    }

    public void RevertToGroupStage()
    {
        if (FormatConfig.Type == TournamentType.DirectPlayoffs)
            throw new DomainException("Torneios sem fase de grupos não podem ser revertidos para fase de grupos.");

        if (Status != TournamentStatus.Playoffs)
            throw new DomainException("Apenas torneios na fase de Playoffs podem ser revertidos para Fase de Grupos.");

        _bracketSlots.Clear();
        _bracketSlots.AddRange(BuildBracketSlots(FormatConfig));
        Status = TournamentStatus.GroupStage;
        Touch();
    }

    public void RevertChampion()
    {
        if (Status != TournamentStatus.Completed)
            throw new DomainException("Apenas torneios concluídos podem ter o campeão revertido.");

        ChampionId = null;
        ChampionName = null;
        Status = TournamentStatus.Playoffs;
        Touch();
    }

    public void LockResultSubmission(string? reason, string adminId)
    {
        if (string.IsNullOrWhiteSpace(adminId))
            throw new DomainException("Identificador do administrador é obrigatório.");

        if (IsResultSubmissionLocked)
            throw new DomainException("O envio de resultados já está bloqueado para este campeonato.");

        var trimmedReason = string.IsNullOrWhiteSpace(reason)
            ? "Envio de resultados bloqueado pela administração."
            : reason.Trim();

        if (trimmedReason.Length > 500)
        {
            trimmedReason = trimmedReason[..500];
        }

        IsResultSubmissionLocked = true;
        ResultLockReason = trimmedReason;
        ResultLockedAtUtc = DateTime.UtcNow;
        ResultLockedBy = adminId.Trim();
        Touch();
    }

    public void UnlockResultSubmission(string adminId)
    {
        if (string.IsNullOrWhiteSpace(adminId))
            throw new DomainException("Identificador do administrador é obrigatório.");

        if (!IsResultSubmissionLocked)
            throw new DomainException("O envio de resultados não está bloqueado para este campeonato.");

        IsResultSubmissionLocked = false;
        ResultLockReason = null;
        ResultLockedAtUtc = null;
        ResultLockedBy = null;
        Touch();
    }
}
