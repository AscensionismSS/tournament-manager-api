using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;
using Asp.Versioning;
using BuildingBlocks.Domain;
using Identity.Application;
using Identity.Application.Common.Interfaces;
using Identity.Application.Features.Members.GetMemberById;
using Identity.Application.Features.Members.GetMembers;
using Identity.Application.Features.Members.UpdateMemberStatus;
using Identity.Contracts.Members;
using Identity.Domain.Entities;
using Identity.Domain.Enums;
using Identity.Domain.Repositories;
using Identity.Infrastructure;
using Identity.Infrastructure.ExternalServices;
using Identity.Infrastructure.Repositories;
using Identity.Infrastructure.Security;
using MediatR;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TournamentCreator.WebApi.Controllers.v1;
using TournamentCreator.WebApi.Controllers.v1.Requests;
using TournamentCreator.WebApi.Hubs.v1;
using TournamentCreator.WebApi.Middlewares;
using TournamentCreator.WebApi.Services;
using Tournaments.Application;
using Tournaments.Application.Common.Interfaces;
using Tournaments.Application.Features.Draft.AddLatePlayerToPool;
using Tournaments.Application.Features.Draft.AddPlayerToPool;
using Tournaments.Application.Features.Draft.RemovePlayerFromPool;
using Tournaments.Contracts.Draft;
using Tournaments.Domain.Entities;
using Tournaments.Domain.Enums;
using Tournaments.Domain.Repositories;
using Tournaments.Domain.ValueObjects;
using Tournaments.Infrastructure.ReplayParsing;
using Tournaments.Infrastructure.Repositories;
using Tournaments.Infrastructure.Services;
using Xunit;

namespace Tournaments.UnitTests;

public class SecurityHardeningAuditTests
{
    #region 1. DraftController Anti-Spoofing, Eligibility & Fail-Closed

