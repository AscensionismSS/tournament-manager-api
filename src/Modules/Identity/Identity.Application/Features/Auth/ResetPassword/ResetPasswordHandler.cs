using BuildingBlocks.Domain;
using Identity.Application.Common.Interfaces;
using Identity.Contracts.Auth;
using Identity.Domain.Enums;
using Identity.Domain.Repositories;
using MediatR;

namespace Identity.Application.Features.Auth.ResetPassword;

public class ResetPasswordHandler : IRequestHandler<ResetPasswordCommand, Result<RecoveryActionResponseDto>>
{
    private readonly IUserRepository _userRepository;
    private readonly IAccountRecoveryRepository _recoveryRepository;
    private readonly IPasswordHasher _passwordHasher;

    public ResetPasswordHandler(
        IUserRepository userRepository,
        IAccountRecoveryRepository recoveryRepository,
        IPasswordHasher passwordHasher)
    {
        _userRepository = userRepository;
        _recoveryRepository = recoveryRepository;
        _passwordHasher = passwordHasher;
    }

    public async Task<Result<RecoveryActionResponseDto>> Handle(
        ResetPasswordCommand request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Token))
        {
            return Result.Failure<RecoveryActionResponseDto>("O token de recuperação é obrigatório.");
        }

        if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 6)
        {
            return Result.Failure<RecoveryActionResponseDto>("A nova senha deve ter no mínimo 6 caracteres.");
        }

        if (request.NewPassword != request.ConfirmPassword)
        {
            return Result.Failure<RecoveryActionResponseDto>("A nova senha e a confirmação de senha não conferem.");
        }

        var recoveryToken = await _recoveryRepository.GetByTokenAsync(request.Token.Trim().ToLowerInvariant(), cancellationToken);
        if (recoveryToken is null || recoveryToken.Type != RecoveryTokenType.PasswordReset || !recoveryToken.IsActive(DateTime.UtcNow))
        {
            return Result.Failure<RecoveryActionResponseDto>("Link de redefinição de senha inválido ou expirado.");
        }

        var user = await _userRepository.GetByIdAsync(recoveryToken.UserId, cancellationToken);
        if (user is null)
        {
            return Result.Failure<RecoveryActionResponseDto>("Usuário associado ao token não foi encontrado.");
        }

        // Atualiza a senha com hash seguro PBKDF2
        var newHash = _passwordHasher.HashPassword(request.NewPassword);
        user.UpdatePassword(newHash);
        await _userRepository.UpdateAsync(user, cancellationToken);

        // Invalida o token para uso único
        recoveryToken.Consume(DateTime.UtcNow);
        await _recoveryRepository.UpdateAsync(recoveryToken, cancellationToken);

        return Result.Success(new RecoveryActionResponseDto("Senha redefinida com sucesso. Você já pode fazer login com sua nova senha."));
    }
}
