using Tournaments.Domain.ValueObjects;

namespace Tournaments.Domain.Services;

public static class SingleEliminationBracketBuilder
{
    public static IReadOnlyList<BracketSlot> BuildSlots(int teamCount)
    {
        return teamCount switch
        {
            4 => Build4Teams(),
            8 => Build8Teams(),
            _ => throw new ArgumentException($"Quantidade de equipes inválida para Mata-Mata Simples ({teamCount}). Suportado exclusivamente: 4 ou 8 equipes.")
        };
    }

    private static IReadOnlyList<BracketSlot> Build4Teams()
    {
        return
        [
            // Semifinais
            new BracketSlot("SF1", BracketStage.Upper, 1, "1st", "4th", "GF", null),
            new BracketSlot("SF2", BracketStage.Upper, 1, "2nd", "3rd", "GF", null),

            // Grande Final (sem Lower Bracket)
            new BracketSlot("GF", BracketStage.GrandFinal, 2, "Winner(SF1)", "Winner(SF2)", null, null)
        ];
    }

    private static IReadOnlyList<BracketSlot> Build8Teams()
    {
        return
        [
            // Quartas de Final (Round 1)
            new BracketSlot("QF1", BracketStage.Upper, 1, "1st", "8th", "SF1", null),
            new BracketSlot("QF2", BracketStage.Upper, 1, "4th", "5th", "SF1", null),
            new BracketSlot("QF3", BracketStage.Upper, 1, "2nd", "7th", "SF2", null),
            new BracketSlot("QF4", BracketStage.Upper, 1, "3rd", "6th", "SF2", null),

            // Semifinais (Round 2)
            new BracketSlot("SF1", BracketStage.Upper, 2, "Winner(QF1)", "Winner(QF2)", "GF", null),
            new BracketSlot("SF2", BracketStage.Upper, 2, "Winner(QF3)", "Winner(QF4)", "GF", null),

            // Grande Final (Round 3 - sem Lower Bracket)
            new BracketSlot("GF", BracketStage.GrandFinal, 3, "Winner(SF1)", "Winner(SF2)", null, null)
        ];
    }
}
