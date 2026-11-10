using MediatR;
using Tournaments.Contracts.Tournaments;
using Tournaments.Domain.Enums;

namespace Tournaments.Application.Features.Tournaments.GetTournamentFormatOptions;

public class GetTournamentFormatOptionsHandler : IRequestHandler<GetTournamentFormatOptionsQuery, TournamentConfigurationOptionsDto>
{
    private static readonly int[] SupportedCounts = [4, 5, 6, 7, 8];

    public Task<TournamentConfigurationOptionsDto> Handle(GetTournamentFormatOptionsQuery request, CancellationToken cancellationToken)
    {
        var formats = new List<TournamentFormatOptionDto>();

        // 1. Mata-Mata Direto: permitido se não informou time ou se for exatamente 4 ou 8
        if (!request.TeamCount.HasValue || request.TeamCount.Value == 4 || request.TeamCount.Value == 8)
        {
            formats.Add(new TournamentFormatOptionDto(
                Type: TournamentType.DirectPlayoffs.ToString(),
                Name: "Mata-Mata Direto (Single Elimination)",
                Description: "Eliminação simples direta para os playoffs. Exige exatamente 4 ou 8 equipes para manter a simetria perfeita sem times de folga (BYE).",
                AllowedPlayoffFormats: [PlayoffFormat.SingleElimination.ToString()],
                AllowedGroupCounts: [1],
                SupportsDraftMode: true
            ));
        }

        // 2. Fase de Grupos + Dupla Eliminação: permitido para qualquer contagem de 4 a 8
        int[] allowedGroups = request.TeamCount == 8 || !request.TeamCount.HasValue ? [1, 2] : [1];
        formats.Add(new TournamentFormatOptionDto(
            Type: TournamentType.GroupStageAndPlayoffs.ToString(),
            Name: "Fase de Grupos + Dupla Eliminação (Double Elimination)",
            Description: "Fase de grupos classificatória com progressão para chave dos vencedores (Upper) e repescagem (Lower). Suporta de 4 a 8 equipes (com partida extra de Play-In para 5 equipes).",
            AllowedPlayoffFormats: [PlayoffFormat.DoubleElimination.ToString()],
            AllowedGroupCounts: allowedGroups,
            SupportsDraftMode: true
        ));

        var seriesFormats = new SeriesFormatOptionsDto(
            GroupStageFormats: [SeriesFormat.Bo1.ToString(), SeriesFormat.Bo2.ToString(), SeriesFormat.Bo3.ToString()],
            PlayoffFormats: [SeriesFormat.Bo1.ToString(), SeriesFormat.Bo3.ToString(), SeriesFormat.Bo5.ToString()],
            GrandFinalFormats: [SeriesFormat.Bo1.ToString(), SeriesFormat.Bo3.ToString(), SeriesFormat.Bo5.ToString()]
        );

        var response = new TournamentConfigurationOptionsDto(
            SelectedTeamCount: request.TeamCount,
            SupportedTeamCounts: SupportedCounts,
            AvailableFormats: formats,
            SeriesFormats: seriesFormats
        );

        return Task.FromResult(response);
    }
}
