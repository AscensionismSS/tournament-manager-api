using BuildingBlocks.Domain;
using Identity.Contracts.Members;
using MediatR;

namespace Identity.Application.Features.Members.UpdateMemberStatus;

public record UpdateMemberStatusCommand(
    Guid UserId,
    bool IsActive,
    string? Reason = null
) : IRequest<Result<MemberDto>>;
