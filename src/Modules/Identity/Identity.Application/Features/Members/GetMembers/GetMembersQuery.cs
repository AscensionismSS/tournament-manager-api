using BuildingBlocks.Domain;
using Identity.Contracts.Members;
using MediatR;

namespace Identity.Application.Features.Members.GetMembers;

public record GetMembersQuery(
    string? Search = null,
    string? Role = null,
    bool? HasSteam = null,
    bool? IsActive = null
) : IRequest<Result<IReadOnlyList<MemberDto>>>;
