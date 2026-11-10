using BuildingBlocks.Domain;

namespace Tournaments.Domain.Entities;

public class SystemSecuritySettings : AggregateRoot<Guid>
{
    public static readonly Guid DefaultSettingsId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    public bool LockResultSubmission { get; private set; }
    public bool LockTournamentRegistration { get; private set; }
    public bool LockTournamentCreation { get; private set; }
    public bool LockDraftPicks { get; private set; }
    public bool MaintenanceMode { get; private set; }
    public string? Message { get; private set; }
    public DateTime? UpdatedAtUtc { get; private set; }
    public string? UpdatedBy { get; private set; }

    // Required by EF Core
    private SystemSecuritySettings()
    {
        Id = DefaultSettingsId;
    }

    public SystemSecuritySettings(
        bool lockResultSubmission,
        bool lockTournamentRegistration,
        bool lockTournamentCreation,
        bool lockDraftPicks,
        bool maintenanceMode,
        string? message,
        string? updatedBy,
        DateTime? updatedAtUtc = null)
    {
        Id = DefaultSettingsId;
        Update(lockResultSubmission, lockTournamentRegistration, lockTournamentCreation, lockDraftPicks, maintenanceMode, message, updatedBy, updatedAtUtc);
    }

    public static SystemSecuritySettings CreateDefault()
    {
        return new SystemSecuritySettings(
            lockResultSubmission: false,
            lockTournamentRegistration: false,
            lockTournamentCreation: false,
            lockDraftPicks: false,
            maintenanceMode: false,
            message: null,
            updatedBy: "System",
            updatedAtUtc: DateTime.UtcNow);
    }

    public void Update(
        bool lockResultSubmission,
        bool lockTournamentRegistration,
        bool lockTournamentCreation,
        bool lockDraftPicks,
        bool maintenanceMode,
        string? message,
        string? updatedBy,
        DateTime? updatedAtUtc = null)
    {
        LockResultSubmission = lockResultSubmission;
        LockTournamentRegistration = lockTournamentRegistration;
        LockTournamentCreation = lockTournamentCreation;
        LockDraftPicks = lockDraftPicks;
        MaintenanceMode = maintenanceMode;
        Message = string.IsNullOrWhiteSpace(message) ? null : message.Trim();
        var normalizedUpdatedBy = string.IsNullOrWhiteSpace(updatedBy) ? "SuperAdmin" : updatedBy.Trim();
        UpdatedBy = normalizedUpdatedBy.Length > 150 ? normalizedUpdatedBy.Substring(0, 150) : normalizedUpdatedBy;
        UpdatedAtUtc = updatedAtUtc ?? DateTime.UtcNow;
    }

    public bool IsResultSubmissionLocked => MaintenanceMode || LockResultSubmission;
    public bool IsTournamentRegistrationLocked => MaintenanceMode || LockTournamentRegistration;
    public bool IsTournamentCreationLocked => MaintenanceMode || LockTournamentCreation;
    public bool IsDraftPicksLocked => MaintenanceMode || LockDraftPicks;

    public void EnsureResultSubmissionAllowed()
    {
        if (IsResultSubmissionLocked)
        {
            var detail = !string.IsNullOrWhiteSpace(Message) ? $" Motivo: {Message}" : string.Empty;
            throw new DomainException($"O envio e alteração de resultados de partidas está temporariamente bloqueado pela administração do sistema.{detail}");
        }
    }

    public void EnsureTournamentRegistrationAllowed()
    {
        if (IsTournamentRegistrationLocked)
        {
            var detail = !string.IsNullOrWhiteSpace(Message) ? $" Motivo: {Message}" : string.Empty;
            throw new DomainException($"A inscrição de jogadores em torneios está temporariamente bloqueada pela administração do sistema.{detail}");
        }
    }

    public void EnsureTournamentCreationAllowed()
    {
        if (IsTournamentCreationLocked)
        {
            var detail = !string.IsNullOrWhiteSpace(Message) ? $" Motivo: {Message}" : string.Empty;
            throw new DomainException($"A criação de novos torneios está temporariamente bloqueada pela administração do sistema.{detail}");
        }
    }

    public void EnsureDraftPicksAllowed()
    {
        if (IsDraftPicksLocked)
        {
            var detail = !string.IsNullOrWhiteSpace(Message) ? $" Motivo: {Message}" : string.Empty;
            throw new DomainException($"A seleção de jogadores e operações de draft estão temporariamente bloqueadas pela administração do sistema.{detail}");
        }
    }
}
