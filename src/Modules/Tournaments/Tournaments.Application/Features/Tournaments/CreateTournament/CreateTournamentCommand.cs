using MediatR;
using Tournaments.Contracts.Tournaments;
using Tournaments.Domain.Enums;

namespace Tournaments.Application.Features.Tournaments.CreateTournament;

public record CreateTournamentCommand(
    string Name,
    TournamentType Type,
    PlayoffFormat PlayoffFormat,
    int TeamCount,
    int GroupCount,
    SeriesFormat? GroupStageFormat,
    SeriesFormat PlayoffSeriesFormat,
    SeriesFormat GrandFinalSeriesFormat,
    bool EnableDraftMode = true,
    string? BannerUrl = null
) : IRequest<TournamentDto>;
