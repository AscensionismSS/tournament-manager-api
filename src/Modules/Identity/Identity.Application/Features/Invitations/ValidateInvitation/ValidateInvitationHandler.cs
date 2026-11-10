using BuildingBlocks.Domain;
using Identity.Contracts.Invitations;
using Identity.Domain.Repositories;
using MediatR;

namespace Identity.Application.Features.Invitations.ValidateInvitation;

public record ValidateInvitationQuery(string Code) : IRequest<Result<ValidateInvitationDto>>;

public class ValidateInvitationHandler : IRequestHandler<ValidateInvitationQuery, Result<ValidateInvitationDto>>
{
    private readonly IInvitationRepository _invitationRepository;

    public ValidateInvitationHandler(IInvitationRepository invitationRepository)
    {
        _invitationRepository = invitationRepository;
    }

    public async Task<Result<ValidateInvitationDto>> Handle(ValidateInvitationQuery request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Code))
        {
            return Result.Success(new ValidateInvitationDto(false, ErrorMessage: "Código de convite não informado."));
        }

        var invitation = await _invitationRepository.GetByCodeAsync(request.Code.Trim(), ct);
        if (invitation is null)
        {
            return Result.Success(new ValidateInvitationDto(false, ErrorMessage: "Convite não encontrado ou inválido."));
        }

        if (invitation.IsUsed)
        {
            return Result.Success(new ValidateInvitationDto(false, ErrorMessage: "Este convite já foi utilizado."));
        }

        if (invitation.IsRevoked)
        {
            return Result.Success(new ValidateInvitationDto(false, ErrorMessage: "Este convite foi desativado pelo administrador."));
        }

        if (invitation.IsExpired)
        {
            return Result.Success(new ValidateInvitationDto(false, ErrorMessage: "Este convite expirou."));
        }

        return Result.Success(new ValidateInvitationDto(
            IsValid: true,
            Email: invitation.Email,
            TargetRole: invitation.TargetRole.ToString()
        ));
    }
}
