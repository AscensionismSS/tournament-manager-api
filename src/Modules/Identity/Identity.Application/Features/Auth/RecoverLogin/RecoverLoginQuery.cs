using BuildingBlocks.Domain;
using Identity.Contracts.Auth;
using MediatR;

namespace Identity.Application.Features.Auth.RecoverLogin;

public record RecoverLoginQuery(string Token) : IRequest<Result<RecoveredLoginDto>>;
