using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TournamentCreator.WebApi.Controllers.v1.Requests;
using TournamentCreator.WebApi.Services;
using Tournaments.Application.Common.Interfaces;
using Tournaments.Application.Features.Matches.GetTournamentMatches;
using Tournaments.Application.Features.Tournaments.AddTeamToTournament;
using Tournaments.Application.Features.Tournaments.AdvanceToPlayoffs;
using Tournaments.Application.Features.Tournaments.CancelTournament;
using Tournaments.Application.Features.Tournaments.CloseRegistrations;
using Tournaments.Application.Features.Tournaments.CreateTournament;
using Tournaments.Application.Features.Tournaments.DeleteTournament;
using Tournaments.Application.Features.Tournaments.GetTournamentById;
using Tournaments.Application.Features.Tournaments.GetTournamentFormatOptions;
using Tournaments.Application.Features.Tournaments.GetTournaments;
using Tournaments.Application.Features.Tournaments.GetTournamentStandings;
using Tournaments.Application.Features.Tournaments.LockResultSubmission;
using Tournaments.Application.Features.Tournaments.OpenRegistrations;
using Tournaments.Application.Features.Tournaments.RevertToGroupStage;
using Tournaments.Application.Features.Tournaments.StartTournament;
using Tournaments.Application.Features.Tournaments.UnlockResultSubmission;
using Tournaments.Contracts.Matches;
using Tournaments.Contracts.Tournaments;
using Tournaments.Domain.Enums;

namespace TournamentCreator.WebApi.Controllers.v1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/tournaments")]
public class TournamentsController : ControllerBase
{
    private readonly ISender _sender;
    private readonly IFileStorageService _fileStorageService;
    private readonly TournamentWebhookStreamService _webhookStream;
    private readonly IBannerImageValidator _bannerValidator;

    public TournamentsController(
        ISender sender,
        IFileStorageService fileStorageService,
        TournamentWebhookStreamService webhookStream,
        IBannerImageValidator bannerValidator)
    {
        _sender = sender;
        _fileStorageService = fileStorageService;
        _webhookStream = webhookStream;
        _bannerValidator = bannerValidator;
    }

    [AllowAnonymous]
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TournamentDto>>> GetAll([FromQuery] TournamentStatus? status, CancellationToken ct) =>
        Ok(await _sender.Send(new GetTournamentsQuery(status), ct));

    [AllowAnonymous]
    [HttpGet("options")]
    public async Task<ActionResult<TournamentConfigurationOptionsDto>> GetFormatOptions([FromQuery] int? teamCount, CancellationToken ct) =>
        Ok(await _sender.Send(new GetTournamentFormatOptionsQuery(teamCount), ct));

    [Authorize(Roles = "TournamentAdmin,SuperAdmin")]
    [HttpPost]
    public async Task<ActionResult<TournamentDto>> Create([FromBody] CreateTournamentRequest request, CancellationToken ct)
    {
        var resolvedPlayoffFormat = request.PlayoffFormat ?? (request.Type == TournamentType.GroupStageAndPlayoffs
            ? PlayoffFormat.DoubleElimination
            : PlayoffFormat.SingleElimination);

        var cmd = new CreateTournamentCommand(
            request.Name, request.Type, resolvedPlayoffFormat, request.TeamCount,
            request.GroupCount, request.GroupStageFormat, request.PlayoffSeriesFormat,
            request.GrandFinalSeriesFormat, EnableDraftMode: true, request.BannerUrl);

        var tournament = await _sender.Send(cmd, ct);
        return CreatedAtAction(nameof(GetById), new { version = "1.0", id = tournament.Id }, tournament);
    }

    [AllowAnonymous]
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<TournamentDto>> GetById(Guid id, CancellationToken ct)
    {
        var tournament = await _sender.Send(new GetTournamentByIdQuery(id), ct);
        return tournament is null
            ? NotFound(new ProblemDetails { Status = StatusCodes.Status404NotFound, Title = "Torneio Não Encontrado", Detail = $"O campeonato '{id}' não foi encontrado." })
            : Ok(tournament);
    }

    [AllowAnonymous]
    [HttpGet("{id:guid}/sync")]
    public async Task<IActionResult> CheckSync(Guid id, [FromQuery] int? knownVersion, CancellationToken ct)
    {
        var tournament = await _sender.Send(new GetTournamentByIdQuery(id), ct);
        if (tournament is null) return NotFound();
        if (knownVersion.HasValue && knownVersion.Value == tournament.Version) return NoContent();

        return Ok(new { tournamentId = tournament.Id, version = tournament.Version, status = tournament.Status, lastUpdatedAtUtc = tournament.LastUpdatedAtUtc });
    }

    [AllowAnonymous]
    [HttpGet("{id:guid}/events")]
    [ProducesResponseType(typeof(TournamentWebhookEventDto), StatusCodes.Status200OK)]
    public Task StreamEvents(Guid id, CancellationToken ct) =>
        _webhookStream.StreamToResponseAsync(Response, id, ct);

    [AllowAnonymous]
    [HttpGet("events")]
    [ProducesResponseType(typeof(TournamentWebhookEventDto), StatusCodes.Status200OK)]
    public Task StreamGlobalEvents(CancellationToken ct) =>
        _webhookStream.StreamToResponseAsync(Response, null, ct);

    [AllowAnonymous]
    [HttpGet("{id:guid}/matches")]
    public async Task<ActionResult<IReadOnlyList<MatchSeriesDto>>> GetMatches(Guid id, CancellationToken ct) =>
        Ok(await _sender.Send(new GetTournamentMatchesQuery(id), ct));

