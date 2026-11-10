using Asp.Versioning;
using Identity.Application.Features.Preferences.GetGeneralPreferences;
using Identity.Application.Features.Preferences.UpdateGeneralPreferences;
using Identity.Contracts.Preferences;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using TournamentCreator.WebApi.Extensions;

namespace TournamentCreator.WebApi.Controllers.v1;

[ApiController]
[ApiVersion("1.0")]
[Authorize(Roles = "SuperAdmin,TournamentAdmin")]
[Route("api/v{version:apiVersion}/admin/preferences")]
[Route("api/v{version:apiVersion}/users/me/preferences")]
public class AdminPreferencesController : ControllerBase
{
    private readonly ISender _sender;

    public AdminPreferencesController(ISender sender)
    {
        _sender = sender;
    }

    /// <summary>
    /// Obtém as preferências gerais da plataforma configuradas para o administrador autenticado.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<GeneralPreferencesDto>> GetPreferences(CancellationToken ct)
    {
        var result = await _sender.Send(new GetGeneralPreferencesQuery(), ct);
        return result.ToActionResult("Erro ao buscar preferências");
    }

    /// <summary>
    /// Atualiza as preferências gerais (ex: Página Inicial Preferencial: 'Player' ou 'Admin').
    /// </summary>
    [HttpPut]
    public async Task<ActionResult<GeneralPreferencesDto>> UpdatePreferences(
        [FromBody] UpdateGeneralPreferencesDto request,
        CancellationToken ct)
    {
        var result = await _sender.Send(new UpdateGeneralPreferencesCommand(request.PreferredLandingPage), ct);
        return result.ToActionResult("Erro ao atualizar preferências");
    }
}
