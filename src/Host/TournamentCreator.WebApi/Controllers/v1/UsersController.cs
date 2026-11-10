using Asp.Versioning;
using Identity.Application.Features.Members.GetMembers;
using Identity.Contracts.Members;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TournamentCreator.WebApi.Extensions;

namespace TournamentCreator.WebApi.Controllers.v1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/users")]
[Route("api/v{version:apiVersion}/members")]
public class UsersController : ControllerBase
{
    private readonly ISender _sender;

    public UsersController(ISender sender)
    {
        _sender = sender;
    }

    /// <summary>
    /// Lista os membros da plataforma com dados enriquecidos de perfil, rank do Dota 2, ID numérico de amigo, links e roles.
    /// </summary>
    [Authorize]
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<MemberDto>>> GetMembers(
        [FromQuery] string? search,
        [FromQuery] string? role,
        [FromQuery] bool? hasSteam,
        [FromQuery] bool? isActive,
        CancellationToken ct)
    {
        var result = await _sender.Send(new GetMembersQuery(search, role, hasSteam, isActive), ct);
        return result.ToActionResult("Erro ao listar membros");
    }

    /// <summary>
    /// Obtém os dados detalhados de um membro pelo seu identificador único.
    /// </summary>
    [Authorize]
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<MemberDto>> GetMemberById(Guid id, CancellationToken ct)
    {
        var result = await _sender.Send(new Identity.Application.Features.Members.GetMemberById.GetMemberByIdQuery(id), ct);
        return result.ToActionResult("Membro Não Encontrado", StatusCodes.Status404NotFound);
    }

    /// <summary>
    /// Altera o status de ativação da conta de um membro (Ativo / Inativo).
    /// </summary>
    [Authorize(Roles = "TournamentAdmin,SuperAdmin")]
    [HttpPatch("{id:guid}/status")]
    public async Task<ActionResult<MemberDto>> UpdateStatus(
        Guid id,
        [FromBody] UpdateMemberStatusApiRequest request,
        CancellationToken ct)
    {
        var result = await _sender.Send(new Identity.Application.Features.Members.UpdateMemberStatus.UpdateMemberStatusCommand(
            id,
            request.IsActive,
            request.Reason), ct);
        return result.ToActionResult("Falha ao Atualizar Status do Membro");
    }
}

public record UpdateMemberStatusApiRequest(bool IsActive, string? Reason = null);
