using BuildingBlocks.Domain;
using Identity.Application.Common.Interfaces;
using Identity.Contracts.Members;
using Identity.Domain.Repositories;
using MediatR;

namespace Identity.Application.Features.Members.UpdateMemberStatus;

public class UpdateMemberStatusHandler : IRequestHandler<UpdateMemberStatusCommand, Result<MemberDto>>
{
    private readonly IUserRepository _userRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IUserAuthStateCache? _authStateCache;
    private readonly IRootOwnerService? _rootOwnerService;

    public UpdateMemberStatusHandler(
        IUserRepository userRepository,
        ICurrentUserService currentUserService,
        IUserAuthStateCache? authStateCache = null,
        IRootOwnerService? rootOwnerService = null)
    {
        _userRepository = userRepository;
        _currentUserService = currentUserService;
        _authStateCache = authStateCache;
        _rootOwnerService = rootOwnerService;
    }

    public async Task<Result<MemberDto>> Handle(UpdateMemberStatusCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUserService.IsAuthenticated || !_currentUserService.UserId.HasValue)
        {
            return Result.Failure<MemberDto>("Usuário não autenticado.");
        }

        var adminUser = await _userRepository.GetByIdAsync(_currentUserService.UserId.Value, cancellationToken);
        if (adminUser is null)
        {
            return Result.Failure<MemberDto>("Administrador solicitante não encontrado.");
        }

        if (!adminUser.IsActive)
        {
            return Result.Failure<MemberDto>("Sua conta está desativada.");
        }

        var targetUser = await _userRepository.GetByIdAsync(request.UserId, cancellationToken);
        if (targetUser is null)
        {
            return Result.Failure<MemberDto>("Membro não encontrado.");
        }

        try
        {
            bool isTargetRootOwner = _rootOwnerService?.IsRootOwner(targetUser) == true;
            targetUser.SetActive(request.IsActive, request.Reason, adminUser, isTargetRootOwner);
            await _userRepository.UpdateAsync(targetUser, cancellationToken);
            _authStateCache?.Invalidate(targetUser.Id);

            return Result.Success(targetUser.ToMemberDto(includeSensitiveData: true, isRootOwner: isTargetRootOwner));
        }
        catch (DomainException ex)
        {
            return Result.Failure<MemberDto>(ex.Message);
        }
    }
}
