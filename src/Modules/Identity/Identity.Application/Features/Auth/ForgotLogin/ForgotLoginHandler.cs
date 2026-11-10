using BuildingBlocks.Domain;
using Identity.Contracts.Auth;
using Identity.Domain.Entities;
using Identity.Domain.Enums;
using Identity.Domain.Repositories;
using MediatR;

namespace Identity.Application.Features.Auth.ForgotLogin;

public class ForgotLoginHandler : IRequestHandler<ForgotLoginCommand, Result<RecoveryActionResponseDto>>
{
    private const string GenericSuccessMessage =
        "Se o e-mail informado estiver cadastrado em nossa plataforma, o link para visualização do seu login foi gerado com sucesso.";

    private readonly IUserRepository _userRepository;
    private readonly IAccountRecoveryRepository _recoveryRepository;

    public ForgotLoginHandler(
        IUserRepository userRepository,
        IAccountRecoveryRepository recoveryRepository)
    {
        _userRepository = userRepository;
        _recoveryRepository = recoveryRepository;
    }

    public async Task<Result<RecoveryActionResponseDto>> Handle(
        ForgotLoginCommand request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
        {
            return Result.Failure<RecoveryActionResponseDto>("O e-mail é obrigatório.");
        }

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var user = await _userRepository.GetByEmailAsync(normalizedEmail, cancellationToken);

        // Prevenção de Enumeração de Contas (OWASP)
        if (user is null)
        {
            return Result.Success(new RecoveryActionResponseDto(GenericSuccessMessage));
        }

        // Token expira em 24 horas para recuperação de login
        var recoveryToken = new AccountRecoveryToken(
            user.Id,
            user.Email,
            RecoveryTokenType.LoginRecovery,
            TimeSpan.FromHours(24)
        );

        await _recoveryRepository.AddAsync(recoveryToken, cancellationToken);

        return Result.Success(new RecoveryActionResponseDto(GenericSuccessMessage));
    }
}
