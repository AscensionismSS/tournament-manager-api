using BuildingBlocks.Domain;
using Identity.Application.Common.Interfaces;
using Identity.Contracts.Auth;
using Identity.Domain.Entities;
using Identity.Domain.Enums;
using Identity.Domain.Repositories;
using MediatR;

namespace Identity.Application.Features.Auth.ForgotPassword;

public class ForgotPasswordHandler : IRequestHandler<ForgotPasswordCommand, Result<RecoveryActionResponseDto>>
{
    private const string GenericSuccessMessage =
        "Se o e-mail informado estiver cadastrado em nossa plataforma, as instruções de recuperação foram geradas com sucesso.";

    private readonly IUserRepository _userRepository;
    private readonly IAccountRecoveryRepository _recoveryRepository;
    private readonly IRootOwnerService? _rootOwnerService;

    public ForgotPasswordHandler(
        IUserRepository userRepository,
        IAccountRecoveryRepository recoveryRepository,
        IRootOwnerService? rootOwnerService = null)
    {
        _userRepository = userRepository;
        _recoveryRepository = recoveryRepository;
        _rootOwnerService = rootOwnerService;
    }

    public async Task<Result<RecoveryActionResponseDto>> Handle(
        ForgotPasswordCommand request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
        {
            return Result.Failure<RecoveryActionResponseDto>("O e-mail é obrigatório.");
        }

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var user = await _userRepository.GetByEmailAsync(normalizedEmail, cancellationToken);

        // Prevenção de Enumeração de Contas (OWASP) + Blindagem do Root Owner:
        // Se o usuário não existir ou for a conta AdminSeed, não gera token exposível no painel administrativo.
        if (user is null || _rootOwnerService?.IsRootOwner(user) == true)
        {
            return Result.Success(new RecoveryActionResponseDto(GenericSuccessMessage));
        }

        // Token expira em 1 hora para redefinição de senha
        var recoveryToken = new AccountRecoveryToken(
            user.Id,
            user.Email,
            RecoveryTokenType.PasswordReset,
            TimeSpan.FromHours(1)
        );

        await _recoveryRepository.AddAsync(recoveryToken, cancellationToken);

        return Result.Success(new RecoveryActionResponseDto(GenericSuccessMessage));
    }
}
