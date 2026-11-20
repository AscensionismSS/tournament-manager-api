using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Asp.Versioning;
using BuildingBlocks.Domain;
using Identity.Application;
using Identity.Application.Common.Interfaces;
using Identity.Application.Common.Security;
using Identity.Application.Features.Auth.ApproveSteamUnlock;
using Identity.Application.Features.Auth.ForgotLogin;
using Identity.Application.Features.Auth.ForgotPassword;
using Identity.Application.Features.Auth.GetCurrentUser;
using Identity.Application.Features.Auth.GetRecoveryRequests;
using Identity.Application.Features.Auth.Login;
using Identity.Application.Features.Auth.RejectSteamUnlock;
using Identity.Application.Features.Auth.ResetPassword;
using Identity.Application.Features.Auth.UnlockSteam;
using Identity.Application.Features.Invitations.CreateInvitation;
using Identity.Application.Features.Invitations.GetInvitations;
using Identity.Application.Features.Invitations.RevokeInvitation;
using Identity.Application.Features.Invitations.ValidateInvitation;
using Identity.Application.Features.Members.GetMemberById;
using Identity.Application.Features.Members.GetMembers;
using Identity.Application.Features.Members.UpdateMemberStatus;
using Identity.Domain.Entities;
using Identity.Domain.Enums;
using Identity.Domain.Repositories;
using Identity.Infrastructure;
using Identity.Infrastructure.Repositories;
using Identity.Infrastructure.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using TournamentCreator.WebApi.Middlewares;
using TournamentCreator.WebApi.Services;
using Tournaments.Application;
using Tournaments.Application.Common.Interfaces;
using Tournaments.Contracts.Draft;
using Tournaments.Contracts.Security;
using Tournaments.Domain.Repositories;
using Tournaments.Infrastructure.Repositories;

namespace Tournaments.UnitTests;

public class RootOwnerProtectionTests
{
    private const string RootEmail = "rootowner@tournament.local";
    private readonly Pbkdf2PasswordHasher _passwordHasher = new();
    private readonly JwtTokenGenerator _jwtTokenGenerator;
    private readonly RootOwnerService _rootOwnerService = new(RootEmail);

    public RootOwnerProtectionTests()
    {
        _jwtTokenGenerator = new JwtTokenGenerator(Options.Create(new JwtOptions
        {
            SecretKey = "SuperSecretKeyForRootOwnerProtectionTests_AtLeast32Bytes!",
            Issuer = "TestIssuer",
            Audience = "TestAudience",
            ExpiryMinutes = 60
        }));
    }

    [Fact]
    public async Task UpdateMemberStatus_OtherSuperAdminCannotDeactivateRootOwner_ButRootOwnerCanDeactivateOtherSuperAdmin()
    {
        var userRepo = new InMemoryUserRepository();
        var rootOwner = new User(RootEmail, "hash", "RootOwner", UserRole.SuperAdmin);
        var otherSuperAdmin = new User("co-admin@dota.local", "hash", "CoSuperAdmin", UserRole.SuperAdmin);
        await userRepo.AddAsync(rootOwner);
        await userRepo.AddAsync(otherSuperAdmin);

        var otherService = new FakeCurrentUserService(otherSuperAdmin.Id, otherSuperAdmin.Email, "SuperAdmin");
        var handlerByOther = new UpdateMemberStatusHandler(userRepo, otherService, authStateCache: null, _rootOwnerService);

        var blockedResult = await handlerByOther.Handle(new UpdateMemberStatusCommand(rootOwner.Id, false, "Tentativa hostil"), CancellationToken.None);
        Assert.True(blockedResult.IsFailure);
        Assert.Contains("proprietário raiz", blockedResult.Error);
        Assert.True((await userRepo.GetByIdAsync(rootOwner.Id))!.IsActive);

        var rootService = new FakeCurrentUserService(rootOwner.Id, rootOwner.Email, "SuperAdmin");
        var handlerByRoot = new UpdateMemberStatusHandler(userRepo, rootService, authStateCache: null, _rootOwnerService);

        var allowedResult = await handlerByRoot.Handle(new UpdateMemberStatusCommand(otherSuperAdmin.Id, false, "Revogação de acesso"), CancellationToken.None);
        Assert.True(allowedResult.IsSuccess);
        Assert.False(allowedResult.Value.IsActive);
        Assert.False((await userRepo.GetByIdAsync(otherSuperAdmin.Id))!.IsActive);
    }

