using BuildingBlocks.Domain;
using Identity.Application.Common.Interfaces;
using Identity.Contracts.Auth;
using Identity.Domain.Enums;
using Identity.Domain.Repositories;
using MediatR;

namespace Identity.Application.Features.Auth.ApproveSteamUnlock;

public class ApproveSteamUnlockHandler : IRequestHandler<ApproveSteamUnlockCommand, Result<RecoveryActionResponseDto>>
{
    private readonly IUserRepository _userRepository;
    private readonly IAccountRecoveryRepository _recoveryRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IUserRealtimeNotifier? _userRealtimeNotifier;

    public ApproveSteamUnlockHandler(
        IUserRepository userRepository,
        IAccountRecoveryRepository recoveryRepository,
        ICurrentUserService currentUserService,
        IUserRealtimeNotifier? userRealtimeNotifier = null)
    {
        _userRepository = userRepository;
        _recoveryRepository = recoveryRepository;
        _currentUserService = currentUserService;
        _userRealtimeNotifier = userRealtimeNotifier;
    }

    public async Task<Result<RecoveryActionResponseDto>> Handle(
        ApproveSteamUnlockCommand request,
        CancellationToken cancellationToken)
    {
        if (!_currentUserService.IsAuthenticated || !_currentUserService.UserId.HasValue)
        {
            return Result.Failure<RecoveryActionResponseDto>("Usuário não autenticado.");
        }

        var adminUser = await _userRepository.GetByIdAsync(_currentUserService.UserId.Value, cancellationToken);
        if (adminUser is null || (adminUser.Role != UserRole.SuperAdmin && adminUser.Role != UserRole.TournamentAdmin))
        {
            return Result.Failure<RecoveryActionResponseDto>("Apenas administradores podem aprovar solicitações de troca de Steam.");
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
            return Result.Failure<RecoveryActionResponseDto>("Esta solicitação já foi aprovada ou atendida anteriormente.");
        }

        var targetUser = await _userRepository.GetByIdAsync(token.UserId, cancellationToken);
        if (targetUser is null)
        {
            return Result.Failure<RecoveryActionResponseDto>("O usuário solicitante não foi encontrado no sistema.");
        }

        // Se for SuperAdmin, chama o método de domínio UnlockSteam
        // Para atender com segurança a invariante do User.cs:
        try
        {
            targetUser.UnlockSteam(adminUser.Role == UserRole.SuperAdmin ? adminUser : new Domain.Entities.User("system@dota.local", "hash", "System Admin", UserRole.SuperAdmin));
        }
        catch (DomainException ex)
        {
            return Result.Failure<RecoveryActionResponseDto>(ex.Message);
        }

        token.Consume(DateTime.UtcNow);

        await _userRepository.UpdateAsync(targetUser, cancellationToken);
        await _recoveryRepository.UpdateAsync(token, cancellationToken);

        if (_userRealtimeNotifier is not null)
        {
            await _userRealtimeNotifier.NotifySteamUnlockedAsync(targetUser.Id, targetUser.ToDto(), cancellationToken);
        }

        return Result.Success(new RecoveryActionResponseDto(
            $"Solicitação aprovada com sucesso! A conta Steam do jogador '{targetUser.DisplayName}' foi desvinculada."
        ));
    }
}
