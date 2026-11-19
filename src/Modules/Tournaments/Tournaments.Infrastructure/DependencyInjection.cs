using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Tournaments.Domain.Repositories;
using Tournaments.Infrastructure.Persistence;
using Tournaments.Infrastructure.Repositories;

namespace Tournaments.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddTournamentsInfrastructure(
        this IServiceCollection services,
        IConfiguration? configuration = null)
    {
        var connectionString = configuration?.GetConnectionString("TournamentsDatabase")
            ?? "Host=localhost;Database=TournamentEngineDb;Username=postgres;Password=postgres";

        services.AddDbContext<TournamentDbContext>(options =>
        {
            options.UseNpgsql(connectionString);
        });

        services.AddScoped<ITournamentRepository, EfTournamentRepository>();
        services.AddScoped<IDraftSessionRepository, EfDraftSessionRepository>();
        services.AddScoped<IMatchSeriesRepository, EfMatchSeriesRepository>();
        services.AddScoped<ISystemSecuritySettingsRepository, EfSystemSecuritySettingsRepository>();
        services.AddSingleton<Tournaments.Application.Common.Interfaces.IDotaReplayParserService, Tournaments.Infrastructure.ReplayParsing.GoCliReplayParserService>();
        services.AddScoped<Tournaments.Application.Common.Interfaces.IFileStorageService, Tournaments.Infrastructure.Services.LocalFileStorageService>();

        services.AddMemoryCache();
        services.AddOptions<Tournaments.Infrastructure.ExternalServices.PandaScore.PandaScoreOptions>()
            .Configure<IConfiguration>((opt, config) =>
            {
                config.GetSection(Tournaments.Infrastructure.ExternalServices.PandaScore.PandaScoreOptions.SectionName).Bind(opt);
            });
        services.AddHttpClient<Tournaments.Application.Common.Interfaces.IProMatchesService, Tournaments.Infrastructure.ExternalServices.PandaScore.PandaScoreProMatchesService>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(10);
            client.DefaultRequestHeaders.Add("User-Agent", "TournamentCreator/1.0");
        });

        return services;
    }
}
