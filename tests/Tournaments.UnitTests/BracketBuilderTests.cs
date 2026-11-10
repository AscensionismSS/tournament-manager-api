using Tournaments.Domain.Services;
using Tournaments.Domain.ValueObjects;
using Xunit;

namespace Tournaments.UnitTests;

public class BracketBuilderTests
{
    [Fact]
    public void BuildSlots_4Teams_CreatesUpperLowerFinals()
    {
        var slots = DoubleEliminationBracketBuilder.BuildSlots(4);

        Assert.Equal(4, slots.Count);
        Assert.Contains(slots, s => s.Code == "U1" && s.TeamASource == "1st" && s.TeamBSource == "2nd");
        Assert.Contains(slots, s => s.Code == "L1" && s.TeamASource == "3rd" && s.TeamBSource == "4th");
        Assert.Contains(slots, s => s.Code == "LF" && s.TeamASource == "Loser(U1)" && s.TeamBSource == "Winner(L1)");
        Assert.Contains(slots, s => s.Code == "GF" && s.TeamASource == "Winner(U1)" && s.TeamBSource == "Winner(LF)");
    }

    [Fact]
    public void BuildSlots_5Teams_IncludesPlayInFor4thAnd5th()
    {
        var slots = DoubleEliminationBracketBuilder.BuildSlots(5);

        Assert.Equal(5, slots.Count);
        var playIn = slots.First(s => s.Code == "PI");
        Assert.Equal(BracketStage.PlayIn, playIn.Stage);
        Assert.Equal("4th", playIn.TeamASource);
        Assert.Equal("5th", playIn.TeamBSource);

        var l1 = slots.First(s => s.Code == "L1");
        Assert.Equal("3rd", l1.TeamASource);
        Assert.Equal("Winner(PI)", l1.TeamBSource);
    }

    [Fact]
    public void BuildSlots_7Teams_IncludesPlayInFor6thAnd7th()
    {
        var slots = DoubleEliminationBracketBuilder.BuildSlots(7);

        var playIn = slots.First(s => s.Code == "PI");
        Assert.Equal(BracketStage.PlayIn, playIn.Stage);
        Assert.Equal("6th", playIn.TeamASource);
        Assert.Equal("7th", playIn.TeamBSource);
    }

    [Fact]
    public void BuildSlots_8Teams_EnforcesStrictCrossBracketingDropdown()
    {
        var slots = DoubleEliminationBracketBuilder.BuildSlots(8);

        // U1 e U2
        var u1 = slots.First(s => s.Code == "U1");
        var u2 = slots.First(s => s.Code == "U2");

        // Regra de Inversão na Queda (Cross-Bracketing Drop-Down):
        // Perdedor de U1 cai para enfrentar vencedor de L2 (LR2_2)
        // Perdedor de U2 cai para enfrentar vencedor de L1 (LR2_1)
        Assert.Equal("LR2_2", u1.LoserTargetSlotCode);
        Assert.Equal("LR2_1", u2.LoserTargetSlotCode);

        var lr2_1 = slots.First(s => s.Code == "LR2_1");
        Assert.Equal("Loser(U2)", lr2_1.TeamASource);
        Assert.Equal("Winner(L1)", lr2_1.TeamBSource);

        var lr2_2 = slots.First(s => s.Code == "LR2_2");
        Assert.Equal("Loser(U1)", lr2_2.TeamASource);
        Assert.Equal("Winner(L2)", lr2_2.TeamBSource);
    }

    [Fact]
    public void BuildSlots_SingleElimination_4Teams_CreatesOnlySemifinalsAndGrandFinal_NoLower()
    {
        var slots = SingleEliminationBracketBuilder.BuildSlots(4);

        Assert.Equal(3, slots.Count);
        // Semifinais
        Assert.Contains(slots, s => s.Code == "SF1" && s.TeamASource == "1st" && s.TeamBSource == "4th" && s.WinnerTargetSlotCode == "GF" && s.LoserTargetSlotCode == null);
        Assert.Contains(slots, s => s.Code == "SF2" && s.TeamASource == "2nd" && s.TeamBSource == "3rd" && s.WinnerTargetSlotCode == "GF" && s.LoserTargetSlotCode == null);
        // Grande Final
        Assert.Contains(slots, s => s.Code == "GF" && s.Stage == BracketStage.GrandFinal && s.TeamASource == "Winner(SF1)" && s.TeamBSource == "Winner(SF2)" && s.LoserTargetSlotCode == null);
        // NENHUM slot pode ser Lower
        Assert.DoesNotContain(slots, s => s.Stage == BracketStage.Lower);
    }

    [Fact]
    public void BuildSlots_SingleElimination_8Teams_CreatesQuarterfinalsSemifinalsAndGrandFinal_NoLower()
    {
        var slots = SingleEliminationBracketBuilder.BuildSlots(8);

        Assert.Equal(7, slots.Count);
        Assert.Contains(slots, s => s.Code == "QF1" && s.TeamASource == "1st" && s.TeamBSource == "8th" && s.WinnerTargetSlotCode == "SF1");
        Assert.Contains(slots, s => s.Code == "QF2" && s.TeamASource == "4th" && s.TeamBSource == "5th" && s.WinnerTargetSlotCode == "SF1");
        Assert.Contains(slots, s => s.Code == "QF3" && s.TeamASource == "2nd" && s.TeamBSource == "7th" && s.WinnerTargetSlotCode == "SF2");
        Assert.Contains(slots, s => s.Code == "QF4" && s.TeamASource == "3rd" && s.TeamBSource == "6th" && s.WinnerTargetSlotCode == "SF2");

        Assert.Contains(slots, s => s.Code == "SF1" && s.TeamASource == "Winner(QF1)" && s.TeamBSource == "Winner(QF2)" && s.WinnerTargetSlotCode == "GF");
        Assert.Contains(slots, s => s.Code == "SF2" && s.TeamASource == "Winner(QF3)" && s.TeamBSource == "Winner(QF4)" && s.WinnerTargetSlotCode == "GF");

        Assert.Contains(slots, s => s.Code == "GF" && s.TeamASource == "Winner(SF1)" && s.TeamBSource == "Winner(SF2)");
        // NENHUM slot com LoserTarget ou Stage Lower
        Assert.All(slots, s => Assert.Null(s.LoserTargetSlotCode));
        Assert.DoesNotContain(slots, s => s.Stage == BracketStage.Lower);
    }
}
