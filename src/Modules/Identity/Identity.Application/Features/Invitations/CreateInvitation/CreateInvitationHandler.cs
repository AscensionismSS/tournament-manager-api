using BuildingBlocks.Domain;
using Identity.Application.Common.Interfaces;
using Identity.Contracts.Invitations;
using Identity.Domain.Entities;
using Identity.Domain.Enums;
using Identity.Domain.Repositories;
using MediatR;

namespace Identity.Application.Features.Invitations.CreateInvitation;

public class CreateInvitationHandler : IRequestHandler<CreateInvitationCommand, Result<InvitationDto>>
{
    private readonly IInvitationRepository _invitationRepository;
    private readonly IUserRepository _userRepository;
    private readonly ICurrentUserService _currentUserService;

    public CreateInvitationHandler(
        IInvitationRepository invitationRepository,
        IUserRepository userRepository,
        ICurrentUserService currentUserService)
    {
        _invitationRepository = invitationRepository;
        _userRepository = userRepository;
        _currentUserService = currentUserService;
    }

    public async Task<Result<InvitationDto>> Handle(CreateInvitationCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUserService.IsAuthenticated || !_currentUserService.UserId.HasValue)
        {
            return Result.Failure<InvitationDto>("Usuário não autenticado.");
        }

        // TournamentAdmin só pode convidar usuários comuns (User). Apenas SuperAdmin pode convidar Admins.
        if (request.TargetRole != UserRole.User && !_currentUserService.IsSuperAdmin)
        {
            return Result.Failure<InvitationDto>("Apenas o SuperAdmin pode criar convites com privilégios administrativos.");
        }

        if (!Invitation.AllowedExpirationHours.Contains(request.ValidHours))
        {
            return Result.Failure<InvitationDto>($"A validade do convite deve ser de 24, 48 ou 72 horas. Informado: {request.ValidHours}h.");
        }

        string normalizedEmail = request.Email.Trim().ToLowerInvariant();

        bool userExists = await _userRepository.ExistsByEmailAsync(normalizedEmail, cancellationToken);
        if (userExists)
        {
            return Result.Failure<InvitationDto>($"Já existe um usuário cadastrado com o email '{normalizedEmail}'.");
        }

        var existingPending = await _invitationRepository.GetPendingByEmailAsync(normalizedEmail, cancellationToken);
        if (existingPending is not null && !existingPending.IsExpired)
        {
            return Result.Success(new InvitationDto(
                existingPending.Id,
                existingPending.Code,
                existingPending.Email,
                existingPending.TargetRole.ToString(),
                existingPending.CreatedByUserId,
                existingPending.CreatedAtUtc,
                existingPending.ExpiresAtUtc,
                existingPending.IsUsed,
                existingPending.UsedAtUtc,
                existingPending.IsRevoked,
                existingPending.RevokedAtUtc,
                existingPending.ValidHours
            ));
        }

        var invitation = new Invitation(
            normalizedEmail,
            request.TargetRole,
            _currentUserService.UserId.Value,
            TimeSpan.FromHours(request.ValidHours)
        );

        await _invitationRepository.AddAsync(invitation, cancellationToken);

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
