using BuildingBlocks.Domain;
using Identity.Application.Common.Interfaces;
using Identity.Contracts.Invitations;
using Identity.Domain.Repositories;
using MediatR;

namespace Identity.Application.Features.Invitations.RevokeInvitation;

public class RevokeInvitationHandler : IRequestHandler<RevokeInvitationCommand, Result<InvitationDto>>
{
    private readonly IInvitationRepository _invitationRepository;
    private readonly ICurrentUserService _currentUserService;

    public RevokeInvitationHandler(
        IInvitationRepository invitationRepository,
        ICurrentUserService currentUserService)
    {
        _invitationRepository = invitationRepository;
        _currentUserService = currentUserService;
    }

    public async Task<Result<InvitationDto>> Handle(RevokeInvitationCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUserService.IsAuthenticated || !_currentUserService.UserId.HasValue)
        {
            return Result.Failure<InvitationDto>("Usuário não autenticado.");
        }

        var invitation = await _invitationRepository.GetByIdAsync(request.InvitationId, cancellationToken);
        if (invitation is null)
        {
            return Result.Failure<InvitationDto>("Convite não encontrado.");
        }

        try
        {
            invitation.Revoke();
        }
        catch (DomainException ex)
        {
            return Result.Failure<InvitationDto>(ex.Message);
        }

        await _invitationRepository.UpdateAsync(invitation, cancellationToken);

        return Result.Success(new InvitationDto(
            invitation.Id,
            invitation.Code,
            invitation.Email,
            invitation.TargetRole.ToString(),
            invitation.CreatedByUserId,
            invitation.CreatedAtUtc,
            invitation.ExpiresAtUtc,
            invitation.IsUsed,
            invitation.UsedAtUtc,
            invitation.IsRevoked,
            invitation.RevokedAtUtc,
            invitation.ValidHours
        ));
    }
}
