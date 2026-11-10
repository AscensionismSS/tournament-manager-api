using BuildingBlocks.Domain;
using Identity.Contracts.Members;
using MediatR;

namespace Identity.Application.Features.Members.GetMemberById;

public record GetMemberByIdQuery(Guid UserId) : IRequest<Result<MemberDto>>;
