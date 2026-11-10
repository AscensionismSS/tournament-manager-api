using System.Text;
using Identity.Application.Common.Interfaces;
using Identity.Domain.Repositories;
using Identity.Infrastructure.ExternalServices;
using Identity.Infrastructure.Persistence;
using Identity.Infrastructure.Repositories;
using Identity.Infrastructure.Security;
using Identity.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Identity.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddIdentityInfrastructure(
        this IServiceCollection services,
        IConfiguration? configuration = null)
    {
        var jwtOptions = new JwtOptions();
        configuration?.GetSection(JwtOptions.SectionName).Bind(jwtOptions);
        services.Configure<JwtOptions>(opt =>
        {
            opt.SecretKey = jwtOptions.SecretKey;
            opt.Issuer = jwtOptions.Issuer;
            opt.Audience = jwtOptions.Audience;
            opt.ExpiryMinutes = jwtOptions.ExpiryMinutes;
        });

        // Autenticação JWT
        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.RequireHttpsMetadata = false;
            options.SaveToken = true;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SecretKey)),
                ValidateIssuer = true,
                ValidIssuer = jwtOptions.Issuer,
                ValidateAudience = true,
                ValidAudience = jwtOptions.Audience,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            };

            // Suporte para conexões SignalR / WebSockets com token JWT via Query String (?access_token=)
            options.Events = new JwtBearerEvents
            {
                OnMessageReceived = context =>
                {
                    var accessToken = context.Request.Query["access_token"];
                    var path = context.HttpContext.Request.Path;
                    if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                    {
                        context.Token = accessToken;
                    }
                    return Task.CompletedTask;
                },
                OnChallenge = async context =>
                {
                    context.HandleResponse();
                    context.Response.StatusCode = Microsoft.AspNetCore.Http.StatusCodes.Status401Unauthorized;
                    context.Response.ContentType = "application/problem+json";

                    var problemDetails = new Microsoft.AspNetCore.Mvc.ProblemDetails
                    {
                        Status = Microsoft.AspNetCore.Http.StatusCodes.Status401Unauthorized,
                        Title = "Sessão Expirada ou Não Autorizada",
                        Type = "https://tournaments.local/errors/unauthorized",
                        Detail = !string.IsNullOrWhiteSpace(context.ErrorDescription)
                            ? context.ErrorDescription
                            : "O token de autenticação é inválido ou expirou. Por favor, realize o login novamente.",
                        Instance = context.Request.Path
                    };

                    var json = System.Text.Json.JsonSerializer.Serialize(problemDetails);
                    await Microsoft.AspNetCore.Http.HttpResponseWritingExtensions.WriteAsync(context.Response, json);
                }
            };
        });

        services.AddAuthorization();

        var connectionString = configuration?.GetConnectionString("TournamentsDatabase")
            ?? "Host=localhost;Database=TournamentEngineDb;Username=postgres;Password=postgres";

        services.AddDbContext<IdentityDbContext>(options =>
        {
            options.UseNpgsql(connectionString);
        });

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserService, CurrentUserService>();

        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddSingleton<IJwtTokenGenerator, JwtTokenGenerator>();

        services.AddHttpClient<IOpenDotaProfileService, OpenDotaProfileService>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(10);
            client.DefaultRequestHeaders.Add("User-Agent", "TournamentCreator/1.0");
        });

        services.AddScoped<IUserRepository, EfUserRepository>();
        services.AddScoped<IInvitationRepository, EfInvitationRepository>();
        services.AddScoped<IAccountRecoveryRepository, EfAccountRecoveryRepository>();

        return services;
    }
}