    [Fact]
    public async Task UnlockAndRecoverySteam_OtherSuperAdminBlockedOnRootOwner_WhileAllowedOnRegularPlayersAndSelfForRootOwner()
    {
        var userRepo = new InMemoryUserRepository();
        var recoveryRepo = new InMemoryAccountRecoveryRepository();

        var rootOwner = new User(RootEmail, "hash", "RootOwner", UserRole.SuperAdmin);
        rootOwner.LinkSteamAccount(76561198011111111ul, "RootSteam");

        var otherSuperAdmin = new User("co-admin@dota.local", "hash", "CoSuperAdmin", UserRole.SuperAdmin);
        var player = new User("player@dota.local", "hash", "RegularPlayer", UserRole.User);
        player.LinkSteamAccount(76561198022222222ul, "PlayerSteam");

        await userRepo.AddAsync(rootOwner);
        await userRepo.AddAsync(otherSuperAdmin);
        await userRepo.AddAsync(player);

        var rootSteamReq = new AccountRecoveryToken(rootOwner.Id, rootOwner.Email, RecoveryTokenType.SteamUnlockRequest, TimeSpan.FromDays(7));
        await recoveryRepo.AddAsync(rootSteamReq);

        var otherService = new FakeCurrentUserService(otherSuperAdmin.Id, otherSuperAdmin.Email, "SuperAdmin");

        // 1. Outro SuperAdmin NÃO pode desbloquear Steam do Root Owner diretamente
        var unlockHandlerByOther = new UnlockSteamHandler(userRepo, otherService, userRealtimeNotifier: null, _rootOwnerService);
        var blockedUnlock = await unlockHandlerByOther.Handle(new UnlockSteamCommand(rootOwner.Id), CancellationToken.None);
        Assert.True(blockedUnlock.IsFailure);
        Assert.Contains("proprietário raiz", blockedUnlock.Error);
        Assert.True((await userRepo.GetByIdAsync(rootOwner.Id))!.IsSteamLocked);

        // 2. Outro SuperAdmin NÃO pode aprovar nem recusar solicitação de troca de Steam do Root Owner
        var approveByOther = new ApproveSteamUnlockHandler(userRepo, recoveryRepo, otherService, userRealtimeNotifier: null, _rootOwnerService);
        var blockedApprove = await approveByOther.Handle(new ApproveSteamUnlockCommand(rootSteamReq.Id), CancellationToken.None);
        Assert.True(blockedApprove.IsFailure);
        Assert.Contains("proprietário raiz", blockedApprove.Error);

        var rejectByOther = new RejectSteamUnlockHandler(userRepo, recoveryRepo, otherService, _rootOwnerService);
        var blockedReject = await rejectByOther.Handle(new RejectSteamUnlockCommand(rootSteamReq.Id, "Recusa hostil"), CancellationToken.None);
        Assert.True(blockedReject.IsFailure);
        Assert.Contains("proprietário raiz", blockedReject.Error);

        // 3. Outro SuperAdmin CONTINUA podendo desbloquear Steam de jogadores comuns
        var allowedPlayerUnlock = await unlockHandlerByOther.Handle(new UnlockSteamCommand(player.Id), CancellationToken.None);
        Assert.True(allowedPlayerUnlock.IsSuccess);
        Assert.False(allowedPlayerUnlock.Value.IsSteamLocked);

        // 4. Root Owner pode desbloquear a própria Steam se precisar
        var rootService = new FakeCurrentUserService(rootOwner.Id, rootOwner.Email, "SuperAdmin");
        var unlockByRoot = new UnlockSteamHandler(userRepo, rootService, userRealtimeNotifier: null, _rootOwnerService);
        var allowedSelfUnlock = await unlockByRoot.Handle(new UnlockSteamCommand(rootOwner.Id), CancellationToken.None);
        Assert.True(allowedSelfUnlock.IsSuccess);
        Assert.False(allowedSelfUnlock.Value.IsSteamLocked);
    }

