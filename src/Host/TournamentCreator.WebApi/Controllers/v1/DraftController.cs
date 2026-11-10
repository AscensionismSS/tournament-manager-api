using System.Security.Claims;
using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TournamentCreator.WebApi.Controllers.v1.Requests;
using Tournaments.Application.Features.Draft.AddLatePlayerToPool;
using Tournaments.Application.Features.Draft.AddPlayerToPool;
using Tournaments.Application.Features.Draft.AdvanceToCaptainOrderSelection;
using Tournaments.Application.Features.Draft.AdvanceToSubstitutes;
using Tournaments.Application.Features.Draft.AllocateReserve;
using Tournaments.Application.Features.Draft.CancelSubstitution;
using Tournaments.Application.Features.Draft.CompleteDraft;
using Tournaments.Application.Features.Draft.ConfirmSubstitution;
using Tournaments.Application.Features.Draft.FinalizeCaptainOrder;
using Tournaments.Application.Features.Draft.GetDraftSnapshot;
using Tournaments.Application.Features.Draft.PickStarterPlayer;
using Tournaments.Application.Features.Draft.RemoveCaptain;
using Tournaments.Application.Features.Draft.RemovePlayerFromPool;
using Tournaments.Application.Features.Draft.RemoveReserve;
using Tournaments.Application.Features.Draft.RenameTeam;
using Tournaments.Application.Features.Draft.SelectCaptain;
using Tournaments.Application.Features.Draft.StartSubstitution;
using Tournaments.Contracts.Draft;

namespace TournamentCreator.WebApi.Controllers.v1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/draft")]
public class DraftController : ControllerBase
{
    private readonly ISender _sender;

    public DraftController(ISender sender) => _sender = sender;

    [AllowAnonymous]
    [HttpGet("{draftSessionId:guid}")]
    public async Task<ActionResult<DraftStateSnapshotDto>> GetSnapshot(Guid draftSessionId, CancellationToken ct) =>
        Ok(await _sender.Send(new GetDraftSnapshotQuery(draftSessionId), ct));

    [Authorize(Roles = "TournamentAdmin,SuperAdmin")]
    [HttpPost("{draftSessionId:guid}/pool")]
    public async Task<ActionResult<DraftStateSnapshotDto>> AddPlayerToPool(Guid draftSessionId, [FromBody] AddPlayerRequest request, CancellationToken ct) =>
        Ok(await _sender.Send(new AddPlayerToPoolCommand(
            draftSessionId, request.PlayerId ?? Guid.NewGuid(), request.Name,
            request.ExternalUserId, request.Roles, request.PreferredRole, request.RankTier, request.AvatarUrl), ct));

    [Authorize(Roles = "TournamentAdmin,SuperAdmin")]
    [HttpPost("{draftSessionId:guid}/late-players")]
    public async Task<ActionResult<DraftStateSnapshotDto>> AddLatePlayer(Guid draftSessionId, [FromBody] AddLatePlayerRequest request, CancellationToken ct) =>
        Ok(await _sender.Send(new AddLatePlayerToPoolCommand(
            draftSessionId, request.PlayerId ?? Guid.NewGuid(), request.Name,
            request.ExternalUserId, request.Roles, request.PreferredRole, request.RankTier, request.AvatarUrl, request.ExpectedVersion), ct));

    [Authorize(Roles = "TournamentAdmin,SuperAdmin")]
    [HttpPost("{draftSessionId:guid}/captains")]
    [HttpPost("{draftSessionId:guid}/captain")]
    public async Task<ActionResult<DraftStateSnapshotDto>> SelectCaptain(Guid draftSessionId, [FromBody] SelectCaptainRequest request, CancellationToken ct) =>
        Ok(await _sender.Send(new SelectCaptainCommand(draftSessionId, request.PlayerId, request.ExpectedVersion, request.TeamId), ct));

