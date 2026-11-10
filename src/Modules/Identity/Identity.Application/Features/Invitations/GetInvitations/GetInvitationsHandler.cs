using Identity.Contracts.Invitations;
using Identity.Domain.Repositories;
using MediatR;

namespace Identity.Application.Features.Invitations.GetInvitations;

public class GetInvitationsHandler : IRequestHandler<GetInvitationsQuery, IReadOnlyList<InvitationDto>>
{
    private readonly IInvitationRepository _invitationRepository;

    public GetInvitationsHandler(IInvitationRepository invitationRepository)
    {
        _invitationRepository = invitationRepository;
    }

    public async Task<IReadOnlyList<InvitationDto>> Handle(GetInvitationsQuery request, CancellationToken cancellationToken)
    {
        var invitations = await _invitationRepository.GetAllAsync(cancellationToken);

        return invitations.Select(i => new InvitationDto(
            i.Id,
            i.Code,
            i.Email,
            i.TargetRole.ToString(),
            i.CreatedByUserId,
            i.CreatedAtUtc,
            i.ExpiresAtUtc,
            i.IsUsed,
            i.UsedAtUtc,
            i.IsRevoked,
            i.RevokedAtUtc,
            i.ValidHours
        )).ToList();
    }
}