    [Fact]
    public async Task ForgotPasswordAndLogin_RootOwnerDoesNotGenerateRecoveryTokens_And_ResetPasswordRejectsRootOwnerToken()
    {
        var userRepo = new InMemoryUserRepository();
        var recoveryRepo = new InMemoryAccountRecoveryRepository();

        var originalHash = _passwordHasher.HashPassword("MinhaSenhaForte#123");
        var rootOwner = new User(RootEmail, originalHash, "RootOwner", UserRole.SuperAdmin);
        await userRepo.AddAsync(rootOwner);

        var forgotPwd = new ForgotPasswordHandler(userRepo, recoveryRepo, _rootOwnerService);
        var pwdResp = await forgotPwd.Handle(new ForgotPasswordCommand(RootEmail), CancellationToken.None);
        Assert.True(pwdResp.IsSuccess);

        var forgotLogin = new ForgotLoginHandler(userRepo, recoveryRepo, _rootOwnerService);
        var loginResp = await forgotLogin.Handle(new ForgotLoginCommand(RootEmail), CancellationToken.None);
        Assert.True(loginResp.IsSuccess);

        // Nenhum token criado para o Root Owner
        Assert.Empty(await recoveryRepo.GetAllAsync());

        // Defesa em profundidade: mesmo com token pré-existente no banco, ResetPassword bloqueia troca
        var injectedToken = new AccountRecoveryToken(rootOwner.Id, rootOwner.Email, RecoveryTokenType.PasswordReset, TimeSpan.FromHours(1));
        await recoveryRepo.AddAsync(injectedToken);

        var resetHandler = new ResetPasswordHandler(userRepo, recoveryRepo, _passwordHasher, _rootOwnerService);
        var resetResult = await resetHandler.Handle(new ResetPasswordCommand(injectedToken.Token, "SenhaDoInvasor123", "SenhaDoInvasor123"), CancellationToken.None);
        Assert.True(resetResult.IsFailure);
        Assert.True(_passwordHasher.VerifyPassword("MinhaSenhaForte#123", (await userRepo.GetByIdAsync(rootOwner.Id))!.PasswordHash));
    }

    [Fact]
    public async Task GetRecoveryRequests_HidesRootOwnerAndSuperAdminCredentialTokensFromOtherSuperAdmins_WhileShowingPlayers()
    {
        var userRepo = new InMemoryUserRepository();
        var recoveryRepo = new InMemoryAccountRecoveryRepository();

        var rootOwner = new User(RootEmail, "hash", "RootOwner", UserRole.SuperAdmin);
        var coAdmin = new User("coadmin1@dota.local", "hash", "CoAdmin1", UserRole.SuperAdmin);
        var coAdmin2 = new User("coadmin2@dota.local", "hash", "CoAdmin2", UserRole.SuperAdmin);
        var player = new User("player@dota.local", "hash", "PlayerOne", UserRole.User);

        await userRepo.AddAsync(rootOwner);
        await userRepo.AddAsync(coAdmin);
        await userRepo.AddAsync(coAdmin2);
        await userRepo.AddAsync(player);

        var rootToken = new AccountRecoveryToken(rootOwner.Id, rootOwner.Email, RecoveryTokenType.PasswordReset, TimeSpan.FromHours(1));
        var coAdmin2ResetToken = new AccountRecoveryToken(coAdmin2.Id, coAdmin2.Email, RecoveryTokenType.PasswordReset, TimeSpan.FromHours(1));
        var coAdmin2LoginToken = new AccountRecoveryToken(coAdmin2.Id, coAdmin2.Email, RecoveryTokenType.LoginRecovery, TimeSpan.FromHours(24));
        var coAdmin2SteamToken = new AccountRecoveryToken(coAdmin2.Id, coAdmin2.Email, RecoveryTokenType.SteamUnlockRequest, TimeSpan.FromDays(7));
        var playerToken = new AccountRecoveryToken(player.Id, player.Email, RecoveryTokenType.PasswordReset, TimeSpan.FromHours(1));

        await recoveryRepo.AddAsync(rootToken);
        await recoveryRepo.AddAsync(coAdmin2ResetToken);
        await recoveryRepo.AddAsync(coAdmin2LoginToken);
        await recoveryRepo.AddAsync(coAdmin2SteamToken);
        await recoveryRepo.AddAsync(playerToken);

        var coAdminService = new FakeCurrentUserService(coAdmin.Id, coAdmin.Email, "SuperAdmin");
        var handlerAsOtherSuperAdmin = new GetRecoveryRequestsHandler(recoveryRepo, userRepo, coAdminService, _rootOwnerService);

        var visibleToCoAdmin = await handlerAsOtherSuperAdmin.Handle(new GetRecoveryRequestsQuery(null), CancellationToken.None);
        Assert.DoesNotContain(visibleToCoAdmin, t => t.Id == rootToken.Id);
        Assert.DoesNotContain(visibleToCoAdmin, t => t.Id == coAdmin2ResetToken.Id);
        Assert.DoesNotContain(visibleToCoAdmin, t => t.Id == coAdmin2LoginToken.Id);
        Assert.Contains(visibleToCoAdmin, t => t.Id == coAdmin2SteamToken.Id);
        Assert.Contains(visibleToCoAdmin, t => t.Id == playerToken.Id);

        var rootService = new FakeCurrentUserService(rootOwner.Id, rootOwner.Email, "SuperAdmin");
        var handlerAsRoot = new GetRecoveryRequestsHandler(recoveryRepo, userRepo, rootService, _rootOwnerService);

        var visibleToRoot = await handlerAsRoot.Handle(new GetRecoveryRequestsQuery(null), CancellationToken.None);
        Assert.Equal(5, visibleToRoot.Count);
    }

