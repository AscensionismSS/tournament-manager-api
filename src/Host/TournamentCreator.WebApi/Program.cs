using Asp.Versioning;
using Identity.Application;
using Identity.Application.Common.Interfaces;
using Identity.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Tournaments.Application;
using Tournaments.Application.Common.Interfaces;
using Tournaments.Infrastructure;
using TournamentCreator.WebApi.Hubs.v1;
using TournamentCreator.WebApi.Middlewares;
using TournamentCreator.WebApi.Services;

var builder = WebApplication.CreateBuilder(args);

// 1. Suporte Nativo a RFC 9457 (ProblemDetails) & IExceptionHandler
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

// 2. Versionamento Oficial de API por Rota (Asp.Versioning)
builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true; // Envia o header "api-supported-versions"
    options.ApiVersionReader = new UrlSegmentApiVersionReader();
}).AddApiExplorer(options =>
{
    options.GroupNameFormat = "'v'VVV";
    options.SubstituteApiVersionInUrl = true;
});

// Suporte a Documentação Interativa via Swagger/OpenAPI com JWT
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.OpenApiInfo
    {
        Title = "Tournament Creator API",
        Version = "v1",
        Description = "API oficial do motor de torneios e drafts em tempo real."
    });

    options.ResolveConflictingActions(apiDescriptions => apiDescriptions.First());

    options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.ParameterLocation.Header,
        Description = "Insira o token JWT gerado pelo endpoint de login. Exemplo: eyJhbGciOi..."
    });

    options.AddSecurityRequirement(doc => new Microsoft.OpenApi.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.OpenApiSecuritySchemeReference("Bearer"),
            new List<string>()
        }
    });
});

// 3. Controllers, SignalR, SSE Webhook Stream e CORS
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString;
    });
builder.Services.AddSignalR();
builder.Services.AddSingleton<TournamentWebhookStreamService>();
builder.Services.AddSingleton<IBannerImageValidator, BannerImageValidator>();
builder.Services.AddScoped<ITournamentRealtimeNotifier, TournamentRealtimeNotifier>();
builder.Services.AddScoped<IUserRealtimeNotifier, UserRealtimeNotifier>();

// Política de CORS com suporte a origens configuráveis para produção e suporte a WebSockets/SignalR
builder.Services.AddCors();
builder.Services.AddOptions<Microsoft.AspNetCore.Cors.Infrastructure.CorsOptions>()
    .Configure<IConfiguration, IHostEnvironment>((corsOptions, config, env) =>
    {
        corsOptions.AddPolicy("AllowFrontend", policy =>
        {
            ProductionSecurityValidator.ConfigureCorsPolicy(
                policy,
                config["Cors:AllowedOrigins"],
                env.IsDevelopment());
        });
    });

// 4. Rate Limiting por IP para Endpoints Sensíveis (Autenticação, Recuperação, Convites e Steam/OpenDota)
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.HttpContext.Response.ContentType = "application/problem+json";

        if (context.Lease.TryGetMetadata(System.Threading.RateLimiting.MetadataName.RetryAfter, out var retryAfter))
        {
            context.HttpContext.Response.Headers.RetryAfter = ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString();
        }

        var problem = new Microsoft.AspNetCore.Mvc.ProblemDetails
        {
            Status = StatusCodes.Status429TooManyRequests,
            Title = "Muitas Requisições",
            Type = "https://tournaments.local/errors/rate-limit-exceeded",
            Detail = "Limite de tentativas excedido para este endereço IP. Aguarde um minuto antes de tentar novamente.",
            Instance = context.HttpContext.Request.Path
        };

        await context.HttpContext.Response.WriteAsJsonAsync(
            problem,
            options: null,
            contentType: "application/problem+json",
            cancellationToken: cancellationToken);
    };

    options.AddPolicy(ProductionSecurityValidator.RateLimitPolicyAuthStrict, httpContext =>
        System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: RateLimitClientIpResolver.ResolveClientIp(httpContext),
            factory: _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = System.Threading.RateLimiting.QueueProcessingOrder.OldestFirst,
                QueueLimit = 0,
                AutoReplenishment = true
            }));

    options.AddPolicy(ProductionSecurityValidator.RateLimitPolicySteamPreview, httpContext =>
        System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: RateLimitClientIpResolver.ResolveClientIp(httpContext),
            factory: _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
            {
                PermitLimit = 20,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = System.Threading.RateLimiting.QueueProcessingOrder.OldestFirst,
                QueueLimit = 0,
                AutoReplenishment = true
            }));
});

