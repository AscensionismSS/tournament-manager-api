using BuildingBlocks.Domain;
using Identity.Contracts.Auth;
using MediatR;

namespace Identity.Application.Features.Auth.Login;

public record LoginCommand(string Identifier, string Password) : IRequest<Result<AuthResponseDto>>
{
    // Alias para compatibilidade com chamadas existentes
    public string Email => Identifier;
}