    [Fact]
    public async Task CreateInvitation_OtherSuperAdminCanInviteTournamentAdminAndUser_ButOnlyRootOwnerCanInviteSuperAdmin()
    {
        var userRepo = new InMemoryUserRepository();
        var inviteRepo = new InMemoryInvitationRepository();

        var rootOwner = new User(RootEmail, "hash", "RootOwner", UserRole.SuperAdmin);
        var coAdmin = new User("coadmin@dota.local", "hash", "CoAdmin", UserRole.SuperAdmin);
        await userRepo.AddAsync(rootOwner);
        await userRepo.AddAsync(coAdmin);

        var coAdminService = new FakeCurrentUserService(coAdmin.Id, coAdmin.Email, "SuperAdmin");
        var coAdminHandler = new CreateInvitationHandler(inviteRepo, userRepo, coAdminService, _rootOwnerService);

        var blockedSuperInvite = await coAdminHandler.Handle(new CreateInvitationCommand("newsuper@dota.local", UserRole.SuperAdmin, 24), CancellationToken.None);
        Assert.True(blockedSuperInvite.IsFailure);
        Assert.Contains("proprietário raiz", blockedSuperInvite.Error);

        var allowedAdminInvite = await coAdminHandler.Handle(new CreateInvitationCommand("newref@dota.local", UserRole.TournamentAdmin, 24), CancellationToken.None);
        Assert.True(allowedAdminInvite.IsSuccess);

        var allowedUserInvite = await coAdminHandler.Handle(new CreateInvitationCommand("newplayer@dota.local", UserRole.User, 24), CancellationToken.None);
        Assert.True(allowedUserInvite.IsSuccess);

        var rootService = new FakeCurrentUserService(rootOwner.Id, rootOwner.Email, "SuperAdmin");
        var rootHandler = new CreateInvitationHandler(inviteRepo, userRepo, rootService, _rootOwnerService);

        var allowedSuperInvite = await rootHandler.Handle(new CreateInvitationCommand("trusted-super@dota.local", UserRole.SuperAdmin, 24), CancellationToken.None);
        Assert.True(allowedSuperInvite.IsSuccess);
        Assert.Equal("SuperAdmin", allowedSuperInvite.Value.TargetRole);
    }

    [Fact]
    public async Task AuthAndMemberDtos_ExposeIsRootOwnerFlagOnlyForRootOwner()
    {
        var userRepo = new InMemoryUserRepository();
        var password = "StrongPassword123!";
        var rootOwner = new User(RootEmail, _passwordHasher.HashPassword(password), "RootOwner", UserRole.SuperAdmin);
        var coAdmin = new User("coadmin@dota.local", _passwordHasher.HashPassword(password), "CoAdmin", UserRole.SuperAdmin);
        await userRepo.AddAsync(rootOwner);
        await userRepo.AddAsync(coAdmin);

        var loginHandler = new LoginHandler(userRepo, _passwordHasher, _jwtTokenGenerator, _rootOwnerService);
        var rootLogin = await loginHandler.Handle(new LoginCommand(RootEmail, password), CancellationToken.None);
        var coLogin = await loginHandler.Handle(new LoginCommand(coAdmin.Email, password), CancellationToken.None);

        Assert.True(rootLogin.IsSuccess);
        Assert.True(rootLogin.Value.IsRootOwner);
        Assert.True(coLogin.IsSuccess);
        Assert.False(coLogin.Value.IsRootOwner);

        var rootMe = await new GetCurrentUserHandler(userRepo, new FakeCurrentUserService(rootOwner.Id, rootOwner.Email, "SuperAdmin"), _rootOwnerService)
            .Handle(new GetCurrentUserQuery(), CancellationToken.None);
        var coMe = await new GetCurrentUserHandler(userRepo, new FakeCurrentUserService(coAdmin.Id, coAdmin.Email, "SuperAdmin"), _rootOwnerService)
            .Handle(new GetCurrentUserQuery(), CancellationToken.None);

        Assert.True(rootMe.Value.IsRootOwner);
        Assert.False(coMe.Value.IsRootOwner);

        var membersHandler = new GetMembersHandler(userRepo, new FakeCurrentUserService(coAdmin.Id, coAdmin.Email, "SuperAdmin"), _rootOwnerService);
        var members = await membersHandler.Handle(new GetMembersQuery(), CancellationToken.None);
        Assert.True(members.Value.First(m => m.UserId == rootOwner.Id).IsRootOwner);
        Assert.False(members.Value.First(m => m.UserId == coAdmin.Id).IsRootOwner);

        var memberByIdHandler = new GetMemberByIdHandler(userRepo, new FakeCurrentUserService(coAdmin.Id, coAdmin.Email, "SuperAdmin"), _rootOwnerService);
        var rootMember = await memberByIdHandler.Handle(new GetMemberByIdQuery(rootOwner.Id), CancellationToken.None);
        Assert.True(rootMember.Value.IsRootOwner);

        // Jogador comum NÃO recebe isRootOwner=true para o Root Owner em /users ou /users/{id}
        var regularPlayer = new User("regular@dota.local", _passwordHasher.HashPassword(password), "Regular", UserRole.User);
        await userRepo.AddAsync(regularPlayer);
        var playerService = new FakeCurrentUserService(regularPlayer.Id, regularPlayer.Email, "User");

        var membersForPlayer = await new GetMembersHandler(userRepo, playerService, _rootOwnerService)
            .Handle(new GetMembersQuery(), CancellationToken.None);
        Assert.False(membersForPlayer.Value.First(m => m.UserId == rootOwner.Id).IsRootOwner);

        var rootByIdForPlayer = await new GetMemberByIdHandler(userRepo, playerService, _rootOwnerService)
            .Handle(new GetMemberByIdQuery(rootOwner.Id), CancellationToken.None);
        Assert.False(rootByIdForPlayer.Value.IsRootOwner);
    }

