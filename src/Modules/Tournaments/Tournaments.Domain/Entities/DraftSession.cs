using BuildingBlocks.Domain;
using Tournaments.Domain.Enums;
using Tournaments.Domain.Services;

namespace Tournaments.Domain.Entities;

public class DraftSession : AggregateRoot<Guid>
{
    private readonly List<DraftPlayer> _playerPool = [];
    private readonly List<DraftTeam> _teams = [];

    public Guid TournamentId { get; private set; }
    public DraftState State { get; private set; }
    public int TargetTeamCount { get; private set; }
    public int CurrentPickIndex { get; private set; }
    public int DraftVersion { get; private set; } // Concurrency Token

    // Estado temporário de Substituição
    public Guid? SubstitutingPlayerId { get; private set; }
    public Guid? SubstitutingTeamId { get; private set; }
    public DraftState? PreviousState { get; private set; }

    public IReadOnlyCollection<DraftPlayer> PlayerPool => _playerPool.AsReadOnly();
    public IReadOnlyCollection<DraftTeam> Teams => _teams.AsReadOnly();

    protected DraftSession() { }

    public DraftSession(Guid tournamentId, int targetTeamCount)
    {
        if (targetTeamCount < 4 || targetTeamCount > 8)
            throw new DomainException("O modo Draft exige entre 4 e 8 equipes.");

        Id = Guid.NewGuid();
        TournamentId = tournamentId;
        TargetTeamCount = targetTeamCount;
        State = DraftState.SelectingCaptains;
        CurrentPickIndex = 0;
        DraftVersion = 1;

        for (int i = 1; i <= targetTeamCount; i++)
        {
            _teams.Add(new DraftTeam($"Time {i}", pickOrder: i - 1));
        }
    }

    public void AddPlayerToPool(
        Guid playerId,
        string name,
        string? externalUserId = null,
        IReadOnlyList<string>? roles = null,
        string? preferredRole = null,
        int? rankTier = null,
        string? avatarUrl = null)
    {
        if (State == DraftState.Completed)
            throw new DomainException("Não é possível adicionar jogadores a um draft já concluído.");

        EnsurePlayerNotAlreadyRegistered(playerId, name);
        _playerPool.Add(new DraftPlayer(playerId, name.Trim(), externalUserId, roles, preferredRole, rankTier, avatarUrl));
        DraftVersion++;
    }

    public void AddLatePlayerToPool(
        Guid playerId,
        string name,
        string? externalUserId = null,
        IReadOnlyList<string>? roles = null,
        string? preferredRole = null,
        int? rankTier = null,
        string? avatarUrl = null,
        int? expectedVersion = null)
    {
        if (expectedVersion.HasValue && State != DraftState.Completed)
        {
            ValidateVersion(expectedVersion.Value);
        }

        EnsurePlayerNotAlreadyRegistered(playerId, name);
        _playerPool.Add(new DraftPlayer(playerId, name.Trim(), externalUserId, roles, preferredRole, rankTier, avatarUrl));
        DraftVersion++;
    }

    public void SelectCaptain(Guid playerId, int expectedVersion, Guid? teamId = null)
    {
        ValidateVersion(expectedVersion);

        if (State != DraftState.SelectingCaptains)
            throw new DomainException("Não está na fase de seleção de capitães.");

        var player = _playerPool.FirstOrDefault(p => p.Id == playerId)
            ?? throw new DomainException("Jogador não encontrado na pool.");

        DraftTeam team;
        if (teamId.HasValue)
        {
            team = _teams.FirstOrDefault(t => t.Id == teamId.Value)
                ?? throw new DomainException("Equipe não encontrada.");

            if (team.CaptainId.HasValue)
                throw new DomainException($"A equipe '{team.Name}' já possui um capitão atribuído.");
        }
        else
        {
            team = _teams.FirstOrDefault(t => !t.CaptainId.HasValue)
                ?? throw new DomainException($"Já foram selecionados todos os {TargetTeamCount} capitães.");
        }

        _playerPool.Remove(player);
        team.AssignCaptain(player);

        DraftVersion++;
    }

    public void AdvanceToCaptainOrderSelection(int expectedVersion)
    {
        ValidateVersion(expectedVersion);

        if (State != DraftState.SelectingCaptains)
            throw new DomainException("O draft não está na fase de seleção de capitães.");

        if (_teams.Any(t => !t.CaptainId.HasValue))
            throw new DomainException($"Todas as {TargetTeamCount} equipes precisam ter um capitão atribuído antes de avançar.");

        State = DraftState.CaptainOrderSelection;
        DraftVersion++;
    }