// 5. Módulos do Sistema (Modular Monolith)
builder.Services.AddTournamentsApplication();
builder.Services.AddTournamentsInfrastructure(builder.Configuration);
builder.Services.AddIdentityApplication();
builder.Services.AddIdentityInfrastructure(builder.Configuration);

var app = builder.Build();

// Validação Fail-Fast de Segredos em Produção (após Build para honrar todas as fontes de configuração, antes de abrir portas ou tocar no banco)
ProductionSecurityValidator.ValidateJwtSecret(
    app.Configuration["Jwt:SecretKey"],
    app.Environment.IsDevelopment());

// 5. Inicialização automática do banco de dados (habilitada por padrão em Dev e Produção, controlável via Database:InitializeOnStartup)
var initializeDbOnStartup = app.Configuration.GetValue<bool>("Database:InitializeOnStartup", true);
if (initializeDbOnStartup)
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<Tournaments.Infrastructure.Persistence.TournamentDbContext>();
    var identityDbContext = scope.ServiceProvider.GetRequiredService<Identity.Infrastructure.Persistence.IdentityDbContext>();
    var passwordHasher = scope.ServiceProvider.GetRequiredService<Identity.Application.Common.Interfaces.IPasswordHasher>();

    try
    {
        dbContext.Database.EnsureCreated();

        // No EF Core, com múltiplos DbContexts compartilhando o mesmo BD, EnsureCreated()
        // não cria tabelas do segundo contexto se o banco já tiver tabelas do primeiro.
        var identityCreator = identityDbContext.Database.GetService<Microsoft.EntityFrameworkCore.Storage.IDatabaseCreator>() as Microsoft.EntityFrameworkCore.Storage.RelationalDatabaseCreator;
        if (identityCreator != null)
        {
            try { identityCreator.CreateTables(); } catch { /* tabelas já existem */ }
        }

        try
        {
            identityDbContext.Database.ExecuteSqlRaw("""
                ALTER TABLE "users" ADD COLUMN IF NOT EXISTS "IsActive" BOOLEAN NOT NULL DEFAULT TRUE;
                ALTER TABLE "users" ADD COLUMN IF NOT EXISTS "IsTournamentEligible" BOOLEAN NOT NULL DEFAULT TRUE;
                ALTER TABLE "users" ADD COLUMN IF NOT EXISTS "StatusReason" VARCHAR(500);
                ALTER TABLE "users" ADD COLUMN IF NOT EXISTS "PreferredLandingPage" VARCHAR(50) DEFAULT 'Player';

                CREATE TABLE IF NOT EXISTS "invitations" (
                    "Id" uuid NOT NULL,
                    "Code" character varying(64) NOT NULL,
                    "Email" character varying(256) NOT NULL,
                    "TargetRole" character varying(50) NOT NULL,
                    "CreatedByUserId" uuid NOT NULL,
                    "CreatedAtUtc" timestamp with time zone NOT NULL,
                    "ExpiresAtUtc" timestamp with time zone NOT NULL,
                    "IsRevoked" boolean NOT NULL DEFAULT FALSE,
                    "RevokedAtUtc" timestamp with time zone,
                    CONSTRAINT "PK_invitations" PRIMARY KEY ("Id")
                );
                CREATE UNIQUE INDEX IF NOT EXISTS "IX_invitations_Code" ON "invitations" ("Code");
                CREATE INDEX IF NOT EXISTS "IX_invitations_Email" ON "invitations" ("Email");

                CREATE TABLE IF NOT EXISTS "AccountRecoveryTokens" (
                    "Id" uuid NOT NULL,
                    "UserId" uuid NOT NULL,
                    "Email" character varying(256) NOT NULL,
                    "Token" character varying(64) NOT NULL,
                    "Type" integer NOT NULL,
                    "CreatedAtUtc" timestamp with time zone NOT NULL,
                    "ExpiresAtUtc" timestamp with time zone NOT NULL,
                    "IsUsed" boolean NOT NULL,
                    "UsedAtUtc" timestamp with time zone,
                    CONSTRAINT "PK_AccountRecoveryTokens" PRIMARY KEY ("Id")
                );
                CREATE UNIQUE INDEX IF NOT EXISTS "IX_AccountRecoveryTokens_Token" ON "AccountRecoveryTokens" ("Token");
            """);
        }
        catch (Exception ex)
        {
            app.Logger.LogWarning("Falha ao sincronizar esquema do IdentityDbContext: {Message}", ex.Message);
        }

        try
        {
            dbContext.Database.ExecuteSqlRaw("""
                ALTER TABLE "Tournaments" ADD COLUMN IF NOT EXISTS "BannerUrl" VARCHAR(500);
                ALTER TABLE "Tournaments" ADD COLUMN IF NOT EXISTS "IsResultSubmissionLocked" BOOLEAN NOT NULL DEFAULT FALSE;
                ALTER TABLE "Tournaments" ADD COLUMN IF NOT EXISTS "ResultLockReason" VARCHAR(500);
                ALTER TABLE "Tournaments" ADD COLUMN IF NOT EXISTS "ResultLockedAtUtc" timestamp with time zone;
                ALTER TABLE "Tournaments" ADD COLUMN IF NOT EXISTS "ResultLockedBy" VARCHAR(150);
                ALTER TABLE "MatchSeries" ADD COLUMN IF NOT EXISTS "Round" INTEGER NOT NULL DEFAULT 1;
                ALTER TABLE "MatchGames" ADD COLUMN IF NOT EXISTS "FirstPickTeamId" uuid;
                ALTER TABLE "MatchGames" ADD COLUMN IF NOT EXISTS "TeamABanHeroIds" jsonb DEFAULT '[]'::jsonb;
                ALTER TABLE "MatchGames" ADD COLUMN IF NOT EXISTS "TeamBBanHeroIds" jsonb DEFAULT '[]'::jsonb;
                ALTER TABLE "MatchGames" ADD COLUMN IF NOT EXISTS "TeamARoshanKills" INTEGER NOT NULL DEFAULT 0;
                ALTER TABLE "MatchGames" ADD COLUMN IF NOT EXISTS "TeamBRoshanKills" INTEGER NOT NULL DEFAULT 0;
                ALTER TABLE "MatchGames" ADD COLUMN IF NOT EXISTS "TeamAAegisStolen" INTEGER NOT NULL DEFAULT 0;
                ALTER TABLE "MatchGames" ADD COLUMN IF NOT EXISTS "TeamBAegisStolen" INTEGER NOT NULL DEFAULT 0;
                ALTER TABLE "DraftSessions" ADD COLUMN IF NOT EXISTS "PreviousState" VARCHAR(50);
                UPDATE users SET "Role" = 'User' WHERE "Role" = 'Player';
                UPDATE users SET "Roles" = REPLACE("Roles", 'OffLane', 'Offlane') WHERE "Roles" LIKE '%OffLane%';
                UPDATE users SET "PreferredRole" = 'Offlane' WHERE "PreferredRole" = 'OffLane';
                UPDATE users SET "Roles" = REPLACE("Roles", 'MidLane', 'Mid') WHERE "Roles" LIKE '%MidLane%';
                UPDATE users SET "PreferredRole" = 'Mid' WHERE "PreferredRole" = 'MidLane';

                CREATE TABLE IF NOT EXISTS "SystemSecuritySettings" (
                    "Id" uuid NOT NULL,
                    "LockResultSubmission" boolean NOT NULL DEFAULT FALSE,
                    "LockTournamentRegistration" boolean NOT NULL DEFAULT FALSE,
                    "LockTournamentCreation" boolean NOT NULL DEFAULT FALSE,
                    "LockDraftPicks" boolean NOT NULL DEFAULT FALSE,
                    "MaintenanceMode" boolean NOT NULL DEFAULT FALSE,
                    "Message" character varying(1000),
                    "UpdatedAtUtc" timestamp with time zone,
                    "UpdatedBy" character varying(150),
                    CONSTRAINT "PK_SystemSecuritySettings" PRIMARY KEY ("Id")
                );
                INSERT INTO "SystemSecuritySettings" ("Id", "LockResultSubmission", "LockTournamentRegistration", "LockTournamentCreation", "LockDraftPicks", "MaintenanceMode", "Message", "UpdatedAtUtc", "UpdatedBy")
                VALUES ('00000000-0000-0000-0000-000000000001', FALSE, FALSE, FALSE, FALSE, FALSE, NULL, NOW(), 'System')
                ON CONFLICT ("Id") DO NOTHING;
            """);
        }
        catch (Exception ex)
        {
            app.Logger.LogWarning("Falha ao sincronizar colunas em Tournaments/MatchSeries: {Message}", ex.Message);
        }

        // Seed Master SuperAdmin se a tabela estiver vazia
        if (!identityDbContext.Users.Any())
        {
            var adminEmail = app.Configuration["AdminSeed:Email"] ?? "admin@tournament.local";
            var configuredAdminPassword = app.Configuration["AdminSeed:Password"];
            ProductionSecurityValidator.ValidateAdminSeedPassword(configuredAdminPassword, app.Environment.IsDevelopment());

            var adminPassword = string.IsNullOrWhiteSpace(configuredAdminPassword)
                ? "Admin123!"
                : configuredAdminPassword.Trim();

            var defaultAdmin = new Identity.Domain.Entities.User(
                adminEmail,
                passwordHasher.HashPassword(adminPassword),
                "Master Admin",
                Identity.Domain.Enums.UserRole.SuperAdmin
            );
            identityDbContext.Users.Add(defaultAdmin);
            identityDbContext.SaveChanges();
            app.Logger.LogInformation("SuperAdmin padrão criado com sucesso: {Email}", adminEmail);
        }
    }
    catch (InvalidOperationException)
    {
        throw;
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning("PostgreSQL não conectado durante startup. As tabelas serão gerenciadas externamente ou quando o banco subir: {Message}", ex.Message);
    }
}

