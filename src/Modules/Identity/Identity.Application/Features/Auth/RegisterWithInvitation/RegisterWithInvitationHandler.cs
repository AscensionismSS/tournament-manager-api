using BuildingBlocks.Domain;
using Identity.Application.Common.Interfaces;
using Identity.Contracts.Auth;
using Identity.Domain.Entities;
using Identity.Domain.Repositories;
using MediatR;

namespace Identity.Application.Features.Auth.RegisterWithInvitation;

public class RegisterWithInvitationHandler : IRequestHandler<RegisterWithInvitationCommand, Result<AuthResponseDto>>
{
    private readonly IInvitationRepository _invitationRepository;
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;
    private readonly IOpenDotaProfileService _openDotaProfileService;

    public RegisterWithInvitationHandler(
        IInvitationRepository invitationRepository,
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        IJwtTokenGenerator jwtTokenGenerator,
        IOpenDotaProfileService openDotaProfileService)
    {
        _invitationRepository = invitationRepository;
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _jwtTokenGenerator = jwtTokenGenerator;
        _openDotaProfileService = openDotaProfileService;
    }

    public async Task<Result<AuthResponseDto>> Handle(RegisterWithInvitationCommand request, CancellationToken cancellationToken)
    {
        var invitation = await _invitationRepository.GetByCodeAsync(request.Code.Trim(), cancellationToken);
        if (invitation is null)
        {
            return Result.Failure<AuthResponseDto>("Convite não encontrado ou inválido.");
        }

        if (invitation.IsUsed)
        {
            return Result.Failure<AuthResponseDto>("Este convite já foi utilizado.");
        }

        if (invitation.IsRevoked)
        {
            return Result.Failure<AuthResponseDto>("Este convite foi desativado pelo administrador.");
        }

        if (invitation.IsExpired)
        {
            return Result.Failure<AuthResponseDto>("Este convite expirou.");
        }

        if (string.IsNullOrWhiteSpace(request.Email) || !string.Equals(request.Email.Trim(), invitation.Email, StringComparison.OrdinalIgnoreCase))
        {
            return Result.Failure<AuthResponseDto>("O email informado não corresponde ao email deste convite.");
        }

        if (string.IsNullOrWhiteSpace(request.DisplayName) || request.DisplayName.Trim().Length < 2)
        {
            return Result.Failure<AuthResponseDto>("O nome de usuário/login deve ter no mínimo 2 caracteres.");
        }

        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 6)
        {
            return Result.Failure<AuthResponseDto>("A senha deve ter no mínimo 6 caracteres.");
        }

        bool emailExists = await _userRepository.ExistsByEmailAsync(invitation.Email, cancellationToken);
        if (emailExists)
        {
            return Result.Failure<AuthResponseDto>($"Já existe um usuário cadastrado com o email '{invitation.Email}'.");
        }

        bool displayNameExists = await _userRepository.ExistsByDisplayNameAsync(request.DisplayName.Trim(), cancellationToken);
        if (displayNameExists)
        {
            return Result.Failure<AuthResponseDto>($"O nome de usuário '{request.DisplayName}' já está em uso por outro jogador.");
        }

        // SteamId é obrigatório no cadastro
        if (!request.SteamId.HasValue || request.SteamId.Value <= 76561197960265728ul)
        {
            return Result.Failure<AuthResponseDto>("A vinculação da conta Steam é obrigatória para concluir o cadastro. Busque e selecione seu perfil Steam.");
        }

        var existingSteamUser = await _userRepository.GetBySteamIdAsync(request.SteamId.Value, cancellationToken);
        if (existingSteamUser is not null)
        {
            return Result.Failure<AuthResponseDto>("Esta conta Steam já está vinculada a outro jogador na plataforma.");
        }

        string passwordHash = _passwordHasher.HashPassword(request.Password);

        var user = new User(
            invitation.Email,
            passwordHash,
            request.DisplayName,
            invitation.TargetRole
        );

        // Vinculação atômica de Steam se fornecida no cadastro
        if (request.SteamId.HasValue && request.SteamId.Value > 0)
        {
            var profile = await _openDotaProfileService.FetchProfileAsync(request.SteamId.Value, cancellationToken);
            string personaName = profile?.PersonaName ?? $"Player_{request.SteamId.Value}";
            string? avatarUrl = profile?.AvatarUrl;
            int? rankTier = profile?.RankTier;
            int? leaderboardRank = profile?.LeaderboardRank;

            user.LinkSteamAccount(request.SteamId.Value, personaName, avatarUrl, rankTier, leaderboardRank);
        }

        invitation.Accept(user.Id);

        await _userRepository.AddAsync(user, cancellationToken);
        await _invitationRepository.UpdateAsync(invitation, cancellationToken);

        var (token, expiresAtUtc) = _jwtTokenGenerator.GenerateToken(user);

        return Result.Success(user.ToAuthResponseDto(token, expiresAtUtc));
    }
}
