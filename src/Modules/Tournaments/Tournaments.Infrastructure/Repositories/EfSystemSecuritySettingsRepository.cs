using Microsoft.EntityFrameworkCore;
using Tournaments.Domain.Entities;
using Tournaments.Domain.Repositories;
using Tournaments.Infrastructure.Persistence;

namespace Tournaments.Infrastructure.Repositories;

public class EfSystemSecuritySettingsRepository : ISystemSecuritySettingsRepository
{
    private readonly TournamentDbContext _dbContext;

    public EfSystemSecuritySettingsRepository(TournamentDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<SystemSecuritySettings> GetAsync(CancellationToken cancellationToken = default)
    {
        var settings = await _dbContext.SystemSecuritySettings
            .FirstOrDefaultAsync(s => s.Id == SystemSecuritySettings.DefaultSettingsId, cancellationToken);

        if (settings == null)
        {
            settings = SystemSecuritySettings.CreateDefault();
            _dbContext.SystemSecuritySettings.Add(settings);
            try
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException)
            {
                // In case of race condition during startup, detach or reload
                _dbContext.Entry(settings).State = EntityState.Detached;
                settings = await _dbContext.SystemSecuritySettings
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.Id == SystemSecuritySettings.DefaultSettingsId, cancellationToken)
                    ?? settings;
            }
        }

        return settings;
    }

    public async Task UpdateAsync(SystemSecuritySettings settings, CancellationToken cancellationToken = default)
    {
        var entry = _dbContext.Entry(settings);
        if (entry.State == EntityState.Detached)
        {
            _dbContext.SystemSecuritySettings.Attach(settings);
            entry.State = EntityState.Modified;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
