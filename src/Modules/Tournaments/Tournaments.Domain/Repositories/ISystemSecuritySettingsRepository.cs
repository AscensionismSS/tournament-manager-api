using Tournaments.Domain.Entities;

namespace Tournaments.Domain.Repositories;

public interface ISystemSecuritySettingsRepository
{
    Task<SystemSecuritySettings> GetAsync(CancellationToken cancellationToken = default);
    Task UpdateAsync(SystemSecuritySettings settings, CancellationToken cancellationToken = default);
}

public class DefaultSystemSecuritySettingsRepository : ISystemSecuritySettingsRepository
{
    private SystemSecuritySettings _settings;

    public DefaultSystemSecuritySettingsRepository(SystemSecuritySettings? settings = null)
    {
        _settings = settings ?? SystemSecuritySettings.CreateDefault();
    }

    public Task<SystemSecuritySettings> GetAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(_settings);

    public Task UpdateAsync(SystemSecuritySettings settings, CancellationToken cancellationToken = default)
    {
        _settings = settings;
        return Task.CompletedTask;
    }
}
