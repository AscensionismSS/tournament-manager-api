using Identity.Contracts.Invitations;
using MediatR;

namespace Identity.Application.Features.Invitations.GetInvitations;

public record GetInvitationsQuery : IRequest<IReadOnlyList<InvitationDto>>;
