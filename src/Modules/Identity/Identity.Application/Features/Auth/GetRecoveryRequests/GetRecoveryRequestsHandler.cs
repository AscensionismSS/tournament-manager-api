using Identity.Contracts.Auth;
using Identity.Domain.Enums;
using Identity.Domain.Repositories;
using MediatR;

namespace Identity.Application.Features.Auth.GetRecoveryRequests;

public class GetRecoveryRequestsHandler : IRequestHandler<GetRecoveryRequestsQuery, IReadOnlyList<RecoveryRequestAdminDto>>
{
    private readonly IAccountRecoveryRepository _recoveryRepository;
    private readonly IUserRepository _userRepository;

    public GetRecoveryRequestsHandler(
        IAccountRecoveryRepository recoveryRepository,
        IUserRepository userRepository)
    {
        _recoveryRepository = recoveryRepository;
        _userRepository = userRepository;
    }

    public async Task<IReadOnlyList<RecoveryRequestAdminDto>> Handle(
        GetRecoveryRequestsQuery request,
        CancellationToken cancellationToken)
    {
        var tokens = await _recoveryRepository.GetAllAsync(cancellationToken);
        var users = await _userRepository.GetAllAsync(cancellationToken);
        var userDict = users.ToDictionary(u => u.Id);

        var query = tokens.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim().ToLowerInvariant();
            query = query.Where(t =>
            {
                userDict.TryGetValue(t.UserId, out var matchedUser);
                var userName = matchedUser?.DisplayName?.ToLowerInvariant() ?? "";
                return t.Email.ToLowerInvariant().Contains(search)
                    || userName.Contains(search)
                    || t.Token.ToLowerInvariant().Contains(search)
                    || t.Type.ToString().ToLowerInvariant().Contains(search);
            });
        }

        return query
            .OrderByDescending(t => t.CreatedAtUtc)
            .Select(t =>
            {
                var actionUrl = t.Type switch
                {
                    RecoveryTokenType.PasswordReset => $"/reset-password?token={t.Token}",
                    RecoveryTokenType.LoginRecovery => $"/recover-login?token={t.Token}",
                    _ => $"/admin/steam-requests/{t.Id}"
                };

                userDict.TryGetValue(t.UserId, out var matchedUser);

                return new RecoveryRequestAdminDto(
                    t.Id,
                    t.UserId,
                    t.Email,
                    matchedUser?.DisplayName,
                    t.Token,
                    t.Type.ToString(),
                    t.CreatedAtUtc,
                    t.ExpiresAtUtc,
                    t.IsUsed,
                    t.UsedAtUtc,
                    actionUrl,
                    matchedUser?.DotaAvatarUrl,
                    matchedUser?.SteamId?.ToString(),
                    matchedUser?.DotaPersonaName
                );
            })
            .ToList();
    }
}
