using BuildingBlocks.Domain;
using Identity.Application.Common.Interfaces;
using Identity.Contracts.Members;
using Identity.Domain.Entities;
using Identity.Domain.Repositories;
using MediatR;

namespace Identity.Application.Features.Members.GetMembers;

public class GetMembersHandler : IRequestHandler<GetMembersQuery, Result<IReadOnlyList<MemberDto>>>
{
    private readonly IUserRepository _userRepository;
    private readonly ICurrentUserService? _currentUserService;
    private readonly IRootOwnerService? _rootOwnerService;

    public GetMembersHandler(
        IUserRepository userRepository,
        ICurrentUserService? currentUserService = null,
        IRootOwnerService? rootOwnerService = null)
    {
        _userRepository = userRepository;
        _currentUserService = currentUserService;
        _rootOwnerService = rootOwnerService;
    }

    public async Task<Result<IReadOnlyList<MemberDto>>> Handle(
        GetMembersQuery request,
        CancellationToken cancellationToken)
    {
        var users = await _userRepository.GetAllAsync(cancellationToken);

        IEnumerable<User> query = users;

        if (request.HasSteam.HasValue)
        {
            query = request.HasSteam.Value
                ? query.Where(u => u.SteamId.HasValue && u.SteamId.Value > 0)
                : query.Where(u => !u.SteamId.HasValue || u.SteamId.Value == 0);
        }

        if (request.IsActive.HasValue)
        {
            query = query.Where(u => u.IsActive == request.IsActive.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim().ToLowerInvariant();
            query = query.Where(u =>
                u.DisplayName.ToLowerInvariant().Contains(search) ||
                (u.DotaPersonaName != null && u.DotaPersonaName.ToLowerInvariant().Contains(search)) ||
                (u.SteamId.HasValue && u.SteamId.Value.ToString().Contains(search))
            );
        }

        if (!string.IsNullOrWhiteSpace(request.Role))
        {
            var roleFilter = request.Role.Trim();
            query = query.Where(u =>
                u.Roles.Any(r => r.ToString().Equals(roleFilter, StringComparison.OrdinalIgnoreCase)) ||
                (u.PreferredRole.HasValue && u.PreferredRole.Value.ToString().Equals(roleFilter, StringComparison.OrdinalIgnoreCase))
            );
        }

        bool isAdminCaller = _currentUserService?.IsTournamentAdmin == true || _currentUserService?.IsSuperAdmin == true;
        Guid? callerUserId = _currentUserService?.IsAuthenticated == true ? _currentUserService.UserId : null;

        // Ordena por RankTier decrescente (jogadores de rank maior primeiro) e depois por DisplayName
        var ordered = query
            .OrderByDescending(u => u.DotaRankTier ?? 0)
            .ThenBy(u => u.DisplayName)
            .Select(u =>
            {
                bool isSelf = callerUserId.HasValue && callerUserId.Value == u.Id;
                return u.ToMemberDto(
                    includeSensitiveData: isAdminCaller || isSelf,
                    isRootOwner: (isAdminCaller || isSelf) && _rootOwnerService?.IsRootOwner(u) == true);
            })
            .ToList();

        return Result.Success<IReadOnlyList<MemberDto>>(ordered);
    }
}
