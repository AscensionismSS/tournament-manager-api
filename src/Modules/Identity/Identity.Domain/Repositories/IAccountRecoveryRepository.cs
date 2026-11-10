using Identity.Domain.Entities;

namespace Identity.Domain.Repositories;

public interface IAccountRecoveryRepository
{
    Task<AccountRecoveryToken?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<AccountRecoveryToken?> GetByTokenAsync(string token, CancellationToken ct = default);
    Task<IReadOnlyList<AccountRecoveryToken>> GetAllAsync(CancellationToken ct = default);
    Task AddAsync(AccountRecoveryToken token, CancellationToken ct = default);
    Task UpdateAsync(AccountRecoveryToken token, CancellationToken ct = default);
}
