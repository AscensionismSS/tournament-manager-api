using System.Security.Claims;
using BuildingBlocks.Domain;
using Identity.Application.Features.Auth.Login;
using Identity.Application.Features.Auth.UpdateRoles;
using Identity.Application.Features.Invitations.GetInvitations;
using Identity.Application.Features.Invitations.RevokeInvitation;
using Identity.Contracts.Auth;
using Identity.Contracts.Invitations;
using Identity.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using TournamentCreator.WebApi.Controllers.v1;
using TournamentCreator.WebApi.Controllers.v1.Requests;
using TournamentCreator.WebApi.Services;
using Tournaments.Application.Common.Interfaces;
using Tournaments.Application.Features.Matches.UpdateGameWalkover;
using Tournaments.Application.Features.Matches.UploadMatchReplay;
using Tournaments.Application.Features.Tournaments.LockResultSubmission;
using Tournaments.Application.Features.Tournaments.UnlockResultSubmission;
using Tournaments.Contracts.Tournaments;

namespace Tournaments.UnitTests;

/// <summary>
/// Testes Automatizados de Endpoints / Controllers do WebApi em C#.
/// Demonstra como testar status codes HTTP (200, 204, 400), ProblemDetails (RFC 9457)
/// e payloads de resposta exatamente como chegam ao cliente/Postman.
/// </summary>
public class EndpointControllerTests
{
    private readonly FakeSender _sender = new();

    [Fact]
    public async Task AuthController_Login_WhenValidCredentials_ShouldReturn200Ok_WithToken()
    {
        // 1. Arrange (Preparação do cenário de teste)
        var controller = new AuthController(_sender);
        var request = new LoginApiRequest("admin@tournament.local", "Admin123!");

        // 2. Act (Execução da chamada HTTP do endpoint)
        var actionResult = await controller.Login(request, CancellationToken.None);

        // 3. Assert (Validação profissional de Status Code e Contrato)
        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        Assert.Equal(StatusCodes.Status200OK, okResult.StatusCode);

        var payload = Assert.IsType<AuthResponseDto>(okResult.Value);
        Assert.False(string.IsNullOrWhiteSpace(payload.Token));
        Assert.Equal("admin@tournament.local", payload.Email);
    }

    [Fact]
    public async Task AuthController_Login_WhenInvalidCredentials_ShouldReturn400BadRequest_WithProblemDetails()
    {
        // 1. Arrange
        var controller = new AuthController(_sender);
        var request = new LoginApiRequest("admin@tournament.local", "SenhaErrada");

        // 2. Act
        var actionResult = await controller.Login(request, CancellationToken.None);

        // 3. Assert: Garante que o endpoint respeita a RFC 9457 (ProblemDetails)
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(actionResult.Result);
        Assert.Equal(StatusCodes.Status400BadRequest, badRequestResult.StatusCode);

        var problemDetails = Assert.IsType<ProblemDetails>(badRequestResult.Value);
        Assert.Equal("Falha no Login", problemDetails.Title);
        Assert.Equal("Credenciais inválidas.", problemDetails.Detail);
    }

    [Fact]
    public async Task AuthController_UpdateRoles_WhenSuccessful_ShouldReturn200Ok()
    {
        // 1. Arrange
        var controller = new AuthController(_sender);
        var request = new UpdateRolesApiRequest(
            new List<DotaRole> { DotaRole.Carry, DotaRole.Mid, DotaRole.Offlane, DotaRole.SoftSupport, DotaRole.HardSupport },
            DotaRole.Mid
        );

        // 2. Act
        var actionResult = await controller.UpdateRoles(request, CancellationToken.None);

        // 3. Assert
        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        Assert.Equal(StatusCodes.Status200OK, okResult.StatusCode);

        var userDto = Assert.IsType<UserDto>(okResult.Value);
        Assert.NotNull(userDto.Roles);
        Assert.Contains("Flex", userDto.Roles); // Auto-atribuição de Flex ao selecionar 5 funções
        Assert.Equal("Mid", userDto.PreferredRole);
    }

