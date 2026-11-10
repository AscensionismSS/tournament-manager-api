using BuildingBlocks.Domain;
using Identity.Contracts.Invitations;
using MediatR;

namespace Identity.Application.Features.Invitations.RevokeInvitation;

public record RevokeInvitationCommand(Guid InvitationId) : IRequest<Result<InvitationDto>>;