    public void RemovePlayerFromPool(Guid playerId, string actingUserId, bool isAdmin, int? expectedVersion = null)
    {
        if (expectedVersion.HasValue)
            ValidateVersion(expectedVersion.Value);

        if (State != DraftState.SelectingCaptains)
            throw new DomainException("Não é permitido cancelar a inscrição após o avanço do draft.");

        if (_teams.Any(t => t.CaptainId == playerId || t.Starters.Any(s => s.Id == playerId) || t.Reserves.Any(r => r.Id == playerId)))
            throw new DomainException("Não é possível cancelar a inscrição de um jogador já alocado em uma equipe.");

        var player = _playerPool.FirstOrDefault(p => p.Id == playerId)
            ?? throw new DomainException("Jogador não encontrado na pool de inscrições.");

        bool isSelf = !string.IsNullOrWhiteSpace(player.ExternalUserId)
            && !string.IsNullOrWhiteSpace(actingUserId)
            && string.Equals(player.ExternalUserId.Trim(), actingUserId.Trim(), StringComparison.OrdinalIgnoreCase);

        if (!isAdmin && !isSelf)
            throw new DomainException("Você não possui permissão para remover este jogador.");

        _playerPool.Remove(player);
        DraftVersion++;
    }

    public void RemoveCaptain(Guid teamId, int expectedVersion)
    {
        ValidateVersion(expectedVersion);

        if (State != DraftState.SelectingCaptains && State != DraftState.CaptainOrderSelection)
            throw new DomainException("Capitães só podem ser removidos antes do início da escolha de titulares.");

        var team = _teams.FirstOrDefault(t => t.Id == teamId)
            ?? throw new DomainException("Equipe não encontrada.");

        var captain = team.RemoveCaptain();
        _playerPool.Add(captain);

        State = DraftState.SelectingCaptains;
        DraftVersion++;
    }

    public void FinalizeCaptainOrder(PickOrderType orderType, IReadOnlyList<Guid>? manualOrderIds, int expectedVersion)
    {
        ValidateVersion(expectedVersion);

        if (State != DraftState.CaptainOrderSelection && (State != DraftState.SelectingCaptains || _teams.Any(t => !t.CaptainId.HasValue)))
            throw new DomainException("O draft não está na fase de definição de ordem de turnos.");

        SnakeDraftCalculator.ApplyPickOrder(_teams, orderType, manualOrderIds);

        State = DraftState.DraftingStarters;
        DraftVersion++;
    }

    public void PickStarterPlayer(Guid playerId, int expectedVersion)
    {
        ValidateVersion(expectedVersion);

        if (State != DraftState.DraftingStarters)
            throw new DomainException("Não está na fase de escolha de titulares.");

        if (SnakeDraftTurnCalculator.IsStarterPhaseComplete(CurrentPickIndex, TargetTeamCount))
            throw new DomainException("Todos os picks de titulares já foram concluídos.");

        var player = _playerPool.FirstOrDefault(p => p.Id == playerId)
            ?? throw new DomainException("Jogador não disponível na pool.");

        int teamOrder = SnakeDraftTurnCalculator.GetTeamIndexForPick(CurrentPickIndex, TargetTeamCount);
        var team = _teams.First(t => t.PickOrder == teamOrder);

        _playerPool.Remove(player);
        team.AddStarter(player);
        CurrentPickIndex++;

        DraftVersion++;
    }

    public void AdvanceToSubstitutes(int expectedVersion)
    {
        ValidateVersion(expectedVersion);

        int totalStarterPicks = SnakeDraftTurnCalculator.GetTotalStarterPicks(TargetTeamCount);
        if (CurrentPickIndex < totalStarterPicks)
            throw new DomainException($"Todos os {totalStarterPicks} picks de titulares precisam ser concluídos antes de avançar para a fase de reservas.");

        if (State != DraftState.DraftingStarters)
            throw new DomainException("O draft não está na fase de escolha de titulares.");

        State = DraftState.DraftingSubstitutes;
        DraftVersion++;
    }

    public void StartSubstitution(Guid playerId, Guid teamId, int expectedVersion)
    {
        if (State != DraftState.Completed)
        {
            ValidateVersion(expectedVersion);
        }

        if (State != DraftState.DraftingStarters && State != DraftState.DraftingSubstitutes && State != DraftState.Completed)
            throw new DomainException("Substituição só pode ser acionada durante o Snake Draft ou com o torneio em andamento.");

        var team = _teams.FirstOrDefault(t => t.Id == teamId)
            ?? throw new DomainException("Equipe não encontrada.");

        if (!team.Starters.Any(p => p.Id == playerId))
            throw new DomainException("O jogador não pertence aos titulares desta equipe.");

        PreviousState = State;
        State = DraftState.Substituting;

        SubstitutingPlayerId = playerId;
        SubstitutingTeamId = teamId;
        DraftVersion++;
    }

