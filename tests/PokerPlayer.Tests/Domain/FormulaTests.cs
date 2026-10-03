using PokerPlayer.Domain.Kpis.Calculators;
using static PokerPlayer.Tests.TestData;

namespace PokerPlayer.Tests.Domain;

public class FormulaTests
{
    [Fact]
    public void Draws_FlushDrawOnFlop()
    {
        // 9 outs, 47 unseen: ~19% next card, ~35% by the river, rule of 4 says 36%.
        Assert.Equal(9 / 47.0, PokerMath.HitNextCard(9, 47), 6);
        Assert.Equal(0.3497, PokerMath.HitWithinTwoCards(9, 47), 4);
        Assert.Equal(0.36, PokerMath.RuleOfTwoAndFour(9, cardsToCome: 2), 6);
        Assert.Equal(0.18, PokerMath.RuleOfTwoAndFour(9, cardsToCome: 1), 6);
        Assert.Equal(38 / 9.0, PokerMath.OddsAgainst(9, 47)!.Value, 6);
        Assert.Null(PokerMath.OddsAgainst(0, 47));
    }

    [Fact]
    public void OddsToProbability_ConvertsRatio()
    {
        Assert.Equal(0.2, PokerMath.OddsToProbability(4, 1), 6);
    }

    [Fact]
    public void Combinations_StartingHands()
    {
        Assert.Equal(1326, PokerMath.Combinations(52, 2));
        Assert.Equal(6, PokerMath.Combinations(4, 2));
        Assert.Equal(0, PokerMath.Combinations(2, 3));
    }

    [Fact]
    public void Pricing_PotSizedBet()
    {
        // Villain bets 10 into 10: pot is now 20, Hero calls 10.
        Assert.Equal(1 / 3.0, PokerMath.RequiredEquity(10, 20), 6);
        Assert.Equal(2.0, PokerMath.PotOddsRatio(10, 20), 6);
        Assert.Equal(0.5, PokerMath.MinimumDefenseFrequency(10, 10), 6);
        Assert.Equal(0.5, PokerMath.BluffBreakEven(10, 10), 6);
        Assert.Equal(1 / 3.0, PokerMath.OptimalBluffShare(10, 10), 6);
    }

    [Fact]
    public void Stacks()
    {
        Assert.Equal(25.0, PokerMath.StackInBigBlinds(50, 2), 6);
        Assert.Equal(50 / 3.0, PokerMath.MRatio(50, 1, 2), 6);
        Assert.Equal(5.0, PokerMath.StackToPotRatio(50, 10)!.Value, 6);
        Assert.Null(PokerMath.StackToPotRatio(50, 0));
        Assert.Equal(0.2, PokerMath.PotCommitment(10, 40), 6);
    }

    [Fact]
    public void GeometricBetSize_GetsAllInOnTheLastStreet()
    {
        const int pot = 10, stack = 100, streets = 3;
        var bet = PokerMath.GeometricBetSize(pot, stack, streets);

        // Replay: each street both players put in `bet` sized as the same pot fraction.
        double currentPot = pot, remaining = stack;
        var fraction = bet / pot;
        for (var i = 0; i < streets; i++)
        {
            var size = fraction * currentPot;
            remaining -= size;
            currentPot += 2 * size;
        }
        Assert.Equal(0, remaining, 6);
        Assert.Equal(stack, PokerMath.GeometricBetSize(pot, stack, 1), 6);
    }

    [Fact]
    public void ExpectedValue_Formulas()
    {
        // 40% equity, pot 20 (incl. villain bet of 10), call 10: 0.4·30 - 10 = 2.
        Assert.Equal(2.0, ExpectedValue.Call(0.4, 20, 10), 6);
        // Bet 10 into 10 with 30% equity when called: 0.3·30 - 10 = -1.
        var whenCalled = ExpectedValue.WhenCalled(0.3, 10, 10, 10);
        Assert.Equal(-1.0, whenCalled, 6);
        Assert.Equal(1 / 11.0, ExpectedValue.BreakEvenFoldFrequency(10, whenCalled), 6);
        Assert.Equal(0, ExpectedValue.BetOrRaise(1 / 11.0, 10, whenCalled), 6);
        Assert.Equal(0, ExpectedValue.BreakEvenFoldFrequency(10, 3), 6);
        Assert.Equal(0, ExpectedValue.PureBluff(0.5, 10, 10), 6);
    }

    [Theory]
    [InlineData("4H 5H", "KH 9H 2C", 9)]   // Flush draw only; pairing a 4 or 5 is not an out
    [InlineData("6C 5D", "7H 8S KC", 8)]   // Open-ended straight draw
    [InlineData("AC QD", "7H 8S 2C", 6)]   // Two overcards
    [InlineData("6C 5D", "7H 9S KC", 4)]   // Gutshot
    [InlineData("AS AD", "AH AC 2D", 0)]   // Quads: nothing improves meaningfully
    public void Outs_CountCommonDraws(string hand, string board, int expected)
    {
        Assert.Equal(expected, OutsCounter.Count(Hand(hand), Cards(board)));
    }
}