// 6. Pipeline de Tratamento Global de Exceções, Headers de Segurança e Middlewares
app.Use(async (context, next) =>
{
    context.Response.OnStarting(static state =>
    {
        var httpContext = (HttpContext)state;
        httpContext.Response.Headers.TryAdd("X-Content-Type-Options", "nosniff");
        httpContext.Response.Headers.TryAdd("X-Frame-Options", "DENY");
        httpContext.Response.Headers.TryAdd("Referrer-Policy", "strict-origin-when-cross-origin");
        return Task.CompletedTask;
    }, context);

    await next();
});

app.UseExceptionHandler();

app.UseCors("AllowFrontend");
app.UseStaticFiles();

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

var enableSwagger = app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("Swagger:Enabled", false);
if (enableSwagger)
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Tournament Creator API v1");
        c.RoutePrefix = "swagger";
    });
}

app.MapControllers();

// Health Check público e para orquestradores (Kubernetes / Docker)
app.MapMethods("/healthz", ["GET", "HEAD"], () => Results.Ok("OK")).AllowAnonymous();
app.MapMethods("/api/v1/health", ["GET", "HEAD"], () => Results.Ok(new 
{ 
    status = "Healthy", 
    timestampUtc = DateTime.UtcNow 
})).AllowAnonymous();

// 7. Hub SignalR versionado
app.MapHub<TournamentDraftHub>("/hubs/v1/tournament-draft");

app.Run();

// Necessário para que WebApplicationFactory<Program> nos testes de integração possa referenciar a classe Program
public partial class Program { }