    [Authorize(Roles = "TournamentAdmin,SuperAdmin")]
    [HttpDelete("{draftSessionId:guid}/captains/{teamId:guid}")]
    [HttpDelete("{draftSessionId:guid}/captain/{teamId:guid}")]
    public async Task<ActionResult<DraftStateSnapshotDto>> RemoveCaptain(Guid draftSessionId, Guid teamId, [FromQuery] int expectedVersion, CancellationToken ct) =>
        Ok(await _sender.Send(new RemoveCaptainCommand(draftSessionId, teamId, expectedVersion), ct));

    [Authorize(Roles = "TournamentAdmin,SuperAdmin")]
    [HttpPost("{draftSessionId:guid}/advance-to-order-selection")]
    [HttpPost("{draftSessionId:guid}/advance-to-order")]
    public async Task<ActionResult<DraftStateSnapshotDto>> AdvanceToOrderSelection(Guid draftSessionId, [FromBody] VersionOnlyRequest request, CancellationToken ct) =>
        Ok(await _sender.Send(new AdvanceToCaptainOrderSelectionCommand(draftSessionId, request.ExpectedVersion), ct));

    [Authorize]
    [HttpDelete("{draftSessionId:guid}/pool/{playerId:guid}")]
    [HttpDelete("{draftSessionId:guid}/player/{playerId:guid}")]
    public async Task<ActionResult<DraftStateSnapshotDto>> RemovePlayerFromPool(Guid draftSessionId, Guid playerId, [FromQuery] int? expectedVersion, CancellationToken ct)
    {
        var (actingUserId, isAdmin) = GetActingUserContext();
        return Ok(await _sender.Send(new RemovePlayerFromPoolCommand(draftSessionId, playerId, actingUserId, isAdmin, expectedVersion), ct));
    }

    [Authorize(Roles = "TournamentAdmin,SuperAdmin")]
    [HttpPost("{draftSessionId:guid}/captains/finalize-order")]
    [HttpPost("{draftSessionId:guid}/finalize-order")]
    public async Task<ActionResult<DraftStateSnapshotDto>> FinalizeCaptainOrder(Guid draftSessionId, [FromBody] FinalizeOrderRequest request, CancellationToken ct) =>
        Ok(await _sender.Send(new FinalizeCaptainOrderCommand(draftSessionId, request.OrderType, request.ManualOrderIds, request.ExpectedVersion), ct));

    [Authorize(Roles = "TournamentAdmin,SuperAdmin")]
    [HttpPost("{draftSessionId:guid}/pick")]
    public async Task<ActionResult<DraftStateSnapshotDto>> PickStarterPlayer(Guid draftSessionId, [FromBody] PickPlayerRequest request, CancellationToken ct) =>
        Ok(await _sender.Send(new PickStarterPlayerCommand(draftSessionId, request.PlayerId, request.ExpectedVersion), ct));

    [Authorize(Roles = "TournamentAdmin,SuperAdmin")]
    [HttpPost("{draftSessionId:guid}/substitutions/start")]
    [HttpPost("{draftSessionId:guid}/substitution/start")]
    public async Task<ActionResult<DraftStateSnapshotDto>> StartSubstitution(Guid draftSessionId, [FromBody] StartSubstitutionRequest request, CancellationToken ct) =>
        Ok(await _sender.Send(new StartSubstitutionCommand(draftSessionId, request.PlayerId, request.TeamId, request.ExpectedVersion), ct));

    [Authorize(Roles = "TournamentAdmin,SuperAdmin")]
    [HttpPost("{draftSessionId:guid}/substitutions/confirm")]
    [HttpPost("{draftSessionId:guid}/substitution/confirm")]
    public async Task<ActionResult<DraftStateSnapshotDto>> ConfirmSubstitution(Guid draftSessionId, [FromBody] ConfirmSubstitutionRequest request, CancellationToken ct) =>
        Ok(await _sender.Send(new ConfirmSubstitutionCommand(draftSessionId, request.NewPlayerId, request.ExpectedVersion), ct));

    [Authorize(Roles = "TournamentAdmin,SuperAdmin")]
    [HttpPost("{draftSessionId:guid}/substitutions/cancel")]
    [HttpPost("{draftSessionId:guid}/substitution/cancel")]
    public async Task<ActionResult<DraftStateSnapshotDto>> CancelSubstitution(Guid draftSessionId, [FromBody] VersionOnlyRequest request, CancellationToken ct) =>
        Ok(await _sender.Send(new CancelSubstitutionCommand(draftSessionId, request.ExpectedVersion), ct));

