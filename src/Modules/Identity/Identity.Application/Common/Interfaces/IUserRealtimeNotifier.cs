using Identity.Contracts.Auth;

namespace Identity.Application.Common.Interfaces;

public interface IUserRealtimeNotifier
{
    Task NotifySteamUnlockedAsync(Guid userId, UserDto updatedUser, CancellationToken ct = default);
}
