using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TournamentCreator.WebApi.Controllers.v1.Requests;
using Tournaments.Application.Features.Matches.GetMatchById;
using Tournaments.Application.Features.Matches.RecordManualResult;
using Tournaments.Application.Features.Matches.RecordReplayResult;
using Tournaments.Application.Features.Matches.RecordWalkover;
using Tournaments.Application.Features.Matches.RemoveLastGame;
using Tournaments.Application.Features.Matches.RollbackMatchSeries;
using Tournaments.Application.Features.Matches.UpdateGameResult;
using Tournaments.Application.Features.Matches.UpdateGameWalkover;
using Tournaments.Application.Features.Matches.UploadMatchReplay;
using Tournaments.Contracts.Matches;
using Tournaments.Contracts.Replay;

namespace TournamentCreator.WebApi.Controllers.v1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/matches")]
public class MatchesController : ControllerBase
{
    private readonly ISender _sender;

    public MatchesController(ISender sender)
    {
        _sender = sender;
    }

    [AllowAnonymous]
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<MatchSeriesDto>> GetById(Guid id, CancellationToken ct)
    {
        var series = await _sender.Send(new GetMatchByIdQuery(id), ct);
        if (series == null)
        {
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Partida Não Encontrada",
                Detail = $"A série de partida '{id}' não foi encontrada ou foi removida."
            });
        }

        return Ok(series);
    }

    [Authorize(Roles = "TournamentAdmin,SuperAdmin")]
    [HttpPost("result/replay-ingest")]
    [HttpPost("{id:guid}/replay/result")]
    public async Task<IActionResult> IngestReplayResult(
        [FromRoute] Guid? id,
        [FromBody] MatchResultIngestionDto ingestionDto,
        CancellationToken ct)
    {
        var effectiveDto = id.HasValue && id.Value != Guid.Empty && ingestionDto.MatchSeriesId == Guid.Empty
            ? ingestionDto with { MatchSeriesId = id.Value }
            : ingestionDto;

        var cmd = new RecordReplayResultCommand(effectiveDto);
        await _sender.Send(cmd, ct);
        return Ok(new { message = "Estatísticas ricas do Replay Parser processadas com sucesso." });
    }

    [Authorize(Roles = "TournamentAdmin,SuperAdmin")]
    [HttpPost("{id:guid}/result/manual")]
    public async Task<IActionResult> RecordManualResult(
        Guid id,
        [FromBody] RecordManualResultRequest request,
        CancellationToken ct)
    {
        var cmd = new RecordManualResultCommand(
            id,
            request.WinnerTeamId,
            request.DurationSeconds,
            request.TeamAKills,
            request.TeamBKills,
            request.FirstPickTeamId,
            request.TeamABanHeroIds,
            request.TeamBBanHeroIds,
            request.DotaMatchId,
            request.GameNumber);

        await _sender.Send(cmd, ct);
        return Ok(new { message = "Resultado manual registrado com sucesso." });
    }

    [Authorize(Roles = "TournamentAdmin,SuperAdmin")]
    [HttpPost("{id:guid}/result/walkover")]
    public async Task<IActionResult> RecordWalkover(
        Guid id,
        [FromBody] RecordWalkoverRequest request,
        CancellationToken ct)
    {
        var cmd = new RecordWalkoverCommand(id, request.WinnerTeamId);
        await _sender.Send(cmd, ct);
        return Ok(new { message = "W.O. aplicado com penalidade estatística neutra computada." });
    }

    [Authorize(Roles = "TournamentAdmin,SuperAdmin")]
    [HttpPost("{id:guid}/rollback")]
    public async Task<IActionResult> Rollback(
        Guid id,
        CancellationToken ct)
    {
        var cmd = new RollbackMatchSeriesCommand(id);
        await _sender.Send(cmd, ct);
        return Ok(new { message = "Resultado da série revertido com sucesso." });
    }

    [Authorize(Roles = "TournamentAdmin,SuperAdmin")]
    [HttpPost("{id:guid}/games/remove-last")]
    [HttpPost("{id:guid}/games/last")]
    [HttpDelete("{id:guid}/games/last")]
    public async Task<IActionResult> RemoveLastGame(
        Guid id,
        CancellationToken ct)
    {
        var cmd = new RemoveLastGameCommand(id);
        await _sender.Send(cmd, ct);
        return Ok(new { message = "Último mapa da série desfeito com sucesso." });
    }

    [Authorize(Roles = "TournamentAdmin,SuperAdmin")]
    [HttpPut("{id:guid}/games/{gameNumber:int}/walkover")]
    [HttpPost("{id:guid}/games/{gameNumber:int}/walkover")]
    public async Task<IActionResult> UpdateGameWalkover(
        Guid id,
        int gameNumber,
        [FromBody] RecordWalkoverRequest request,
        CancellationToken ct)
    {
        if (request == null || request.WinnerTeamId == Guid.Empty)
        {
            return BadRequest(new { error = "O time vencedor do W.O. é obrigatório." });
        }

        var cmd = new UpdateGameWalkoverCommand(id, gameNumber, request.WinnerTeamId);
        await _sender.Send(cmd, ct);
        return Ok(new { message = $"W.O. aplicado com sucesso ao jogo #{gameNumber}." });
    }

    [Authorize(Roles = "TournamentAdmin,SuperAdmin")]
    [HttpPut("{id:guid}/games/{gameNumber:int}")]
    [HttpPost("{id:guid}/games/{gameNumber:int}")]
    public async Task<IActionResult> UpdateGameResult(
        Guid id,
        int gameNumber,
        [FromBody] UpdateGameResultRequest request,
        CancellationToken ct)
    {
        var cmd = new UpdateGameResultCommand(
            id,
            gameNumber,
            request.WinnerTeamId,
            request.DurationSeconds,
            request.TeamAKills,
            request.TeamBKills,
            request.FirstPickTeamId,
            request.TeamABanHeroIds,
            request.TeamBBanHeroIds,
            request.DotaMatchId);

        await _sender.Send(cmd, ct);
        return Ok(new { message = $"Dados do jogo #{gameNumber} atualizados com sucesso." });
    }

    [Authorize]
    [HttpPost("{id:guid}/upload-replay")]
    [HttpPost("{id:guid}/replay")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(350 * 1024 * 1024)] // 350 MB
    public async Task<ActionResult<UploadMatchReplayResult>> UploadReplay(
        Guid id,
        IFormFile replayFile,
        [FromQuery] Guid? requestUserId,
        [FromQuery] int? gameNumber,
        CancellationToken ct)
    {
        if (replayFile == null || replayFile.Length == 0)
        {
            return BadRequest(new { error = "O arquivo de replay (.dem) não foi fornecido." });
        }

        if (!gameNumber.HasValue && Request?.HasFormContentType == true && Request.Form.TryGetValue("gameNumber", out var formGn) && int.TryParse(formGn, out var parsedGn))
        {
            gameNumber = parsedGn;
        }

        if (!requestUserId.HasValue && Request?.HasFormContentType == true && Request.Form.TryGetValue("requestUserId", out var formUid) && Guid.TryParse(formUid, out var parsedUid))
        {
            requestUserId = parsedUid;
        }

        var (callerId, isAdmin) = ResolveCallerIdentity(requestUserId);

        await using var stream = replayFile.OpenReadStream();
        var cmd = new UploadMatchReplayCommand(id, stream, replayFile.FileName, callerId, gameNumber, isAdmin);
        var result = await _sender.Send(cmd, ct);
        return Ok(result);
    }

    [Authorize]
    [HttpPut("{id:guid}/games/{gameNumber:int}/replay")]
    [HttpPost("{id:guid}/games/{gameNumber:int}/replay")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(350 * 1024 * 1024)] // 350 MB
    public async Task<ActionResult<UploadMatchReplayResult>> UploadGameReplay(
        Guid id,
        int gameNumber,
        IFormFile replayFile,
        [FromQuery] Guid? requestUserId,
        CancellationToken ct)
    {
        if (replayFile == null || replayFile.Length == 0)
        {
            return BadRequest(new { error = "O arquivo de replay (.dem) não foi fornecido." });
        }

        if (!requestUserId.HasValue && Request?.HasFormContentType == true && Request.Form.TryGetValue("requestUserId", out var formUid) && Guid.TryParse(formUid, out var parsedUid))
        {
            requestUserId = parsedUid;
        }

        var (callerId, isAdmin) = ResolveCallerIdentity(requestUserId);

        await using var stream = replayFile.OpenReadStream();
        var cmd = new UploadMatchReplayCommand(id, stream, replayFile.FileName, callerId, gameNumber, isAdmin);
        var result = await _sender.Send(cmd, ct);
        return Ok(result);
    }

    private (Guid? userId, bool isAdmin) ResolveCallerIdentity(Guid? fallbackUserId)
    {
        var isAuthenticated = User?.Identity?.IsAuthenticated == true;
        var isAdmin = User?.IsInRole("TournamentAdmin") == true || User?.IsInRole("SuperAdmin") == true;

        var claimId = User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? User?.FindFirst("sub")?.Value;

        if (Guid.TryParse(claimId, out var parsedClaimId))
        {
            return (parsedClaimId, isAdmin);
        }

        // Se o chamador estiver autenticado via JWT mas sem claim de Guid válido,
        // não permitimos que ele forje a identidade de outro capitão via query param.
        if (isAuthenticated)
        {
            return (null, isAdmin);
        }

        return (fallbackUserId, isAdmin);
    }
}