    [AllowAnonymous]
    [HttpGet("{id:guid}/standings")]
    public async Task<ActionResult<TournamentStandingsDto>> GetStandings(Guid id, CancellationToken ct) =>
        Ok(await _sender.Send(new GetTournamentStandingsQuery(id), ct));

    [AllowAnonymous]
    [HttpGet("{id:guid}/teams")]
    public async Task<ActionResult<IReadOnlyList<TournamentTeamDto>>> GetTeams(Guid id, CancellationToken ct)
    {
        var tournament = await _sender.Send(new GetTournamentByIdQuery(id), ct);
        return tournament is null ? NotFound() : Ok(tournament.Teams);
    }

    [Authorize(Roles = "TournamentAdmin,SuperAdmin")]
    [HttpPost("{id:guid}/teams")]
    public async Task<IActionResult> AddTeam(Guid id, [FromBody] AddTeamRequest request, CancellationToken ct)
    {
        await _sender.Send(new AddTeamToTournamentCommand(id, request.TeamId ?? Guid.NewGuid(), request.TeamName), ct);
        return Ok(new { message = $"Equipe '{request.TeamName}' adicionada ao torneio." });
    }

    [Authorize(Roles = "TournamentAdmin,SuperAdmin")]
    [HttpPost("{id:guid}/registrations/close")]
    public async Task<ActionResult<TournamentDto>> CloseRegistrations(Guid id, CancellationToken ct) =>
        Ok(await _sender.Send(new CloseRegistrationsCommand(id), ct));

    [Authorize(Roles = "TournamentAdmin,SuperAdmin")]
    [HttpPost("{id:guid}/registrations/open")]
    public async Task<ActionResult<TournamentDto>> OpenRegistrations(Guid id, CancellationToken ct) =>
        Ok(await _sender.Send(new OpenRegistrationsCommand(id), ct));

    [Authorize(Roles = "TournamentAdmin,SuperAdmin")]
    [HttpPost("{id:guid}/start")]
    public async Task<IActionResult> Start(Guid id, CancellationToken ct)
    {
        await _sender.Send(new StartTournamentCommand(id), ct);
        return Ok(new { message = "Torneio iniciado com sucesso!" });
    }

    [Authorize(Roles = "TournamentAdmin,SuperAdmin")]
    [HttpPost("{id:guid}/playoffs/generate")]
    public async Task<ActionResult<TournamentDto>> AdvanceToPlayoffs(Guid id, CancellationToken ct) =>
        Ok(await _sender.Send(new AdvanceToPlayoffsCommand(id), ct));

    [Authorize(Roles = "TournamentAdmin,SuperAdmin")]
    [HttpPost("{id:guid}/playoffs/revert")]
    public async Task<ActionResult<TournamentDto>> RevertToGroupStage(Guid id, CancellationToken ct) =>
        Ok(await _sender.Send(new RevertToGroupStageCommand(id), ct));

    [Authorize(Roles = "TournamentAdmin,SuperAdmin")]
    [HttpPost("{id:guid}/cancel")]
    public async Task<ActionResult<TournamentDto>> Cancel(Guid id, CancellationToken ct) =>
        Ok(await _sender.Send(new CancelTournamentCommand(id), ct));

    [Authorize(Roles = "TournamentAdmin,SuperAdmin")]
    [HttpPost("{id:guid}/results/lock")]
    public async Task<ActionResult<TournamentDto>> LockResults(
        Guid id,
        [FromBody(EmptyBodyBehavior = Microsoft.AspNetCore.Mvc.ModelBinding.EmptyBodyBehavior.Allow)] LockResultSubmissionRequest? request,
        CancellationToken ct)
    {
        var adminId = User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? User?.FindFirst("sub")?.Value
            ?? "Admin";

        var tournament = await _sender.Send(new LockResultSubmissionCommand(id, request?.Reason, adminId), ct);
        return Ok(tournament);
    }

    [Authorize(Roles = "TournamentAdmin,SuperAdmin")]
    [HttpPost("{id:guid}/results/unlock")]
    public async Task<ActionResult<TournamentDto>> UnlockResults(
        Guid id,
        CancellationToken ct)
    {
        var adminId = User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? User?.FindFirst("sub")?.Value
            ?? "Admin";

        var tournament = await _sender.Send(new UnlockResultSubmissionCommand(id, adminId), ct);
        return Ok(tournament);
    }

    [Authorize(Roles = "TournamentAdmin,SuperAdmin")]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _sender.Send(new DeleteTournamentCommand(id), ct);
        return Ok(new { message = "Torneio excluído com sucesso." });
    }

    [Authorize(Roles = "TournamentAdmin,SuperAdmin")]
    [HttpPost("upload-banner")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadBanner(IFormFile? file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new ProblemDetails { Title = "Nenhum arquivo enviado", Detail = "Por favor, selecione uma imagem para o banner.", Status = StatusCodes.Status400BadRequest });

        await using var stream = file.OpenReadStream();
        var validation = _bannerValidator.Validate(stream, file.FileName, file.Length);
        if (!validation.IsValid)
            return BadRequest(new ProblemDetails { Title = validation.ErrorTitle, Detail = validation.ErrorDetail, Status = StatusCodes.Status400BadRequest });

        var relativeUrl = await _fileStorageService.UploadAsync(stream, file.FileName, file.ContentType, ct);
        return Ok(new { bannerUrl = relativeUrl });
    }
}
