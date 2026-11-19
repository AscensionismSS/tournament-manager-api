using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Tournaments.Application.Features.ProMatches.GetProMatchesOverview;
using Tournaments.Contracts.ProMatches;

namespace TournamentCreator.WebApi.Controllers.v1;

[ApiController]
[ApiVersion("1.0")]
[Authorize]
[Route("api/v{version:apiVersion}/pro-matches")]
public class ProMatchesController : ControllerBase
{
    private readonly ISender _sender;

    public ProMatchesController(ISender sender)
    {
        _sender = sender;
    }

    /// <summary>
    /// Retorna os campeonatos e partidas profissionais de Dota 2 (Ao Vivo e Agenda da Semana),
    /// agrupados por campeonato com links oficiais de stream em PT-BR e EN separados por plataforma (Twitch / YouTube).
    /// Exclusivo para usuários autenticados.
    /// </summary>
    [HttpGet]
    [HttpGet("overview")]
    public async Task<ActionResult<ProMatchesOverviewDto>> GetOverview(CancellationToken ct)
    {
        var overview = await _sender.Send(new GetProMatchesOverviewQuery(), ct);
        return Ok(overview);
    }
}
