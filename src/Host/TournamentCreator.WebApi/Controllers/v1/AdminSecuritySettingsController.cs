using System.Security.Claims;
using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Tournaments.Application.Features.Security.GetSystemSecuritySettings;
using Tournaments.Application.Features.Security.UpdateSystemSecuritySettings;
using Tournaments.Contracts.Security;

namespace TournamentCreator.WebApi.Controllers.v1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/security-settings")]
[Route("api/v{version:apiVersion}/system/security-settings")]
public class AdminSecuritySettingsController : ControllerBase
{
    private readonly ISender _sender;

    public AdminSecuritySettingsController(ISender sender)
    {
        _sender = sender;
    }

    /// <summary>
    /// Consulta o estado atual das travas globais de segurança e do modo de manutenção do sistema.
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<SystemSecuritySettingsDto>> GetSecuritySettings(CancellationToken ct)
    {
        var settings = await _sender.Send(new GetSystemSecuritySettingsQuery(), ct);
        return Ok(settings);
    }

    /// <summary>
    /// Atualiza as travas globais de segurança do sistema (exclusivo para SuperAdmin).
    /// </summary>
    [HttpPut]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<ActionResult<SystemSecuritySettingsDto>> UpdateSecuritySettings(
        [FromBody] UpdateSystemSecuritySettingsRequest request,
        CancellationToken ct)
    {
        var adminId = User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User?.FindFirst("sub")?.Value
            ?? User?.FindFirst(ClaimTypes.Email)?.Value
            ?? "SuperAdmin";

        var command = new UpdateSystemSecuritySettingsCommand(
            request.LockResultSubmission,
            request.LockTournamentRegistration,
            request.LockTournamentCreation,
            request.LockDraftPicks,
            request.MaintenanceMode,
            request.Message,
            adminId
        );

        var result = await _sender.Send(command, ct);
        return Ok(result);
    }

    /// <summary>
    /// Atualiza as travas globais de segurança do sistema via POST (exclusivo para SuperAdmin).
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<ActionResult<SystemSecuritySettingsDto>> UpdateSecuritySettingsPost(
        [FromBody] UpdateSystemSecuritySettingsRequest request,
        CancellationToken ct)
    {
        return await UpdateSecuritySettings(request, ct);
    }
}
