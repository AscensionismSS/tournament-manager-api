using BuildingBlocks.Domain;
using Identity.Application.Common.Interfaces;
using Identity.Application.Features.Auth.GetCurrentUser;
using Identity.Application.Features.Auth.LinkSteam;
using Identity.Application.Features.Auth.Login;
using Identity.Application.Features.Auth.RegisterWithInvitation;
using Identity.Application.Features.Auth.UnlockSteam;
using Identity.Application.Features.Invitations.CreateInvitation;
using Identity.Contracts.Steam;
using Identity.Domain.Entities;
using Identity.Domain.Enums;
using Identity.Infrastructure.Repositories;
using Identity.Infrastructure.Security;
using Microsoft.Extensions.Options;

namespace Tournaments.UnitTests;

public class IdentityUnitTests
{
    private readonly Pbkdf2PasswordHasher _passwordHasher;
    private readonly JwtTokenGenerator _jwtTokenGenerator;

    public IdentityUnitTests()
    {
        _passwordHasher = new Pbkdf2PasswordHasher();
        var jwtOptions = Options.Create(new JwtOptions
        {
            SecretKey = "SuperSecretKeyForUnitTestingOnly12345678901234567890!",
            Issuer = "TestIssuer",
            Audience = "TestAudience",
            ExpiryMinutes = 60
        });
        _jwtTokenGenerator = new JwtTokenGenerator(jwtOptions);
    }

    [Fact]
    public void PasswordHasher_ShouldHashAndVerifyCorrectly()
    {
        string rawPassword = "MySecurePassword123!";
        string hash = _passwordHasher.HashPassword(rawPassword);

        Assert.NotEmpty(hash);
        Assert.True(_passwordHasher.VerifyPassword(rawPassword, hash));
        Assert.False(_passwordHasher.VerifyPassword("WrongPassword", hash));
    }

    [Fact]
    public void User_LinkSteam_ShouldLockAccount_And_ThrowIfAlreadyLocked()
    {
        var user = new User("player@dota.local", "hashed_pass", "Player One", UserRole.User);
        Assert.False(user.IsSteamLocked);
        Assert.Null(user.SteamId);

        ulong steamId = 76561198000000001ul;
        user.LinkSteamAccount(steamId, "DotaPro", "https://avatar.url", 52, null);

        Assert.True(user.IsSteamLocked);
        Assert.Equal(steamId, user.SteamId);
        Assert.Equal("DotaPro", user.DotaPersonaName);
        Assert.Equal(52, user.DotaRankTier);

        // Tentativa de vincular novamente enquanto bloqueado deve lançar exceção
        Assert.Throws<DomainException>(() => user.LinkSteamAccount(76561198000000002ul, "OtherName"));
    }

    [Fact]
    public void User_UnlockSteam_ShouldRequireSuperAdmin()
    {
        var superAdmin = new User("admin@dota.local", "hash", "Admin", UserRole.SuperAdmin);
        var regularAdmin = new User("ref@dota.local", "hash", "Referee", UserRole.TournamentAdmin);
        var player = new User("player@dota.local", "hash", "Player", UserRole.User);

        player.LinkSteamAccount(76561198000000001ul, "DotaPro");
        Assert.True(player.IsSteamLocked);

        // TournamentAdmin comum não pode destravar a Steam
        Assert.Throws<DomainException>(() => player.UnlockSteam(regularAdmin));

        // SuperAdmin pode destravar a Steam
        player.UnlockSteam(superAdmin);
        Assert.False(player.IsSteamLocked);
        Assert.Null(player.SteamId);
    }

    [Fact]
    public void Invitation_GenerateCode_And_Accept_ShouldWorkCorrectly()
    {
        var invitation = new Invitation("invited@dota.local", UserRole.User, Guid.NewGuid());
        Assert.NotEmpty(invitation.Code);
        Assert.Equal(32, invitation.Code.Length); // 16 bytes em Hex
        Assert.False(invitation.IsUsed);
        Assert.False(invitation.IsExpired);

        var registeredUserId = Guid.NewGuid();
        invitation.Accept(registeredUserId);

        Assert.True(invitation.IsUsed);
        Assert.Equal(registeredUserId, invitation.UsedByUserId);

        // Não pode aceitar convite já utilizado
        Assert.Throws<DomainException>(() => invitation.Accept(Guid.NewGuid()));
    }