    [Authorize(Roles = "TournamentAdmin,SuperAdmin")]
    [HttpPost("{draftSessionId:guid}/advance-to-substitutes")]
    [HttpPost("{draftSessionId:guid}/advance-to-reserves")]
    public async Task<ActionResult<DraftStateSnapshotDto>> AdvanceToSubstitutes(Guid draftSessionId, [FromBody] VersionOnlyRequest request, CancellationToken ct) =>
        Ok(await _sender.Send(new AdvanceToSubstitutesCommand(draftSessionId, request.ExpectedVersion), ct));

    [Authorize(Roles = "TournamentAdmin,SuperAdmin")]
    [HttpPost("{draftSessionId:guid}/reserves")]
    [HttpPost("{draftSessionId:guid}/reserve")]
    public async Task<ActionResult<DraftStateSnapshotDto>> AllocateReserve(Guid draftSessionId, [FromBody] AllocateReserveRequest request, CancellationToken ct) =>
        Ok(await _sender.Send(new AllocateReserveCommand(draftSessionId, request.TeamId, request.PlayerId, request.ExpectedVersion), ct));

    [Authorize(Roles = "TournamentAdmin,SuperAdmin")]
    [HttpDelete("{draftSessionId:guid}/teams/{teamId:guid}/reserves/{playerId:guid}")]
    [HttpDelete("{draftSessionId:guid}/teams/{teamId:guid}/reserve/{playerId:guid}")]
    [HttpDelete("{draftSessionId:guid}/reserves/{teamId:guid}/{playerId:guid}")]
    public async Task<ActionResult<DraftStateSnapshotDto>> RemoveReserve(Guid draftSessionId, Guid teamId, Guid playerId, [FromQuery] int expectedVersion, CancellationToken ct) =>
        Ok(await _sender.Send(new RemoveReserveCommand(draftSessionId, teamId, playerId, expectedVersion), ct));

    [Authorize(Roles = "TournamentAdmin,SuperAdmin")]
    [HttpPost("{draftSessionId:guid}/complete")]
    public async Task<ActionResult<DraftStateSnapshotDto>> CompleteDraft(Guid draftSessionId, [FromBody] VersionOnlyRequest request, CancellationToken ct) =>
        Ok(await _sender.Send(new CompleteDraftCommand(draftSessionId, request.ExpectedVersion), ct));

    [Authorize(Roles = "TournamentAdmin,SuperAdmin")]
    [HttpPost("{draftSessionId:guid}/teams/{teamId:guid}/rename")]
    [HttpPost("{draftSessionId:guid}/team/rename")]
    public async Task<ActionResult<DraftStateSnapshotDto>> RenameTeam(
        Guid draftSessionId,
        [FromRoute] Guid? teamId,
        [FromBody] RenameTeamRequest request,
        [FromQuery] Guid? queryTeamId,
        CancellationToken ct)
    {
        var effectiveTeamId = teamId ?? request.TeamId ?? queryTeamId;
        if (!effectiveTeamId.HasValue || effectiveTeamId.Value == Guid.Empty)
            return BadRequest(new ProblemDetails { Title = "Equipe Não Informada", Detail = "O identificador da equipe (teamId) deve ser fornecido na rota, query ou no corpo da requisição.", Status = StatusCodes.Status400BadRequest });

        var (actingUserId, isAdmin) = GetActingUserContext();
        return Ok(await _sender.Send(new RenameTeamCommand(draftSessionId, effectiveTeamId.Value, request.NewName, request.ExpectedVersion, actingUserId, isAdmin), ct));
    }

    private (string ActingUserId, bool IsAdmin) GetActingUserContext()
    {
        if (User is null)
            return (string.Empty, true);

        var actingUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value
            ?? string.Empty;
        var isAdmin = User.IsInRole("TournamentAdmin") || User.IsInRole("SuperAdmin");
        return (actingUserId, isAdmin);
    }
}