    [Fact]
    public async Task DraftController_AddPlayerToPool_WhenNonAdminSpoofsBody_ShouldIgnoreBodyAndUseOfficialDbProfile()
    {
        var officialUserId = Guid.NewGuid();
        var officialMember = new MemberDto(
            UserId: officialUserId,
            Email: "legit@dota.local",
            DisplayName: "OfficialNick",
            PersonaName: "SteamPersona",
            AvatarUrl: "https://official.steam/avatar.jpg",
            SteamId: "76561198011111111",
            SteamProfileUrl: "https://steamcommunity.com/profiles/76561198011111111",
            DotaFriendId: "50845383",
            RankTier: 32, // Legend 2 oficial
            LeaderboardRank: null,
            Roles: ["HardSupport", "SoftSupport"],
            PreferredRole: "HardSupport",
            Role: "User",
            IsSteamLocked: true,
            IsActive: true,
            StatusReason: null,
            IsTournamentEligible: true
        );

        var sender = new RecordingDraftSender(officialMember);
        var controller = CreateDraftControllerWithUser(sender, officialUserId.ToString(), "User");

        // Atacante envia JSON forjando Rank Immortal (80), outro SteamID, outro PlayerId e outro Nome
        var spoofedRequest = new AddPlayerRequest(
            PlayerId: Guid.NewGuid(),
            Name: "SpoofedMiracle",
            ExternalUserId: "76561198099999999",
            Roles: ["Carry"],
            PreferredRole: "Carry",
            RankTier: 80,
            AvatarUrl: "https://evil.example/fake.png"
        );

        var draftId = Guid.NewGuid();
        var response = await controller.AddPlayerToPool(draftId, spoofedRequest, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(response.Result);
        Assert.Equal(StatusCodes.Status200OK, okResult.StatusCode);

        Assert.NotNull(sender.LastAddPlayerCommand);
        Assert.Equal(officialUserId, sender.LastAddPlayerCommand!.PlayerId);
        Assert.Equal("OfficialNick", sender.LastAddPlayerCommand.Name);
        Assert.Equal("76561198011111111", sender.LastAddPlayerCommand.ExternalUserId);
        Assert.Equal(32, sender.LastAddPlayerCommand.RankTier);
        Assert.Equal("https://official.steam/avatar.jpg", sender.LastAddPlayerCommand.AvatarUrl);
        Assert.Equal("HardSupport", sender.LastAddPlayerCommand.PreferredRole);
        Assert.Equal(new[] { "HardSupport", "SoftSupport" }, sender.LastAddPlayerCommand.Roles);
    }

    [Fact]
    public async Task DraftController_AddPlayerToPool_WhenNonAdminSendsNullBody_ShouldStillEnrollOfficialUser()
    {
        var officialUserId = Guid.NewGuid();
        var officialMember = CreateMemberDto(officialUserId, isActive: true, isEligible: true, steamId: "76561198022222222");

        var sender = new RecordingDraftSender(officialMember);
        var controller = CreateDraftControllerWithUser(sender, officialUserId.ToString(), "User");

        var response = await controller.AddPlayerToPool(Guid.NewGuid(), null, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(response.Result);
        Assert.Equal(StatusCodes.Status200OK, okResult.StatusCode);
        Assert.NotNull(sender.LastAddPlayerCommand);
        Assert.Equal(officialUserId, sender.LastAddPlayerCommand!.PlayerId);
    }

    [Theory]
    [InlineData(false, true, "76561198011111111", StatusCodes.Status403Forbidden)] // Conta inativa
    [InlineData(true, false, "76561198011111111", StatusCodes.Status403Forbidden)] // Inelegível para torneios
    [InlineData(true, true, null, StatusCodes.Status400BadRequest)]                // Sem Steam vinculada
    [InlineData(true, true, "   ", StatusCodes.Status400BadRequest)]               // Steam vazia
    public async Task DraftController_AddPlayerToPool_WhenNonAdminIneligibleOrInactiveOrNoSteam_ShouldBlock(
        bool isActive,
        bool isEligible,
        string? steamId,
        int expectedStatusCode)
    {
        var userId = Guid.NewGuid();
        var member = CreateMemberDto(userId, isActive: isActive, isEligible: isEligible, steamId: steamId);
        var sender = new RecordingDraftSender(member);
        var controller = CreateDraftControllerWithUser(sender, userId.ToString(), "User");

        var response = await controller.AddPlayerToPool(Guid.NewGuid(), new AddPlayerRequest(null, "Any"), CancellationToken.None);

        var objResult = Assert.IsAssignableFrom<ObjectResult>(response.Result);
        Assert.Equal(expectedStatusCode, objResult.StatusCode);
        Assert.Null(sender.LastAddPlayerCommand);
    }

    [Fact]
    public async Task DraftController_AddPlayerToPool_WhenNonAdminUserNotFoundOrInvalidClaim_ShouldReturn403()
    {
        var sender = new RecordingDraftSender(memberToReturn: null);
        var controllerMissingDbUser = CreateDraftControllerWithUser(sender, Guid.NewGuid().ToString(), "User");

        var resNotFound = await controllerMissingDbUser.AddPlayerToPool(Guid.NewGuid(), new AddPlayerRequest(null, "Any"), CancellationToken.None);
        var objNotFound = Assert.IsType<ObjectResult>(resNotFound.Result);
        Assert.Equal(StatusCodes.Status403Forbidden, objNotFound.StatusCode);
        Assert.Null(sender.LastAddPlayerCommand);

        var controllerInvalidClaim = CreateDraftControllerWithUser(sender, "not-a-guid", "User");
        var resBadClaim = await controllerInvalidClaim.AddPlayerToPool(Guid.NewGuid(), new AddPlayerRequest(null, "Any"), CancellationToken.None);
        var objBadClaim = Assert.IsType<ObjectResult>(resBadClaim.Result);
        Assert.Equal(StatusCodes.Status403Forbidden, objBadClaim.StatusCode);
        Assert.Null(sender.LastAddPlayerCommand);
    }

    [Fact]
    public async Task DraftController_WhenUserIsNull_ShouldFailClosedAsNonAdmin()
    {
        var sender = new RecordingDraftSender(memberToReturn: null);
        var controller = new DraftController(sender)
        {
            // ControllerContext vazio -> User == null
            ControllerContext = new ControllerContext()
        };

        // 1. AddPlayerToPool deve cair no fluxo !isAdmin e bloquear com 403
        var addRes = await controller.AddPlayerToPool(Guid.NewGuid(), new AddPlayerRequest(Guid.NewGuid(), "Attacker"), CancellationToken.None);
        var objRes = Assert.IsType<ObjectResult>(addRes.Result);
        Assert.Equal(StatusCodes.Status403Forbidden, objRes.StatusCode);
        Assert.Null(sender.LastAddPlayerCommand);

        // 2. RemovePlayerFromPool deve repassar IsAdmin = false (nunca true!)
        await controller.RemovePlayerFromPool(Guid.NewGuid(), Guid.NewGuid(), null, CancellationToken.None);
        Assert.NotNull(sender.LastRemovePlayerCommand);
        Assert.False(sender.LastRemovePlayerCommand!.IsAdmin);
        Assert.Equal(string.Empty, sender.LastRemovePlayerCommand.ActingUserId);
    }

    [Fact]
    public async Task DraftController_AddPlayerToPool_WhenAdmin_ShouldAllowCustomPlayerPayload()
    {
        var adminId = Guid.NewGuid();
        var customPlayerId = Guid.NewGuid();
        var sender = new RecordingDraftSender(memberToReturn: null);
        var controller = CreateDraftControllerWithUser(sender, adminId.ToString(), "TournamentAdmin");

        var req = new AddPlayerRequest(customPlayerId, "GuestStandin", "76561198055555555", ["Mid"], "Mid", 75, null);
        var res = await controller.AddPlayerToPool(Guid.NewGuid(), req, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(res.Result);
        Assert.Equal(StatusCodes.Status200OK, ok.StatusCode);
        Assert.NotNull(sender.LastAddPlayerCommand);
        Assert.Equal(customPlayerId, sender.LastAddPlayerCommand!.PlayerId);
        Assert.Equal("GuestStandin", sender.LastAddPlayerCommand.Name);
        Assert.Equal(75, sender.LastAddPlayerCommand.RankTier);
    }

    [Fact]
    public void DraftSession_AddPlayerToPool_WhenDuplicateSteamIdInPoolOrTeams_ShouldThrowDomainException()
    {
        var session = new DraftSession(Guid.NewGuid(), 4);
        var p1 = Guid.NewGuid();
        session.AddPlayerToPool(p1, "PlayerOne", externalUserId: "76561198012345678");

        // Mesmo SteamID na pool com outro Guid e outro Nome -> bloqueado!
        var exPool = Assert.Throws<DomainException>(() =>
            session.AddPlayerToPool(Guid.NewGuid(), "CloneInPool", externalUserId: "76561198012345678"));
        Assert.Contains("76561198012345678", exPool.Message);

        // Move p1 para capitão de um time (sai da pool e entra nos Starters do time)
        session.SelectCaptain(p1, session.DraftVersion);

        // Tenta inscrever novamente o mesmo SteamID via AddPlayerToPool ou AddLatePlayerToPool -> bloqueado!
        var exTeam = Assert.Throws<DomainException>(() =>
            session.AddLatePlayerToPool(Guid.NewGuid(), "CloneLate", externalUserId: "76561198012345678"));
        Assert.Contains("76561198012345678", exTeam.Message);
    }

    #endregion

    #region 2. Immediate JWT Revocation & Cache Invalidation

    [Fact]
    public async Task JwtOnTokenValidated_ShouldCacheActiveUser_SyncRole_AndImmediatelyRejectDeactivatedUserOnInvalidation()
    {
        var countingRepo = new CountingUserRepository();
        var superAdmin = new User("super@dota.local", "hash", "Super", UserRole.SuperAdmin);
        var player = new User("player@dota.local", "hash", "TargetPlayer", UserRole.User);
        await countingRepo.AddAsync(superAdmin);
        await countingRepo.AddAsync(player);

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:SecretKey"] = "UnitTestingStrongJwtSecretKeyWithMoreThan32Chars!!",
                ["Jwt:Issuer"] = "TestIssuer",
                ["Jwt:Audience"] = "TestAudience",
                ["Jwt:ExpiryMinutes"] = "43200"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddIdentityApplication();
        services.AddIdentityInfrastructure(config);
        // Substitui IUserRepository pelo repositório em memória instrumentado
        services.AddScoped<IUserRepository>(_ => countingRepo);
        services.AddScoped<ICurrentUserService>(_ => new StubCurrentUserService(superAdmin.Id, superAdmin.Email, "SuperAdmin"));

        using var provider = services.BuildServiceProvider();
        var jwtMonitor = provider.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>();
        var bearerOptions = jwtMonitor.Get(JwtBearerDefaults.AuthenticationScheme);

        // 1. Primeira requisição autenticada (token antigo tinha Role = TournamentAdmin, mas no banco o usuário é User)
        var ctx1 = CreateTokenValidatedContext(provider, bearerOptions, player.Id.ToString(), "TournamentAdmin");
        await bearerOptions.Events.OnTokenValidated(ctx1);

        Assert.Null(ctx1.Result); // Não falhou
        Assert.Equal(1, countingRepo.GetByIdCalls);
        // Role sincronizado com o banco ("User", removendo "TournamentAdmin" defasado)
        Assert.False(ctx1.Principal!.IsInRole("TournamentAdmin"));
        Assert.True(ctx1.Principal!.IsInRole("User"));

        // 2. Segunda requisição dentro de 60s -> atendida pelo IMemoryCache sem bater no banco!
        var ctx2 = CreateTokenValidatedContext(provider, bearerOptions, player.Id.ToString(), "User");
        await bearerOptions.Events.OnTokenValidated(ctx2);

        Assert.Null(ctx2.Result);
        Assert.Equal(1, countingRepo.GetByIdCalls); // Continua 1!

        // 3. Admin desativa o membro via MediatR (UpdateMemberStatusHandler busca admin + target [+2 chamadas = 3] e invalida o cache imediatamente!)
        using (var scope = provider.CreateScope())
        {
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            var disableResult = await mediator.Send(new UpdateMemberStatusCommand(player.Id, false, "Banimento imediato"));
            Assert.True(disableResult.IsSuccess);
        }
        Assert.Equal(3, countingRepo.GetByIdCalls);

        // 4. Próxima requisição com o MESMO JWT de 30 dias -> falha IMEDIATAMENTE com context.Fail("Sua conta está desativada.")!
        var ctx3 = CreateTokenValidatedContext(provider, bearerOptions, player.Id.ToString(), "User");
        await bearerOptions.Events.OnTokenValidated(ctx3);

        Assert.NotNull(ctx3.Result);
        Assert.NotNull(ctx3.Result!.Failure);
        Assert.Equal("Sua conta está desativada.", ctx3.Result.Failure!.Message);
        Assert.Equal(4, countingRepo.GetByIdCalls); // Recarregou do banco (+1 = 4) devido à invalidação imediata

        // Verifica também que OnChallenge propaga a mensagem amigável em ProblemDetails 401
        var challengeHttpContext = ctx3.HttpContext;
        challengeHttpContext.Response.Body = new MemoryStream();
        var challengeContext = new JwtBearerChallengeContext(
            challengeHttpContext,
            ctx3.Scheme,
            bearerOptions,
            new AuthenticationProperties { Items = { } })
        {
            AuthenticateFailure = ctx3.Result.Failure
        };
        await bearerOptions.Events.OnChallenge(challengeContext);

        Assert.Equal(StatusCodes.Status401Unauthorized, challengeHttpContext.Response.StatusCode);
        challengeHttpContext.Response.Body.Position = 0;
        var bodyJson = await new StreamReader(challengeHttpContext.Response.Body).ReadToEndAsync();
        Assert.Contains("Sua conta está desativada.", bodyJson);
    }

    [Fact]
    public async Task JwtOnTokenValidated_WhenUserMissingOrInvalidClaim_ShouldFail()
    {
        var repo = new InMemoryUserRepository();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddIdentityInfrastructure();
        services.AddScoped<IUserRepository>(_ => repo);

        using var provider = services.BuildServiceProvider();
        var bearerOptions = provider.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>()
            .Get(JwtBearerDefaults.AuthenticationScheme);

        // Claim inválida
        var badClaimCtx = CreateTokenValidatedContext(provider, bearerOptions, "invalid-guid", "User");
        await bearerOptions.Events.OnTokenValidated(badClaimCtx);
        Assert.NotNull(badClaimCtx.Result?.Failure);

        // Usuário inexistente no banco
        var missingUserCtx = CreateTokenValidatedContext(provider, bearerOptions, Guid.NewGuid().ToString(), "User");
        await bearerOptions.Events.OnTokenValidated(missingUserCtx);
        Assert.NotNull(missingUserCtx.Result?.Failure);
        Assert.Equal("Sua conta está desativada.", missingUserCtx.Result!.Failure!.Message);
    }

    #endregion

    #region 3. Fail-Fast Production Secrets Validation

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("1234567890123456789012345678901")] // 31 caracteres (abaixo do mínimo de 32)
    [InlineData("CHANGE_ME_IN_DEVELOPMENT_OR_ENV_MIN_32_CHARS_LONG!")]
    [InlineData("TournamentCreatorSecretKeySuperSecureMinimum256BitsLong2026!")]
    [InlineData("custom_CHANGE_ME_secret_with_more_than_32_characters!!")]
    public void ProductionSecurityValidator_ValidateJwtSecret_InProduction_ShouldRejectWeakOrDefaultSecrets(string? invalidSecret)
    {
        Assert.Throws<InvalidOperationException>(() =>
            ProductionSecurityValidator.ValidateJwtSecret(invalidSecret, isDevelopment: false));
    }

    [Fact]
    public void ProductionSecurityValidator_ValidateJwtSecret_Boundary32CharsAndDevelopment_ShouldPass()
    {
        // Exatamente 32 caracteres aleatórios fortes em Produção -> válido
        const string exact32Chars = "A9f#kL2$mP8@xQ5!vR7&zN4*wB6%yT10";
        Assert.Equal(32, exact32Chars.Length);
        ProductionSecurityValidator.ValidateJwtSecret(exact32Chars, isDevelopment: false);

        // Em Development, os placeholders de dev não impedem a subida local
        ProductionSecurityValidator.ValidateJwtSecret("TournamentCreatorSecretKeySuperSecureMinimum256BitsLong2026!", isDevelopment: true);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Admin123!")]
    [InlineData("admin123!")]
    [InlineData("CHANGE_ME_STRONG_ADMIN_PASSWORD")]
    public void ProductionSecurityValidator_ValidateAdminSeedPassword_InProduction_ShouldRejectDefaultOrEmpty(string? invalidPassword)
    {
        Assert.Throws<InvalidOperationException>(() =>
            ProductionSecurityValidator.ValidateAdminSeedPassword(invalidPassword, isDevelopment: false));
    }

    [Fact]
    public void ProductionSecurityValidator_ValidateAdminSeedPassword_ValidProdAndDev_ShouldPass()
    {
        ProductionSecurityValidator.ValidateAdminSeedPassword("S3cur3#Prod!MasterPass2026", isDevelopment: false);
        ProductionSecurityValidator.ValidateAdminSeedPassword("Admin123!", isDevelopment: true);
        ProductionSecurityValidator.ValidateAdminSeedPassword(null, isDevelopment: true);
    }

    #endregion

    #region 4. Rate Limiting, Proxy IP Resolution & Replay Parser Concurrency Gate

    [Fact]
    public void Controllers_ShouldHaveExpectedRateLimitingAttributes()
    {
        var authType = typeof(AuthController);
        string[] strictAuthMethods = ["Login", "RegisterWithInvite", "ForgotPassword", "ResetPassword", "ForgotLogin", "RecoverLogin"];

        foreach (var methodName in strictAuthMethods)
        {
            var attr = authType.GetMethod(methodName)!
                .GetCustomAttributes(typeof(EnableRateLimitingAttribute), inherit: true)
                .Cast<EnableRateLimitingAttribute>()
                .SingleOrDefault();

            Assert.NotNull(attr);
            Assert.Equal(ProductionSecurityValidator.RateLimitPolicyAuthStrict, attr!.PolicyName);
        }

        var previewAttr = authType.GetMethod("PreviewSteam")!
            .GetCustomAttributes(typeof(EnableRateLimitingAttribute), inherit: true)
            .Cast<EnableRateLimitingAttribute>()
            .SingleOrDefault();
        Assert.NotNull(previewAttr);
        Assert.Equal(ProductionSecurityValidator.RateLimitPolicySteamPreview, previewAttr!.PolicyName);

        var validateInviteAttr = typeof(InvitationsController).GetMethod("ValidateInvitation")!
            .GetCustomAttributes(typeof(EnableRateLimitingAttribute), inherit: true)
            .Cast<EnableRateLimitingAttribute>()
            .SingleOrDefault();
        Assert.NotNull(validateInviteAttr);
        Assert.Equal(ProductionSecurityValidator.RateLimitPolicyAuthStrict, validateInviteAttr!.PolicyName);
    }

    [Fact]
    public void RateLimitClientIpResolver_ShouldPreventSpoofingOnDirectIp_AndExtractRightmostNonPrivateBehindProxy()
    {
        // 1. Conexão direta de IP público tentando falsificar X-Forwarded-For -> ignora X-Forwarded-For!
        var directCtx = new DefaultHttpContext();
        directCtx.Connection.RemoteIpAddress = IPAddress.Parse("198.51.100.42");
        directCtx.Request.Headers["X-Forwarded-For"] = "1.2.3.4, 5.6.7.8";
        Assert.Equal("198.51.100.42", RateLimitClientIpResolver.ResolveClientIp(directCtx));

        // 2. Conexão atrás de proxy interno Render/Cloudflare (10.0.1.5) onde cliente injetou 8.8.8.8 no início:
        // X-Forwarded-For: 8.8.8.8, 203.0.113.77, 172.18.0.3 -> deve retornar 203.0.113.77!
        var proxyCtx = new DefaultHttpContext();
        proxyCtx.Connection.RemoteIpAddress = IPAddress.Parse("10.0.1.5");
        proxyCtx.Request.Headers["X-Forwarded-For"] = "8.8.8.8, 203.0.113.77, 172.18.0.3";
        Assert.Equal("203.0.113.77", RateLimitClientIpResolver.ResolveClientIp(proxyCtx));
    }

    [Fact]
    public async Task RateLimiterPolicies_ShouldEnforce10PerMinOnAuthStrict_And20PerMinOnSteamPreview_PerIp()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.Services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy(ProductionSecurityValidator.RateLimitPolicyAuthStrict, httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    RateLimitClientIpResolver.ResolveClientIp(httpContext),
                    _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));

            options.AddPolicy(ProductionSecurityValidator.RateLimitPolicySteamPreview, httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    RateLimitClientIpResolver.ResolveClientIp(httpContext),
                    _ => new FixedWindowRateLimiterOptions { PermitLimit = 20, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
        });

        await using var app = builder.Build();
        app.Urls.Add("http://127.0.0.1:0");
        app.UseRateLimiter();
        app.MapPost("/test-auth", () => Results.Ok("ok")).RequireRateLimiting(ProductionSecurityValidator.RateLimitPolicyAuthStrict);
        app.MapGet("/test-steam", () => Results.Ok("ok")).RequireRateLimiting(ProductionSecurityValidator.RateLimitPolicySteamPreview);

        await app.StartAsync();
        var baseAddress = app.Urls.Single();
        using var httpClient = new HttpClient { BaseAddress = new Uri(baseAddress) };

        try
        {
            // AuthStrict: 10 passam, a 11ª do mesmo IP bloqueia com 429, mas outro IP passa!
            for (int i = 1; i <= 10; i++)
            {
                var status = await SendWithForwardedIpAsync(httpClient, HttpMethod.Post, "/test-auth", "203.0.113.10");
                Assert.Equal(StatusCodes.Status200OK, status);
            }
            Assert.Equal(StatusCodes.Status429TooManyRequests, await SendWithForwardedIpAsync(httpClient, HttpMethod.Post, "/test-auth", "203.0.113.10"));
            Assert.Equal(StatusCodes.Status200OK, await SendWithForwardedIpAsync(httpClient, HttpMethod.Post, "/test-auth", "203.0.113.11"));

            // SteamPreview: 20 passam, a 21ª do mesmo IP bloqueia com 429!
            for (int i = 1; i <= 20; i++)
            {
                var status = await SendWithForwardedIpAsync(httpClient, HttpMethod.Get, "/test-steam", "203.0.113.20");
                Assert.Equal(StatusCodes.Status200OK, status);
            }
            Assert.Equal(StatusCodes.Status429TooManyRequests, await SendWithForwardedIpAsync(httpClient, HttpMethod.Get, "/test-steam", "203.0.113.20"));
        }
        finally
        {
            await app.StopAsync();
        }
    }

    [Fact]
    public async Task GoCliReplayParserService_WhenTwoConcurrentParsesSaturateSemaphore_ThirdShouldTimeoutCleanly()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "tc_parser_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var fakeBinaryPath = Path.Combine(tempDir, OperatingSystem.IsWindows() ? "parsegodota.exe" : "parsegodota");
        await File.WriteAllBytesAsync(fakeBinaryPath, [0x00]);

        try
        {
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ReplayParser:ExecutablePath"] = fakeBinaryPath,
                    ["ReplayParser:QueueTimeoutMs"] = "80" // 80ms para teste rápido
                })
                .Build();

