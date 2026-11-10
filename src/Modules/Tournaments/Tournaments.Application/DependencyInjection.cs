using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Tournaments.Application.Common.Behaviors;

namespace Tournaments.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddTournamentsApplication(this IServiceCollection services)
    {
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly);
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
        });

        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);

        services.AddScoped<Tournaments.Application.Common.Interfaces.IBracketProgressionService, Tournaments.Application.Common.Services.BracketProgressionService>();
        services.AddScoped<Tournaments.Application.Features.Matches.UploadMatchReplay.IReplayValidationService, Tournaments.Application.Features.Matches.UploadMatchReplay.ReplayValidationService>();
        services.AddScoped<Tournaments.Application.Features.Matches.UploadMatchReplay.IReplayRosterMatcherService, Tournaments.Application.Features.Matches.UploadMatchReplay.ReplayRosterMatcherService>();

        return services;
    }
}
