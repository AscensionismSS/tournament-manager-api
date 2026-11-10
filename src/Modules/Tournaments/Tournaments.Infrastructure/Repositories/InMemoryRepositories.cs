using System.Collections.Concurrent;
using Tournaments.Domain.Entities;
using Tournaments.Domain.Repositories;

namespace Tournaments.Infrastructure.Repositories;

public class InMemoryTournamentRepository : ITournamentRepository
{
    private static readonly ConcurrentDictionary<Guid, Tournament> _storage = new();

    public Task<Tournament?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _storage.TryGetValue(id, out var tournament);
        return Task.FromResult(tournament);
    }

    public Task<IReadOnlyList<Tournament>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<Tournament> list = _storage.Values.ToList();
        return Task.FromResult(list);
    }

    public Task AddAsync(Tournament tournament, CancellationToken cancellationToken = default)
    {
        _storage[tournament.Id] = tournament;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Tournament tournament, CancellationToken cancellationToken = default)
    {
        _storage[tournament.Id] = tournament;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Tournament tournament, CancellationToken cancellationToken = default)
    {
        _storage.TryRemove(tournament.Id, out _);
        return Task.CompletedTask;
    }
}

public class InMemoryDraftSessionRepository : IDraftSessionRepository
{
    private static readonly ConcurrentDictionary<Guid, DraftSession> _storage = new();

    public Task<DraftSession?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _storage.TryGetValue(id, out var session);
        return Task.FromResult(session);
    }

    public Task<DraftSession?> GetByTournamentIdAsync(Guid tournamentId, CancellationToken cancellationToken = default)
    {
        var session = _storage.Values.FirstOrDefault(s => s.TournamentId == tournamentId);
        return Task.FromResult(session);
    }

    public Task AddAsync(DraftSession session, CancellationToken cancellationToken = default)
    {
        _storage[session.Id] = session;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(DraftSession session, CancellationToken cancellationToken = default)
    {
        _storage[session.Id] = session;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(DraftSession session, CancellationToken cancellationToken = default)
    {
        _storage.TryRemove(session.Id, out _);
        return Task.CompletedTask;
    }
}

public class InMemoryMatchSeriesRepository : IMatchSeriesRepository
{
    private static readonly ConcurrentDictionary<Guid, MatchSeries> _storage = new();

    public Task<MatchSeries?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _storage.TryGetValue(id, out var series);
        return Task.FromResult(series);
    }

    public Task<IReadOnlyList<MatchSeries>> GetByTournamentIdAsync(Guid tournamentId, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<MatchSeries> list = _storage.Values.Where(s => s.TournamentId == tournamentId).ToList();
        return Task.FromResult(list);
    }

    public Task AddAsync(MatchSeries series, CancellationToken cancellationToken = default)
    {
        _storage[series.Id] = series;
        return Task.CompletedTask;
    }

    public Task AddRangeAsync(IEnumerable<MatchSeries> seriesList, CancellationToken cancellationToken = default)
    {
        foreach (var s in seriesList)
        {
            _storage[s.Id] = s;
        }
        return Task.CompletedTask;
    }

    public Task UpdateAsync(MatchSeries series, CancellationToken cancellationToken = default)
    {
        _storage[series.Id] = series;
        return Task.CompletedTask;
    }

    public Task RemoveRangeAsync(IEnumerable<MatchSeries> seriesList, CancellationToken cancellationToken = default)
    {
        foreach (var s in seriesList)
        {
            _storage.TryRemove(s.Id, out _);
        }
        return Task.CompletedTask;
    }
}

public class InMemorySystemSecuritySettingsRepository : ISystemSecuritySettingsRepository
{
    private SystemSecuritySettings _settings;

    public InMemorySystemSecuritySettingsRepository(SystemSecuritySettings? initialSettings = null)
    {
        _settings = initialSettings ?? SystemSecuritySettings.CreateDefault();
    }

    public Task<SystemSecuritySettings> GetAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_settings);
    }

    public Task UpdateAsync(SystemSecuritySettings settings, CancellationToken cancellationToken = default)
    {
        _settings = settings;
        return Task.CompletedTask;
    }
}