            using var gate = new SemaphoreSlim(2, 2);
            var parser = new GoCliReplayParserService(config, NullLogger<GoCliReplayParserService>.Instance, gate);

            // Ocupa os 2 slots simultâneos
            await gate.WaitAsync();
            await gate.WaitAsync();
            Assert.Equal(0, gate.CurrentCount);

            // Monta um stream válido de >= 500 KB com assinatura PBDEMS2\0
            var buffer = new byte[520 * 1024];
            Encoding.ASCII.GetBytes("PBDEMS2\0").CopyTo(buffer, 0);
            using var validStream = new MemoryStream(buffer);

            // 3ª chamada concorrente deve sofrer timeout na fila e lançar DomainException sem dar Release indevido!
            var ex = await Assert.ThrowsAsync<DomainException>(() =>
                parser.ParseReplayAsync(validStream, "match.dem", CancellationToken.None));

            Assert.Contains("limite máximo de replays simultâneos", ex.Message);
            Assert.Equal(0, gate.CurrentCount); // Semáforo permanece íntegro (não houve Release indevido)

            gate.Release(2);
            Assert.Equal(2, gate.CurrentCount);
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { /* ignore */ }
        }
    }

    #endregion

    #region 5. PII Masking in GetMembersHandler & GetMemberByIdHandler

    [Fact]
    public async Task GetMembersAndGetMemberById_ShouldMaskEmailAndStatusReasonForRegularUsers_ExceptSelfAndAdmins()
    {
        var repo = new InMemoryUserRepository();
        var superAdmin = new User("super@dota.local", "hash", "SuperAdminNick", UserRole.SuperAdmin);
        var callerPlayer = new User("caller@dota.local", "hash", "CallerPlayer", UserRole.User);
        var bannedPlayer = new User("banned@secret-email.com", "hash", "BannedPlayer", UserRole.User);
        bannedPlayer.SetActive(false, "Suspenso por toxicidade em lobby", superAdmin);

        await repo.AddAsync(superAdmin);
        await repo.AddAsync(callerPlayer);
        await repo.AddAsync(bannedPlayer);

        // 1. Jogador comum (Role = User) lista membros
        var regularUserContext = new StubCurrentUserService(callerPlayer.Id, callerPlayer.Email, "User");
        var getMembersAsUser = new GetMembersHandler(repo, regularUserContext);
        var listAsUser = await getMembersAsUser.Handle(new GetMembersQuery(), CancellationToken.None);

        Assert.True(listAsUser.IsSuccess);
        var bannedDtoForUser = listAsUser.Value.Single(m => m.UserId == bannedPlayer.Id);
        Assert.Equal(string.Empty, bannedDtoForUser.Email); // E-mail de terceiros mascarado!
        Assert.Null(bannedDtoForUser.StatusReason);         // Motivo administrativo omitido!

        var selfDtoForUser = listAsUser.Value.Single(m => m.UserId == callerPlayer.Id);
        Assert.Equal("caller@dota.local", selfDtoForUser.Email); // Próprio usuário vê seu e-mail!

        // 2. Jogador comum busca outro membro por ID vs si mesmo
        var getByIdAsUser = new GetMemberByIdHandler(repo, regularUserContext);
        var otherById = await getByIdAsUser.Handle(new GetMemberByIdQuery(bannedPlayer.Id), CancellationToken.None);
        Assert.Equal(string.Empty, otherById.Value.Email);
        Assert.Null(otherById.Value.StatusReason);

        var selfById = await getByIdAsUser.Handle(new GetMemberByIdQuery(callerPlayer.Id), CancellationToken.None);
        Assert.Equal("caller@dota.local", selfById.Value.Email);

        // 3. TournamentAdmin vê Email e StatusReason de todos os membros
        var adminContext = new StubCurrentUserService(Guid.NewGuid(), "ref@dota.local", "TournamentAdmin");
        var getMembersAsAdmin = new GetMembersHandler(repo, adminContext);
        var listAsAdmin = await getMembersAsAdmin.Handle(new GetMembersQuery(), CancellationToken.None);

        var bannedDtoForAdmin = listAsAdmin.Value.Single(m => m.UserId == bannedPlayer.Id);
        Assert.Equal("banned@secret-email.com", bannedDtoForAdmin.Email);
        Assert.Equal("Suspenso por toxicidade em lobby", bannedDtoForAdmin.StatusReason);
    }

    #endregion

    #region 6. OpenDota Vanity Regex Hardening & Dockerfile Non-Root Verification

    [Theory]
    [InlineData("a")]                                              // Curto demais (< 2)
    [InlineData("abcdefghijklmnopqrstuvwxyz1234567")]              // Longo demais (33 > 32)
    [InlineData("https://steamcommunity.com/id/../../etc/passwd")] // Path traversal
    [InlineData("https://steamcommunity.com/id/evil%2fadmin")]     // URL-encoded slash
    [InlineData("evil@internal")]                                  // Caractere @ inválido
    [InlineData("nick com espaco")]                                // Espaços inválidos
    public async Task OpenDotaProfileService_ResolveSteamIdAsync_WhenVanityInvalid_ShouldNotSendHttpRequest(string maliciousInput)
    {
        var handler = new CountingHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        using var httpClient = new HttpClient(handler);
        var service = new OpenDotaProfileService(httpClient, NullLogger<OpenDotaProfileService>.Instance);

        var resolved = await service.ResolveSteamIdAsync(maliciousInput, CancellationToken.None);

        Assert.Equal(0ul, resolved);
        Assert.Equal(0, handler.CallCount); // Zero chamadas HTTP externas!
    }

    [Fact]
    public async Task OpenDotaProfileService_ResolveSteamIdAsync_WhenValidVanity_ShouldResolveAndCache()
    {
        const string xmlBody = "<profile><steamID64>76561198052079950</steamID64></profile>";
        var handler = new CountingHttpMessageHandler(req =>
        {
            Assert.Equal("https://steamcommunity.com/id/Miracle_Dota-1/?xml=1", req.RequestUri!.ToString());
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(xmlBody, Encoding.UTF8, "application/xml")
            };
        });

        using var memoryCache = new MemoryCache(new MemoryCacheOptions());
        using var httpClient = new HttpClient(handler);
        var service = new OpenDotaProfileService(httpClient, NullLogger<OpenDotaProfileService>.Instance, memoryCache);

        var first = await service.ResolveSteamIdAsync("https://steamcommunity.com/id/Miracle_Dota-1/", CancellationToken.None);
        var second = await service.ResolveSteamIdAsync("Miracle_Dota-1", CancellationToken.None);

        Assert.Equal(76561198052079950ul, first);
        Assert.Equal(76561198052079950ul, second);
        Assert.Equal(1, handler.CallCount); // Segunda chamada servida pelo IMemoryCache!
    }

    [Fact]
    public void Dockerfile_ShouldChownAppDirectoryAndSwitchToNonRootAppUidBeforeEntrypoint()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        string? dockerfilePath = null;
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "Dockerfile");
            if (File.Exists(candidate))
            {
                dockerfilePath = candidate;
                break;
            }
            dir = dir.Parent;
        }

        Assert.NotNull(dockerfilePath);
        var content = File.ReadAllText(dockerfilePath!);

        int mkdirIdx = content.IndexOf("mkdir -p /app/wwwroot/uploads/banners /app/tools/ParseGoDota/runs", StringComparison.Ordinal);
        int chownIdx = content.IndexOf("chown -R $APP_UID:$APP_UID /app", StringComparison.Ordinal);
        int userAppIdx = content.LastIndexOf("USER $APP_UID", StringComparison.Ordinal);
        int entrypointIdx = content.LastIndexOf("ENTRYPOINT", StringComparison.Ordinal);

        Assert.True(mkdirIdx > 0, "Dockerfile deve criar os diretórios de uploads e runs.");
        Assert.True(chownIdx > mkdirIdx, "Dockerfile deve executar chown -R $APP_UID:$APP_UID /app após criar os diretórios.");
        Assert.True(userAppIdx > chownIdx && userAppIdx < entrypointIdx, "Dockerfile deve definir USER $APP_UID antes do ENTRYPOINT.");
    }

    #endregion

    #region 7. Adversarial Review Regression Suite (Real Kestrel HTTP, Race Conditions & Edge Cases)

    [Fact]
    public async Task FullKestrelPipeline_EmptyBodyDraftEnroll_JwtRevocation_RoleSync_AndPiiMasking_ShouldWorkEndToEnd()
    {
        const string prodSecret = "AdversarialReviewStrongProductionJwtSecretKey2026!!";
        var userRepo = new InMemoryUserRepository();
        var tournamentRepo = new InMemoryTournamentRepository();
        var draftRepo = new InMemoryDraftSessionRepository();

        var superAdmin = new User("super@tsurus.gg", "hash", "SuperRoot", UserRole.SuperAdmin);
        var playerA = new User("playera@private.mail", "hash", "PlayerAlpha", UserRole.User);
        playerA.LinkSteamAccount(76561198011111111UL, "AlphaSteam", "https://steam/a.png", 44, null);
        playerA.SetRoles([DotaRole.Carry], DotaRole.Carry);

        var demotedAdmin = new User("demoted@private.mail", "hash", "ExAdmin", UserRole.SuperAdmin);
        demotedAdmin.LinkSteamAccount(76561198022222222UL, "ExAdminSteam", null, 55, null);

        await userRepo.AddAsync(superAdmin);
        await userRepo.AddAsync(playerA);
        await userRepo.AddAsync(demotedAdmin);

        var formatCfg = TournamentFormatConfig.Create(TournamentType.DirectPlayoffs, PlayoffFormat.SingleElimination, 4, 1).Value;
        var seriesCfg = SeriesConfig.Create(null, SeriesFormat.Bo3, SeriesFormat.Bo5).Value;
        var tournament = new Tournament("Tsurus Cup", formatCfg, seriesCfg);
        var draftSession = new DraftSession(tournament.Id, 4);
        tournament.AssignDraftSession(draftSession.Id);

        await tournamentRepo.AddAsync(tournament);
        await draftRepo.AddAsync(draftSession);

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = Environments.Production
        });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:SecretKey"] = prodSecret,
            ["Jwt:Issuer"] = "TournamentCreator",
            ["Jwt:Audience"] = "TournamentCreatorApp",
            ["Jwt:ExpiryMinutes"] = "43200",
            ["Cors:AllowedOrigins"] = "*, https://tsurus-cup.pages.dev/"
        });

        builder.Services.AddProblemDetails();
        builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
        builder.Services.AddApiVersioning(o =>
        {
            o.DefaultApiVersion = new ApiVersion(1, 0);
            o.AssumeDefaultVersionWhenUnspecified = true;
            o.ApiVersionReader = new UrlSegmentApiVersionReader();
        });
        builder.Services.AddControllers()
            .AddApplicationPart(typeof(Program).Assembly);

        builder.Services.AddCors();
        builder.Services.AddOptions<Microsoft.AspNetCore.Cors.Infrastructure.CorsOptions>()
            .Configure<IConfiguration, IHostEnvironment>((corsOptions, config, env) =>
            {
                corsOptions.AddPolicy("AllowFrontend", p =>
                {
                    ProductionSecurityValidator.ConfigureCorsPolicy(p, config["Cors:AllowedOrigins"], env.IsDevelopment());
                });
            });

        builder.Services.AddTournamentsApplication();
        builder.Services.AddIdentityApplication();
        builder.Services.AddIdentityInfrastructure(builder.Configuration);

        builder.Services.AddScoped<IUserRepository>(_ => userRepo);
        builder.Services.AddScoped<ITournamentRepository>(_ => tournamentRepo);
        builder.Services.AddScoped<IDraftSessionRepository>(_ => draftRepo);
        builder.Services.AddScoped<ITournamentRealtimeNotifier, NoOpTournamentNotifier>();

        await using var app = builder.Build();
        ProductionSecurityValidator.ValidateJwtSecret(app.Configuration["Jwt:SecretKey"], app.Environment.IsDevelopment());

        app.Urls.Add("http://127.0.0.1:0");
        app.UseExceptionHandler();
        app.UseCors("AllowFrontend");
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();

        await app.StartAsync();
        try
        {
            var tokenGen = app.Services.GetRequiredService<IJwtTokenGenerator>();
            var (playerToken, _) = tokenGen.GenerateToken(playerA);
            var (superToken, _) = tokenGen.GenerateToken(superAdmin);
            var (exAdminToken, _) = tokenGen.GenerateToken(demotedAdmin); // JWT emitido como SuperAdmin!

            using var client = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };

            // A) CORS em Produção: deve aceitar https://tsurus-cup.pages.dev (barra final normalizada) e REJEITAR https://evil.attacker.com mesmo tendo '*' na config!
            using (var evilCorsReq = new HttpRequestMessage(HttpMethod.Get, "/api/v1/users"))
            {
                evilCorsReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", playerToken);
                evilCorsReq.Headers.Add("Origin", "https://evil.attacker.com");
                using var evilCorsResp = await client.SendAsync(evilCorsReq);
                Assert.Equal(HttpStatusCode.OK, evilCorsResp.StatusCode);
                Assert.False(evilCorsResp.Headers.Contains("Access-Control-Allow-Origin"));
            }

            using (var legitCorsReq = new HttpRequestMessage(HttpMethod.Get, "/api/v1/users"))
            {
                legitCorsReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", playerToken);
                legitCorsReq.Headers.Add("Origin", "https://tsurus-cup.pages.dev");
                using var legitCorsResp = await client.SendAsync(legitCorsReq);
                Assert.Equal(HttpStatusCode.OK, legitCorsResp.StatusCode);
                Assert.Equal("https://tsurus-cup.pages.dev", legitCorsResp.Headers.GetValues("Access-Control-Allow-Origin").Single());

                // PII: Jogador comum NÃO vê e-mail do SuperAdmin, mas vê o seu próprio
                var members = await legitCorsResp.Content.ReadFromJsonAsync<List<MemberDto>>();
                Assert.NotNull(members);
                Assert.Equal(string.Empty, members!.Single(m => m.UserId == superAdmin.Id).Email);
                Assert.Equal("playera@private.mail", members.Single(m => m.UserId == playerA.Id).Email);
            }

            // B) Torneio ainda em Rascunho (Draft) -> jogador comum tentando POST sem body deve ser bloqueado pela regra de inscrições fechadas (não 400 de model binder!)
            using (var closedEnrollReq = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/draft/{draftSession.Id}/pool"))
            {
                closedEnrollReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", playerToken);
                using var closedEnrollResp = await client.SendAsync(closedEnrollReq);
                Assert.Equal(HttpStatusCode.BadRequest, closedEnrollResp.StatusCode);
                var errJson = await closedEnrollResp.Content.ReadAsStringAsync();
                Assert.Contains("inscrições não estão abertas", errJson);
            }

            // C) Admin abre as inscrições -> jogador comum envia POST REAL com body VAZIO (Content = null) -> 200 OK com dados oficiais!
            tournament.OpenRegistrations();
            await tournamentRepo.UpdateAsync(tournament);

            using (var emptyBodyReq = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/draft/{draftSession.Id}/pool"))
            {
                emptyBodyReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", playerToken);
                using var emptyBodyResp = await client.SendAsync(emptyBodyReq);
                Assert.Equal(HttpStatusCode.OK, emptyBodyResp.StatusCode);
                var snap = await emptyBodyResp.Content.ReadFromJsonAsync<DraftStateSnapshotDto>();
                Assert.NotNull(snap);
                var enrolled = Assert.Single(snap!.PlayerPool);
                Assert.Equal(playerA.Id, enrolled.Id);
                Assert.Equal("PlayerAlpha", enrolled.Name);
                Assert.Equal("76561198011111111", enrolled.ExternalUserId);
                Assert.Equal(44, enrolled.RankTier);
            }

            // D) Rebaixamento de Role: demotedAdmin tinha JWT de 30 dias com SuperAdmin, mas foi rebaixado para User no banco -> bloqueia rota [Authorize(Roles = "TournamentAdmin,SuperAdmin")] com 403!
            demotedAdmin.PromoteRole(UserRole.User, superAdmin);
            await userRepo.UpdateAsync(demotedAdmin);
            app.Services.GetRequiredService<IUserAuthStateCache>().Invalidate(demotedAdmin.Id);

            using (var demotedPatchReq = new HttpRequestMessage(HttpMethod.Patch, $"/api/v1/users/{playerA.Id}/status")
            {
                Content = JsonContent.Create(new { isActive = false, reason = "Tentativa com token antigo de SuperAdmin" })
            })
            {
                demotedPatchReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", exAdminToken);
                using var demotedResp = await client.SendAsync(demotedPatchReq);
                Assert.Equal(HttpStatusCode.Forbidden, demotedResp.StatusCode);
            }

            // E) Revogação imediata de conta desativada via PATCH real -> próxima chamada HTTP com o mesmo JWT de 30 dias falha com 401!
            using (var banReq = new HttpRequestMessage(HttpMethod.Patch, $"/api/v1/users/{playerA.Id}/status")
            {
                Content = JsonContent.Create(new { isActive = false, reason = "Conta suspensa" })
            })
            {
                banReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", superToken);
                using var banResp = await client.SendAsync(banReq);
                Assert.Equal(HttpStatusCode.OK, banResp.StatusCode);
            }

            using (var afterBanReq = new HttpRequestMessage(HttpMethod.Get, "/api/v1/users"))
            {
                afterBanReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", playerToken);
                using var afterBanResp = await client.SendAsync(afterBanReq);
                Assert.Equal(HttpStatusCode.Unauthorized, afterBanResp.StatusCode);
                var problemBody = await afterBanResp.Content.ReadAsStringAsync();
                Assert.Contains("Sua conta está desativada.", problemBody);
            }
        }
        finally
        {
            await app.StopAsync();
        }
    }

    [Fact]
    public void ProgramEntrypoint_WhenStartedInProductionWithDefaultJwtSecret_ShouldFailFastBeforeRun()
    {
        var entryPoint = typeof(Program).Assembly.EntryPoint!;
        var ex = Assert.Throws<System.Reflection.TargetInvocationException>(() =>
            entryPoint.Invoke(null, [new[] { "--environment=Production", "--Jwt:SecretKey=CHANGE_ME_IN_DEVELOPMENT_OR_ENV_MIN_32_CHARS_LONG!", "--Database:InitializeOnStartup=false" }]));

        Assert.IsType<InvalidOperationException>(ex.InnerException);
        Assert.Contains("Jwt:SecretKey", ex.InnerException!.Message);
    }

    [Fact]
    public async Task MemoryUserAuthStateCache_WhenInvalidatedMidFlight_ShouldNotCacheStaleActiveSnapshot()
    {
        using var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var cache = new MemoryUserAuthStateCache(memoryCache, TimeSpan.FromSeconds(60));
        var userId = Guid.NewGuid();

        bool dbIsActive = true;
        var firstReadEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var allowFirstReadToFinish = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int dbReads = 0;

        var inflightTask = cache.GetOrLoadAsync(userId, async _ =>
        {
            int call = Interlocked.Increment(ref dbReads);
            bool observedActive = Volatile.Read(ref dbIsActive);
            if (call == 1)
            {
                firstReadEntered.TrySetResult();
                await allowFirstReadToFinish.Task;
            }
            return new UserAuthSnapshot(Exists: true, IsActive: observedActive, Role: "User");
        });

        await firstReadEntered.Task;

        // Admin desativa no banco e invalida o cache ENQUANTO a requisição 1 ainda estava em voo!
        Volatile.Write(ref dbIsActive, false);
        cache.Invalidate(userId);

        allowFirstReadToFinish.TrySetResult();
        var inflightSnapshot = await inflightTask;

        Assert.False(inflightSnapshot.IsActive);
        Assert.Equal(2, dbReads);

        var subsequentSnapshot = await cache.GetOrLoadAsync(userId, _ =>
        {
            Interlocked.Increment(ref dbReads);
            return Task.FromResult(new UserAuthSnapshot(true, dbIsActive, "User"));
        });
        Assert.False(subsequentSnapshot.IsActive);
        Assert.Equal(2, dbReads);
    }

    [Fact]
    public async Task DraftController_AdminSelfEnrollAndAddLatePlayer_ShouldEnforceOfficialProfileAndBackfillSteamId()
    {
        var ineligibleAdminId = Guid.NewGuid();
        var ineligibleAdminDto = new MemberDto(
            ineligibleAdminId, "admin@dota.local", "CasterAdmin", "Caster", null,
            "76561198033333333", null, null, 20, null, ["HardSupport"], "HardSupport",
            "TournamentAdmin", true, IsActive: true, StatusReason: "Apenas narrador", IsTournamentEligible: false);

        var sender1 = new RecordingDraftSender(ineligibleAdminDto);
        var ctrl1 = CreateDraftControllerWithUser(sender1, ineligibleAdminId.ToString(), "TournamentAdmin");
        var selfBlocked = await ctrl1.AddPlayerToPool(
            Guid.NewGuid(),
            new AddPlayerRequest(ineligibleAdminId, "SpoofAdmin", "76561198099999999", ["Carry"], "Carry", 80, null),
            CancellationToken.None);

        var forbidden = Assert.IsType<ObjectResult>(selfBlocked.Result);
        Assert.Equal(StatusCodes.Status403Forbidden, forbidden.StatusCode);
        Assert.Null(sender1.LastAddPlayerCommand);

        var eligibleAdminId = Guid.NewGuid();
        var eligibleAdminDto = new MemberDto(
            eligibleAdminId, "admin2@dota.local", "RealAdminNick", "AdminSteam", "https://steam/real.jpg",
            "76561198044444444", null, null, 52, null, ["Offlane"], "Offlane",
            "TournamentAdmin", true, IsActive: true, StatusReason: null, IsTournamentEligible: true);

        var sender2 = new RecordingDraftSender(eligibleAdminDto);
        var ctrl2 = CreateDraftControllerWithUser(sender2, eligibleAdminId.ToString(), "TournamentAdmin");
        var selfOk = await ctrl2.AddPlayerToPool(
            Guid.NewGuid(),
            new AddPlayerRequest(eligibleAdminId, "StaleLocalStorageNick", "76561198000000001", ["Mid"], "Mid", 10, null),
            CancellationToken.None);

        Assert.IsType<OkObjectResult>(selfOk.Result);
        Assert.NotNull(sender2.LastAddPlayerCommand);
        Assert.Equal("RealAdminNick", sender2.LastAddPlayerCommand!.Name);
        Assert.Equal("76561198044444444", sender2.LastAddPlayerCommand.ExternalUserId);
        Assert.Equal(52, sender2.LastAddPlayerCommand.RankTier);

        var latePlayerId = Guid.NewGuid();
        var lateMemberDto = new MemberDto(
            latePlayerId, "late@dota.local", "LateMember", "LatePersona", "https://steam/late.jpg",
            "76561198077777777", null, null, 64, null, ["Mid"], "Mid",
            "User", true, IsActive: true, StatusReason: null, IsTournamentEligible: true);

        var sender3 = new RecordingDraftSender(lateMemberDto);
        var ctrl3 = CreateDraftControllerWithUser(sender3, eligibleAdminId.ToString(), "TournamentAdmin");
        var lateRes = await ctrl3.AddLatePlayer(
            Guid.NewGuid(),
            new AddLatePlayerRequest(latePlayerId, "LatePersona", ExternalUserId: null, Roles: ["Mid"], PreferredRole: "Mid", RankTier: 64, AvatarUrl: null, ExpectedVersion: 3),
            CancellationToken.None);

        Assert.IsType<OkObjectResult>(lateRes.Result);
        Assert.NotNull(sender3.LastAddLatePlayerCommand);
        Assert.Equal("76561198077777777", sender3.LastAddLatePlayerCommand!.ExternalUserId);
    }

    [Fact]
    public async Task AddPlayerToPoolHandler_WhenNonAdminAndDraftAlreadyAdvancedOrRegistrationsClosed_ShouldReject()
    {
        var tournamentRepo = new InMemoryTournamentRepository();
        var draftRepo = new InMemoryDraftSessionRepository();
        var notifier = new NoOpTournamentNotifier();

        var formatCfg = TournamentFormatConfig.Create(TournamentType.DirectPlayoffs, PlayoffFormat.SingleElimination, 4, 1).Value;
        var seriesCfg = SeriesConfig.Create(null, SeriesFormat.Bo3, SeriesFormat.Bo5).Value;
        var tournament = new Tournament("Closed Cup", formatCfg, seriesCfg);
        tournament.OpenRegistrations();
        tournament.CloseRegistrations();

        var session = new DraftSession(tournament.Id, 4);
        tournament.AssignDraftSession(session.Id);
        await tournamentRepo.AddAsync(tournament);
        await draftRepo.AddAsync(session);

        var handler = new AddPlayerToPoolHandler(draftRepo, notifier, null, tournamentRepo);

        await Assert.ThrowsAsync<DomainException>(() =>
            handler.Handle(new AddPlayerToPoolCommand(session.Id, Guid.NewGuid(), "LateRunner", "76561198011111111", IsAdmin: false), CancellationToken.None));

        var adminSnap = await handler.Handle(
            new AddPlayerToPoolCommand(session.Id, Guid.NewGuid(), "AdminAdded", "76561198011111112", IsAdmin: true),
            CancellationToken.None);
        Assert.Single(adminSnap.PlayerPool);
    }

    [Fact]
    public void RateLimitClientIpResolver_WhenProxyAppendsPortOrIpv6Rotation_ShouldNotBeBypassed()
    {
        var proxyWithPortCtx = new DefaultHttpContext();
        proxyWithPortCtx.Connection.RemoteIpAddress = IPAddress.Parse("10.0.1.5");
        proxyWithPortCtx.Request.Headers["X-Forwarded-For"] = "1.2.3.4, 203.0.113.50:54321";
        Assert.Equal("203.0.113.50", RateLimitClientIpResolver.ResolveClientIp(proxyWithPortCtx));

        var ipv6CtxA = new DefaultHttpContext();
        ipv6CtxA.Connection.RemoteIpAddress = IPAddress.Parse("10.0.1.5");
        ipv6CtxA.Request.Headers["X-Forwarded-For"] = "[2001:db8:abcd:1234::1]:44321";

        var ipv6CtxB = new DefaultHttpContext();
        ipv6CtxB.Connection.RemoteIpAddress = IPAddress.Parse("10.0.1.5");
        ipv6CtxB.Request.Headers["X-Forwarded-For"] = "2001:db8:abcd:1234:9999:8888:7777:6666";

        var keyA = RateLimitClientIpResolver.ResolveClientIp(ipv6CtxA);
        var keyB = RateLimitClientIpResolver.ResolveClientIp(ipv6CtxB);
        Assert.Equal("2001:db8:abcd:1234::/64", keyA);
        Assert.Equal(keyA, keyB);
    }

    [Fact]
    public void ProductionSecurityValidator_CorsAndShortAdminPassword_ShouldEnforceStrictProductionRules()
    {
        var prodWildcard = ProductionSecurityValidator.ResolveAllowedCorsOrigins("*", isDevelopment: false);
        Assert.NotNull(prodWildcard);
        Assert.Equal([ProductionSecurityValidator.DefaultProductionFrontendOrigin], prodWildcard);

        var prodTrailingSlash = ProductionSecurityValidator.ResolveAllowedCorsOrigins("https://tsurus-cup.pages.dev/, *", isDevelopment: false);
        Assert.NotNull(prodTrailingSlash);
        Assert.Equal(["https://tsurus-cup.pages.dev"], prodTrailingSlash);

        Assert.Null(ProductionSecurityValidator.ResolveAllowedCorsOrigins("*", isDevelopment: true));

        Assert.Throws<InvalidOperationException>(() =>
            ProductionSecurityValidator.ValidateAdminSeedPassword("1234567", isDevelopment: false));
    }

    [Fact]
    public async Task OpenDotaProfileService_UppercaseIdPath_NonSteamHost_TruncatedProfile_AndNullProfileCache_ShouldBehaveSafely()
    {
        const string xmlBody = "<profile><steamID64>76561198052079950</steamID64></profile>";
        int openDotaCalls = 0;
        bool profileNowPublic = false;

        var handler = new CountingHttpMessageHandler(req =>
        {
            var uri = req.RequestUri!.ToString();
            if (uri == "https://steamcommunity.com/id/Miracle_Dota-1/?xml=1")
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(xmlBody, Encoding.UTF8, "application/xml")
                };
            }

            if (uri.StartsWith("https://api.opendota.com/api/players/", StringComparison.Ordinal))
            {
                Interlocked.Increment(ref openDotaCalls);
                var json = profileNowPublic
                    ? """{"profile":{"account_id":91814222,"personaname":"Miracle-","avatarfull":"https://steam/m.jpg"},"rank_tier":80}"""
                    : """{"profile":null,"rank_tier":null}""";
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                };
            }

            throw new InvalidOperationException($"Unexpected outbound URL: {uri}");
        });

        using var cache = new MemoryCache(new MemoryCacheOptions());
        using var httpClient = new HttpClient(handler);
        var service = new OpenDotaProfileService(httpClient, NullLogger<OpenDotaProfileService>.Instance, cache);

        var upperResolved = await service.ResolveSteamIdAsync("https://steamcommunity.com/ID/Miracle_Dota-1/", CancellationToken.None);
        Assert.Equal(76561198052079950UL, upperResolved);
        Assert.Equal(1, handler.CallCount);

        var nonSteamResolved = await service.ResolveSteamIdAsync("https://evil.example.com/id/NewTargetVanity", CancellationToken.None);
        Assert.Equal(0UL, nonSteamResolved);
        Assert.Equal(1, handler.CallCount);

        var overflowProfile = await service.ResolveSteamIdAsync("https://steamcommunity.com/profiles/76561198052079950999", CancellationToken.None);
        Assert.Equal(0UL, overflowProfile);

        var firstFetch = await service.FetchProfileAsync(76561198052079950UL, CancellationToken.None);
        Assert.Equal("Player_91814222", firstFetch!.PersonaName);
        Assert.Equal(1, openDotaCalls);

        profileNowPublic = true;
        var secondFetch = await service.FetchProfileAsync(76561198052079950UL, CancellationToken.None);
        Assert.Equal("Miracle-", secondFetch!.PersonaName);
        Assert.Equal(2, openDotaCalls);

        var thirdFetch = await service.FetchProfileAsync(76561198052079950UL, CancellationToken.None);
        Assert.Equal("Miracle-", thirdFetch!.PersonaName);
        Assert.Equal(2, openDotaCalls);
    }

    [Fact]
    public async Task TournamentDraftHub_JoinUserChannel_WhenUserDeactivatedMidSession_ShouldAbortAndThrowHubException()
    {
        var userRepo = new InMemoryUserRepository();
        var superAdmin = new User("super@dota.local", "hash", "Super", UserRole.SuperAdmin);
        var player = new User("p@dota.local", "hash", "Player", UserRole.User);
        player.SetActive(false, "Banido no meio da conexão WebSocket", superAdmin);
        await userRepo.AddAsync(player);

        var services = new ServiceCollection();
        services.AddMemoryCache();
        services.AddSingleton<IUserAuthStateCache, MemoryUserAuthStateCache>();
        services.AddScoped<IUserRepository>(_ => userRepo);
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var httpContext = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        httpContext.Features.Set<Microsoft.AspNetCore.Http.Connections.Features.IHttpContextFeature>(
            new StubHttpContextFeature { HttpContext = httpContext });

        var claims = new[] { new Claim(ClaimTypes.NameIdentifier, player.Id.ToString()), new Claim(ClaimTypes.Role, "User") };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "Bearer"));

        var hubContext = new StubHubCallerContext(principal, httpContext);
        var hub = new TournamentDraftHub(NullLogger<TournamentDraftHub>.Instance)
        {
            Context = hubContext,
            Groups = new NoOpGroupManager()
        };

        var ex = await Assert.ThrowsAsync<HubException>(() => hub.JoinUserChannel(player.Id));
        Assert.Contains("Sua conta está desativada.", ex.Message);
        Assert.True(hubContext.Aborted);
    }

    [Fact]
    public void MatchesController_DockerCompose_AndLocalFileStorage_ShouldHaveUploadAndVolumeHardening()
    {
        foreach (var methodName in new[] { "UploadReplay", "UploadGameReplay" })
        {
            var method = typeof(MatchesController).GetMethod(methodName)!;
            var formLimit = method.GetCustomAttributes(typeof(RequestFormLimitsAttribute), inherit: true)
                .Cast<RequestFormLimitsAttribute>()
                .SingleOrDefault();
            Assert.NotNull(formLimit);
            Assert.Equal(350L * 1024 * 1024, formLimit!.MultipartBodyLengthLimit);
        }

        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        string? composePath = null;
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "docker-compose.yml");
            if (File.Exists(candidate))
            {
                composePath = candidate;
                break;
            }
            dir = dir.Parent;
        }

        Assert.NotNull(composePath);
        var composeContent = File.ReadAllText(composePath!);
        Assert.Contains("uploads_init:", composeContent);
        Assert.Contains("chown -R 1654:1654 /uploads", composeContent);
        Assert.Contains("condition: service_completed_successfully", composeContent);

        var tempFileBlockingDir = Path.GetTempFileName();
        try
        {
            var invalidSubFolder = Path.Combine(tempFileBlockingDir, "banners");
            var storage = new LocalFileStorageService(NullLogger<LocalFileStorageService>.Instance, invalidSubFolder);
            Assert.NotNull(storage);
        }
        finally
        {
            try { File.Delete(tempFileBlockingDir); } catch { /* ignore */ }
        }
    }

    [Theory]
    [InlineData("http://localhost:54321", true)]
    [InlineData("https://LOCALHOST:7208", true)]
    [InlineData("http://localhost", true)]
    [InlineData("http://127.0.0.1:54321", true)]
    [InlineData("https://127.0.0.1:8443", true)]
    [InlineData("http://[::1]:5000", true)]
    [InlineData("http://[::ffff:127.0.0.1]:8080", true)]
    [InlineData("http://localhost.evil.com", false)]
    [InlineData("http://127.0.0.1.evil.com", false)]
    [InlineData("http://evil.com@localhost:54321", false)]
    [InlineData("http://localhost:54321/extra-path", false)]
    [InlineData("http://localhost:54321?query=1", false)]
    [InlineData("file://localhost", false)]
    [InlineData("javascript://localhost", false)]
    [InlineData("null", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    [InlineData("https://tsurus-cup.pages.dev", false)]
    public void ProductionSecurityValidator_IsLoopbackOrigin_ShouldStrictlyValidateLoopbackAndRejectSpoofs(
        string? origin,
        bool expected)
    {
        Assert.Equal(expected, ProductionSecurityValidator.IsLoopbackOrigin(origin));
    }

    [Fact]
    public async Task CorsHttpPipeline_InDevelopmentWithoutDevOverridesAndInProduction_ShouldEnforceExpectedOriginRules()
    {
        // 1. Development sem appsettings.Development.json (herdando Cors:AllowedOrigins = "https://tsurus-cup.pages.dev")
        // Deve permitir portas dinâmicas de loopback (flutter run -d chrome) + origem configurada, mas bloquear domínios externos não configurados!
        var devBuilder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = Environments.Development
        });
        devBuilder.Configuration.Sources.Clear();
        devBuilder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Cors:AllowedOrigins"] = "https://tsurus-cup.pages.dev"
        });
        devBuilder.Services.AddCors();
        devBuilder.Services.AddOptions<Microsoft.AspNetCore.Cors.Infrastructure.CorsOptions>()
            .Configure<IConfiguration, IHostEnvironment>((corsOptions, config, env) =>
            {
                corsOptions.AddPolicy("AllowFrontend", p =>
                    ProductionSecurityValidator.ConfigureCorsPolicy(p, config["Cors:AllowedOrigins"], env.IsDevelopment()));
            });

        await using (var devApp = devBuilder.Build())
        {
            devApp.Urls.Add("http://127.0.0.1:0");
            devApp.UseCors("AllowFrontend");
            devApp.MapGet("/ping", () => Results.Ok("pong"));
            await devApp.StartAsync();

            try
            {
                using var devClient = new HttpClient { BaseAddress = new Uri(devApp.Urls.Single()) };

                foreach (var allowedDevOrigin in new[]
                {
                    "http://localhost:54321",
                    "http://127.0.0.1:54321",
                    "http://[::1]:54321",
                    "https://tsurus-cup.pages.dev"
                })
                {
                    // Preflight OPTIONS
                    using var preflightReq = new HttpRequestMessage(HttpMethod.Options, "/ping");
                    preflightReq.Headers.Add("Origin", allowedDevOrigin);
                    preflightReq.Headers.Add("Access-Control-Request-Method", "POST");
                    preflightReq.Headers.Add("Access-Control-Request-Headers", "authorization,content-type");
                    using var preflightResp = await devClient.SendAsync(preflightReq);
                    Assert.Equal(allowedDevOrigin, preflightResp.Headers.GetValues("Access-Control-Allow-Origin").Single());
                    Assert.Equal("true", preflightResp.Headers.GetValues("Access-Control-Allow-Credentials").Single());

                    // Requisição GET real
                    using var getReq = new HttpRequestMessage(HttpMethod.Get, "/ping");
                    getReq.Headers.Add("Origin", allowedDevOrigin);
                    using var getResp = await devClient.SendAsync(getReq);
                    Assert.Equal(HttpStatusCode.OK, getResp.StatusCode);
                    Assert.Equal(allowedDevOrigin, getResp.Headers.GetValues("Access-Control-Allow-Origin").Single());
                }

                foreach (var blockedDevOrigin in new[]
                {
                    "https://evil.attacker.com",
                    "http://localhost.evil.com",
                    "http://127.0.0.1.evil.com"
                })
                {
                    using var blockedReq = new HttpRequestMessage(HttpMethod.Get, "/ping");
                    blockedReq.Headers.Add("Origin", blockedDevOrigin);
                    using var blockedResp = await devClient.SendAsync(blockedReq);
                    Assert.False(blockedResp.Headers.Contains("Access-Control-Allow-Origin"));
                }
            }
            finally
            {
                await devApp.StopAsync();
            }
        }

        // 2. Production com Cors:AllowedOrigins = "https://tsurus-cup.pages.dev":
        // Deve BLOQUEAR estritamente http://localhost:54321 e http://127.0.0.1:54321, permitindo apenas https://tsurus-cup.pages.dev!
        var prodBuilder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = Environments.Production
        });
        prodBuilder.Configuration.Sources.Clear();
        prodBuilder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Cors:AllowedOrigins"] = "https://tsurus-cup.pages.dev"
        });
        prodBuilder.Services.AddCors();
        prodBuilder.Services.AddOptions<Microsoft.AspNetCore.Cors.Infrastructure.CorsOptions>()
            .Configure<IConfiguration, IHostEnvironment>((corsOptions, config, env) =>
            {
                corsOptions.AddPolicy("AllowFrontend", p =>
                    ProductionSecurityValidator.ConfigureCorsPolicy(p, config["Cors:AllowedOrigins"], env.IsDevelopment()));
            });

        await using (var prodApp = prodBuilder.Build())
        {
            prodApp.Urls.Add("http://127.0.0.1:0");
            prodApp.UseCors("AllowFrontend");
            prodApp.MapGet("/ping", () => Results.Ok("pong"));
            await prodApp.StartAsync();

            try
            {
                using var prodClient = new HttpClient { BaseAddress = new Uri(prodApp.Urls.Single()) };

                foreach (var blockedProdOrigin in new[]
                {
                    "http://localhost:54321",
                    "http://127.0.0.1:54321",
                    "http://[::1]:54321",
                    "https://evil.attacker.com"
                })
                {
                    using var preflightReq = new HttpRequestMessage(HttpMethod.Options, "/ping");
                    preflightReq.Headers.Add("Origin", blockedProdOrigin);
                    preflightReq.Headers.Add("Access-Control-Request-Method", "POST");
                    using var preflightResp = await prodClient.SendAsync(preflightReq);
                    Assert.False(preflightResp.Headers.Contains("Access-Control-Allow-Origin"));

                    using var getReq = new HttpRequestMessage(HttpMethod.Get, "/ping");
                    getReq.Headers.Add("Origin", blockedProdOrigin);
                    using var getResp = await prodClient.SendAsync(getReq);
                    Assert.False(getResp.Headers.Contains("Access-Control-Allow-Origin"));
                }

                using (var allowedProdReq = new HttpRequestMessage(HttpMethod.Get, "/ping"))
                {
                    allowedProdReq.Headers.Add("Origin", "https://tsurus-cup.pages.dev");
                    using var allowedProdResp = await prodClient.SendAsync(allowedProdReq);
                    Assert.Equal(HttpStatusCode.OK, allowedProdResp.StatusCode);
                    Assert.Equal("https://tsurus-cup.pages.dev", allowedProdResp.Headers.GetValues("Access-Control-Allow-Origin").Single());
                    Assert.Equal("true", allowedProdResp.Headers.GetValues("Access-Control-Allow-Credentials").Single());
                }
            }
            finally
            {
                await prodApp.StopAsync();
            }
        }
    }

    [Fact]
    public void TrackedConfigTemplates_ShouldNotContainHardcodedLocalPasswords()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        string? repoRoot = null;
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, ".env.example")))
            {
                repoRoot = dir.FullName;
                break;
            }
            dir = dir.Parent;
        }

        Assert.NotNull(repoRoot);

        var envExample = File.ReadAllText(Path.Combine(repoRoot!, ".env.example"));
        Assert.Contains("POSTGRES_PASSWORD=CHANGE_ME_STRONG_POSTGRES_PASSWORD", envExample);
        Assert.DoesNotContain("s3ver4lc4lam1ty", envExample, StringComparison.OrdinalIgnoreCase);

        foreach (var relativePath in new[]
        {
            "appsettings.json",
            Path.Combine("src", "Host", "TournamentCreator.WebApi", "appsettings.json"),
            "docker-compose.yml"
        })
        {
            var content = File.ReadAllText(Path.Combine(repoRoot!, relativePath));
            Assert.DoesNotContain("s3ver4lc4lam1ty", content, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Admin123!", content, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task TournamentDraftHub_JoinUserChannel_ShouldUseFreshScopeInsteadOfStaleHandshakeScope()
    {
        var superAdmin = new User("super@dota.local", "hash", "Super", UserRole.SuperAdmin);
        var playerId = Guid.NewGuid();

        bool dbCurrentlyActive = true;
        var services = new ServiceCollection();
        services.AddMemoryCache();
        services.AddSingleton<IUserAuthStateCache, MemoryUserAuthStateCache>();
        // Simula comportamento do EF Core ChangeTracker: cada instância Scoped captura o estado no momento em que o escopo foi criado!
        services.AddScoped<IUserRepository>(_ =>
        {
            bool capturedActiveAtScopeCreation = Volatile.Read(ref dbCurrentlyActive);
            var snapshotUser = new User("p@dota.local", "hash", "Player", UserRole.User);
            typeof(Entity<Guid>).GetProperty(nameof(Entity<Guid>.Id))!.SetValue(snapshotUser, playerId);
            if (!capturedActiveAtScopeCreation)
            {
                snapshotUser.SetActive(false, "Desativado durante sessão WebSocket", superAdmin);
            }

            var repo = new InMemoryUserRepository();
            repo.AddAsync(snapshotUser).GetAwaiter().GetResult();
            return repo;
        });

        using var provider = services.BuildServiceProvider();
        using var initialHandshakeScope = provider.CreateScope();
        // Força a materialização do IUserRepository antigo (ativo) no escopo de handshake do WebSocket
        _ = initialHandshakeScope.ServiceProvider.GetRequiredService<IUserRepository>();

        var httpContext = new DefaultHttpContext { RequestServices = initialHandshakeScope.ServiceProvider };
        httpContext.Features.Set<Microsoft.AspNetCore.Http.Connections.Features.IHttpContextFeature>(
            new StubHttpContextFeature { HttpContext = httpContext });

        var claims = new[] { new Claim(ClaimTypes.NameIdentifier, playerId.ToString()), new Claim(ClaimTypes.Role, "User") };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "Bearer"));
        var hubContext = new StubHubCallerContext(principal, httpContext);
        var hub = new TournamentDraftHub(NullLogger<TournamentDraftHub>.Instance)
        {
            Context = hubContext,
            Groups = new NoOpGroupManager()
        };

        // 1. Primeira chamada: usuário ativo -> sucesso
        await hub.JoinUserChannel(playerId);
        Assert.False(hubContext.Aborted);

        // 2. Usuário é desativado no banco e cache invalidado enquanto a mesma conexão WebSocket continua aberta
        Volatile.Write(ref dbCurrentlyActive, false);
        provider.GetRequiredService<IUserAuthStateCache>().Invalidate(playerId);

        // 3. Nova chamada na mesma conexão WebSocket cria novo IServiceScope -> lê estado fresco (inativo) e aborta!
        var ex = await Assert.ThrowsAsync<HubException>(() => hub.JoinUserChannel(playerId));
        Assert.Contains("Sua conta está desativada.", ex.Message);
        Assert.True(hubContext.Aborted);
    }

    [Fact]
    public void IdentityInfrastructure_InDevelopmentWithEmptyEnvJwtSecret_ShouldFallbackCleanlyAndRejectShortCustomKey()
    {
        // Simula docker-compose.yml com ASPNETCORE_ENVIRONMENT=Development e JWT_SECRET_KEY não definido (Jwt__SecretKey="")
        ProductionSecurityValidator.ValidateJwtSecret("", isDevelopment: true);

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:SecretKey"] = "",
                ["Jwt:Issuer"] = "TournamentCreator",
                ["Jwt:Audience"] = "TournamentCreatorApp",
                ["Jwt:ExpiryMinutes"] = "60"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddIdentityInfrastructure(config);

        using var provider = services.BuildServiceProvider();
        var tokenGen = provider.GetRequiredService<IJwtTokenGenerator>();
        var user = new User("dev@dota.local", "hash", "DevPlayer", UserRole.User);

        // Não deve lançar IDX10703 / IDX10720!
        var (token, _) = tokenGen.GenerateToken(user);
        Assert.False(string.IsNullOrWhiteSpace(token));

        var bearerOptions = provider.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>()
            .Get(JwtBearerDefaults.AuthenticationScheme);
        Assert.NotNull(bearerOptions.TokenValidationParameters.IssuerSigningKey);

        // Mas se uma chave customizada curta (< 32 bytes) for definida explicitamente em Development, falha rápido no startup!
        Assert.Throws<InvalidOperationException>(() =>
            ProductionSecurityValidator.ValidateJwtSecret("short-custom-dev-secret", isDevelopment: true));
    }

    [Fact]
    public async Task SteamId64Bounds_ShouldRejectUintOverflowAndSubOffset_WhileSupporting7656120Accounts_Without32BitFriendIdCollision()
    {
        var handler = new CountingHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        using var httpClient = new HttpClient(handler);
        var openDota = new OpenDotaProfileService(httpClient, NullLogger<OpenDotaProfileService>.Instance);

        // 1. Valores inválidos: base exata (accountId=0), overflow de 32 bits (colisão com Miracle 91814222), ulong.MaxValue, FriendId > uint.MaxValue, URL < base
        ulong overflowMiracle = 76561198052079950UL + (1UL << 32); // 76561202347047246
        string[] invalidInputs =
        [
            "76561197960265728",
            overflowMiracle.ToString(),
            ulong.MaxValue.ToString(),
            "999999999999",
            "https://steamcommunity.com/profiles/76561190000000000",
            $"https://steamcommunity.com/profiles/{overflowMiracle}"
        ];

        foreach (var bad in invalidInputs)
        {
            Assert.Equal(0UL, await openDota.ResolveSteamIdAsync(bad, CancellationToken.None));
        }

        Assert.Null(await openDota.FetchProfileAsync(overflowMiracle, CancellationToken.None));
        Assert.Equal(0, handler.CallCount); // Nenhuma chamada HTTP externa desperdiçada!

        // 2. Contas SteamID64 válidas na faixa 7656120... (accountId >= 2.04B até uint.MaxValue) devem ser aceitas!
        Assert.Equal(76561200000000001UL, await openDota.ResolveSteamIdAsync("https://steamcommunity.com/profiles/76561200000000001", CancellationToken.None));
        Assert.Equal(User.MaxSteamId64, await openDota.ResolveSteamIdAsync(User.MaxSteamId64.ToString(), CancellationToken.None));

        // 3. Entidade User e Validadores devem bloquear overflow de 32 bits
        var testUser = new User("u@dota.local", "hash", "Player", UserRole.User);
        Assert.Throws<DomainException>(() => testUser.LinkSteamAccount(overflowMiracle, "FakeMiracle"));
        Assert.Throws<DomainException>(() => testUser.LinkSteamAccount(User.SteamId64Base, "ZeroAcc"));

        var linkVal = new Identity.Application.Features.Auth.LinkSteam.LinkSteamValidator();
        Assert.False(linkVal.Validate(new Identity.Application.Features.Auth.LinkSteam.LinkSteamCommand(overflowMiracle)).IsValid);
        Assert.True(linkVal.Validate(new Identity.Application.Features.Auth.LinkSteam.LinkSteamCommand(76561200000000001UL)).IsValid);

        // 4. DraftSnapshotMapper e SteamIdConverter nunca truncam 64 bits inválidos em DotaFriendId de outro jogador
        var spoofedDraftPlayer = new DraftPlayer(Guid.NewGuid(), "Spoofer", overflowMiracle.ToString());
        var dto = Tournaments.Application.Features.Draft.DraftSnapshotMapper.ToDto(spoofedDraftPlayer);
        Assert.Null(dto.DotaFriendId);
        Assert.Null(Tournaments.Application.Common.Utilities.SteamIdConverter.ToAccountId(overflowMiracle.ToString()));
    }

    [Fact]
    public async Task KestrelExceptionPipeline_WhenDomainExceptionThrown_ShouldPreserveSecurityAndCorsHeaders()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = Environments.Development
        });
        builder.Configuration.Sources.Clear();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Cors:AllowedOrigins"] = "https://tsurus-cup.pages.dev"
        });
        builder.Services.AddProblemDetails();
        builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
        builder.Services.AddCors();
        builder.Services.AddOptions<Microsoft.AspNetCore.Cors.Infrastructure.CorsOptions>()
            .Configure<IConfiguration, IHostEnvironment>((corsOptions, config, env) =>
            {
                corsOptions.AddPolicy("AllowFrontend", p =>
                    ProductionSecurityValidator.ConfigureCorsPolicy(p, config["Cors:AllowedOrigins"], env.IsDevelopment()));
            });

        await using var app = builder.Build();
        app.Urls.Add("http://127.0.0.1:0");

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
        app.MapGet("/boom", () => { throw new DomainException("Erro de regra de negócio"); });

        await app.StartAsync();
        try
        {
            using var client = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };
            using var req = new HttpRequestMessage(HttpMethod.Get, "/boom");
            req.Headers.Add("Origin", "http://localhost:54321");

            using var resp = await client.SendAsync(req);
            Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
            Assert.Equal("http://localhost:54321", resp.Headers.GetValues("Access-Control-Allow-Origin").Single());
            Assert.Equal("nosniff", resp.Headers.GetValues("X-Content-Type-Options").Single());
            Assert.Equal("DENY", resp.Headers.GetValues("X-Frame-Options").Single());
            Assert.Equal("strict-origin-when-cross-origin", resp.Headers.GetValues("Referrer-Policy").Single());
        }
        finally
        {
            await app.StopAsync();
        }
    }

    [Fact]
    public async Task DraftControllerAndHub_WhenUnauthenticatedPrincipalHasClaims_OrAdminDemotedInLeaveChannel_ShouldFailClosed()
    {
        // 1. ClaimsPrincipal com claims de SuperAdmin, porém sem authenticationType (IsAuthenticated == false) -> deve falhar fechado!
        var unauthClaims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.Role, "SuperAdmin")
        };
        var unauthPrincipal = new ClaimsPrincipal(new ClaimsIdentity(unauthClaims)); // IsAuthenticated == false!
        Assert.False(unauthPrincipal.Identity!.IsAuthenticated);

        var sender = new RecordingDraftSender(memberToReturn: null);
        var controller = new DraftController(sender)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = unauthPrincipal }
            }
        };

        var addRes = await controller.AddPlayerToPool(Guid.NewGuid(), new AddPlayerRequest(Guid.NewGuid(), "FakeAdmin"), CancellationToken.None);
        var forbidden = Assert.IsType<ObjectResult>(addRes.Result);
        Assert.Equal(StatusCodes.Status403Forbidden, forbidden.StatusCode);
        Assert.Null(sender.LastAddPlayerCommand);

        await controller.RemovePlayerFromPool(Guid.NewGuid(), Guid.NewGuid(), null, CancellationToken.None);
        Assert.False(sender.LastRemovePlayerCommand!.IsAdmin);
        Assert.Equal(string.Empty, sender.LastRemovePlayerCommand.ActingUserId);

        // 2. Hub OnConnectedAsync não assina grupo pessoal se IsAuthenticated == false
        var groupTracker = new RecordingGroupManager();
        var unauthHttp = new DefaultHttpContext();
        unauthHttp.Features.Set<Microsoft.AspNetCore.Http.Connections.Features.IHttpContextFeature>(
            new StubHttpContextFeature { HttpContext = unauthHttp });
        var unauthHub = new TournamentDraftHub(NullLogger<TournamentDraftHub>.Instance)
        {
            Context = new StubHubCallerContext(unauthPrincipal, unauthHttp),
            Groups = groupTracker
        };
        await unauthHub.OnConnectedAsync();
        Assert.Empty(groupTracker.AddedGroups);

        // 3. Admin rebaixado no meio da sessão WebSocket tentando LeaveUserChannel de terceiro -> bloqueado!
        var superAdmin = new User("super@dota.local", "hash", "Super", UserRole.SuperAdmin);
        var exAdmin = new User("ex@dota.local", "hash", "ExAdmin", UserRole.User); // Já rebaixado no banco!
        var userRepo = new InMemoryUserRepository();
        await userRepo.AddAsync(superAdmin);
        await userRepo.AddAsync(exAdmin);

        var services = new ServiceCollection();
        services.AddMemoryCache();
        services.AddSingleton<IUserAuthStateCache, MemoryUserAuthStateCache>();
        services.AddScoped<IUserRepository>(_ => userRepo);
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var wsHttp = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        wsHttp.Features.Set<Microsoft.AspNetCore.Http.Connections.Features.IHttpContextFeature>(
            new StubHttpContextFeature { HttpContext = wsHttp });

        // Handshake antigo ainda carregava SuperAdmin
        var staleAdminPrincipal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, exAdmin.Id.ToString()),
            new Claim(ClaimTypes.Role, "SuperAdmin")
        ], "Bearer"));

        var wsHub = new TournamentDraftHub(NullLogger<TournamentDraftHub>.Instance)
        {
            Context = new StubHubCallerContext(staleAdminPrincipal, wsHttp),
            Groups = groupTracker
        };

        await Assert.ThrowsAsync<HubException>(() => wsHub.LeaveUserChannel(Guid.NewGuid()));
    }

    #endregion

    #region Helpers & Fakes

    private static DraftController CreateDraftControllerWithUser(ISender sender, string userIdClaim, string roleClaim)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userIdClaim),
            new Claim(ClaimTypes.Role, roleClaim)
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));

        return new DraftController(sender)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = principal }
            }
        };
    }

    private static MemberDto CreateMemberDto(Guid userId, bool isActive, bool isEligible, string? steamId)
    {
        return new MemberDto(
            UserId: userId,
            Email: "user@dota.local",
            DisplayName: "OfficialUser",
            PersonaName: "SteamNick",
            AvatarUrl: null,
            SteamId: steamId,
            SteamProfileUrl: null,
            DotaFriendId: null,
            RankTier: 40,
            LeaderboardRank: null,
            Roles: ["Mid"],
            PreferredRole: "Mid",
            Role: "User",
            IsSteamLocked: true,
            IsActive: isActive,
            StatusReason: null,
            IsTournamentEligible: isEligible
        );
    }

    private static TokenValidatedContext CreateTokenValidatedContext(
        IServiceProvider rootProvider,
        JwtBearerOptions bearerOptions,
        string userIdClaim,
        string roleClaim)
    {
        var scope = rootProvider.CreateScope();
        var httpContext = new DefaultHttpContext
        {
            RequestServices = scope.ServiceProvider
        };

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userIdClaim),
            new(ClaimTypes.Role, roleClaim)
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, JwtBearerDefaults.AuthenticationScheme));
        var scheme = new AuthenticationScheme(JwtBearerDefaults.AuthenticationScheme, JwtBearerDefaults.AuthenticationScheme, typeof(JwtBearerHandler));

        return new TokenValidatedContext(httpContext, scheme, bearerOptions)
        {
            Principal = principal
        };
    }

    private static async Task<int> SendWithForwardedIpAsync(HttpClient client, HttpMethod method, string path, string clientIp)
    {
        using var req = new HttpRequestMessage(method, path);
        req.Headers.TryAddWithoutValidation("X-Forwarded-For", clientIp);
        using var resp = await client.SendAsync(req);
        return (int)resp.StatusCode;
    }

    private sealed class RecordingDraftSender : ISender
    {
        private readonly MemberDto? _member;
        public AddPlayerToPoolCommand? LastAddPlayerCommand { get; private set; }
        public AddLatePlayerToPoolCommand? LastAddLatePlayerCommand { get; private set; }
        public RemovePlayerFromPoolCommand? LastRemovePlayerCommand { get; private set; }

        public RecordingDraftSender(MemberDto? memberToReturn)
        {
            _member = memberToReturn;
        }

        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            if (request is GetMemberByIdQuery)
            {
                var res = _member is not null
                    ? Result.Success(_member)
                    : Result.Failure<MemberDto>("Membro não encontrado.");
                return Task.FromResult((TResponse)(object)res);
            }

            if (request is AddPlayerToPoolCommand addCmd)
            {
                LastAddPlayerCommand = addCmd;
                var snap = new DraftStateSnapshotDto(
                    addCmd.DraftSessionId, Guid.NewGuid(), "SelectingCaptains", 4, 0, null, null, 2, null, null, [], [], 20, false, false, false, 16);
                return Task.FromResult((TResponse)(object)snap);
            }

            if (request is AddLatePlayerToPoolCommand lateCmd)
            {
                LastAddLatePlayerCommand = lateCmd;
                var snap = new DraftStateSnapshotDto(
                    lateCmd.DraftSessionId, Guid.NewGuid(), "DraftingStarters", 4, 0, null, null, 4, null, null, [], [], 20, false, false, false, 16);
                return Task.FromResult((TResponse)(object)snap);
            }

            if (request is RemovePlayerFromPoolCommand remCmd)
            {
                LastRemovePlayerCommand = remCmd;
                var snap = new DraftStateSnapshotDto(
                    remCmd.DraftSessionId, Guid.NewGuid(), "SelectingCaptains", 4, 0, null, null, 2, null, null, [], [], 20, false, false, false, 16);
                return Task.FromResult((TResponse)(object)snap);
            }

            throw new NotImplementedException(request.GetType().Name);
        }

        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default) where TRequest : IRequest => Task.CompletedTask;
        public Task<object?> Send(object request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }

    private sealed class CountingUserRepository : InMemoryUserRepository, IUserRepository
    {
        private int _getByIdCalls;
        public int GetByIdCalls => _getByIdCalls;

        public new async Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default)
        {
            Interlocked.Increment(ref _getByIdCalls);
            return await base.GetByIdAsync(id, ct);
        }
    }

    private sealed class StubCurrentUserService : ICurrentUserService
    {
        public Guid? UserId { get; }
        public string? Email { get; }
        public string? Role { get; }
        public bool IsAuthenticated => UserId.HasValue;
        public bool IsSuperAdmin => string.Equals(Role, "SuperAdmin", StringComparison.OrdinalIgnoreCase);
        public bool IsTournamentAdmin => string.Equals(Role, "TournamentAdmin", StringComparison.OrdinalIgnoreCase) || IsSuperAdmin;

        public StubCurrentUserService(Guid? userId, string? email, string? role)
        {
            UserId = userId;
            Email = email;
            Role = role;
        }
    }

    private sealed class CountingHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;
        private int _callCount;
        public int CallCount => _callCount;

        public CountingHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        {
            _responder = responder;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _callCount);
            return Task.FromResult(_responder(request));
        }
    }

    private sealed class NoOpTournamentNotifier : ITournamentRealtimeNotifier
    {
        public Task NotifyDraftStateSnapshotAsync(Guid tournamentId, DraftStateSnapshotDto snapshot, CancellationToken ct = default) => Task.CompletedTask;
        public Task NotifyPlayerPickedAsync(Guid tournamentId, PlayerPickedEventDto evt, CancellationToken ct = default) => Task.CompletedTask;
        public Task NotifySubstitutionStartedAsync(Guid tournamentId, SubstitutionStartedEventDto evt, CancellationToken ct = default) => Task.CompletedTask;
        public Task NotifySubstitutionConfirmedAsync(Guid tournamentId, SubstitutionConfirmedEventDto evt, CancellationToken ct = default) => Task.CompletedTask;
        public Task NotifySubstitutionCanceledAsync(Guid tournamentId, SubstitutionCanceledEventDto evt, CancellationToken ct = default) => Task.CompletedTask;
        public Task NotifyDraftCompletedAsync(Guid tournamentId, DraftCompletedEventDto evt, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class StubHttpContextFeature : Microsoft.AspNetCore.Http.Connections.Features.IHttpContextFeature
    {
        public HttpContext? HttpContext { get; set; }
    }

    private sealed class StubHubCallerContext : HubCallerContext
    {
        private readonly ClaimsPrincipal _user;
        private readonly Microsoft.AspNetCore.Http.Features.IFeatureCollection _features;
        public bool Aborted { get; private set; }

        public StubHubCallerContext(ClaimsPrincipal user, HttpContext httpContext)
        {
            _user = user;
            _features = httpContext.Features;
        }

        public override string ConnectionId => "test-conn-1";
        public override string? UserIdentifier => null;
        public override ClaimsPrincipal? User => _user;
        public override IDictionary<object, object?> Items { get; } = new Dictionary<object, object?>();
        public override Microsoft.AspNetCore.Http.Features.IFeatureCollection Features => _features;
        public override CancellationToken ConnectionAborted => CancellationToken.None;
        public override void Abort() => Aborted = true;
    }

    private sealed class NoOpGroupManager : IGroupManager
    {
        public Task AddToGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RemoveFromGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class RecordingGroupManager : IGroupManager
    {
        public List<string> AddedGroups { get; } = [];
        public Task AddToGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default)
        {
            AddedGroups.Add(groupName);
            return Task.CompletedTask;
        }
        public Task RemoveFromGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    #endregion
}
