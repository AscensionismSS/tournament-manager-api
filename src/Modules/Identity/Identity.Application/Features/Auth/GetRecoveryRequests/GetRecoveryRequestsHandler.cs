using Identity.Application.Common.Interfaces;
using Identity.Application.Common.Security;
using Identity.Contracts.Auth;
using Identity.Domain.Enums;
using Identity.Domain.Repositories;
using MediatR;

namespace Identity.Application.Features.Auth.GetRecoveryRequests;

public class GetRecoveryRequestsHandler : IRequestHandler<GetRecoveryRequestsQuery, IReadOnlyList<RecoveryRequestAdminDto>>
{
    private readonly IAccountRecoveryRepository _recoveryRepository;
    private readonly IUserRepository _userRepository;
    private readonly ICurrentUserService? _currentUserService;
    private readonly IRootOwnerService? _rootOwnerService;

    public GetRecoveryRequestsHandler(
        IAccountRecoveryRepository recoveryRepository,
        IUserRepository userRepository,
        ICurrentUserService? currentUserService = null,
        IRootOwnerService? rootOwnerService = null)
    {
        _recoveryRepository = recoveryRepository;
        _userRepository = userRepository;
        _currentUserService = currentUserService;
        _rootOwnerService = rootOwnerService;
    }

    public async Task<IReadOnlyList<RecoveryRequestAdminDto>> Handle(
        GetRecoveryRequestsQuery request,
        CancellationToken cancellationToken)
    {
        var tokens = await _recoveryRepository.GetAllAsync(cancellationToken);
        var users = await _userRepository.GetAllAsync(cancellationToken);
        var userDict = users.ToDictionary(u => u.Id);

        bool callerIsRootOwner = _currentUserService?.IsAuthenticated == true
            && _currentUserService.UserId.HasValue
            && _rootOwnerService is not null
            && userDict.TryGetValue(_currentUserService.UserId.Value, out var caller)
            && _rootOwnerService.HasRootOwnerAuthority(caller);

        var query = tokens.AsEnumerable();

        if (!callerIsRootOwner)
        {
            query = query.Where(t =>
            {
                userDict.TryGetValue(t.UserId, out var matchedTarget);

                bool isTargetRootOwner = (_rootOwnerService?.IsRootOwnerEmail(t.Email) == true)
                    || (matchedTarget is not null && _rootOwnerService?.IsRootOwner(matchedTarget) == true);

                if (isTargetRootOwner)
                {
                    return false;
                }

                bool isSuperAdminCredentialRecovery = matchedTarget?.Role == UserRole.SuperAdmin
                    && (t.Type == RecoveryTokenType.PasswordReset || t.Type == RecoveryTokenType.LoginRecovery);

                return !isSuperAdminCredentialRecovery;
            });
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim().ToLowerInvariant();
            query = query.Where(t =>
            {
                userDict.TryGetValue(t.UserId, out var matchedUser);
                var userName = matchedUser?.DisplayName?.ToLowerInvariant() ?? "";
                var personaName = matchedUser?.DotaPersonaName?.ToLowerInvariant() ?? "";
                return userName.Contains(search)
                    || personaName.Contains(search)
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
                    EmailMasker.Mask(t.Email),
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
