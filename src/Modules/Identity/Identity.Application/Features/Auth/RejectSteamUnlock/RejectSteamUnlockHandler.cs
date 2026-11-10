using BuildingBlocks.Domain;
using Identity.Application.Common.Interfaces;
using Identity.Contracts.Auth;
using Identity.Domain.Enums;
using Identity.Domain.Repositories;
using MediatR;

namespace Identity.Application.Features.Auth.RejectSteamUnlock;

public class RejectSteamUnlockHandler : IRequestHandler<RejectSteamUnlockCommand, Result<RecoveryActionResponseDto>>
{
    private readonly IUserRepository _userRepository;
    private readonly IAccountRecoveryRepository _recoveryRepository;
    private readonly ICurrentUserService _currentUserService;

    public RejectSteamUnlockHandler(
        IUserRepository userRepository,
        IAccountRecoveryRepository recoveryRepository,
        ICurrentUserService currentUserService)
    {
        _userRepository = userRepository;
        _recoveryRepository = recoveryRepository;
        _currentUserService = currentUserService;
    }

    public async Task<Result<RecoveryActionResponseDto>> Handle(
        RejectSteamUnlockCommand request,
        CancellationToken cancellationToken)
    {
        if (!_currentUserService.IsAuthenticated || !_currentUserService.UserId.HasValue)
        {
            return Result.Failure<RecoveryActionResponseDto>("Usuário não autenticado.");
        }

        var adminUser = await _userRepository.GetByIdAsync(_currentUserService.UserId.Value, cancellationToken);
        if (adminUser is null || (adminUser.Role != UserRole.SuperAdmin && adminUser.Role != UserRole.TournamentAdmin))
        {
            return Result.Failure<RecoveryActionResponseDto>("Apenas administradores podem recusar solicitações de troca de Steam.");
        }

        var token = await _recoveryRepository.GetByIdAsync(request.RequestId, cancellationToken);
        if (token is null)
        {
            return Result.Failure<RecoveryActionResponseDto>("Solicitação não encontrada.");
        }

        if (token.Type != RecoveryTokenType.SteamUnlockRequest)
        {
            return Result.Failure<RecoveryActionResponseDto>("Esta solicitação não é do tipo troca de conta Steam.");
        }

        if (token.IsUsed)
        {
            return Result.Failure<RecoveryActionResponseDto>("Esta solicitação já foi finalizada ou atendida anteriormente.");
        }

        var targetUser = await _userRepository.GetByIdAsync(token.UserId, cancellationToken);

        token.Reject(DateTime.UtcNow);
        await _recoveryRepository.UpdateAsync(token, cancellationToken);

        var userName = targetUser?.DisplayName ?? "o jogador";
        var reasonMsg = !string.IsNullOrWhiteSpace(request.Reason) ? $" Motivo: {request.Reason.Trim()}" : "";

        return Result.Success(new RecoveryActionResponseDto(
            $"Solicitação de troca de Steam de '{userName}' foi recusada.{reasonMsg}"
        ));
    }
}
