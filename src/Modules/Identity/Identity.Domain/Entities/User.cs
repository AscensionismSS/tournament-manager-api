using BuildingBlocks.Domain;
using Identity.Domain.Enums;

namespace Identity.Domain.Entities;

public class User : AggregateRoot<Guid>
{
    public string Email { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;
    public string DisplayName { get; private set; } = string.Empty;
    public UserRole Role { get; private set; }
    public ulong? SteamId { get; private set; }
    public bool IsSteamLocked { get; private set; }

    public int? DotaRankTier { get; private set; }
    public int? DotaLeaderboardRank { get; private set; }
    public string? DotaPersonaName { get; private set; }
    public string? DotaAvatarUrl { get; private set; }
    public DateTime? LastDotaSyncAtUtc { get; private set; }

    private readonly List<DotaRole> _roles = [];
    public IReadOnlyCollection<DotaRole> Roles => _roles.AsReadOnly();
    public DotaRole? PreferredRole { get; private set; }

    public bool IsActive { get; private set; } = true;
    public bool IsTournamentEligible { get; private set; } = true;
    public string? StatusReason { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? LastLoginAtUtc { get; private set; }

    public UserPreferences Preferences { get; private set; } = UserPreferences.Default;

    protected User() { }

    public User(
        string email,
        string passwordHash,
        string displayName,
        UserRole role = UserRole.User)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new DomainException("O email do usuário é obrigatório.");

        if (string.IsNullOrWhiteSpace(passwordHash))
            throw new DomainException("O hash de senha é obrigatório.");

        if (string.IsNullOrWhiteSpace(displayName))
            throw new DomainException("O nome de exibição é obrigatório.");

        Id = Guid.NewGuid();
        Email = email.Trim().ToLowerInvariant();
        PasswordHash = passwordHash;
        DisplayName = displayName.Trim();
        Role = role;
        IsSteamLocked = false;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public void RecordLogin()
    {
        LastLoginAtUtc = DateTime.UtcNow;
    }

    public void LinkSteamAccount(
        ulong steamId,
        string personaName,
        string? avatarUrl = null,
        int? rankTier = null,
        int? leaderboardRank = null)
    {
        if (steamId == 0)
            throw new DomainException("SteamID inválido.");

        if (IsSteamLocked)
            throw new DomainException("A conta Steam já está vinculada e bloqueada. Solicite a um administrador para alterá-la.");

        SteamId = steamId;
        IsSteamLocked = true;
        DotaPersonaName = personaName?.Trim();
        DotaAvatarUrl = avatarUrl?.Trim();
        DotaRankTier = rankTier;
        DotaLeaderboardRank = leaderboardRank;
        LastDotaSyncAtUtc = DateTime.UtcNow;
    }

    public void UnlockSteam(User adminUser)
    {
        if (adminUser.Role != UserRole.SuperAdmin)
            throw new DomainException("Apenas o SuperAdmin pode desbloquear a vinculação da Steam.");

        IsSteamLocked = false;
        SteamId = null;
        DotaRankTier = null;
        DotaLeaderboardRank = null;
        DotaPersonaName = null;
        DotaAvatarUrl = null;
        LastDotaSyncAtUtc = null;
    }

    public void UpdateDotaProfile(
        string personaName,
        string? avatarUrl = null,
        int? rankTier = null,
        int? leaderboardRank = null)
    {
        if (!SteamId.HasValue)
            throw new DomainException("Nenhuma conta Steam vinculada para atualizar dados.");

        DotaPersonaName = personaName?.Trim();
        DotaAvatarUrl = avatarUrl?.Trim();
        DotaRankTier = rankTier;
        DotaLeaderboardRank = leaderboardRank;
        LastDotaSyncAtUtc = DateTime.UtcNow;
    }

    public void UpdatePassword(string newPasswordHash)
    {
        if (string.IsNullOrWhiteSpace(newPasswordHash))
            throw new DomainException("O novo hash de senha é obrigatório.");

        PasswordHash = newPasswordHash;
    }

    public void UpdateDisplayName(string newDisplayName)
    {
        if (string.IsNullOrWhiteSpace(newDisplayName))
            throw new DomainException("O nome de exibição é obrigatório.");

        DisplayName = newDisplayName.Trim();
    }

    public void PromoteRole(UserRole newRole, User adminUser)
    {
        if (adminUser.Role != UserRole.SuperAdmin)
            throw new DomainException("Apenas o SuperAdmin pode alterar o nível de acesso de um usuário.");

        Role = newRole;
    }

    public void SetRoles(IEnumerable<DotaRole>? roles, DotaRole? preferredRole)
    {
        _roles.Clear();

        var inputRoles = (roles ?? []).Distinct().ToList();
        if (inputRoles.Count == 0)
        {
            throw new DomainException("Selecione ao menos uma função no Dota 2.");
        }

        // Regra 1: Se o jogador marcou todas as 5 funções ou marcou Flex, ele é Flex
        bool hasAllFive = inputRoles.Contains(DotaRole.Carry)
                       && inputRoles.Contains(DotaRole.Mid)
                       && inputRoles.Contains(DotaRole.Offlane)
                       && inputRoles.Contains(DotaRole.SoftSupport)
                       && inputRoles.Contains(DotaRole.HardSupport);

        if (hasAllFive && !inputRoles.Contains(DotaRole.Flex))
        {
            inputRoles.Add(DotaRole.Flex);
        }

        _roles.AddRange(inputRoles);

        // Regra 2: Validação da PreferredRole
        if (preferredRole.HasValue)
        {
            if (!_roles.Contains(preferredRole.Value) && !_roles.Contains(DotaRole.Flex))
            {
                throw new DomainException($"A função favorita '{preferredRole.Value}' precisa ser uma das funções selecionadas.");
            }
            PreferredRole = preferredRole.Value;
        }
        else
        {
            PreferredRole = _roles.Contains(DotaRole.Flex) ? DotaRole.Flex : _roles.First();
        }
    }

    public void UpdateGeneralPreferences(string preferredLandingPage, User requestingUser)
    {
        if (requestingUser.Role != UserRole.SuperAdmin && requestingUser.Role != UserRole.TournamentAdmin)
            throw new DomainException("Apenas administradores podem configurar preferências gerais.");

        Preferences = new UserPreferences(preferredLandingPage);
    }

    public void SetActive(bool isActive, string? reason, User requestingUser)
    {
        if (requestingUser.Role != UserRole.SuperAdmin && requestingUser.Role != UserRole.TournamentAdmin)
            throw new DomainException("Apenas administradores podem alterar o status de ativação do membro.");

        if (requestingUser.Id == Id && !isActive)
            throw new DomainException("Você não pode desativar sua própria conta de administrador.");

        if (Role == UserRole.SuperAdmin && requestingUser.Role != UserRole.SuperAdmin)
            throw new DomainException("Apenas outro SuperAdmin pode alterar o status de um SuperAdmin.");

        IsActive = isActive;
        StatusReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
    }

    public void SetTournamentEligibility(bool isEligible, string? reason, User requestingUser)
    {
        if (requestingUser.Role != UserRole.SuperAdmin && requestingUser.Role != UserRole.TournamentAdmin)
            throw new DomainException("Apenas administradores podem alterar a permissão de torneios do membro.");

        IsTournamentEligible = isEligible;
        StatusReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
    }
}

