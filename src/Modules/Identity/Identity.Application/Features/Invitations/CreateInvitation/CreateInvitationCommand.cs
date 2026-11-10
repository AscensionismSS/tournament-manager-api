using BuildingBlocks.Domain;
using Identity.Contracts.Invitations;
using Identity.Domain.Enums;
using MediatR;

namespace Identity.Application.Features.Invitations.CreateInvitation;

public record CreateInvitationCommand(string Email, UserRole TargetRole, int ValidHours = 24) : IRequest<Result<InvitationDto>>;