    [Fact]
    public void JwtTokenGenerator_ShouldProduceValidToken()
    {
        var user = new User("jwt@dota.local", "hash", "JwtUser", UserRole.TournamentAdmin);
        user.LinkSteamAccount(76561198000000099ul, "JwtPersona");

        var (token, expiresAtUtc) = _jwtTokenGenerator.GenerateToken(user);

        Assert.NotEmpty(token);
        Assert.True(expiresAtUtc > DateTime.UtcNow);
    }

    [Fact]
    public async Task LoginHandler_ShouldSucceed_WithValidCredentials()
    {
        var userRepo = new InMemoryUserRepository();
        string rawPassword = "StrongPassword123!";
        var user = new User("login@dota.local", _passwordHasher.HashPassword(rawPassword), "LoginUser", UserRole.User);
        await userRepo.AddAsync(user);

        var handler = new LoginHandler(userRepo, _passwordHasher, _jwtTokenGenerator);

        var result = await handler.Handle(new LoginCommand("login@dota.local", rawPassword), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(user.Id, result.Value.UserId);
        Assert.NotEmpty(result.Value.Token);
    }

    [Fact]
    public async Task LoginHandler_ShouldFail_WithInvalidPassword()
    {
        var userRepo = new InMemoryUserRepository();
        var user = new User("badpass@dota.local", _passwordHasher.HashPassword("RealPassword123!"), "User", UserRole.User);
        await userRepo.AddAsync(user);

        var handler = new LoginHandler(userRepo, _passwordHasher, _jwtTokenGenerator);

        var result = await handler.Handle(new LoginCommand("badpass@dota.local", "WrongPassword!"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Credenciais inválidas.", result.Error);
    }

    [Fact]
    public async Task RegisterWithInvitation_WithoutSteamId_ShouldFail()
    {
        var userRepo = new InMemoryUserRepository();
        var inviteRepo = new InMemoryInvitationRepository();

        var adminId = Guid.NewGuid();
        var invite = new Invitation("newplayer@dota.local", UserRole.User, adminId);
        await inviteRepo.AddAsync(invite);

        var fakeDota = new FakeOpenDotaProfileService(0, "", 0);
        var handler = new RegisterWithInvitationHandler(inviteRepo, userRepo, _passwordHasher, _jwtTokenGenerator, fakeDota);

        var result = await handler.Handle(
            new RegisterWithInvitationCommand(invite.Code, "PlayerPass123!", "Novo Jogador", "newplayer@dota.local", SteamId: null),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("vinculação da conta Steam é obrigatória", result.Error);
    }

    [Fact]
    public async Task RegisterWithInvitation_WithSteamId_ShouldCreateUser_And_LinkSteamDirectly()
    {
        var userRepo = new InMemoryUserRepository();
        var inviteRepo = new InMemoryInvitationRepository();

        var adminId = Guid.NewGuid();
        var invite = new Invitation("steamplayer@dota.local", UserRole.User, adminId);
        await inviteRepo.AddAsync(invite);

        ulong steamId = 76561198032934030ul;
        var fakeDota = new FakeOpenDotaProfileService(steamId, "DotaWarrior", 75);
        var handler = new RegisterWithInvitationHandler(inviteRepo, userRepo, _passwordHasher, _jwtTokenGenerator, fakeDota);

        var result = await handler.Handle(
            new RegisterWithInvitationCommand(invite.Code, "PlayerPass123!", "DotaWarrior", "steamplayer@dota.local", steamId),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.IsSteamLinked);
        Assert.True(result.Value.IsSteamLocked);
        Assert.Equal(steamId, result.Value.SteamId);
        Assert.Equal("DotaWarrior", result.Value.DotaPersonaName);
        Assert.Equal(75, result.Value.DotaRankTier);
    }

    [Fact]
    public async Task RegisterWithInvitation_WithMismatchedEmail_ShouldFail()
    {
        var userRepo = new InMemoryUserRepository();
        var inviteRepo = new InMemoryInvitationRepository();

        var adminId = Guid.NewGuid();
        var invite = new Invitation("invited@dota.local", UserRole.User, adminId);
        await inviteRepo.AddAsync(invite);

        var fakeDota = new FakeOpenDotaProfileService(0, "", 0);
        var handler = new RegisterWithInvitationHandler(inviteRepo, userRepo, _passwordHasher, _jwtTokenGenerator, fakeDota);

        var result = await handler.Handle(
            new RegisterWithInvitationCommand(invite.Code, "PlayerPass123!", "Invasor", "outroemail@dota.local"),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("O email informado não corresponde ao email deste convite.", result.Error);

        // Convite NÃO pode ter sido consumido
        var currentInvite = await inviteRepo.GetByCodeAsync(invite.Code);
        Assert.NotNull(currentInvite);
        Assert.False(currentInvite.IsUsed);
    }

    [Fact]
    public async Task LinkSteamHandler_ShouldLockSteam_And_BlockDuplicateSteam()
    {
        var userRepo = new InMemoryUserRepository();
        var user1 = new User("u1@dota.local", "hash", "User 1", UserRole.User);
        var user2 = new User("u2@dota.local", "hash", "User 2", UserRole.User);
        await userRepo.AddAsync(user1);
        await userRepo.AddAsync(user2);

        ulong steamId = 76561198012345678ul;
        var fakeOpenDota = new FakeOpenDotaProfileService(steamId, "DotaProUser", 63);

        // User 1 vincula a Steam
        var currentUserService = new FakeCurrentUserService(user1.Id, user1.Email, "User");
        var handler = new LinkSteamHandler(userRepo, currentUserService, fakeOpenDota);

        var result1 = await handler.Handle(new LinkSteamCommand(steamId), CancellationToken.None);
        Assert.True(result1.IsSuccess);
        Assert.True(result1.Value.IsSteamLocked);
        Assert.Equal(steamId, result1.Value.SteamId);
        Assert.Equal(63, result1.Value.DotaRankTier);

        // User 2 tenta vincular a MESMA Steam -> Deve falhar!
        var user2Service = new FakeCurrentUserService(user2.Id, user2.Email, "User");
        var handler2 = new LinkSteamHandler(userRepo, user2Service, fakeOpenDota);

        var result2 = await handler2.Handle(new LinkSteamCommand(steamId), CancellationToken.None);
        Assert.True(result2.IsFailure);
        Assert.Equal("Esta conta Steam já está vinculada a outro usuário na plataforma.", result2.Error);
    }

    [Fact]
    public async Task UnlockSteamHandler_ShouldOnlyAllow_SuperAdmin()
    {
        var userRepo = new InMemoryUserRepository();
        var superAdmin = new User("super@dota.local", "hash", "Super", UserRole.SuperAdmin);
        var refAdmin = new User("ref@dota.local", "hash", "Referee", UserRole.TournamentAdmin);
        var player = new User("player@dota.local", "hash", "Player", UserRole.User);
        player.LinkSteamAccount(76561198099999999ul, "Pro");

        await userRepo.AddAsync(superAdmin);
        await userRepo.AddAsync(refAdmin);
        await userRepo.AddAsync(player);

        // Juiz comum tenta destravar -> Falha
        var refService = new FakeCurrentUserService(refAdmin.Id, refAdmin.Email, "TournamentAdmin");
        var handlerRef = new UnlockSteamHandler(userRepo, refService);
        var resultRef = await handlerRef.Handle(new UnlockSteamCommand(player.Id), CancellationToken.None);
        Assert.True(resultRef.IsFailure);

        // SuperAdmin tenta destravar -> Sucesso
        var superService = new FakeCurrentUserService(superAdmin.Id, superAdmin.Email, "SuperAdmin");
        var handlerSuper = new UnlockSteamHandler(userRepo, superService);
        var resultSuper = await handlerSuper.Handle(new UnlockSteamCommand(player.Id), CancellationToken.None);
        Assert.True(resultSuper.IsSuccess);
        Assert.False(resultSuper.Value.IsSteamLocked);
        Assert.Null(resultSuper.Value.SteamId);
    }

    [Fact]
    public void User_SetRoles_ShouldAutoAssignFlex_WhenAllFiveRolesSelected()
    {
        var user = new User("player@dota.local", "hash", "Player", UserRole.User);

        // Seleciona as 5 funções básicas
        var roles = new[] { DotaRole.Carry, DotaRole.Mid, DotaRole.Offlane, DotaRole.SoftSupport, DotaRole.HardSupport };
        user.SetRoles(roles, DotaRole.Mid);

        // Deve conter Flex automaticamente
        Assert.Contains(DotaRole.Flex, user.Roles);
        Assert.Equal(DotaRole.Mid, user.PreferredRole);
    }

    [Fact]
    public void User_SetRoles_ShouldFail_WhenPreferredRoleNotAmongSelected()
    {
        var user = new User("player@dota.local", "hash", "Player", UserRole.User);

        // Escolheu apenas Carry e Offlane, mas tentou marcar Mid como favorito
        var ex = Assert.Throws<DomainException>(() =>
            user.SetRoles(new[] { DotaRole.Carry, DotaRole.Offlane }, DotaRole.Mid)
        );

        Assert.Contains("precisa ser uma das funções selecionadas", ex.Message);
    }

    [Fact]
    public async Task UpdateUserRolesHandler_ShouldUpdateRoles_Successfully()
    {
        var userRepo = new InMemoryUserRepository();
        var user = new User("mid@dota.local", "hash", "MidGod", UserRole.User);
        await userRepo.AddAsync(user);

        var currentUserService = new FakeCurrentUserService(user.Id, user.Email, "User");
        var handler = new Identity.Application.Features.Auth.UpdateRoles.UpdateUserRolesHandler(userRepo, currentUserService);

        var command = new Identity.Application.Features.Auth.UpdateRoles.UpdateUserRolesCommand(
            new[] { DotaRole.Mid, DotaRole.Carry },
            DotaRole.Mid
        );

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Contains("Mid", result.Value.Roles!);
        Assert.Contains("Carry", result.Value.Roles!);
        Assert.Equal("Mid", result.Value.PreferredRole);
    }

    [Fact]
    public async Task GetInvitationsHandler_ShouldReturnAllInvitations_WithEmailAndStatus()
    {
        var repo = new InMemoryInvitationRepository();
        var invite1 = new Invitation("player1@dota.local", UserRole.User, Guid.NewGuid());
        var invite2 = new Invitation("player2@dota.local", UserRole.User, Guid.NewGuid());
        invite2.Accept(Guid.NewGuid()); // Usado

        await repo.AddAsync(invite1);
        await repo.AddAsync(invite2);

        var handler = new Identity.Application.Features.Invitations.GetInvitations.GetInvitationsHandler(repo);
        var result = await handler.Handle(new Identity.Application.Features.Invitations.GetInvitations.GetInvitationsQuery(), CancellationToken.None);

        Assert.Equal(2, result.Count);
        Assert.Contains(result, i => i.Email == "player1@dota.local" && !i.IsUsed);
        Assert.Contains(result, i => i.Email == "player2@dota.local" && i.IsUsed);
        Assert.All(result, i => Assert.Equal(32, i.Code.Length)); // Código criptográfico de 32 chars
    }

    [Fact]
    public async Task RevokeInvitationHandler_ShouldRevoke_And_PreventAccepting()
    {
        var repo = new InMemoryInvitationRepository();
        var invite = new Invitation("cancelme@dota.local", UserRole.User, Guid.NewGuid());
        await repo.AddAsync(invite);

        var currentUserService = new FakeCurrentUserService(Guid.NewGuid(), "admin@dota.local", "TournamentAdmin");
        var handler = new Identity.Application.Features.Invitations.RevokeInvitation.RevokeInvitationHandler(repo, currentUserService);

        // Desativa o convite
        var revokeResult = await handler.Handle(new Identity.Application.Features.Invitations.RevokeInvitation.RevokeInvitationCommand(invite.Id), CancellationToken.None);
        Assert.True(revokeResult.IsSuccess);
        Assert.True(revokeResult.Value.IsRevoked);

        // Tentar aceitar convite desativado -> Deve lançar DomainException
        var ex = Assert.Throws<DomainException>(() => invite.Accept(Guid.NewGuid()));
        Assert.Contains("desativado pelo administrador", ex.Message);
    }

    [Fact]
    public async Task LoginHandler_ShouldSucceed_WhenUsingDisplayNameInsteadOfEmail()
    {
        var userRepo = new InMemoryUserRepository();
        string rawPassword = "StrongPassword123!";
        var user = new User("player@dota.local", _passwordHasher.HashPassword(rawPassword), "Miracle-", UserRole.User);
        await userRepo.AddAsync(user);

        var handler = new LoginHandler(userRepo, _passwordHasher, _jwtTokenGenerator);

        // Faz login usando o Nickname/DisplayName ("Miracle-") em vez do email
        var result = await handler.Handle(new LoginCommand("Miracle-", rawPassword), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("player@dota.local", result.Value.Email);
        Assert.Equal("Miracle-", result.Value.DisplayName);
    }

    [Fact]
    public async Task ForgotPassword_ShouldNotLeakEmailExistence_WhenEmailNotFound()
    {
        var userRepo = new InMemoryUserRepository();
        var recoveryRepo = new InMemoryAccountRecoveryRepository();
        var handler = new Identity.Application.Features.Auth.ForgotPassword.ForgotPasswordHandler(userRepo, recoveryRepo);

        // Requisita recuperação para um e-mail que NÃO existe no banco
        var result = await handler.Handle(new Identity.Application.Features.Auth.ForgotPassword.ForgotPasswordCommand("naoexiste@dota.local"), CancellationToken.None);

        // Anti-enumeração: Deve retornar sucesso genérico para não vazar se o e-mail existe
        Assert.True(result.IsSuccess);
        Assert.Contains("Se o e-mail informado estiver cadastrado", result.Value.Message);

        // Nenhum token deve ter sido gerado
        var tokens = await recoveryRepo.GetAllAsync();
        Assert.Empty(tokens);
    }

    [Fact]
    public async Task ForgotPassword_And_ResetPassword_FullCycle_ShouldSucceed()
    {
        var userRepo = new InMemoryUserRepository();
        var recoveryRepo = new InMemoryAccountRecoveryRepository();
        var user = new User("jogador@dota.local", _passwordHasher.HashPassword("SenhaAntiga123!"), "Topson", UserRole.User);
        await userRepo.AddAsync(user);

        // 1. Solicita redefinição
        var forgotHandler = new Identity.Application.Features.Auth.ForgotPassword.ForgotPasswordHandler(userRepo, recoveryRepo);
        var forgotResult = await forgotHandler.Handle(new Identity.Application.Features.Auth.ForgotPassword.ForgotPasswordCommand("jogador@dota.local"), CancellationToken.None);
        Assert.True(forgotResult.IsSuccess);

        var tokens = await recoveryRepo.GetAllAsync();
        Assert.Single(tokens);
        var resetToken = tokens.First();
        Assert.Equal(64, resetToken.Token.Length); // 32 bytes em hex
        Assert.False(resetToken.IsUsed);

        // 2. Tenta redefinir com senhas divergentes -> Falha
        var resetHandler = new Identity.Application.Features.Auth.ResetPassword.ResetPasswordHandler(userRepo, recoveryRepo, _passwordHasher);
        var mismatchResult = await resetHandler.Handle(new Identity.Application.Features.Auth.ResetPassword.ResetPasswordCommand(resetToken.Token, "NovaSenha123!", "OutraSenha999!"), CancellationToken.None);
        Assert.True(mismatchResult.IsFailure);
        Assert.Equal("A nova senha e a confirmação de senha não conferem.", mismatchResult.Error);

        // 3. Redefine com confirmação correta -> Sucesso
        var successResult = await resetHandler.Handle(new Identity.Application.Features.Auth.ResetPassword.ResetPasswordCommand(resetToken.Token, "NovaSenha123!", "NovaSenha123!"), CancellationToken.None);
        Assert.True(successResult.IsSuccess);

        // 4. Token deve estar marcado como consumido
        Assert.True(resetToken.IsUsed);
        Assert.NotNull(resetToken.UsedAtUtc);

        // 5. Tenta reutilizar o token -> Deve falhar (uso único)
        var reuseResult = await resetHandler.Handle(new Identity.Application.Features.Auth.ResetPassword.ResetPasswordCommand(resetToken.Token, "Outra123!", "Outra123!"), CancellationToken.None);
        Assert.True(reuseResult.IsFailure);
        Assert.Equal("Link de redefinição de senha inválido ou expirado.", reuseResult.Error);

        // 6. Faz login com a NOVA senha -> Sucesso
        var loginHandler = new LoginHandler(userRepo, _passwordHasher, _jwtTokenGenerator);
        var loginResult = await loginHandler.Handle(new LoginCommand("jogador@dota.local", "NovaSenha123!"), CancellationToken.None);
        Assert.True(loginResult.IsSuccess);
    }

    [Fact]
    public async Task ForgotLogin_And_RecoverLogin_FullCycle_ShouldSucceed()
    {
        var userRepo = new InMemoryUserRepository();
        var recoveryRepo = new InMemoryAccountRecoveryRepository();
        var user = new User("esquecido@dota.local", _passwordHasher.HashPassword("Senha123!"), "LoginDoJogador", UserRole.User);
        await userRepo.AddAsync(user);

        // 1. Solicita recuperação de login
        var forgotLoginHandler = new Identity.Application.Features.Auth.ForgotLogin.ForgotLoginHandler(userRepo, recoveryRepo);
        var forgotResult = await forgotLoginHandler.Handle(new Identity.Application.Features.Auth.ForgotLogin.ForgotLoginCommand("esquecido@dota.local"), CancellationToken.None);
        Assert.True(forgotResult.IsSuccess);

        var tokens = await recoveryRepo.GetAllAsync();
        var loginToken = Assert.Single(tokens);

        // 2. Consulta o login com o token válido
        var recoverHandler = new Identity.Application.Features.Auth.RecoverLogin.RecoverLoginHandler(userRepo, recoveryRepo);
        var recoverResult = await recoverHandler.Handle(new Identity.Application.Features.Auth.RecoverLogin.RecoverLoginQuery(loginToken.Token), CancellationToken.None);

        Assert.True(recoverResult.IsSuccess);
        Assert.Equal("LoginDoJogador", recoverResult.Value.Login);
        Assert.Equal("esquecido@dota.local", recoverResult.Value.Email);
    }

    [Fact]
    public async Task AdminPreferences_UpdateAndGet_ByAdmin_ShouldSucceed()
    {
        var userRepo = new InMemoryUserRepository();
        var admin = new User("admin@dota.local", _passwordHasher.HashPassword("Admin123!"), "AdminUser", UserRole.SuperAdmin);
        await userRepo.AddAsync(admin);

        var currentUser = new FakeCurrentUserService(admin.Id, admin.Email, "SuperAdmin");

        // 1. Atualiza preferência para Admin
        var updateHandler = new Identity.Application.Features.Preferences.UpdateGeneralPreferences.UpdateGeneralPreferencesHandler(userRepo, currentUser);
        var updateResult = await updateHandler.Handle(new Identity.Application.Features.Preferences.UpdateGeneralPreferences.UpdateGeneralPreferencesCommand("Admin"), CancellationToken.None);
        Assert.True(updateResult.IsSuccess);
        Assert.Equal("Admin", updateResult.Value.PreferredLandingPage);

        // 2. Consulta preferência
        var getHandler = new Identity.Application.Features.Preferences.GetGeneralPreferences.GetGeneralPreferencesHandler(userRepo, currentUser);
        var getResult = await getHandler.Handle(new Identity.Application.Features.Preferences.GetGeneralPreferences.GetGeneralPreferencesQuery(), CancellationToken.None);
        Assert.True(getResult.IsSuccess);
        Assert.Equal("Admin", getResult.Value.PreferredLandingPage);
    }

    [Fact]
    public async Task AdminPreferences_Update_ByRegularUser_ShouldFail()
    {
        var userRepo = new InMemoryUserRepository();
        var regularUser = new User("user@dota.local", _passwordHasher.HashPassword("User123!"), "RegularUser", UserRole.User);
        await userRepo.AddAsync(regularUser);

        var currentUser = new FakeCurrentUserService(regularUser.Id, regularUser.Email, "User");

        var updateHandler = new Identity.Application.Features.Preferences.UpdateGeneralPreferences.UpdateGeneralPreferencesHandler(userRepo, currentUser);
        var updateResult = await updateHandler.Handle(new Identity.Application.Features.Preferences.UpdateGeneralPreferences.UpdateGeneralPreferencesCommand("Admin"), CancellationToken.None);
        
        Assert.True(updateResult.IsFailure);
        Assert.Contains("Apenas administradores podem configurar preferências gerais.", updateResult.Error);
    }

    [Fact]
    public void UserPreferences_DomainValidation_InvalidValue_ShouldThrowDomainException()
    {
        Assert.Throws<BuildingBlocks.Domain.DomainException>(() => new UserPreferences("InvalidPage"));
        Assert.Throws<BuildingBlocks.Domain.DomainException>(() => new UserPreferences(""));
    }

    [Theory]
    [InlineData(24)]
    [InlineData(48)]
    [InlineData(72)]
    public void Invitation_AllowedHours_ShouldCreateSuccessfully_WithCorrectExpiration(int hours)
    {
        var invite = new Invitation("test@dota.local", UserRole.User, Guid.NewGuid(), TimeSpan.FromHours(hours));

        Assert.Equal(hours, invite.ValidHours);
        Assert.False(invite.IsExpired);
        Assert.True(invite.ExpiresAtUtc > invite.CreatedAtUtc);
        var diff = invite.ExpiresAtUtc - invite.CreatedAtUtc;
        Assert.Equal(hours, Math.Round(diff.TotalHours));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(12)]
    [InlineData(36)]
    [InlineData(96)]
    [InlineData(168)]
    public void Invitation_DisallowedHours_ShouldThrowDomainException(int hours)
    {
        var ex = Assert.Throws<DomainException>(() =>
            new Invitation("test@dota.local", UserRole.User, Guid.NewGuid(), TimeSpan.FromHours(hours)));
        Assert.Contains("24, 48 ou 72 horas", ex.Message);
    }

    [Fact]
    public void CreateInvitationValidator_ShouldValidateHoursCorrectly()
    {
        var validator = new CreateInvitationValidator();

        var valid24 = validator.Validate(new CreateInvitationCommand("valid@dota.local", UserRole.User, 24));
        Assert.True(valid24.IsValid);

        var valid48 = validator.Validate(new CreateInvitationCommand("valid@dota.local", UserRole.User, 48));
        Assert.True(valid48.IsValid);

        var valid72 = validator.Validate(new CreateInvitationCommand("valid@dota.local", UserRole.User, 72));
        Assert.True(valid72.IsValid);

        var invalidHours = validator.Validate(new CreateInvitationCommand("valid@dota.local", UserRole.User, 10));
        Assert.False(invalidHours.IsValid);
        Assert.Contains(invalidHours.Errors, e => e.PropertyName == "ValidHours");
    }

    [Theory]
    [InlineData(24)]
    [InlineData(48)]
    [InlineData(72)]
    public async Task CreateInvitationHandler_ShouldCreateInvitation_WithValidHours(int hours)
    {
        var inviteRepo = new InMemoryInvitationRepository();
        var userRepo = new InMemoryUserRepository();
        var adminId = Guid.NewGuid();
        var currentUserService = new FakeCurrentUserService(adminId, "super@dota.local", "SuperAdmin");

        var handler = new CreateInvitationHandler(inviteRepo, userRepo, currentUserService);
        var result = await handler.Handle(new CreateInvitationCommand("player@dota.local", UserRole.User, hours), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(hours, result.Value.ValidHours);
        Assert.Equal("player@dota.local", result.Value.Email);
    }

    [Fact]
    public async Task CreateInvitationHandler_ShouldFail_WhenHoursInvalid()
    {
        var inviteRepo = new InMemoryInvitationRepository();
        var userRepo = new InMemoryUserRepository();
        var adminId = Guid.NewGuid();
        var currentUserService = new FakeCurrentUserService(adminId, "super@dota.local", "SuperAdmin");

        var handler = new CreateInvitationHandler(inviteRepo, userRepo, currentUserService);
        var result = await handler.Handle(new CreateInvitationCommand("player@dota.local", UserRole.User, 15), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("24, 48 ou 72 horas", result.Error);
    }

    [Fact]
    public async Task UpdateMemberStatus_ShouldDeactivateUser_And_BlockLogin()
    {
        var userRepo = new InMemoryUserRepository();
        var admin = new User("admin@dota.local", "hash", "Admin", UserRole.TournamentAdmin);
        var player = new User("player@dota.local", _passwordHasher.HashPassword("Pass123!"), "Player", UserRole.User);
        await userRepo.AddAsync(admin);
        await userRepo.AddAsync(player);

        var adminService = new FakeCurrentUserService(admin.Id, admin.Email, admin.Role.ToString());
        var statusHandler = new Identity.Application.Features.Members.UpdateMemberStatus.UpdateMemberStatusHandler(userRepo, adminService);

        var result = await statusHandler.Handle(
            new Identity.Application.Features.Members.UpdateMemberStatus.UpdateMemberStatusCommand(player.Id, false, "Conduta tóxica"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.IsActive);
        Assert.Equal("Conduta tóxica", result.Value.StatusReason);

        // Tentativa de login deve ser bloqueada
        var loginHandler = new LoginHandler(userRepo, _passwordHasher, _jwtTokenGenerator);
        var loginResult = await loginHandler.Handle(new LoginCommand("player@dota.local", "Pass123!"), CancellationToken.None);

        Assert.True(loginResult.IsFailure);
        Assert.Contains("desativada", loginResult.Error);
    }


    [Fact]
    public async Task ApproveSteamUnlockHandler_ShouldUnlockSteam_And_ConsumeToken()
    {
        var userRepo = new InMemoryUserRepository();
        var recoveryRepo = new InMemoryAccountRecoveryRepository();

        var superAdmin = new User("super@dota.local", "hash", "SuperAdmin", UserRole.SuperAdmin);
        var player = new User("player@dota.local", "hash", "Player", UserRole.User);
        player.LinkSteamAccount(76561198032934030ul, "DotaPro");
        await userRepo.AddAsync(superAdmin);
        await userRepo.AddAsync(player);

        var token = new AccountRecoveryToken(player.Id, player.Email, RecoveryTokenType.SteamUnlockRequest, TimeSpan.FromDays(7));
        await recoveryRepo.AddAsync(token);

        var superService = new FakeCurrentUserService(superAdmin.Id, superAdmin.Email, "SuperAdmin");
        var approveHandler = new Identity.Application.Features.Auth.ApproveSteamUnlock.ApproveSteamUnlockHandler(userRepo, recoveryRepo, superService);

        var result = await approveHandler.Handle(
            new Identity.Application.Features.Auth.ApproveSteamUnlock.ApproveSteamUnlockCommand(token.Id),
            CancellationToken.None);

        Assert.True(result.IsSuccess);

        var updatedPlayer = await userRepo.GetByIdAsync(player.Id);
        Assert.NotNull(updatedPlayer);
        Assert.False(updatedPlayer.IsSteamLocked);
        Assert.Null(updatedPlayer.SteamId);

        var updatedToken = await recoveryRepo.GetByIdAsync(token.Id);
        Assert.NotNull(updatedToken);
        Assert.True(updatedToken.IsUsed);
    }

    [Fact]
    public async Task RejectSteamUnlockHandler_ShouldRejectToken_WithoutUnlockingSteam()
    {
        var userRepo = new InMemoryUserRepository();
        var recoveryRepo = new InMemoryAccountRecoveryRepository();

        var admin = new User("admin@dota.local", "hash", "Admin", UserRole.TournamentAdmin);
        var player = new User("player@dota.local", "hash", "Player", UserRole.User);
        player.LinkSteamAccount(76561198032934030ul, "DotaPro");
        await userRepo.AddAsync(admin);
        await userRepo.AddAsync(player);

        var token = new AccountRecoveryToken(player.Id, player.Email, RecoveryTokenType.SteamUnlockRequest, TimeSpan.FromDays(7));
        await recoveryRepo.AddAsync(token);

        var adminService = new FakeCurrentUserService(admin.Id, admin.Email, "TournamentAdmin");
        var rejectHandler = new Identity.Application.Features.Auth.RejectSteamUnlock.RejectSteamUnlockHandler(userRepo, recoveryRepo, adminService);

        var result = await rejectHandler.Handle(
            new Identity.Application.Features.Auth.RejectSteamUnlock.RejectSteamUnlockCommand(token.Id, "Documentação insuficiente"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Contains("recusada", result.Value.Message);

        // A Steam do jogador DEVE continuar vinculada/travada
        var updatedPlayer = await userRepo.GetByIdAsync(player.Id);
        Assert.NotNull(updatedPlayer);
        Assert.True(updatedPlayer.IsSteamLocked);
        Assert.Equal(76561198032934030ul, updatedPlayer.SteamId);

        // O token deve estar marcado como IsUsed para não aparecer mais como pendente
        var updatedToken = await recoveryRepo.GetByIdAsync(token.Id);
        Assert.NotNull(updatedToken);
        Assert.True(updatedToken.IsUsed);
    }


    private sealed class FakeOpenDotaProfileService : IOpenDotaProfileService
    {
        private readonly ulong _expectedSteamId;
        private readonly string _personaName;
        private readonly int _rankTier;

        public FakeOpenDotaProfileService(ulong expectedSteamId, string personaName, int rankTier)
        {
            _expectedSteamId = expectedSteamId;
            _personaName = personaName;
            _rankTier = rankTier;
        }

        public Task<ulong> ResolveSteamIdAsync(string identifierOrUrl, CancellationToken ct = default)
        {
            if (ulong.TryParse(identifierOrUrl, out ulong directId))
            {
                return Task.FromResult(directId);
            }
            return Task.FromResult(_expectedSteamId);
        }

        public Task<DotaProfileLookupResult?> FetchProfileAsync(ulong steamId, CancellationToken ct = default)
        {
            if (steamId == _expectedSteamId)
            {
                return Task.FromResult<DotaProfileLookupResult?>(new DotaProfileLookupResult(
                    steamId,
                    _personaName,
                    "https://avatar.url/avatar.jpg",
                    _rankTier,
                    null));
            }

            return Task.FromResult<DotaProfileLookupResult?>(null);
        }
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
}
