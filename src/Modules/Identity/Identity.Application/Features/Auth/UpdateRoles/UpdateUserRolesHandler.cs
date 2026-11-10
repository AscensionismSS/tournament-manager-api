using BuildingBlocks.Domain;
using Identity.Application.Common.Interfaces;
using Identity.Contracts.Auth;
using Identity.Domain.Repositories;
using MediatR;

namespace Identity.Application.Features.Auth.UpdateRoles;

public class UpdateUserRolesHandler : IRequestHandler<UpdateUserRolesCommand, Result<UserDto>>
{
    private readonly IUserRepository _userRepository;
    private readonly ICurrentUserService _currentUserService;

    public UpdateUserRolesHandler(
        IUserRepository userRepository,
        ICurrentUserService currentUserService)
    {
        _userRepository = userRepository;
        _currentUserService = currentUserService;
    }

    public async Task<Result<UserDto>> Handle(UpdateUserRolesCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUserService.IsAuthenticated || !_currentUserService.UserId.HasValue)
        {
            return Result.Failure<UserDto>("Usuário não autenticado.");
        }

        var user = await _userRepository.GetByIdAsync(_currentUserService.UserId.Value, cancellationToken);
        if (user is null)
        {
            return Result.Failure<UserDto>("Usuário não encontrado.");
        }

        try
        {
            user.SetRoles(request.Roles, request.PreferredRole);
        }
        catch (DomainException ex)
        {
            return Result.Failure<UserDto>(ex.Message);
        }

        await _userRepository.UpdateAsync(user, cancellationToken);

        return Result.Success(user.ToDto());
    }
}
