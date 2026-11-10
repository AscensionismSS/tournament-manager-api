using Asp.Versioning;
using Identity.Application.Features.Auth.GetCurrentUser;
using Identity.Application.Features.Auth.LinkSteam;
using Identity.Application.Features.Auth.Login;
using Identity.Application.Features.Auth.RegisterWithInvitation;
using Identity.Application.Features.Auth.UnlockSteam;
using Identity.Contracts.Auth;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TournamentCreator.WebApi.Controllers.v1.Requests;
using TournamentCreator.WebApi.Extensions;

namespace TournamentCreator.WebApi.Controllers.v1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/auth")]
public class AuthController : ControllerBase
{
    private readonly ISender _sender;

    public AuthController(ISender sender)
    {
        _sender = sender;
    }

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<ActionResult<AuthResponseDto>> Login(
        [FromBody] LoginApiRequest request,
        CancellationToken ct)
    {
        var result = await _sender.Send(new LoginCommand(request.GetEffectiveIdentifier(), request.Password), ct);
        return result.ToActionResult("Falha no Login");
    }

    [AllowAnonymous]
    [HttpPost("register-invite")]
    public async Task<ActionResult<AuthResponseDto>> RegisterWithInvite(
        [FromBody] RegisterWithInviteApiRequest request,
        CancellationToken ct)
    {
        var result = await _sender.Send(new RegisterWithInvitationCommand(
            request.Code,
            request.Password,
            request.DisplayName,
            request.Email,
            request.SteamId
        ), ct);
        return result.ToActionResult("Falha no Registro");
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<UserDto>> GetCurrentUser(CancellationToken ct)
    {
        var result = await _sender.Send(new GetCurrentUserQuery(), ct);
        return result.ToActionResult("Usuário Não Encontrado", StatusCodes.Status404NotFound);
    }

    [Authorize]
    [HttpPost("link-steam")]
    public async Task<ActionResult<UserDto>> LinkSteam(
        [FromBody] LinkSteamApiRequest request,
        CancellationToken ct)
    {
        var result = await _sender.Send(new LinkSteamCommand(request.SteamId), ct);
        return result.ToActionResult("Falha na Vinculação da Steam");
    }

    [Authorize(Roles = "SuperAdmin")]
    [HttpPost("users/{userId:guid}/unlock-steam")]
    public async Task<ActionResult<UserDto>> UnlockSteam(
        Guid userId,
        CancellationToken ct)
    {
        var result = await _sender.Send(new UnlockSteamCommand(userId), ct);
        return result.ToActionResult("Falha no Desbloqueio da Steam");
    }

    [Authorize]
    [HttpPut("roles")]
    public async Task<ActionResult<UserDto>> UpdateRoles(
        [FromBody] UpdateRolesApiRequest request,
        CancellationToken ct)
    {
        var result = await _sender.Send(new Identity.Application.Features.Auth.UpdateRoles.UpdateUserRolesCommand(
            request.Roles,
            request.PreferredRole
        ), ct);
        return result.ToActionResult("Falha ao Atualizar Funções");
    }

    [AllowAnonymous]
    [HttpPost("forgot-password")]
    public async Task<ActionResult<RecoveryActionResponseDto>> ForgotPassword(
        [FromBody] ForgotPasswordApiRequest request,
        CancellationToken ct)
    {
        var result = await _sender.Send(new Identity.Application.Features.Auth.ForgotPassword.ForgotPasswordCommand(request.Email), ct);
        return result.ToActionResult("Falha ao Solicitar Redefinição");
    }

    [AllowAnonymous]
    [HttpPost("reset-password")]
    public async Task<ActionResult<RecoveryActionResponseDto>> ResetPassword(
        [FromBody] ResetPasswordApiRequest request,
        CancellationToken ct)
    {
        var result = await _sender.Send(new Identity.Application.Features.Auth.ResetPassword.ResetPasswordCommand(
            request.Token,
            request.NewPassword,
            request.ConfirmPassword
        ), ct);
        return result.ToActionResult("Falha ao Redefinir Senha");
    }

    [AllowAnonymous]
    [HttpPost("forgot-login")]
    public async Task<ActionResult<RecoveryActionResponseDto>> ForgotLogin(
        [FromBody] ForgotLoginApiRequest request,
        CancellationToken ct)
    {
        var result = await _sender.Send(new Identity.Application.Features.Auth.ForgotLogin.ForgotLoginCommand(request.Email), ct);
        return result.ToActionResult("Falha ao Solicitar Recuperação de Login");
    }

    [AllowAnonymous]
    [HttpGet("recover-login")]
    public async Task<ActionResult<RecoveredLoginDto>> RecoverLogin(
        [FromQuery] string token,
        CancellationToken ct)
    {
        var result = await _sender.Send(new Identity.Application.Features.Auth.RecoverLogin.RecoverLoginQuery(token), ct);
        return result.ToActionResult("Falha na Recuperação de Login");
    }

    [Authorize(Roles = "SuperAdmin")]
    [HttpGet("recovery-requests")]
    public async Task<ActionResult<IReadOnlyList<RecoveryRequestAdminDto>>> GetRecoveryRequests(
        [FromQuery] string? search,
        CancellationToken ct)
    {
        var requests = await _sender.Send(new Identity.Application.Features.Auth.GetRecoveryRequests.GetRecoveryRequestsQuery(search), ct);
        return Ok(requests);
    }

    [AllowAnonymous]
    [HttpGet("preview-steam")]
    public async Task<ActionResult<Identity.Contracts.Steam.SteamPreviewDto>> PreviewSteam(
        [FromQuery] string identifier,
        CancellationToken ct)
    {
        var result = await _sender.Send(new Identity.Application.Features.Auth.PreviewSteam.PreviewSteamQuery(identifier), ct);
        return result.ToActionResult("Falha ao Verificar Perfil Steam");
    }

    [Authorize]
    [HttpPost("request-steam-unlock")]
    public async Task<ActionResult<RecoveryActionResponseDto>> RequestSteamUnlock(
        [FromBody] RequestSteamUnlockApiRequest? request,
        CancellationToken ct)
    {
        var result = await _sender.Send(new Identity.Application.Features.Auth.RequestSteamUnlock.RequestSteamUnlockCommand(request?.Reason), ct);
        return result.ToActionResult("Falha ao Solicitar Desbloqueio");
    }

    [Authorize(Roles = "SuperAdmin")]
    [HttpPost("recovery-requests/{id:guid}/approve-steam")]
    public async Task<ActionResult<RecoveryActionResponseDto>> ApproveSteamUnlock(
        Guid id,
        CancellationToken ct)
    {
        var result = await _sender.Send(new Identity.Application.Features.Auth.ApproveSteamUnlock.ApproveSteamUnlockCommand(id), ct);
        return result.ToActionResult("Falha ao Aprovar Troca de Steam");
    }

    [Authorize(Roles = "SuperAdmin")]
    [HttpPost("recovery-requests/{id:guid}/reject-steam")]
    public async Task<ActionResult<RecoveryActionResponseDto>> RejectSteamUnlock(
        Guid id,
        [FromBody] RejectSteamUnlockApiRequest? request,
        CancellationToken ct)
    {
        var result = await _sender.Send(new Identity.Application.Features.Auth.RejectSteamUnlock.RejectSteamUnlockCommand(id, request?.Reason), ct);
        return result.ToActionResult("Falha ao Recusar Troca de Steam");
    }
}

public record RejectSteamUnlockApiRequest(string? Reason = null);