    public void ConfirmSubstitution(Guid newPlayerId, int expectedVersion)
    {
        if (PreviousState != DraftState.Completed)
        {
            ValidateVersion(expectedVersion);
        }

        if (State != DraftState.Substituting || !SubstitutingPlayerId.HasValue || !SubstitutingTeamId.HasValue)
            throw new DomainException("Nenhuma substituição ativa no momento.");

        var newPlayer = _playerPool.FirstOrDefault(p => p.Id == newPlayerId)
            ?? throw new DomainException("Novo jogador não disponível na pool.");

        var team = _teams.First(t => t.Id == SubstitutingTeamId.Value);
        var oldPlayer = team.Starters.First(p => p.Id == SubstitutingPlayerId.Value);

        _playerPool.Remove(newPlayer);
        team.ReplaceStarter(oldPlayer.Id, newPlayer);
        _playerPool.Add(oldPlayer);

        State = PreviousState ?? DraftState.DraftingStarters;
        PreviousState = null;
        SubstitutingPlayerId = null;
        SubstitutingTeamId = null;
        DraftVersion++;
    }

    public void CancelSubstitution(int expectedVersion)
    {
        if (PreviousState != DraftState.Completed)
        {
            ValidateVersion(expectedVersion);
        }

        if (State != DraftState.Substituting)
            throw new DomainException("Nenhuma substituição em andamento para cancelar.");

        State = PreviousState ?? DraftState.DraftingStarters;
        PreviousState = null;
        SubstitutingPlayerId = null;
        SubstitutingTeamId = null;
        DraftVersion++;
    }

    public void AllocateReserve(Guid teamId, Guid playerId, int expectedVersion)
    {
        if (State != DraftState.Completed)
        {
            ValidateVersion(expectedVersion);
        }

        if (!SnakeDraftTurnCalculator.IsStarterPhaseComplete(CurrentPickIndex, TargetTeamCount))
            throw new DomainException("Fase de reservas ainda não está aberta. Todos os titulares devem ser escolhidos antes.");

        if (State == DraftState.DraftingStarters)
        {
            State = DraftState.DraftingSubstitutes;
        }
        else if (State != DraftState.DraftingSubstitutes && State != DraftState.Completed)
        {
            throw new DomainException("Fase de reservas não está disponível neste status.");
        }

        var team = _teams.FirstOrDefault(t => t.Id == teamId)
            ?? throw new DomainException("Equipe não encontrada.");

        var player = _playerPool.FirstOrDefault(p => p.Id == playerId)
            ?? throw new DomainException("Jogador não encontrado na pool.");

        _playerPool.Remove(player);
        team.AddReserve(player);
        DraftVersion++;
    }

    public void RemoveReserve(Guid teamId, Guid playerId, int expectedVersion)
    {
        if (State != DraftState.Completed)
        {
            ValidateVersion(expectedVersion);
        }

        if (State != DraftState.DraftingSubstitutes && State != DraftState.Completed)
            throw new DomainException("Remoção de reservas só é permitida na fase de reservas ou com o draft concluído.");

        var team = _teams.FirstOrDefault(t => t.Id == teamId)
            ?? throw new DomainException("Equipe não encontrada.");

        var removedPlayer = team.RemoveReserve(playerId);
        _playerPool.Add(removedPlayer);
        DraftVersion++;
    }

    public void CompleteDraft(int expectedVersion)
    {
        ValidateVersion(expectedVersion);

        if (!SnakeDraftTurnCalculator.IsStarterPhaseComplete(CurrentPickIndex, TargetTeamCount))
            throw new DomainException("O draft só pode ser concluído após a formação de todos os titulares.");

        if (State != DraftState.DraftingSubstitutes && State != DraftState.DraftingStarters)
            throw new DomainException("O draft só pode ser concluído na fase de titulares ou de reservas.");

        State = DraftState.Completed;
        DraftVersion++;
    }

    public void RenameTeam(Guid teamId, string? newName, int expectedVersion, string? actingUserId = null, bool isAdmin = true)
    {
        ValidateVersion(expectedVersion);

        var team = _teams.FirstOrDefault(t => t.Id == teamId)
            ?? throw new DomainException("Equipe não encontrada.");

        if (!isAdmin)
            throw new DomainException("Apenas administradores do torneio têm permissão para alterar o nome da equipe.");

        var effectiveName = SnakeDraftCalculator.ResolveEffectiveTeamName(team, newName, _teams);
        team.Rename(effectiveName);
        DraftVersion++;
    }

    private void EnsurePlayerNotAlreadyRegistered(Guid playerId, string name)
    {
        if (_playerPool.Any(p => p.Id == playerId || p.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
            throw new DomainException($"Jogador '{name}' já está na pool.");

        if (_teams.Any(t => t.Starters.Any(p => p.Id == playerId) || t.Reserves.Any(p => p.Id == playerId)))
            throw new DomainException($"Jogador '{name}' já está alocado em uma equipe.");
    }

    private void ValidateVersion(int expectedVersion)
    {
        if (DraftVersion != expectedVersion)
            throw new DomainException($"Conflito de versão no Draft (Esperado: {expectedVersion}, Atual: {DraftVersion}). Operação rejeitada por segurança.");
    }
}
