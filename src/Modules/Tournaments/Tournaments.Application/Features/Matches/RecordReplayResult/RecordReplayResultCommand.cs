using MediatR;
using Tournaments.Contracts.Replay;

namespace Tournaments.Application.Features.Matches.RecordReplayResult;

public record RecordReplayResultCommand(MatchResultIngestionDto IngestionDto) : IRequest;
