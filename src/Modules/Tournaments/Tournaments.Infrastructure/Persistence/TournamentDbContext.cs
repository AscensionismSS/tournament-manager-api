using Microsoft.EntityFrameworkCore;
using Tournaments.Domain.Entities;

namespace Tournaments.Infrastructure.Persistence;

public class TournamentDbContext : DbContext
{
    public DbSet<Tournament> Tournaments => Set<Tournament>();
    public DbSet<DraftSession> DraftSessions => Set<DraftSession>();
    public DbSet<DraftTeam> DraftTeams => Set<DraftTeam>();
    public DbSet<MatchSeries> MatchSeries => Set<MatchSeries>();
    public DbSet<MatchGame> MatchGames => Set<MatchGame>();
    public DbSet<SystemSecuritySettings> SystemSecuritySettings => Set<SystemSecuritySettings>();

    public TournamentDbContext(DbContextOptions<TournamentDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TournamentDbContext).Assembly);
    }
}
