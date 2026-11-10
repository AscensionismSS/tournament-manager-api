using BuildingBlocks.Domain;
using Identity.Application.Common.Interfaces;
using Identity.Contracts.Auth;
using Identity.Domain.Entities;
using Identity.Domain.Enums;
using Identity.Domain.Repositories;
using MediatR;

namespace Identity.Application.Features.Auth.RequestSteamUnlock;

public class RequestSteamUnlockHandler : IRequestHandler<RequestSteamUnlockCommand, Result<RecoveryActionResponseDto>>
{
    private readonly IUserRepository _userRepository;
    private readonly IAccountRecoveryRepository _recoveryRepository;
    private readonly ICurrentUserService _currentUserService;

    public RequestSteamUnlockHandler(
        IUserRepository userRepository,
        IAccountRecoveryRepository recoveryRepository,
        ICurrentUserService currentUserService)
    {
        _userRepository = userRepository;
        _recoveryRepository = recoveryRepository;
        _currentUserService = currentUserService;
    }

    public async Task<Result<RecoveryActionResponseDto>> Handle(RequestSteamUnlockCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUserService.IsAuthenticated || !_currentUserService.UserId.HasValue)
        {
            return Result.Failure<RecoveryActionResponseDto>("Usuário não autenticado.");
        }

        var user = await _userRepository.GetByIdAsync(_currentUserService.UserId.Value, cancellationToken);
        if (user is null)
        {
            return Result.Failure<RecoveryActionResponseDto>("Usuário não encontrado.");
        }

        if (!user.IsSteamLocked && !user.SteamId.HasValue)
        {
            return Result.Failure<RecoveryActionResponseDto>("Você ainda não possui uma conta Steam vinculada para solicitar troca.");
        }

        var token = new AccountRecoveryToken(
            user.Id,
            user.Email,
            RecoveryTokenType.SteamUnlockRequest,
            TimeSpan.FromDays(7)
        );

        await _recoveryRepository.AddAsync(token, cancellationToken);

        return Result.Success(new RecoveryActionResponseDto(
            "Solicitação de desbloqueio de conta Steam enviada com sucesso para a administração da plataforma."
        ));
    }
}
