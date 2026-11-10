using BuildingBlocks.Domain;
using Identity.Application.Common.Interfaces;
using Identity.Contracts.Auth;
using Identity.Domain.Repositories;
using MediatR;

namespace Identity.Application.Features.Auth.Login;

public class LoginHandler : IRequestHandler<LoginCommand, Result<AuthResponseDto>>
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;

    public LoginHandler(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        IJwtTokenGenerator jwtTokenGenerator)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _jwtTokenGenerator = jwtTokenGenerator;
    }

    public async Task<Result<AuthResponseDto>> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdentifierAsync(request.Identifier.Trim(), cancellationToken);
        if (user is null)
        {
            return Result.Failure<AuthResponseDto>("Credenciais inválidas.");
        }

        bool isPasswordValid = _passwordHasher.VerifyPassword(request.Password, user.PasswordHash);
        if (!isPasswordValid)
        {
            return Result.Failure<AuthResponseDto>("Credenciais inválidas.");
        }

        if (!user.IsActive)
        {
            string reasonMsg = string.IsNullOrWhiteSpace(user.StatusReason)
                ? "Sua conta foi desativada pela administração da plataforma."
                : $"Sua conta foi desativada pela administração. Motivo: {user.StatusReason}";
            return Result.Failure<AuthResponseDto>(reasonMsg);
        }

        user.RecordLogin();
        await _userRepository.UpdateAsync(user, cancellationToken);

        var (token, expiresAtUtc) = _jwtTokenGenerator.GenerateToken(user);

        return Result.Success(user.ToAuthResponseDto(token, expiresAtUtc));
    }
}
