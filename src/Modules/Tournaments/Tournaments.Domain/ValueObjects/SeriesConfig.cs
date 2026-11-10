using BuildingBlocks.Domain;
using Tournaments.Domain.Enums;

namespace Tournaments.Domain.ValueObjects;

public record SeriesConfig
{
    public SeriesFormat? GroupStageFormat { get; }
    public SeriesFormat PlayoffFormat { get; }
    public SeriesFormat GrandFinalFormat { get; }

    private SeriesConfig() { }

    private SeriesConfig(SeriesFormat? groupStageFormat, SeriesFormat playoffFormat, SeriesFormat grandFinalFormat)
    {
        GroupStageFormat = groupStageFormat;
        PlayoffFormat = playoffFormat;
        GrandFinalFormat = grandFinalFormat;
    }

    public static Result<SeriesConfig> Create(
        SeriesFormat? groupStageFormat,
        SeriesFormat playoffFormat,
        SeriesFormat grandFinalFormat)
    {
        // 1. Playoff format cannot be Bo2 (no draws in playoffs)
        if (playoffFormat == SeriesFormat.Bo2)
            return Result.Failure<SeriesConfig>("Playoffs não podem ter formato Bo2 (MD2) pois exigem vencedor definido.");

        if (grandFinalFormat == SeriesFormat.Bo2)
            return Result.Failure<SeriesConfig>("A Grande Final não pode ter formato Bo2 (MD2).");

        // 2. Se houver fase de grupos, playoffs nunca podem ser menores que a fase de grupos
        if (groupStageFormat.HasValue)
        {
            if (groupStageFormat == SeriesFormat.Bo5)
                return Result.Failure<SeriesConfig>("A fase de grupos não suporta formato Bo5 (MD5).");

            if (groupStageFormat == SeriesFormat.Bo3 && playoffFormat == SeriesFormat.Bo1)
                return Result.Failure<SeriesConfig>("Playoffs (MD1) não podem ter formato inferior à fase de grupos (MD3).");

            if (groupStageFormat == SeriesFormat.Bo2 && playoffFormat == SeriesFormat.Bo1)
                return Result.Failure<SeriesConfig>("Com fase de grupos em MD2, os playoffs devem ser no mínimo MD3.");

            if ((groupStageFormat == SeriesFormat.Bo1 || groupStageFormat == SeriesFormat.Bo2) && playoffFormat == SeriesFormat.Bo5)
                return Result.Failure<SeriesConfig>("Playoffs em MD5 (Bo5) só são permitidos quando a fase de grupos é MD3 (Bo3).");
        }
        else
        {
            if (playoffFormat == SeriesFormat.Bo5)
                return Result.Failure<SeriesConfig>("No modo Mata-Mata Direto, os playoffs devem ser MD1 ou MD3 (MD5 é exclusivo da Grande Final).");
        }

        // 3. Validação Playoffs -> Grande Final
        // A Grande Final nunca pode regredir
        if ((int)grandFinalFormat < (int)playoffFormat)
            return Result.Failure<SeriesConfig>($"A Grande Final ({grandFinalFormat}) não pode ser inferior aos Playoffs ({playoffFormat}).");

        // Proibição restrita: Se playoffs for Bo1, a final NUNCA pode ser Bo5 (salto desproporcional)
        if (playoffFormat == SeriesFormat.Bo1 && grandFinalFormat == SeriesFormat.Bo5)
            return Result.Failure<SeriesConfig>("Se os playoffs forem MD1, a Grande Final não pode pular direto para MD5 (máximo permitido: MD3).");

        return Result.Success(new SeriesConfig(groupStageFormat, playoffFormat, grandFinalFormat));
    }
}
