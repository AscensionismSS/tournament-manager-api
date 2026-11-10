using BuildingBlocks.Domain;
using Identity.Contracts.Auth;
using Identity.Domain.Enums;
using Identity.Domain.Repositories;
using MediatR;

namespace Identity.Application.Features.Auth.RecoverLogin;

public class RecoverLoginHandler : IRequestHandler<RecoverLoginQuery, Result<RecoveredLoginDto>>
{
    private readonly IUserRepository _userRepository;
    private readonly IAccountRecoveryRepository _recoveryRepository;

    public RecoverLoginHandler(
        IUserRepository userRepository,
        IAccountRecoveryRepository recoveryRepository)
    {
        _userRepository = userRepository;
        _recoveryRepository = recoveryRepository;
    }

    public async Task<Result<RecoveredLoginDto>> Handle(
        RecoverLoginQuery request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Token))
        {
            return Result.Failure<RecoveredLoginDto>("O token de recuperação é obrigatório.");
        }

        var recoveryToken = await _recoveryRepository.GetByTokenAsync(request.Token.Trim().ToLowerInvariant(), cancellationToken);
        if (recoveryToken is null || recoveryToken.Type != RecoveryTokenType.LoginRecovery || !recoveryToken.IsActive(DateTime.UtcNow))
        {
            return Result.Failure<RecoveredLoginDto>("Link de recuperação de login inválido ou expirado.");
        }

        var user = await _userRepository.GetByIdAsync(recoveryToken.UserId, cancellationToken);
        if (user is null)
        {
            return Result.Failure<RecoveredLoginDto>("Usuário associado ao link de recuperação não foi encontrado.");
        }

        // Uso único para proteção de privacidade
        recoveryToken.Consume(DateTime.UtcNow);
        await _recoveryRepository.UpdateAsync(recoveryToken, cancellationToken);

        return Result.Success(new RecoveredLoginDto(user.DisplayName, user.Email));
    }
}
