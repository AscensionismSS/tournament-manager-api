using BuildingBlocks.Domain;
using Identity.Contracts.Auth;
using MediatR;

namespace Identity.Application.Features.Auth.ForgotLogin;

public record ForgotLoginCommand(string Email) : IRequest<Result<RecoveryActionResponseDto>>;
