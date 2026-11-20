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
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Identity.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddIdentityInfrastructure(
        this IServiceCollection services,
        IConfiguration? configuration = null)
    {
        var defaultSecretKey = new JwtOptions().SecretKey;
        var fallbackJwtOptions = new JwtOptions();
        configuration?.GetSection(JwtOptions.SectionName).Bind(fallbackJwtOptions);
        if (string.IsNullOrWhiteSpace(fallbackJwtOptions.SecretKey))
        {
            fallbackJwtOptions.SecretKey = defaultSecretKey;
        }

        services.AddOptions<JwtOptions>()
            .Configure<IServiceProvider>((opt, sp) =>
            {
                opt.SecretKey = fallbackJwtOptions.SecretKey;
                opt.Issuer = fallbackJwtOptions.Issuer;
                opt.Audience = fallbackJwtOptions.Audience;
                opt.ExpiryMinutes = fallbackJwtOptions.ExpiryMinutes;

                var resolvedConfig = sp.GetService<IConfiguration>() ?? configuration;
                resolvedConfig?.GetSection(JwtOptions.SectionName).Bind(opt);
                if (string.IsNullOrWhiteSpace(opt.SecretKey))
                {
                    opt.SecretKey = defaultSecretKey;
                }
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
            options.TokenValidationParameters = BuildTokenValidationParameters(fallbackJwtOptions);

            // Suporte para conexões SignalR / WebSockets com token JWT via Query String (?access_token=)
            // e revogação imediata de contas desativadas / sincronização de Role em tokens de longa duração
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
                OnTokenValidated = async context =>
                {
                    var userIdClaim = context.Principal?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                                   ?? context.Principal?.FindFirst("sub")?.Value;

                    if (!Guid.TryParse(userIdClaim, out var userId) || userId == Guid.Empty)
                    {
                        const string invalidTokenMsg = "Token de autenticação inválido.";
                        context.HttpContext.Items["JwtAuthFailureDetail"] = invalidTokenMsg;
                        context.Fail(invalidTokenMsg);
                        return;
                    }

                    var authStateCache = context.HttpContext.RequestServices.GetRequiredService<IUserAuthStateCache>();
                    var snapshot = await authStateCache.GetOrLoadAsync(
                        userId,
                        async ct =>
                        {
                            var userRepo = context.HttpContext.RequestServices.GetRequiredService<IUserRepository>();
                            var user = await userRepo.GetByIdAsync(userId, ct);
                            return user is null
                                ? new UserAuthSnapshot(Exists: false, IsActive: false, Role: string.Empty)
                                : new UserAuthSnapshot(Exists: true, IsActive: user.IsActive, Role: user.Role.ToString());
                        },
                        context.HttpContext.RequestAborted);

                    if (!snapshot.Exists || !snapshot.IsActive)
                    {
                        const string deactivatedMsg = "Sua conta está desativada.";
                        context.HttpContext.Items["JwtAuthFailureDetail"] = deactivatedMsg;
                        context.Fail(deactivatedMsg);
                        return;
                    }

                    // Sincroniza o Role atual do banco no ClaimsPrincipal para evitar privilégios defasados em JWT de 30 dias
                    if (context.Principal?.Identity is System.Security.Claims.ClaimsIdentity claimsIdentity &&
                        !string.IsNullOrWhiteSpace(snapshot.Role))
                    {
                        var effectiveRoleType = string.IsNullOrWhiteSpace(claimsIdentity.RoleClaimType)
                            ? System.Security.Claims.ClaimTypes.Role
                            : claimsIdentity.RoleClaimType;

                        var existingRoleClaims = claimsIdentity.Claims
                            .Where(c => string.Equals(c.Type, System.Security.Claims.ClaimTypes.Role, StringComparison.OrdinalIgnoreCase)
                                     || string.Equals(c.Type, "role", StringComparison.OrdinalIgnoreCase)
                                     || string.Equals(c.Type, effectiveRoleType, StringComparison.OrdinalIgnoreCase))
                            .ToList();

                        bool alreadySynced = existingRoleClaims.Count == 1
                            && string.Equals(existingRoleClaims[0].Type, effectiveRoleType, StringComparison.Ordinal)
                            && string.Equals(existingRoleClaims[0].Value, snapshot.Role, StringComparison.Ordinal);

                        if (!alreadySynced)
                        {
                            foreach (var claim in existingRoleClaims)
                            {
                                claimsIdentity.RemoveClaim(claim);
                            }

                            claimsIdentity.AddClaim(new System.Security.Claims.Claim(effectiveRoleType, snapshot.Role));
                            if (!string.Equals(effectiveRoleType, System.Security.Claims.ClaimTypes.Role, StringComparison.Ordinal))
                            {
                                claimsIdentity.AddClaim(new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role, snapshot.Role));
                            }
                        }
                    }
                },
                OnChallenge = async context =>
                {
                    context.HandleResponse();
                    context.Response.StatusCode = Microsoft.AspNetCore.Http.StatusCodes.Status401Unauthorized;
                    context.Response.ContentType = "application/problem+json";

                    var customFailureDetail = context.HttpContext.Items["JwtAuthFailureDetail"] as string;

                    var problemDetails = new Microsoft.AspNetCore.Mvc.ProblemDetails
                    {
                        Status = Microsoft.AspNetCore.Http.StatusCodes.Status401Unauthorized,
                        Title = "Sessão Expirada ou Não Autorizada",
                        Type = "https://tournaments.local/errors/unauthorized",
                        Detail = !string.IsNullOrWhiteSpace(customFailureDetail)
                            ? customFailureDetail
                            : !string.IsNullOrWhiteSpace(context.ErrorDescription)
                                ? context.ErrorDescription
                                : "O token de autenticação é inválido ou expirou. Por favor, realize o login novamente.",
                        Instance = context.Request.Path
                    };

                    await Microsoft.AspNetCore.Http.HttpResponseJsonExtensions.WriteAsJsonAsync(
                        context.Response,
                        problemDetails,
                        options: null,
                        contentType: "application/problem+json");
                }
            };
        });

        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((bearerOpts, jwtOptsAccessor) =>
            {
                bearerOpts.TokenValidationParameters = BuildTokenValidationParameters(jwtOptsAccessor.Value);
            });

        services.AddAuthorization();
        services.AddMemoryCache();
        services.AddSingleton<IUserAuthStateCache, MemoryUserAuthStateCache>();

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

        var fallbackRootOwnerOptions = new RootOwnerOptions();
        configuration?.GetSection(RootOwnerOptions.SectionName).Bind(fallbackRootOwnerOptions);
        if (string.IsNullOrWhiteSpace(fallbackRootOwnerOptions.Email))
        {
            fallbackRootOwnerOptions.Email = RootOwnerOptions.DefaultEmail;
        }

        services.AddOptions<RootOwnerOptions>()
            .Configure<IServiceProvider>((opt, sp) =>
            {
                opt.Email = fallbackRootOwnerOptions.Email;

                var resolvedConfig = sp.GetService<IConfiguration>() ?? configuration;
                resolvedConfig?.GetSection(RootOwnerOptions.SectionName).Bind(opt);
                if (string.IsNullOrWhiteSpace(opt.Email))
                {
                    opt.Email = RootOwnerOptions.DefaultEmail;
                }
            });

        services.AddSingleton<IRootOwnerService, RootOwnerService>();

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

    private static TokenValidationParameters BuildTokenValidationParameters(JwtOptions jwtOptions) =>
        new()
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SecretKey)),
            ValidateIssuer = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtOptions.Audience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero,
            RoleClaimType = System.Security.Claims.ClaimTypes.Role,
            NameClaimType = System.Security.Claims.ClaimTypes.NameIdentifier
        };
}
