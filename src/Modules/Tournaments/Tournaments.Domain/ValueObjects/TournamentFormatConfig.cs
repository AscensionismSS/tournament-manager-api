using BuildingBlocks.Domain;
using Tournaments.Domain.Enums;

namespace Tournaments.Domain.ValueObjects;

public record TournamentFormatConfig
{
    public TournamentType Type { get; }
    public PlayoffFormat PlayoffFormat { get; }
    public int TeamCount { get; }
    public int GroupCount { get; }

    private TournamentFormatConfig() { }

    private TournamentFormatConfig(TournamentType type, PlayoffFormat playoffFormat, int teamCount, int groupCount)
    {
        Type = type;
        PlayoffFormat = playoffFormat;
        TeamCount = teamCount;
        GroupCount = groupCount;
    }

    public static Result<TournamentFormatConfig> Create(
        TournamentType type,
        PlayoffFormat playoffFormat,
        int teamCount,
        int groupCount = 1)
    {
        if (teamCount < 4 || teamCount > 8)
            return Result.Failure<TournamentFormatConfig>("O motor de torneio suporta de 4 a 8 equipes.");

        if (type == TournamentType.DirectPlayoffs)
        {
            if (teamCount != 4 && teamCount != 8)
                return Result.Failure<TournamentFormatConfig>("O modo Direto para Playoffs (Mata-Mata) exige exatamente 4 ou 8 equipes (sem BYE para simetria estrita).");

            if (groupCount != 1)
                return Result.Failure<TournamentFormatConfig>("Modo direto para playoffs não possui grupos.");
        }

        if (groupCount == 2 && teamCount != 8)
            return Result.Failure<TournamentFormatConfig>("A divisão em 2 grupos é permitida exclusivamente para torneios com 8 equipes (2 grupos de 4).");

        if (groupCount < 1 || groupCount > 2)
            return Result.Failure<TournamentFormatConfig>("A quantidade de grupos permitida é 1 ou 2.");

        return Result.Success(new TournamentFormatConfig(type, playoffFormat, teamCount, groupCount));
    }
}
