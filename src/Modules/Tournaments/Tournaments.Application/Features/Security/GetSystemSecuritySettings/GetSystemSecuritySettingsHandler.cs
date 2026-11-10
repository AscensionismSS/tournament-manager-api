using MediatR;
using Tournaments.Contracts.Security;
using Tournaments.Domain.Repositories;

namespace Tournaments.Application.Features.Security.GetSystemSecuritySettings;

public class GetSystemSecuritySettingsHandler : IRequestHandler<GetSystemSecuritySettingsQuery, SystemSecuritySettingsDto>
{
    private readonly ISystemSecuritySettingsRepository _repository;

    public GetSystemSecuritySettingsHandler(ISystemSecuritySettingsRepository repository)
    {
        _repository = repository;
    }

    public async Task<SystemSecuritySettingsDto> Handle(GetSystemSecuritySettingsQuery request, CancellationToken cancellationToken)
    {
        var settings = await _repository.GetAsync(cancellationToken);
        return settings.ToDto();
    }
}
