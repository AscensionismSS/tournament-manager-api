using Tournaments.Domain.ValueObjects;

namespace Tournaments.Domain.Services;

public static class DoubleEliminationBracketBuilder
{
    public static IReadOnlyList<BracketSlot> BuildSlots(int teamCount)
    {
        return teamCount switch
        {
            4 => Build4Teams(),
            5 => Build5Teams(),
            6 => Build6Teams(),
            7 => Build7Teams(),
            8 => Build8Teams(),
            _ => throw new ArgumentException($"Quantidade de equipes inválida ({teamCount}). Suportado: 4 a 8.")
        };
    }

    private static IReadOnlyList<BracketSlot> Build4Teams()
    {
        return
        [
            new BracketSlot("U1", BracketStage.Upper, 1, "1st", "2nd", "GF", "LF"),
            new BracketSlot("L1", BracketStage.Lower, 1, "3rd", "4th", "LF", null),
            new BracketSlot("LF", BracketStage.Lower, 2, "Loser(U1)", "Winner(L1)", "GF", null),
            new BracketSlot("GF", BracketStage.GrandFinal, 3, "Winner(U1)", "Winner(LF)", null, null)
        ];
    }

    private static IReadOnlyList<BracketSlot> Build5Teams()
    {
        return
        [
            new BracketSlot("PI", BracketStage.PlayIn, 1, "4th", "5th", "L1", null),
            new BracketSlot("U1", BracketStage.Upper, 2, "1st", "2nd", "GF", "LF"),
            new BracketSlot("L1", BracketStage.Lower, 2, "3rd", "Winner(PI)", "LF", null),
            new BracketSlot("LF", BracketStage.Lower, 3, "Loser(U1)", "Winner(L1)", "GF", null),
            new BracketSlot("GF", BracketStage.GrandFinal, 4, "Winner(U1)", "Winner(LF)", null, null)
        ];
    }

    private static IReadOnlyList<BracketSlot> Build6Teams()
    {
        return
        [
            // Upper R1
            new BracketSlot("U1", BracketStage.Upper, 1, "1st", "4th", "UF", "L2"), // Loser U1 -> L2 (Cross-bracket)
            new BracketSlot("U2", BracketStage.Upper, 1, "2nd", "3rd", "UF", "L1"), // Loser U2 -> L1 (Cross-bracket)

            // Lower R1 (5th e 6th aguardam os perdedores da Upper com Cross-Bracketing)
            new BracketSlot("L1", BracketStage.Lower, 1, "Loser(U2)", "5th", "LR2", null),
            new BracketSlot("L2", BracketStage.Lower, 1, "Loser(U1)", "6th", "LR2", null),

            // Upper Final & Lower R2
            new BracketSlot("UF", BracketStage.Upper, 2, "Winner(U1)", "Winner(U2)", "GF", "LF"),
            new BracketSlot("LR2", BracketStage.Lower, 2, "Winner(L1)", "Winner(L2)", "LF", null),

            // Lower Final & Grand Final
            new BracketSlot("LF", BracketStage.Lower, 3, "Loser(UF)", "Winner(LR2)", "GF", null),
            new BracketSlot("GF", BracketStage.GrandFinal, 4, "Winner(UF)", "Winner(LF)", null, null)
        ];
    }

    private static IReadOnlyList<BracketSlot> Build7Teams()
    {
        return
        [
            // Play-in 6th vs 7th
            new BracketSlot("PI", BracketStage.PlayIn, 1, "6th", "7th", "L2", null),

            // Upper R1
            new BracketSlot("U1", BracketStage.Upper, 2, "1st", "4th", "UF", "L2"), // Loser U1 -> L2 (Cross-bracket)
            new BracketSlot("U2", BracketStage.Upper, 2, "2nd", "3rd", "UF", "L1"), // Loser U2 -> L1 (Cross-bracket)

            // Lower R1
            new BracketSlot("L1", BracketStage.Lower, 2, "Loser(U2)", "5th", "LR2", null),
            new BracketSlot("L2", BracketStage.Lower, 2, "Loser(U1)", "Winner(PI)", "LR2", null),

            // Upper Final & Lower R2
            new BracketSlot("UF", BracketStage.Upper, 3, "Winner(U1)", "Winner(U2)", "GF", "LF"),
            new BracketSlot("LR2", BracketStage.Lower, 3, "Winner(L1)", "Winner(L2)", "LF", null),

            // Lower Final & Grand Final
            new BracketSlot("LF", BracketStage.Lower, 4, "Loser(UF)", "Winner(LR2)", "GF", null),
            new BracketSlot("GF", BracketStage.GrandFinal, 5, "Winner(UF)", "Winner(LF)", null, null)
        ];
    }

    private static IReadOnlyList<BracketSlot> Build8Teams()
    {
        return
        [
            // Upper R1
            new BracketSlot("U1", BracketStage.Upper, 1, "1st", "4th", "UF", "LR2_2"), // Loser U1 -> LR2_2 (Cross-bracket contra vencedor de L2)
            new BracketSlot("U2", BracketStage.Upper, 1, "2nd", "3rd", "UF", "LR2_1"), // Loser U2 -> LR2_1 (Cross-bracket contra vencedor de L1)

            // Lower R1
            new BracketSlot("L1", BracketStage.Lower, 1, "5th", "8th", "LR2_1", null),
            new BracketSlot("L2", BracketStage.Lower, 1, "6th", "7th", "LR2_2", null),

            // Lower R2 (Cross-Bracketing Drop-Down)
            // U1 cai para enfrentar vencedor de L2
            // U2 cai para enfrentar vencedor de L1
            new BracketSlot("LR2_1", BracketStage.Lower, 2, "Loser(U2)", "Winner(L1)", "LR3", null),
            new BracketSlot("LR2_2", BracketStage.Lower, 2, "Loser(U1)", "Winner(L2)", "LR3", null),

            // Upper Final & Lower R3
            new BracketSlot("UF", BracketStage.Upper, 3, "Winner(U1)", "Winner(U2)", "GF", "LF"),
            new BracketSlot("LR3", BracketStage.Lower, 3, "Winner(LR2_1)", "Winner(LR2_2)", "LF", null),

            // Lower Final & Grand Final
            new BracketSlot("LF", BracketStage.Lower, 4, "Loser(UF)", "Winner(LR3)", "GF", null),
            new BracketSlot("GF", BracketStage.GrandFinal, 5, "Winner(UF)", "Winner(LF)", null, null)
        ];
    }
}
