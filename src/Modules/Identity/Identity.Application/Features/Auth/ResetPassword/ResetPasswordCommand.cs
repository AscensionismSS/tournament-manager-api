using BuildingBlocks.Domain;
using Identity.Contracts.Auth;
using MediatR;

namespace Identity.Application.Features.Auth.ResetPassword;

public record ResetPasswordCommand(
    string Token,
    string NewPassword,
    string ConfirmPassword
) : IRequest<Result<RecoveryActionResponseDto>>;