    [Fact]
    public async Task InvitationsController_GetInvitations_ShouldReturn200Ok_WithList()
    {
        // 1. Arrange
        var controller = new InvitationsController(_sender);

        // 2. Act
        var actionResult = await controller.GetInvitations(CancellationToken.None);

        // 3. Assert
        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        Assert.Equal(StatusCodes.Status200OK, okResult.StatusCode);

        var invitations = Assert.IsAssignableFrom<IReadOnlyList<InvitationDto>>(okResult.Value);
        Assert.NotEmpty(invitations);
    }

    [Fact]
    public void InvitationsController_GetExpirationOptions_ShouldReturn200Ok_With_24_48_72()
    {
        // 1. Arrange
        var controller = new InvitationsController(_sender);

        // 2. Act
        var actionResult = controller.GetExpirationOptions();

        // 3. Assert
        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        Assert.Equal(StatusCodes.Status200OK, okResult.StatusCode);

        var options = Assert.IsAssignableFrom<IReadOnlyList<int>>(okResult.Value);
        Assert.Equal(new[] { 24, 48, 72 }, options);
    }

    [Fact]
    public async Task InvitationsController_Revoke_WhenInvitationExists_ShouldReturn200Ok_WithRevokedDto()
    {
        // 1. Arrange
        var controller = new InvitationsController(_sender);
        var existingInviteId = Guid.NewGuid();

        // 2. Act (DELETE /api/v1/invitations/{id})
        var actionResult = await controller.RevokeInvitation(existingInviteId, CancellationToken.None);

        // 3. Assert: Valida retorno 200 OK com o DTO atualizado marcando IsRevoked = true
        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        Assert.Equal(StatusCodes.Status200OK, okResult.StatusCode);

        var revokedDto = Assert.IsType<InvitationDto>(okResult.Value);
        Assert.True(revokedDto.IsRevoked);
    }

    [Fact]
    public async Task InvitationsController_Revoke_WhenNotFound_ShouldReturn400BadRequest()
    {
        // 1. Arrange
        var controller = new InvitationsController(_sender);
        var notFoundId = Guid.Empty;

        // 2. Act
        var actionResult = await controller.RevokeInvitation(notFoundId, CancellationToken.None);

        // 3. Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(actionResult.Result);
        Assert.Equal(StatusCodes.Status400BadRequest, badRequestResult.StatusCode);
    }

    [Fact]
    public async Task MatchesController_UpdateGameWalkover_ShouldReturn200Ok()
    {
        var controller = new MatchesController(_sender);
        var seriesId = Guid.NewGuid();
        var winnerTeamId = Guid.NewGuid();

        var result = await controller.UpdateGameWalkover(
            seriesId,
            1,
            new RecordWalkoverRequest(winnerTeamId),
            CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(StatusCodes.Status200OK, okResult.StatusCode);
    }

    [Fact]
    public async Task MatchesController_UpdateGameWalkover_WhenEmptyWinner_ShouldReturn400BadRequest()
    {
        var controller = new MatchesController(_sender);
        var seriesId = Guid.NewGuid();

        var result = await controller.UpdateGameWalkover(
            seriesId,
            1,
            new RecordWalkoverRequest(Guid.Empty),
            CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, badRequest.StatusCode);
    }

    [Fact]
    public async Task MatchesController_UploadReplay_WithGameNumber_ShouldReturn200Ok()
    {
        var controller = new MatchesController(_sender);
        var seriesId = Guid.NewGuid();

        var formFile = new FormFile(
            new MemoryStream(new byte[100]),
            0,
            100,
            "replayFile",
            "match.dem");

        var result = await controller.UploadReplay(
            seriesId,
            formFile,
            null,
            1,
            CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status200OK, okResult.StatusCode);
        var payload = Assert.IsType<UploadMatchReplayResult>(okResult.Value);
        Assert.Equal(1, payload.GameNumber);
    }

    [Fact]
    public async Task MatchesController_UploadGameReplay_ShouldReturn200Ok()
    {
        var controller = new MatchesController(_sender);
        var seriesId = Guid.NewGuid();

        var formFile = new FormFile(
            new MemoryStream(new byte[100]),
            0,
            100,
            "replayFile",
            "match.dem");

        var result = await controller.UploadGameReplay(
            seriesId,
            2,
            formFile,
            null,
            CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status200OK, okResult.StatusCode);
        var payload = Assert.IsType<UploadMatchReplayResult>(okResult.Value);
        Assert.Equal(2, payload.GameNumber);
    }

    [Fact]
    public async Task MatchesController_UploadReplay_WhenAuthenticatedAdmin_ShouldExtractClaimsAndPassAdminTrue()
    {
        var controller = new MatchesController(_sender);
        var adminUserId = Guid.NewGuid();
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, adminUserId.ToString()),
            new Claim(ClaimTypes.Role, "TournamentAdmin")
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };

        var seriesId = Guid.NewGuid();
        var formFile = new FormFile(
            new MemoryStream(new byte[100]),
            0,
            100,
            "replayFile",
            "match.dem");

        var result = await controller.UploadReplay(
            seriesId,
            formFile,
            null,
            1,
            CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status200OK, okResult.StatusCode);
        var payload = Assert.IsType<UploadMatchReplayResult>(okResult.Value);
        Assert.Equal(1, payload.GameNumber);
        Assert.NotNull(_sender.LastUploadCommand);
        Assert.Equal(adminUserId, _sender.LastUploadCommand.RequestUserId);
        Assert.True(_sender.LastUploadCommand.IsAdmin);
    }

    [Fact]
    public async Task MatchesController_UploadReplay_WhenAuthenticatedNonAdminPassesSpoofedQueryUserId_ShouldIgnoreQueryAndUseClaims()
    {
        var controller = new MatchesController(_sender);
        var genuineUserId = Guid.NewGuid();
        var spoofedUserId = Guid.NewGuid();

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, genuineUserId.ToString()),
            new Claim(ClaimTypes.Role, "User")
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };

        var seriesId = Guid.NewGuid();
        var formFile = new FormFile(
            new MemoryStream(new byte[100]),
            0,
            100,
            "replayFile",
            "match.dem");

        // Atacante envia na query param o requestUserId de outro capitão
        var result = await controller.UploadReplay(
            seriesId,
            formFile,
            spoofedUserId,
            1,
            CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status200OK, okResult.StatusCode);
        Assert.NotNull(_sender.LastUploadCommand);
        Assert.Equal(genuineUserId, _sender.LastUploadCommand.RequestUserId);
        Assert.False(_sender.LastUploadCommand.IsAdmin);
    }

    [Fact]
    public async Task MatchesController_UploadReplay_WhenAuthenticatedUserLacksGuidClaim_ShouldNotFallbackToQueryParam()
    {
        var controller = new MatchesController(_sender);
        var spoofedUserId = Guid.NewGuid();

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "invalid-guid-value"),
            new Claim(ClaimTypes.Role, "User")
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };

        var seriesId = Guid.NewGuid();
        var formFile = new FormFile(
            new MemoryStream(new byte[100]),
            0,
            100,
            "replayFile",
            "match.dem");

        var result = await controller.UploadReplay(
            seriesId,
            formFile,
            spoofedUserId,
            1,
            CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status200OK, okResult.StatusCode);
        Assert.NotNull(_sender.LastUploadCommand);
        Assert.Null(_sender.LastUploadCommand.RequestUserId);
        Assert.False(_sender.LastUploadCommand.IsAdmin);
    }

    [Fact]
    public async Task TournamentsController_LockResults_WhenRequestIsNull_ShouldReturn200Ok_WithDefaultReason()
    {
        var dummyFileStorage = new DummyFileStorageService();
        var dummyStream = new TournamentWebhookStreamService();
        var dummyBannerValidator = new BannerImageValidator();
        var controller = new TournamentsController(_sender, dummyFileStorage, dummyStream, dummyBannerValidator);

        var tournamentId = Guid.NewGuid();

        var result = await controller.LockResults(tournamentId, null, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status200OK, okResult.StatusCode);
        var dto = Assert.IsType<TournamentDto>(okResult.Value);
        Assert.True(dto.IsResultSubmissionLocked);
    }

    [Fact]
    public async Task TournamentsController_LockResults_ShouldReturn200Ok_WithLockedStatus()
    {
        var dummyFileStorage = new DummyFileStorageService();
        var dummyStream = new TournamentWebhookStreamService();
        var dummyBannerValidator = new BannerImageValidator();
        var controller = new TournamentsController(_sender, dummyFileStorage, dummyStream, dummyBannerValidator);

        var tournamentId = Guid.NewGuid();
        var request = new LockResultSubmissionRequest("Auditoria técnica");

        var result = await controller.LockResults(tournamentId, request, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status200OK, okResult.StatusCode);
        var dto = Assert.IsType<TournamentDto>(okResult.Value);
        Assert.True(dto.IsResultSubmissionLocked);
        Assert.Equal("Auditoria técnica", dto.ResultLockReason);
    }

    [Fact]
    public async Task TournamentsController_UnlockResults_ShouldReturn200Ok_WithUnlockedStatus()
    {
        var dummyFileStorage = new DummyFileStorageService();
        var dummyStream = new TournamentWebhookStreamService();
        var dummyBannerValidator = new BannerImageValidator();
        var controller = new TournamentsController(_sender, dummyFileStorage, dummyStream, dummyBannerValidator);

        var tournamentId = Guid.NewGuid();

        var result = await controller.UnlockResults(tournamentId, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status200OK, okResult.StatusCode);
        var dto = Assert.IsType<TournamentDto>(okResult.Value);
        Assert.False(dto.IsResultSubmissionLocked);
        Assert.Null(dto.ResultLockReason);
    }

    [Fact]
    public async Task AdminSecuritySettingsController_GetSecuritySettings_ShouldReturn200Ok_WithCurrentSettings()
    {
        var controller = new AdminSecuritySettingsController(_sender);

        var actionResult = await controller.GetSecuritySettings(CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        Assert.Equal(StatusCodes.Status200OK, okResult.StatusCode);
        var dto = Assert.IsType<Tournaments.Contracts.Security.SystemSecuritySettingsDto>(okResult.Value);
        Assert.False(dto.MaintenanceMode);
        Assert.False(dto.LockResultSubmission);
    }

    [Fact]
    public async Task AdminSecuritySettingsController_UpdateSecuritySettings_ShouldReturn200Ok_WithUpdatedSettings()
    {
        var controller = new AdminSecuritySettingsController(_sender);
        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "superadmin-123"),
            new Claim(ClaimTypes.Role, "SuperAdmin")
        }, "TestAuth"));
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = user }
        };

        var request = new Tournaments.Contracts.Security.UpdateSystemSecuritySettingsRequest(
            LockResultSubmission: true,
            LockTournamentRegistration: true,
            LockTournamentCreation: false,
            LockDraftPicks: false,
            MaintenanceMode: false,
            Message: "Travamento geral acionado"
        );

        var actionResult = await controller.UpdateSecuritySettings(request, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        Assert.Equal(StatusCodes.Status200OK, okResult.StatusCode);
        var dto = Assert.IsType<Tournaments.Contracts.Security.SystemSecuritySettingsDto>(okResult.Value);
        Assert.True(dto.LockResultSubmission);
        Assert.True(dto.LockTournamentRegistration);
        Assert.False(dto.LockTournamentCreation);
        Assert.Equal("Travamento geral acionado", dto.Message);
        Assert.Equal("superadmin-123", dto.UpdatedBy);
    }

    private sealed class DummyFileStorageService : IFileStorageService
    {
        public Task<string> UploadAsync(Stream stream, string fileName, string contentType, CancellationToken ct = default)
            => Task.FromResult("/uploads/" + fileName);

        public Task DeleteAsync(string fileUrl, CancellationToken ct = default)
            => Task.CompletedTask;
    }

    #region Fake MediatR Sender para Isolamento dos Testes de Endpoints
    private sealed class FakeSender : ISender
    {
        public UploadMatchReplayCommand? LastUploadCommand { get; private set; }

        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            if (request is LoginCommand loginCmd)
            {
                if (loginCmd.Password == "Admin123!")
                {
                    var dto = new AuthResponseDto(
                        Guid.NewGuid(),
                        loginCmd.Email,
                        "Master Admin",
                        "SuperAdmin",
                        null,
                        false,
                        null,
                        null,
                        null,
                        null,
                        "fake-jwt-token-xyz-123",
                        DateTime.UtcNow.AddHours(2)
                    );
                    return Task.FromResult((TResponse)(object)Result<AuthResponseDto>.Success(dto));
                }

                return Task.FromResult((TResponse)(object)Result<AuthResponseDto>.Failure("Credenciais inválidas."));
            }

            if (request is UpdateUserRolesCommand updateRolesCmd)
            {
                var roleNames = updateRolesCmd.Roles.Select(r => r.ToString()).ToList();
                if (roleNames.Count == 5 && !roleNames.Contains("Flex"))
                {
                    roleNames.Add("Flex");
                }

                var userDto = new UserDto(
                    Guid.NewGuid(),
                    "admin@tournament.local",
                    "Master Admin",
                    "SuperAdmin",
                    null,
                    false,
                    null,
                    null,
                    null,
                    null,
                    DateTime.UtcNow,
                    null,
                    roleNames,
                    updateRolesCmd.PreferredRole?.ToString()
                );
                return Task.FromResult((TResponse)(object)Result<UserDto>.Success(userDto));
            }

            if (request is GetInvitationsQuery)
            {
                var list = (IReadOnlyList<InvitationDto>)new List<InvitationDto>
                {
                    new(
                        Guid.NewGuid(),
                        "a1b2c3d4e5f67a8b9c0d1e2f3a4b5c6d",
                        "jogador1@dota.local",
                        "Player",
                        Guid.NewGuid(),
                        DateTime.UtcNow,
                        DateTime.UtcNow.AddDays(7),
                        false,
                        null
                    )
                };
                return Task.FromResult((TResponse)(object)list);
            }

            if (request is RevokeInvitationCommand revokeCmd)
            {
                if (revokeCmd.InvitationId == Guid.Empty)
                {
                    return Task.FromResult((TResponse)(object)Result<InvitationDto>.Failure("Convite não encontrado."));
                }

                var revokedDto = new InvitationDto(
                    revokeCmd.InvitationId,
                    "code-123",
                    "jogador1@dota.local",
                    "Player",
                    Guid.NewGuid(),
                    DateTime.UtcNow.AddDays(-1),
                    DateTime.UtcNow.AddDays(6),
                    false,
                    null,
                    true,
                    DateTime.UtcNow
                );

                return Task.FromResult((TResponse)(object)Result<InvitationDto>.Success(revokedDto));
            }

            if (request is UploadMatchReplayCommand uploadCmd)
            {
                LastUploadCommand = uploadCmd;
                var response = new UploadMatchReplayResult(
                    12345678,
                    Guid.NewGuid(),
                    "Team Alpha",
                    uploadCmd.GameNumber ?? 1,
                    2100,
                    1,
                    0,
                    "InProgress",
                    "Success"
                );
                return Task.FromResult((TResponse)(object)response);
            }

            if (request is LockResultSubmissionCommand lockCmd)
            {
                var dto = new TournamentDto(
                    lockCmd.TournamentId,
                    "Locked Tournament",
                    null,
                    "GroupStage",
                    8,
                    2,
                    "GroupStageAndPlayoffs",
                    "SingleElimination",
                    "Bo2",
                    "Bo3",
                    "Bo5",
                    null,
                    null,
                    null,
                    DateTime.UtcNow,
                    [],
                    [],
                    IsResultSubmissionLocked: true,
                    ResultLockReason: lockCmd.Reason
                );
                return Task.FromResult((TResponse)(object)dto);
            }

            if (request is UnlockResultSubmissionCommand unlockCmd)
            {
                var dto = new TournamentDto(
                    unlockCmd.TournamentId,
                    "Unlocked Tournament",
                    null,
                    "GroupStage",
                    8,
                    2,
                    "GroupStageAndPlayoffs",
                    "SingleElimination",
                    "Bo2",
                    "Bo3",
                    "Bo5",
                    null,
                    null,
                    null,
                    DateTime.UtcNow,
                    [],
                    [],
                    IsResultSubmissionLocked: false,
                    ResultLockReason: null
                );
                return Task.FromResult((TResponse)(object)dto);
            }

            if (request is Tournaments.Application.Features.Security.GetSystemSecuritySettings.GetSystemSecuritySettingsQuery)
            {
                var dto = new Tournaments.Contracts.Security.SystemSecuritySettingsDto(
                    LockResultSubmission: false,
                    LockTournamentRegistration: false,
                    LockTournamentCreation: false,
                    LockDraftPicks: false,
                    MaintenanceMode: false,
                    Message: null,
                    UpdatedAtUtc: DateTime.UtcNow,
                    UpdatedBy: "System",
                    IsResultSubmissionLocked: false,
                    IsTournamentRegistrationLocked: false,
                    IsTournamentCreationLocked: false,
                    IsDraftPicksLocked: false
                );
                return Task.FromResult((TResponse)(object)dto);
            }

            if (request is Tournaments.Application.Features.Security.UpdateSystemSecuritySettings.UpdateSystemSecuritySettingsCommand updateSecCmd)
            {
                var dto = new Tournaments.Contracts.Security.SystemSecuritySettingsDto(
                    LockResultSubmission: updateSecCmd.LockResultSubmission ?? false,
                    LockTournamentRegistration: updateSecCmd.LockTournamentRegistration ?? false,
                    LockTournamentCreation: updateSecCmd.LockTournamentCreation ?? false,
                    LockDraftPicks: updateSecCmd.LockDraftPicks ?? false,
                    MaintenanceMode: updateSecCmd.MaintenanceMode ?? false,
                    Message: updateSecCmd.Message,
                    UpdatedAtUtc: DateTime.UtcNow,
                    UpdatedBy: updateSecCmd.UpdatedBy,
                    IsResultSubmissionLocked: (updateSecCmd.MaintenanceMode ?? false) || (updateSecCmd.LockResultSubmission ?? false),
                    IsTournamentRegistrationLocked: (updateSecCmd.MaintenanceMode ?? false) || (updateSecCmd.LockTournamentRegistration ?? false),
                    IsTournamentCreationLocked: (updateSecCmd.MaintenanceMode ?? false) || (updateSecCmd.LockTournamentCreation ?? false),
                    IsDraftPicksLocked: (updateSecCmd.MaintenanceMode ?? false) || (updateSecCmd.LockDraftPicks ?? false)
                );
                return Task.FromResult((TResponse)(object)dto);
            }

            throw new NotImplementedException($"Fake handler não configurado para: {request.GetType().Name}");
        }

        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default) where TRequest : IRequest
        {
            return Task.CompletedTask;
        }

        public Task<object?> Send(object request, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
    }
    #endregion
}
