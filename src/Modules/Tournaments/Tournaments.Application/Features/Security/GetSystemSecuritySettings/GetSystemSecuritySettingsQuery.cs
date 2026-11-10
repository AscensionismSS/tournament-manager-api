using MediatR;
using Tournaments.Contracts.Security;

namespace Tournaments.Application.Features.Security.GetSystemSecuritySettings;

public record GetSystemSecuritySettingsQuery : IRequest<SystemSecuritySettingsDto>;
