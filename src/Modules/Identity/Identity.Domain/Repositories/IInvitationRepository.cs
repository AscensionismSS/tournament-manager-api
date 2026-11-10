using Identity.Domain.Entities;

namespace Identity.Domain.Repositories;

public interface IInvitationRepository
{
    Task<Invitation?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Invitation?> GetByCodeAsync(string code, CancellationToken ct = default);
    Task<Invitation?> GetPendingByEmailAsync(string email, CancellationToken ct = default);
    Task AddAsync(Invitation invitation, CancellationToken ct = default);
    Task UpdateAsync(Invitation invitation, CancellationToken ct = default);
    Task<IReadOnlyList<Invitation>> GetPendingAsync(CancellationToken ct = default);
    Task<IReadOnlyList<Invitation>> GetAllAsync(CancellationToken ct = default);
}
