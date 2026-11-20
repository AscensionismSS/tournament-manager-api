using BuildingBlocks.Domain;
using Identity.Application.Common.Interfaces;
using Identity.Contracts.Auth;
using Identity.Domain.Repositories;
using MediatR;

namespace Identity.Application.Features.Auth.UnlockSteam;

public class UnlockSteamHandler : IRequestHandler<UnlockSteamCommand, Result<UserDto>>
{
    private readonly IUserRepository _userRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IUserRealtimeNotifier? _userRealtimeNotifier;
    private readonly IRootOwnerService? _rootOwnerService;

    public UnlockSteamHandler(
        IUserRepository userRepository,
        ICurrentUserService currentUserService,
        IUserRealtimeNotifier? userRealtimeNotifier = null,
        IRootOwnerService? rootOwnerService = null)
    {
        _userRepository = userRepository;
        _currentUserService = currentUserService;
        _userRealtimeNotifier = userRealtimeNotifier;
        _rootOwnerService = rootOwnerService;
    }

    public async Task<Result<UserDto>> Handle(UnlockSteamCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUserService.IsAuthenticated || !_currentUserService.IsSuperAdmin || !_currentUserService.UserId.HasValue)
        {
            return Result.Failure<UserDto>("Apenas o SuperAdmin pode desbloquear contas Steam.");
        }

        var adminUser = await _userRepository.GetByIdAsync(_currentUserService.UserId.Value, cancellationToken);
        if (adminUser is null)
        {
            return Result.Failure<UserDto>("Usuário administrador não encontrado.");
        }

        var targetUser = await _userRepository.GetByIdAsync(request.TargetUserId, cancellationToken);
        if (targetUser is null)
        {
            return Result.Failure<UserDto>("Usuário alvo não encontrado.");
        }

        bool isTargetRootOwner = _rootOwnerService?.IsRootOwner(targetUser) == true;

        try
        {
            targetUser.UnlockSteam(adminUser, isTargetRootOwner);
        }
        catch (DomainException ex)
        {
            return Result.Failure<UserDto>(ex.Message);
        }

        await _userRepository.UpdateAsync(targetUser, cancellationToken);

        var dto = targetUser.ToDto(
            isRootOwner: _rootOwnerService?.HasRootOwnerAuthority(targetUser) == true,
            maskEmail: adminUser.Id != targetUser.Id);

        if (_userRealtimeNotifier is not null)
        {
            await _userRealtimeNotifier.NotifySteamUnlockedAsync(targetUser.Id, dto, cancellationToken);
        }

        return Result.Success(dto);
    }
}
