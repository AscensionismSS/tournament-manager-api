using Asp.Versioning;
using Identity.Application.Features.Invitations.CreateInvitation;
using Identity.Contracts.Invitations;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TournamentCreator.WebApi.Controllers.v1.Requests;
using TournamentCreator.WebApi.Extensions;

namespace TournamentCreator.WebApi.Controllers.v1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/invitations")]
public class InvitationsController : ControllerBase
{
    private readonly ISender _sender;

    public InvitationsController(ISender sender)
    {
        _sender = sender;
    }

    [Authorize(Roles = "TournamentAdmin,SuperAdmin")]
    [HttpGet("expiration-options")]
    public ActionResult<IReadOnlyList<int>> GetExpirationOptions()
    {
        return Ok(Identity.Domain.Entities.Invitation.AllowedExpirationHours);
    }

    [Authorize(Roles = "TournamentAdmin,SuperAdmin")]
    [HttpPost]
    public async Task<ActionResult<InvitationDto>> CreateInvitation(
        [FromBody] CreateInvitationApiRequest request,
        CancellationToken ct)
    {
        int validHours = request.ValidHours.GetValueOrDefault(24);
        var result = await _sender.Send(new CreateInvitationCommand(request.Email, request.TargetRole, validHours), ct);
        if (result.IsFailure)
        {
            return result.ToActionResult("Falha ao Criar Convite");
        }

        return CreatedAtAction(nameof(CreateInvitation), new { id = result.Value.Id }, result.Value);
    }

    [Authorize(Roles = "TournamentAdmin,SuperAdmin")]
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<InvitationDto>>> GetInvitations(CancellationToken ct)
    {
        var invitations = await _sender.Send(new Identity.Application.Features.Invitations.GetInvitations.GetInvitationsQuery(), ct);
        return Ok(invitations);
    }

    [Authorize(Roles = "TournamentAdmin,SuperAdmin")]
    [HttpDelete("{id:guid}")]
    [HttpDelete("{id:guid}/revoke")]
    [HttpPost("{id:guid}/revoke")]
    public async Task<ActionResult<InvitationDto>> RevokeInvitation(Guid id, CancellationToken ct)
    {
        var result = await _sender.Send(new Identity.Application.Features.Invitations.RevokeInvitation.RevokeInvitationCommand(id), ct);
        return result.ToActionResult("Falha ao Desativar Convite");
    }

    [AllowAnonymous]
    [HttpGet("validate/{code}")]
    public async Task<ActionResult<ValidateInvitationDto>> ValidateInvitation(string code, CancellationToken ct)
    {
        var result = await _sender.Send(new Identity.Application.Features.Invitations.ValidateInvitation.ValidateInvitationQuery(code), ct);
        return result.ToActionResult("Erro ao Validar Convite");
    }
}
