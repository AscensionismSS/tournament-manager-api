using BuildingBlocks.Domain;
using Identity.Application.Common.Interfaces;
using Identity.Contracts.Members;
using Identity.Domain.Repositories;
using MediatR;

namespace Identity.Application.Features.Members.GetMemberById;

public class GetMemberByIdHandler : IRequestHandler<GetMemberByIdQuery, Result<MemberDto>>
{
    private readonly IUserRepository _userRepository;
    private readonly ICurrentUserService? _currentUserService;
    private readonly IRootOwnerService? _rootOwnerService;

    public GetMemberByIdHandler(
        IUserRepository userRepository,
        ICurrentUserService? currentUserService = null,
        IRootOwnerService? rootOwnerService = null)
    {
        _userRepository = userRepository;
        _currentUserService = currentUserService;
        _rootOwnerService = rootOwnerService;
    }

    public async Task<Result<MemberDto>> Handle(
        GetMemberByIdQuery request,
        CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken);
        if (user == null)
        {
            return Result.Failure<MemberDto>("Membro não encontrado.");
        }

        bool isAdminCaller = _currentUserService?.IsTournamentAdmin == true || _currentUserService?.IsSuperAdmin == true;
        bool isSelf = _currentUserService?.IsAuthenticated == true && _currentUserService.UserId == user.Id;

        return Result.Success(user.ToMemberDto(
            includeSensitiveData: isAdminCaller || isSelf,
            isRootOwner: (isAdminCaller || isSelf) && _rootOwnerService?.IsRootOwner(user) == true));
    }
}
