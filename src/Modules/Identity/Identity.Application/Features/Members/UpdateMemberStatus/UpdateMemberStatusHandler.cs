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

    public UpdateMemberStatusHandler(
        IUserRepository userRepository,
        ICurrentUserService currentUserService)
    {
        _userRepository = userRepository;
        _currentUserService = currentUserService;
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

        var targetUser = await _userRepository.GetByIdAsync(request.UserId, cancellationToken);
        if (targetUser is null)
        {
            return Result.Failure<MemberDto>("Membro não encontrado.");
        }

        try
        {
            targetUser.SetActive(request.IsActive, request.Reason, adminUser);
            await _userRepository.UpdateAsync(targetUser, cancellationToken);

            return Result.Success(targetUser.ToMemberDto());
        }
        catch (DomainException ex)
        {
            return Result.Failure<MemberDto>(ex.Message);
        }
    }
}