    [Fact]
    public void EnsureRootOwnerIntegrity_RestoresActiveAndSuperAdminWithoutChangingPasswordHash()
    {
        var originalHash = _passwordHasher.HashPassword("SecretOwnerPass#99");
        var owner = new User(RootEmail, originalHash, "RootOwner", UserRole.SuperAdmin);
        var otherSuper = new User("other@dota.local", "hash", "OtherSuper", UserRole.SuperAdmin);

        owner.PromoteRole(UserRole.User, otherSuper); // Sem flag isTargetRootOwner (simula alteração direta no banco)
        owner.SetActive(false, "Desativado por erro", otherSuper);

        Assert.Equal(UserRole.User, owner.Role);
        Assert.False(owner.IsActive);

        bool changed = owner.EnsureRootOwnerIntegrity();
        Assert.True(changed);
        Assert.Equal(UserRole.SuperAdmin, owner.Role);
        Assert.True(owner.IsActive);
        Assert.Null(owner.StatusReason);
        Assert.Equal(originalHash, owner.PasswordHash);

        // Segunda chamada deve ser idempotente (false)
        Assert.False(owner.EnsureRootOwnerIntegrity());

        // Com isTargetRootOwner=true, o domínio também bloqueia demote
        Assert.Throws<DomainException>(() => owner.PromoteRole(UserRole.User, otherSuper, isTargetRootOwner: true));
    }

