using Identity.Contracts.Auth;
using MediatR;

namespace Identity.Application.Features.Auth.GetRecoveryRequests;

public record GetRecoveryRequestsQuery(string? Search = null) : IRequest<IReadOnlyList<RecoveryRequestAdminDto>>;
