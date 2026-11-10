using BuildingBlocks.Domain;
using Identity.Application.Common.Interfaces;
using Identity.Contracts.Preferences;
using Identity.Domain.Entities;
using Identity.Domain.Enums;
using Identity.Domain.Repositories;
using MediatR;

namespace Identity.Application.Features.Preferences.UpdateGeneralPreferences;

public record UpdateGeneralPreferencesCommand(string PreferredLandingPage) : IRequest<Result<GeneralPreferencesDto>>;

public class UpdateGeneralPreferencesHandler : IRequestHandler<UpdateGeneralPreferencesCommand, Result<GeneralPreferencesDto>>
{
    private readonly IUserRepository _userRepository;
    private readonly ICurrentUserService _currentUserService;

    public UpdateGeneralPreferencesHandler(
        IUserRepository userRepository,
        ICurrentUserService currentUserService)
    {
        _userRepository = userRepository;
        _currentUserService = currentUserService;
    }

    public async Task<Result<GeneralPreferencesDto>> Handle(UpdateGeneralPreferencesCommand request, CancellationToken cancellationToken)
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
            return Result.Failure<GeneralPreferencesDto>("Apenas administradores podem configurar preferências gerais.");
        }

        try
        {
            user.UpdateGeneralPreferences(request.PreferredLandingPage, user);
        }
        catch (DomainException ex)
        {
            return Result.Failure<GeneralPreferencesDto>(ex.Message);
        }

        await _userRepository.UpdateAsync(user, cancellationToken);

        var landingPage = user.Preferences?.PreferredLandingPage ?? "Player";
        return Result.Success(new GeneralPreferencesDto(landingPage));
    }
}