    [Fact]
    public async Task RealKestrelHttp_AdminSecuritySettings_BlocksOtherSuperAdminWith403AndAllowsRootOwnerWith200()
    {
        var prodSecret = "ProdJwtSecretForRootOwnerHttpTest_Minimum32Chars!";
        var userRepo = new InMemoryUserRepository();
        var inviteRepo = new InMemoryInvitationRepository();
        var securityRepo = new InMemorySystemSecuritySettingsRepository();

        var rootOwner = new User(RootEmail, "hash", "OwnerRoot", UserRole.SuperAdmin);
        var otherSuperAdmin = new User("co-admin@dota.local", "hash", "CoSuperAdmin", UserRole.SuperAdmin);
        await userRepo.AddAsync(rootOwner);
        await userRepo.AddAsync(otherSuperAdmin);

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = Environments.Production
        });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AdminSeed:Email"] = RootEmail,
            ["Jwt:SecretKey"] = prodSecret,
            ["Jwt:Issuer"] = "TournamentCreator",
            ["Jwt:Audience"] = "TournamentCreatorApp",
            ["Jwt:ExpiryMinutes"] = "43200",
            ["Cors:AllowedOrigins"] = "https://tsurus-cup.pages.dev"
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
        builder.Services.AddScoped<IInvitationRepository>(_ => inviteRepo);
        builder.Services.AddScoped<ISystemSecuritySettingsRepository>(_ => securityRepo);
        builder.Services.AddScoped<ITournamentRealtimeNotifier, NoOpNotifier>();

        await using var app = builder.Build();
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
            var (rootToken, _) = tokenGen.GenerateToken(rootOwner);
            var (otherSuperToken, _) = tokenGen.GenerateToken(otherSuperAdmin);

            using var client = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };

            var lockRequest = new UpdateSystemSecuritySettingsRequest(
                LockResultSubmission: true,
                LockTournamentRegistration: true,
                LockTournamentCreation: true,
                LockDraftPicks: true,
                MaintenanceMode: true,
                Message: "Manutenção de emergência");

            // 1. Outro SuperAdmin recebe 403 Forbidden tanto em PUT quanto em POST
            foreach (var method in new[] { HttpMethod.Put, HttpMethod.Post })
            {
                using var req = new HttpRequestMessage(method, "/api/v1/admin/security-settings")
                {
                    Content = JsonContent.Create(lockRequest)
                };
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", otherSuperToken);

                using var resp = await client.SendAsync(req);
                Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
            }

            // Garante que nenhuma trava foi alterada pelo ataque
            var currentSettings = await securityRepo.GetAsync();
            Assert.False(currentSettings.MaintenanceMode);

            // 2. Root Owner recebe 200 OK e grava alteração corretamente
            using var rootReq = new HttpRequestMessage(HttpMethod.Put, "/api/v1/admin/security-settings")
            {
                Content = JsonContent.Create(lockRequest)
            };
            rootReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", rootToken);

            using var rootResp = await client.SendAsync(rootReq);
            Assert.Equal(HttpStatusCode.OK, rootResp.StatusCode);

            var updatedDto = await rootResp.Content.ReadFromJsonAsync<SystemSecuritySettingsDto>();
            Assert.NotNull(updatedDto);
            Assert.True(updatedDto!.MaintenanceMode);
            Assert.Equal("OwnerRoot", updatedDto.UpdatedBy);

            // 3. Visitante anônimo vê manutenção/mensagem normalmente, mas sem auditoria (UpdatedBy/UpdatedAtUtc = null)
            using (var anonGetResp = await client.GetAsync("/api/v1/system/security-settings"))
            {
                Assert.Equal(HttpStatusCode.OK, anonGetResp.StatusCode);
                var anonDto = await anonGetResp.Content.ReadFromJsonAsync<SystemSecuritySettingsDto>();
                Assert.NotNull(anonDto);
                Assert.True(anonDto!.MaintenanceMode);
                Assert.Equal("Manutenção de emergência", anonDto.Message);
                Assert.Null(anonDto.UpdatedBy);
                Assert.Null(anonDto.UpdatedAtUtc);
            }

            // 4. Outro SuperAdmin vê manutenção/travas normalmente, mas sem auditoria interna do Root Owner
            using (var coAdminGetReq = new HttpRequestMessage(HttpMethod.Get, "/api/v1/admin/security-settings"))
            {
                coAdminGetReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", otherSuperToken);
                using var coAdminGetResp = await client.SendAsync(coAdminGetReq);
                Assert.Equal(HttpStatusCode.OK, coAdminGetResp.StatusCode);
                var coAdminDto = await coAdminGetResp.Content.ReadFromJsonAsync<SystemSecuritySettingsDto>();
                Assert.NotNull(coAdminDto);
                Assert.True(coAdminDto!.MaintenanceMode);
                Assert.Null(coAdminDto.UpdatedBy);
                Assert.Null(coAdminDto.UpdatedAtUtc);
            }

            // 5. Root Owner vê tudo, inclusive UpdatedBy e UpdatedAtUtc
            using (var rootGetReq = new HttpRequestMessage(HttpMethod.Get, "/api/v1/admin/security-settings"))
            {
                rootGetReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", rootToken);
                using var rootGetResp = await client.SendAsync(rootGetReq);
                Assert.Equal(HttpStatusCode.OK, rootGetResp.StatusCode);
                var rootDto = await rootGetResp.Content.ReadFromJsonAsync<SystemSecuritySettingsDto>();
                Assert.NotNull(rootDto);
                Assert.True(rootDto!.MaintenanceMode);
                Assert.Equal("OwnerRoot", rootDto.UpdatedBy);
                Assert.NotNull(rootDto.UpdatedAtUtc);
            }

            // 6. Com MaintenanceMode ativo, criação de convite (POST /invitations) e cadastro (POST /auth/register-invite) ficam bloqueados (503)
            using (var blockedInviteReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/invitations")
            {
                Content = JsonContent.Create(new { email = "novo@dota.local", targetRole = UserRole.User, validHours = 24 })
            })
            {
                blockedInviteReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", otherSuperToken);
                using var blockedInviteResp = await client.SendAsync(blockedInviteReq);
                Assert.Equal(HttpStatusCode.ServiceUnavailable, blockedInviteResp.StatusCode);
            }

            using (var blockedRegisterReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/register-invite")
            {
                Content = JsonContent.Create(new
                {
                    code = "any-code",
                    email = "novo@dota.local",
                    displayName = "NovoPlayer",
                    password = "Password123!",
                    steamId = 76561198044444444ul
                })
            })
            {
                using var blockedRegisterResp = await client.SendAsync(blockedRegisterReq);
                Assert.Equal(HttpStatusCode.ServiceUnavailable, blockedRegisterResp.StatusCode);
            }

            // 7. Ao desligar MaintenanceMode como Root Owner, criação de convite volta a funcionar (201 Created)
            var unlockRequest = new UpdateSystemSecuritySettingsRequest(
                LockResultSubmission: false,
                LockTournamentRegistration: false,
                LockTournamentCreation: false,
                LockDraftPicks: false,
                MaintenanceMode: false,
                Message: null);

            using (var unlockReq = new HttpRequestMessage(HttpMethod.Put, "/api/v1/admin/security-settings")
            {
                Content = JsonContent.Create(unlockRequest)
            })
            {
                unlockReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", rootToken);
                using var unlockResp = await client.SendAsync(unlockReq);
                Assert.Equal(HttpStatusCode.OK, unlockResp.StatusCode);
            }

            using (var allowedInviteReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/invitations")
            {
                Content = JsonContent.Create(new { email = "liberado@dota.local", targetRole = UserRole.User, validHours = 24 })
            })
            {
                allowedInviteReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", otherSuperToken);
                using var allowedInviteResp = await client.SendAsync(allowedInviteReq);
                Assert.Equal(HttpStatusCode.Created, allowedInviteResp.StatusCode);
            }
        }
        finally
        {
            await app.StopAsync();
        }
    }

    [Theory]
    [InlineData("gab@gmail.com", "ga***@g***.com")]
    [InlineData("gabriel.ferreira.2026@gmail.com", "ga***@g***.com")]
    [InlineData("a@b.io", "a***@b***.io")]
    [InlineData("ab@hotmail.com", "a***@h***.com")]
    [InlineData("invalido", "***")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void EmailMasker_ShouldUseFixedLengthMask_WithoutLeakingOriginalLength(string? input, string expected)
    {
        Assert.Equal(expected, EmailMasker.Mask(input));
    }

    [Fact]
    public async Task TotalEmailMasking_ShouldMaskEmailsAcrossMembersInvitationsRecoveryAndUnlockSteam_AndBlockSearchOracle()
    {
        var userRepo = new InMemoryUserRepository();
        var inviteRepo = new InMemoryInvitationRepository();
        var recoveryRepo = new InMemoryAccountRecoveryRepository();

        var rootOwner = new User(RootEmail, _passwordHasher.HashPassword("Pass123!"), "RootOwner", UserRole.SuperAdmin);
        var otherSuperAdmin = new User("segredo-super@protonmail.com", _passwordHasher.HashPassword("Pass123!"), "CoAdmin", UserRole.SuperAdmin);
        var player = new User("gabriel.ferreira.segredo@gmail.com", _passwordHasher.HashPassword("Pass123!"), "Gabriel", UserRole.User);
        player.LinkSteamAccount(76561198033333333ul, "GabSteam");

        await userRepo.AddAsync(rootOwner);
        await userRepo.AddAsync(otherSuperAdmin);
        await userRepo.AddAsync(player);

        var rootService = new FakeCurrentUserService(rootOwner.Id, rootOwner.Email, "SuperAdmin");
        var coAdminService = new FakeCurrentUserService(otherSuperAdmin.Id, otherSuperAdmin.Email, "SuperAdmin");

        // 1. Membros: GetMembers, GetMemberById e UpdateMemberStatus mascaram e-mail até para RootOwner e SuperAdmin
        var members = await new GetMembersHandler(userRepo, rootService, _rootOwnerService)
            .Handle(new GetMembersQuery(), CancellationToken.None);
        Assert.Equal("ga***@g***.com", members.Value.Single(m => m.UserId == player.Id).Email);
        Assert.Equal("se***@p***.com", members.Value.Single(m => m.UserId == otherSuperAdmin.Id).Email);
        Assert.Equal("ro***@t***.local", members.Value.Single(m => m.UserId == rootOwner.Id).Email);

        var memberById = await new GetMemberByIdHandler(userRepo, coAdminService, _rootOwnerService)
            .Handle(new GetMemberByIdQuery(player.Id), CancellationToken.None);
        Assert.Equal("ga***@g***.com", memberById.Value.Email);

        var statusResult = await new UpdateMemberStatusHandler(userRepo, rootService, authStateCache: null, _rootOwnerService)
            .Handle(new UpdateMemberStatusCommand(player.Id, false, "Teste"), CancellationToken.None);
        Assert.Equal("ga***@g***.com", statusResult.Value.Email);

        // 2. Convites: Create, Get, Validate e Revoke retornam e-mail mascarado enquanto banco preserva e-mail completo
        var createInvite = await new CreateInvitationHandler(inviteRepo, userRepo, coAdminService, _rootOwnerService)
            .Handle(new CreateInvitationCommand("convidado.privado@outlook.com", UserRole.User, 24), CancellationToken.None);
        Assert.True(createInvite.IsSuccess);
        Assert.Equal("co***@o***.com", createInvite.Value.Email);

        var savedInvite = await inviteRepo.GetByCodeAsync(createInvite.Value.Code);
        Assert.NotNull(savedInvite);
        Assert.Equal("convidado.privado@outlook.com", savedInvite!.Email);

        var getInvites = await new GetInvitationsHandler(inviteRepo)
            .Handle(new GetInvitationsQuery(), CancellationToken.None);
        Assert.Equal("co***@o***.com", Assert.Single(getInvites).Email);

        var validateInvite = await new ValidateInvitationHandler(inviteRepo)
            .Handle(new ValidateInvitationQuery(createInvite.Value.Code), CancellationToken.None);
        Assert.True(validateInvite.Value.IsValid);
        Assert.Equal("co***@o***.com", validateInvite.Value.Email);

        var revokeInvite = await new RevokeInvitationHandler(inviteRepo, coAdminService)
            .Handle(new RevokeInvitationCommand(createInvite.Value.Id), CancellationToken.None);
        Assert.Equal("co***@o***.com", revokeInvite.Value.Email);

        // 3. Recovery Requests: retorna e-mail mascarado e bloqueia search-oracle por fragmento de e-mail
        var playerResetToken = new AccountRecoveryToken(player.Id, player.Email, RecoveryTokenType.PasswordReset, TimeSpan.FromHours(1));
        await recoveryRepo.AddAsync(playerResetToken);

        var recoveryHandler = new GetRecoveryRequestsHandler(recoveryRepo, userRepo, rootService, _rootOwnerService);
        var allRecovery = await recoveryHandler.Handle(new GetRecoveryRequestsQuery(null), CancellationToken.None);
        Assert.Equal("ga***@g***.com", Assert.Single(allRecovery).Email);

        var oracleAttempt = await recoveryHandler.Handle(new GetRecoveryRequestsQuery("segredo"), CancellationToken.None);
        Assert.Empty(oracleAttempt);

        // 4. UnlockSteam: admin desbloqueando outro jogador recebe e-mail mascarado no DTO
        var unlockResult = await new UnlockSteamHandler(userRepo, rootService, userRealtimeNotifier: null, _rootOwnerService)
            .Handle(new UnlockSteamCommand(player.Id), CancellationToken.None);
        Assert.True(unlockResult.IsSuccess);
        Assert.Equal("ga***@g***.com", unlockResult.Value.Email);
    }

    private sealed class FakeCurrentUserService : ICurrentUserService
    {
        public Guid? UserId { get; }
        public string? Email { get; }
        public string? Role { get; }
        public bool IsAuthenticated => UserId.HasValue;
        public bool IsSuperAdmin => string.Equals(Role, "SuperAdmin", StringComparison.OrdinalIgnoreCase);
        public bool IsTournamentAdmin => string.Equals(Role, "TournamentAdmin", StringComparison.OrdinalIgnoreCase) || IsSuperAdmin;

        public FakeCurrentUserService(Guid? userId, string? email, string? role)
        {
            UserId = userId;
            Email = email;
            Role = role;
        }
    }

    private sealed class NoOpNotifier : ITournamentRealtimeNotifier
    {
        public Task NotifyDraftStateSnapshotAsync(Guid tournamentId, DraftStateSnapshotDto snapshot, CancellationToken ct = default) => Task.CompletedTask;
        public Task NotifyPlayerPickedAsync(Guid tournamentId, PlayerPickedEventDto evt, CancellationToken ct = default) => Task.CompletedTask;
        public Task NotifySubstitutionStartedAsync(Guid tournamentId, SubstitutionStartedEventDto evt, CancellationToken ct = default) => Task.CompletedTask;
        public Task NotifySubstitutionConfirmedAsync(Guid tournamentId, SubstitutionConfirmedEventDto evt, CancellationToken ct = default) => Task.CompletedTask;
        public Task NotifySubstitutionCanceledAsync(Guid tournamentId, SubstitutionCanceledEventDto evt, CancellationToken ct = default) => Task.CompletedTask;
        public Task NotifyDraftCompletedAsync(Guid tournamentId, DraftCompletedEventDto evt, CancellationToken ct = default) => Task.CompletedTask;
        public Task NotifySecuritySettingsChangedAsync(SystemSecuritySettingsDto settings, CancellationToken ct = default) => Task.CompletedTask;
    }
}
