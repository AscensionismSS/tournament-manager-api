using System.Security.Claims;
using Asp.Versioning;
using Identity.Application.Features.Members.GetMemberById;
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

    [Authorize]
    [HttpPost("{draftSessionId:guid}/pool")]
    public async Task<ActionResult<DraftStateSnapshotDto>> AddPlayerToPool(
        Guid draftSessionId,
        [FromBody(EmptyBodyBehavior = Microsoft.AspNetCore.Mvc.ModelBinding.EmptyBodyBehavior.Allow)] AddPlayerRequest? request,
        CancellationToken ct)
    {
        var (actingUserId, isAdmin) = GetActingUserContext();
        if (!isAdmin)
        {
            if (!Guid.TryParse(actingUserId, out var parsedUserId) || parsedUserId == Guid.Empty)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
                {
                    Title = "Acesso Negado",
                    Detail = "Identificador do usuário autenticado inválido.",
                    Status = StatusCodes.Status403Forbidden
                });
            }

            var memberResult = await _sender.Send(new GetMemberByIdQuery(parsedUserId), ct);
            if (memberResult.IsFailure)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
                {
                    Title = "Usuário Não Encontrado",
                    Detail = "Não foi possível localizar o cadastro oficial do usuário autenticado.",
                    Status = StatusCodes.Status403Forbidden
                });
            }

            var validationError = ValidateOfficialPlayerProfile(memberResult.Value);
            if (validationError is not null)
            {
                return validationError;
            }

            var officialUser = memberResult.Value;
            return Ok(await _sender.Send(new AddPlayerToPoolCommand(
                draftSessionId,
                officialUser.UserId,
                officialUser.DisplayName,
                officialUser.SteamId,
                officialUser.Roles,
                officialUser.PreferredRole,
                officialUser.RankTier,
                officialUser.AvatarUrl,
                IsAdmin: false), ct));
        }

        if (request is null)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Dados Inválidos",
                Detail = "Os dados do jogador são obrigatórios.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        // Se o próprio administrador estiver se inscrevendo no torneio, utiliza os dados oficiais do banco
        if (Guid.TryParse(actingUserId, out var adminUserId) &&
            adminUserId != Guid.Empty &&
            request.PlayerId == adminUserId)
        {
            var selfAdminResult = await _sender.Send(new GetMemberByIdQuery(adminUserId), ct);
            if (selfAdminResult.IsSuccess)
            {
                var validationError = ValidateOfficialPlayerProfile(selfAdminResult.Value);
                if (validationError is not null)
                {
                    return validationError;
                }

                var officialAdmin = selfAdminResult.Value;
                return Ok(await _sender.Send(new AddPlayerToPoolCommand(
                    draftSessionId,
                    officialAdmin.UserId,
                    officialAdmin.DisplayName,
                    officialAdmin.SteamId,
                    officialAdmin.Roles,
                    officialAdmin.PreferredRole,
                    officialAdmin.RankTier,
                    officialAdmin.AvatarUrl,
                    IsAdmin: true), ct));
            }
        }

        var effectivePlayerId = request.PlayerId ?? Guid.NewGuid();
        var effectiveExternalUserId = request.ExternalUserId;
        if (string.IsNullOrWhiteSpace(effectiveExternalUserId) &&
            request.PlayerId.HasValue &&
            request.PlayerId.Value != Guid.Empty)
        {
            var targetMember = await _sender.Send(new GetMemberByIdQuery(request.PlayerId.Value), ct);
            if (targetMember.IsSuccess && !string.IsNullOrWhiteSpace(targetMember.Value.SteamId))
            {
                effectiveExternalUserId = targetMember.Value.SteamId;
            }
        }

        return Ok(await _sender.Send(new AddPlayerToPoolCommand(
            draftSessionId, effectivePlayerId, request.Name,
            effectiveExternalUserId, request.Roles, request.PreferredRole, request.RankTier, request.AvatarUrl, IsAdmin: true), ct));
    }

    [Authorize(Roles = "TournamentAdmin,SuperAdmin")]
    [HttpPost("{draftSessionId:guid}/late-players")]
    public async Task<ActionResult<DraftStateSnapshotDto>> AddLatePlayer(Guid draftSessionId, [FromBody] AddLatePlayerRequest request, CancellationToken ct)
    {
        var effectivePlayerId = request.PlayerId ?? Guid.NewGuid();
        var effectiveExternalUserId = request.ExternalUserId;
        var effectiveRankTier = request.RankTier;
        var effectiveAvatarUrl = request.AvatarUrl;

        if (request.PlayerId.HasValue && request.PlayerId.Value != Guid.Empty && string.IsNullOrWhiteSpace(effectiveExternalUserId))
        {
            var memberResult = await _sender.Send(new GetMemberByIdQuery(request.PlayerId.Value), ct);
            if (memberResult.IsSuccess)
            {
                effectiveExternalUserId = memberResult.Value.SteamId;
                effectiveRankTier ??= memberResult.Value.RankTier;
                effectiveAvatarUrl ??= memberResult.Value.AvatarUrl;
            }
        }

        return Ok(await _sender.Send(new AddLatePlayerToPoolCommand(
            draftSessionId, effectivePlayerId, request.Name,
            effectiveExternalUserId, request.Roles, request.PreferredRole, effectiveRankTier, effectiveAvatarUrl, request.ExpectedVersion), ct));
    }

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

    private ActionResult? ValidateOfficialPlayerProfile(Identity.Contracts.Members.MemberDto officialUser)
    {
        if (!officialUser.IsActive)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
            {
                Title = "Conta Desativada",
                Detail = "Sua conta está desativada e não pode participar de torneios.",
                Status = StatusCodes.Status403Forbidden
            });
        }

        if (!officialUser.IsTournamentEligible)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
            {
                Title = "Inscrição Não Permitida",
                Detail = "Sua conta não está elegível para inscrição em torneios no momento.",
                Status = StatusCodes.Status403Forbidden
            });
        }

        if (string.IsNullOrWhiteSpace(officialUser.SteamId))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Steam Não Vinculada",
                Detail = "Você precisa vincular uma conta Steam válida antes de se inscrever no draft.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        return null;
    }

    private (string ActingUserId, bool IsAdmin) GetActingUserContext()
    {
        if (User?.Identity?.IsAuthenticated != true)
            return (string.Empty, false);

        var actingUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value
            ?? string.Empty;
        var isAdmin = User.IsInRole("TournamentAdmin") || User.IsInRole("SuperAdmin");
        return (actingUserId, isAdmin);
    }
}
