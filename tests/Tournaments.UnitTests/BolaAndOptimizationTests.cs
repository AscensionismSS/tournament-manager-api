using BuildingBlocks.Domain;
using FluentValidation;
using Identity.Application;
using Identity.Application.Features.Auth.RegisterWithInvitation;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using TournamentCreator.WebApi.Extensions;
using Tournaments.Application.Features.Draft.RemovePlayerFromPool;
using Tournaments.Domain.Entities;
using Tournaments.Domain.Enums;
using Tournaments.Domain.ValueObjects;
using Tournaments.Infrastructure.Repositories;
using Xunit;

namespace Tournaments.UnitTests;

public class BolaAndOptimizationTests
{
    [Fact]
    public void DraftSession_RemovePlayerFromPool_AllowedWhenActingUserIsSamePlayer()
    {
        var session = new DraftSession(Guid.NewGuid(), 4);
        var playerId = Guid.NewGuid();
        session.AddPlayerToPool(playerId, "Player 1", externalUserId: "user-123");

        // Player removes themselves (not admin, matching ExternalUserId)
        session.RemovePlayerFromPool(playerId, actingUserId: "user-123", isAdmin: false);

        Assert.DoesNotContain(session.PlayerPool, p => p.Id == playerId);
    }

    [Fact]
    public void DraftSession_RemovePlayerFromPool_AllowedWhenUserIsAdmin_EvenWithDifferentExternalUserId()
    {
        var session = new DraftSession(Guid.NewGuid(), 4);
        var playerId = Guid.NewGuid();
        session.AddPlayerToPool(playerId, "Player 1", externalUserId: "user-123");

        // Admin removes player
        session.RemovePlayerFromPool(playerId, actingUserId: "admin-456", isAdmin: true);

        Assert.DoesNotContain(session.PlayerPool, p => p.Id == playerId);
    }

