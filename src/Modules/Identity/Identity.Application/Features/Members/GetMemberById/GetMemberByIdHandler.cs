using BuildingBlocks.Domain;
using Identity.Contracts.Members;
using Identity.Domain.Repositories;
using MediatR;

namespace Identity.Application.Features.Members.GetMemberById;

public class GetMemberByIdHandler : IRequestHandler<GetMemberByIdQuery, Result<MemberDto>>
{
    private readonly IUserRepository _userRepository;

    public GetMemberByIdHandler(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<Result<MemberDto>> Handle(
        GetMemberByIdQuery request,
        CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken);
        if (user == null)
        {
            return Result.Failure<MemberDto>("Membro não encontrado.");
        }

        return Result.Success(user.ToMemberDto());
    }
}
