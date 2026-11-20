using System.Security.Claims;
using Asp.Versioning;
using Identity.Application.Common.Interfaces;
using Identity.Domain.Repositories;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
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
    private readonly IUserRepository _userRepository;
    private readonly IRootOwnerService _rootOwnerService;

    public AdminSecuritySettingsController(
        ISender sender,
        IUserRepository userRepository,
        IRootOwnerService rootOwnerService)
    {
        _sender = sender;
        _userRepository = userRepository;
        _rootOwnerService = rootOwnerService;
    }

    /// <summary>
    /// Consulta o estado atual das travas globais de segurança e do modo de manutenção do sistema.
    /// Dados de auditoria (UpdatedBy / UpdatedAtUtc) são expostos exclusivamente ao proprietário raiz (AdminSeed).
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<SystemSecuritySettingsDto>> GetSecuritySettings(CancellationToken ct)
    {
        var settings = await _sender.Send(new GetSystemSecuritySettingsQuery(), ct);

        bool isRootOwnerCaller = false;
        if (User?.Identity?.IsAuthenticated == true && User.IsInRole("SuperAdmin"))
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? User.FindFirst("sub")?.Value;

            if (Guid.TryParse(userIdClaim, out var callerUserId) && callerUserId != Guid.Empty)
            {
                var caller = await _userRepository.GetByIdAsync(callerUserId, ct);
                isRootOwnerCaller = _rootOwnerService.HasRootOwnerAuthority(caller);
            }
        }

        if (!isRootOwnerCaller)
        {
            settings = settings with { UpdatedBy = null, UpdatedAtUtc = null };
        }

        return Ok(settings);
    }

    /// <summary>
    /// Atualiza as travas globais de segurança do sistema (exclusivo para o proprietário raiz AdminSeed).
    /// </summary>
    [HttpPut]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<ActionResult<SystemSecuritySettingsDto>> UpdateSecuritySettings(
        [FromBody] UpdateSystemSecuritySettingsRequest request,
        CancellationToken ct)
    {
        var userIdClaim = User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User?.FindFirst("sub")?.Value;

        if (!Guid.TryParse(userIdClaim, out var callerUserId) || callerUserId == Guid.Empty)
        {
            return ForbiddenRootOwner();
        }

        var caller = await _userRepository.GetByIdAsync(callerUserId, ct);
        if (!_rootOwnerService.HasRootOwnerAuthority(caller))
        {
            return ForbiddenRootOwner();
        }

        var adminId = !string.IsNullOrWhiteSpace(caller!.DisplayName)
            ? caller.DisplayName
            : "SuperAdmin";

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
    /// Atualiza as travas globais de segurança do sistema via POST (exclusivo para o proprietário raiz AdminSeed).
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<ActionResult<SystemSecuritySettingsDto>> UpdateSecuritySettingsPost(
        [FromBody] UpdateSystemSecuritySettingsRequest request,
        CancellationToken ct)
    {
        return await UpdateSecuritySettings(request, ct);
    }

    private ObjectResult ForbiddenRootOwner()
    {
        return StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
        {
            Status = StatusCodes.Status403Forbidden,
            Title = "Acesso Restrito ao Proprietário Raiz",
            Type = "https://tournaments.local/errors/forbidden",
            Detail = "Apenas o proprietário raiz (AdminSeed) pode alterar as travas globais de segurança e o modo de manutenção do sistema.",
            Instance = HttpContext?.Request.Path
        });
    }
}