    [Fact]
    public void DraftSession_RemovePlayerFromPool_ThrowsDomainException_WhenActingUserDoesNotMatch_AndNotAdmin()
    {
        var session = new DraftSession(Guid.NewGuid(), 4);
        var playerId = Guid.NewGuid();
        session.AddPlayerToPool(playerId, "Player 1", externalUserId: "user-123");

        // Malicious user attempts BOLA/IDOR attack
        var ex = Assert.Throws<DomainException>(() =>
            session.RemovePlayerFromPool(playerId, actingUserId: "attacker-666", isAdmin: false));

        Assert.Contains("permissão", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(session.PlayerPool, p => p.Id == playerId);
    }

    [Fact]
    public void DraftSession_RemovePlayerFromPool_ThrowsDomainException_WhenPlayerHasNullExternalUserId_AndNotAdmin()
    {
        var session = new DraftSession(Guid.NewGuid(), 4);
        var playerId = Guid.NewGuid();
        session.AddPlayerToPool(playerId, "Player 1", externalUserId: null);

        var ex = Assert.Throws<DomainException>(() =>
            session.RemovePlayerFromPool(playerId, actingUserId: "any-user", isAdmin: false));

        Assert.Contains("permissão", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DraftSession_RemovePlayerFromPool_ThrowsDomainException_WhenStateAdvancedBeyondCaptains()
    {
        var session = new DraftSession(Guid.NewGuid(), 4);
        var cap1Id = Guid.NewGuid();
        var cap2Id = Guid.NewGuid();
        var cap3Id = Guid.NewGuid();
        var cap4Id = Guid.NewGuid();
        var starterId = Guid.NewGuid();

        session.AddPlayerToPool(cap1Id, "Cap 1", externalUserId: "cap-1");
        session.AddPlayerToPool(cap2Id, "Cap 2", externalUserId: "cap-2");
        session.AddPlayerToPool(cap3Id, "Cap 3", externalUserId: "cap-3");
        session.AddPlayerToPool(cap4Id, "Cap 4", externalUserId: "cap-4");
        session.AddPlayerToPool(starterId, "Starter 1", externalUserId: "starter-1");

        session.SelectCaptain(cap1Id, session.DraftVersion, session.Teams.ElementAt(0).Id);
        session.SelectCaptain(cap2Id, session.DraftVersion, session.Teams.ElementAt(1).Id);
        session.SelectCaptain(cap3Id, session.DraftVersion, session.Teams.ElementAt(2).Id);
        session.SelectCaptain(cap4Id, session.DraftVersion, session.Teams.ElementAt(3).Id);
        session.AdvanceToCaptainOrderSelection(session.DraftVersion);

        var ex = Assert.Throws<DomainException>(() =>
            session.RemovePlayerFromPool(starterId, actingUserId: "starter-1", isAdmin: true));

        Assert.Contains("Não é permitido cancelar a inscrição após o avanço do draft", ex.Message);
    }

    [Fact]
    public void DraftSession_RemovePlayerFromPool_ThrowsDomainException_WhenPlayerAllocatedToTeam()
    {
        var session = new DraftSession(Guid.NewGuid(), 4);
        var cap1Id = Guid.NewGuid();
        session.AddPlayerToPool(cap1Id, "Cap 1", externalUserId: "cap-1");
        session.SelectCaptain(cap1Id, session.DraftVersion, session.Teams.ElementAt(0).Id);

        // Captain is already allocated to team 0
        var ex = Assert.Throws<DomainException>(() =>
            session.RemovePlayerFromPool(cap1Id, actingUserId: "cap-1", isAdmin: true));

        Assert.Contains("já alocado em uma equipe", ex.Message);
    }

    [Fact]
    public async Task RemovePlayerFromPoolHandler_DelegatesCorrectly_AndReturnsSnapshot()
    {
        var draftRepo = new InMemoryDraftSessionRepository();
        var tourneyRepo = new InMemoryTournamentRepository();
        var notifier = new TestRealtimeNotifier();

        var tournament = new Tournament("Test Cup",
            TournamentFormatConfig.Create(TournamentType.DirectPlayoffs, PlayoffFormat.SingleElimination, 4).Value!,
            SeriesConfig.Create(SeriesFormat.Bo1, SeriesFormat.Bo3, SeriesFormat.Bo3).Value!);
        await tourneyRepo.AddAsync(tournament);

        var session = new DraftSession(tournament.Id, 4);
        var playerId = Guid.NewGuid();
        session.AddPlayerToPool(playerId, "P1", externalUserId: "user-p1");
        await draftRepo.AddAsync(session);

        var handler = new RemovePlayerFromPoolHandler(draftRepo, tourneyRepo, notifier);
        var cmd = new RemovePlayerFromPoolCommand(session.Id, playerId, ActingUserId: "user-p1", IsAdmin: false);

        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.NotNull(result);
        Assert.DoesNotContain(result.PlayerPool, p => p.Id == playerId);
    }

    [Fact]
    public void ResultExtensions_ToActionResult_Success_ReturnsOkObjectResult()
    {
        var result = Result.Success("test value");
        var actionResult = result.ToActionResult("Custom Title");

        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        Assert.Equal(StatusCodes.Status200OK, okResult.StatusCode);
        Assert.Equal("test value", okResult.Value);
    }

    [Fact]
    public void ResultExtensions_ToActionResult_Failure_ReturnsBadRequestWithProblemDetails()
    {
        var result = Result.Failure<string>("Nome inválido.");
        var actionResult = result.ToActionResult("Falha de Validação");

        var badResult = Assert.IsType<BadRequestObjectResult>(actionResult.Result);
        Assert.Equal(StatusCodes.Status400BadRequest, badResult.StatusCode);

        var details = Assert.IsType<ProblemDetails>(badResult.Value);
        Assert.Equal("Falha de Validação", details.Title);
        Assert.Equal("Nome inválido.", details.Detail);
    }

    [Fact]
    public void ResultExtensions_ToActionResult_Failure_ReturnsNotFoundWhenSpecified()
    {
        var result = Result.Failure<string>("Item não encontrado.");
        var actionResult = result.ToActionResult("Não Encontrado", StatusCodes.Status404NotFound);

        var notFoundResult = Assert.IsType<NotFoundObjectResult>(actionResult.Result);
        Assert.Equal(StatusCodes.Status404NotFound, notFoundResult.StatusCode);

        var details = Assert.IsType<ProblemDetails>(notFoundResult.Value);
        Assert.Equal("Não Encontrado", details.Title);
    }

    [Fact]
    public async Task IdentityApplication_ValidationBehavior_ThrowsValidationException_WhenCommandInvalid()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddIdentityApplication();

        // Stub repos/services required by RegisterWithInvitationHandler
        services.AddScoped<Identity.Domain.Repositories.IInvitationRepository>(_ => new Identity.Infrastructure.Repositories.InMemoryInvitationRepository());
        services.AddScoped<Identity.Domain.Repositories.IUserRepository>(_ => new Identity.Infrastructure.Repositories.InMemoryUserRepository());
        services.AddScoped<Identity.Application.Common.Interfaces.IPasswordHasher>(_ => new FakePasswordHasher());
        services.AddScoped<Identity.Application.Common.Interfaces.IJwtTokenGenerator>(_ => new FakeJwtGen());
        services.AddScoped<Identity.Application.Common.Interfaces.IOpenDotaProfileService>(_ => new FakeDotaService());

        using var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        // Invalid command: display name too short (< 2)
        var invalidCmd = new RegisterWithInvitationCommand(
            Code: "valid-code",
            Password: "Password123!",
            DisplayName: "A", // too short!
            Email: "player@dota.local",
            SteamId: 76561198000000001ul
        );

        var ex = await Assert.ThrowsAsync<ValidationException>(() => mediator.Send(invalidCmd));
        Assert.Contains(ex.Errors, e => e.PropertyName == nameof(RegisterWithInvitationCommand.DisplayName));
    }

    [Fact]
    public void DraftSession_RemovePlayerFromPool_AllowedWhenCasingOrWhitespaceDiffers()
    {
        var session = new DraftSession(Guid.NewGuid(), 4);
        var playerId = Guid.NewGuid();
        session.AddPlayerToPool(playerId, "Player 1", externalUserId: "user-abc-123");

        // Player removes themselves with uppercase and trailing whitespace in token claim
        session.RemovePlayerFromPool(playerId, actingUserId: "  USER-ABC-123  ", isAdmin: false);

        Assert.DoesNotContain(session.PlayerPool, p => p.Id == playerId);
    }

    [Fact]
    public void RemovePlayerFromPoolValidator_FailsWhenSessionOrPlayerEmpty()
    {
        var validator = new RemovePlayerFromPoolValidator();

        var invalidCmd = new RemovePlayerFromPoolCommand(Guid.Empty, Guid.Empty, "user-1", false, -1);
        var result = validator.Validate(invalidCmd);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(RemovePlayerFromPoolCommand.DraftSessionId));
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(RemovePlayerFromPoolCommand.PlayerId));
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(RemovePlayerFromPoolCommand.ExpectedVersion));
    }

    [Fact]
    public void JsonNumberHandling_AllowsReadingSteamIdFromString()
    {
        var json = """
        {
            "code": "INVITE123",
            "password": "Password123!",
            "displayName": "PlayerOne",
            "email": "p1@dota.local",
            "steamId": "76561198000000001"
        }
        """;

        var options = new System.Text.Json.JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString
        };

        var request = System.Text.Json.JsonSerializer.Deserialize<TournamentCreator.WebApi.Controllers.v1.Requests.RegisterWithInviteApiRequest>(json, options);

        Assert.NotNull(request);
        Assert.Equal(76561198000000001ul, request.SteamId);
    }

    #region Test Fakes
    private class FakePasswordHasher : Identity.Application.Common.Interfaces.IPasswordHasher
    {
        public string HashPassword(string password) => "hash";
        public bool VerifyPassword(string password, string hash) => true;
    }

    private class FakeJwtGen : Identity.Application.Common.Interfaces.IJwtTokenGenerator
    {
        public (string Token, DateTime ExpiresAtUtc) GenerateToken(Identity.Domain.Entities.User user) => ("token", DateTime.UtcNow.AddHours(1));
    }

    private class FakeDotaService : Identity.Application.Common.Interfaces.IOpenDotaProfileService
    {
        public Task<ulong> ResolveSteamIdAsync(string identifierOrUrl, CancellationToken ct = default) => Task.FromResult(0ul);
        public Task<Identity.Contracts.Steam.DotaProfileLookupResult?> FetchProfileAsync(ulong steamId, CancellationToken ct = default) => Task.FromResult<Identity.Contracts.Steam.DotaProfileLookupResult?>(null);
    }
    #endregion
}
