using BuildingBlocks.Domain;
using Identity.Application.Common.Interfaces;
using Identity.Contracts.Preferences;
using Identity.Domain.Enums;
using Identity.Domain.Repositories;
using MediatR;

namespace Identity.Application.Features.Preferences.GetGeneralPreferences;

public record GetGeneralPreferencesQuery : IRequest<Result<GeneralPreferencesDto>>;

public class GetGeneralPreferencesHandler : IRequestHandler<GetGeneralPreferencesQuery, Result<GeneralPreferencesDto>>
{
    private readonly IUserRepository _userRepository;
    private readonly ICurrentUserService _currentUserService;

    public GetGeneralPreferencesHandler(
        IUserRepository userRepository,
        ICurrentUserService currentUserService)
    {
        _userRepository = userRepository;
        _currentUserService = currentUserService;
    }

    public async Task<Result<GeneralPreferencesDto>> Handle(GetGeneralPreferencesQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUserService.IsAuthenticated || !_currentUserService.UserId.HasValue)
        {
            return Result.Failure<GeneralPreferencesDto>("Usuário não autenticado.");
        }

        var user = await _userRepository.GetByIdAsync(_currentUserService.UserId.Value, cancellationToken);
        if (user is null)
        {
            return Result.Failure<GeneralPreferencesDto>("Usuário não encontrado.");
        }

        if (user.Role != UserRole.SuperAdmin && user.Role != UserRole.TournamentAdmin)
        {
            return Result.Failure<GeneralPreferencesDto>("Apenas administradores podem acessar as preferências gerais.");
        }

        var landingPage = user.Preferences?.PreferredLandingPage ?? "Player";
        return Result.Success(new GeneralPreferencesDto(landingPage));
    }
}
