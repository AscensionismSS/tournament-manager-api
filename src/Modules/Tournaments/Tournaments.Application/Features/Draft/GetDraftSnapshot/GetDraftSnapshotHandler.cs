using BuildingBlocks.Domain;
using MediatR;
using Tournaments.Contracts.Draft;
using Tournaments.Domain.Repositories;

namespace Tournaments.Application.Features.Draft.GetDraftSnapshot;

public class GetDraftSnapshotHandler : IRequestHandler<GetDraftSnapshotQuery, DraftStateSnapshotDto>
{
    private readonly IDraftSessionRepository _draftRepository;

    public GetDraftSnapshotHandler(IDraftSessionRepository draftRepository)
    {
        _draftRepository = draftRepository;
    }

    public async Task<DraftStateSnapshotDto> Handle(GetDraftSnapshotQuery request, CancellationToken cancellationToken)
    {
        var session = await _draftRepository.GetByIdAsync(request.DraftSessionId, cancellationToken)
            ?? throw new DomainException($"Sessão de Draft '{request.DraftSessionId}' não encontrada.");

        return DraftSnapshotMapper.ToSnapshotDto(session);
    }
}
