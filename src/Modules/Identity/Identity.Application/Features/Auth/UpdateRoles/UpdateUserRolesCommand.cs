using BuildingBlocks.Domain;
using Identity.Contracts.Auth;
using Identity.Domain.Enums;
using MediatR;

namespace Identity.Application.Features.Auth.UpdateRoles;

public record UpdateUserRolesCommand(
    IReadOnlyList<DotaRole> Roles,
    DotaRole? PreferredRole = null
) : IRequest<Result<UserDto>>;
