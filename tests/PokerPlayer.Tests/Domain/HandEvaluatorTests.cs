using PokerPlayer.Domain.Evaluation;
using static PokerPlayer.Tests.TestData;

namespace PokerPlayer.Tests.Domain;

public class HandEvaluatorTests
{
    private static HandValue Eval(string cards) => HandEvaluator.Evaluate(Cards(cards));

    [Theory]
    [InlineData("AS KS QS JS TS 2D 3C", HandCategory.StraightFlush)]
    [InlineData("9H 9D 9S 9C 2D 3C 4H", HandCategory.FourOfAKind)]
    [InlineData("9H 9D 9S 2C 2D 3C 4H", HandCategory.FullHouse)]
    [InlineData("9H 9D 9S 2C 2D 2H 4H", HandCategory.FullHouse)]
    [InlineData("AH 9H 7H 4H 2H KD QC", HandCategory.Flush)]
    [InlineData("9H 8D 7S 6C 5D 2C 2H", HandCategory.Straight)]
    [InlineData("AH 2D 3S 4C 5D KC QH", HandCategory.Straight)]
    [InlineData("9H 9D 9S 2C 5D KC QH", HandCategory.ThreeOfAKind)]
    [InlineData("9H 9D 2S 2C 5D KC QH", HandCategory.TwoPair)]
    [InlineData("9H 9D 2S 3C 5D KC QH", HandCategory.Pair)]
    [InlineData("9H 7D 2S 3C 5D KC QH", HandCategory.HighCard)]
    public void Evaluate_FindsCategory(string cards, HandCategory expected)
    {
        Assert.Equal(expected, Eval(cards).Category);
    }

    [Fact]
    public void WheelStraight_LosesToSixHighStraight()
    {
        Assert.True(Eval("AH 2D 3S 4C 5D") < Eval("2D 3S 4C 5D 6H"));
    }

    [Fact]
    public void Kickers_BreakTies()
    {
        Assert.True(Eval("AH AD KS 7C 5D") > Eval("AH AD QS 7C 5D"));
        Assert.True(Eval("9H 9D 4S 4C AD") > Eval("9H 9D 4S 4C KD"));
    }

    [Fact]
    public void ThreePairs_UseTheBestTwoAndBestKicker()
    {
        // Pairs K, 9, 4 and a 2: best is KK99 with the 4 as kicker.
        Assert.Equal(Eval("KH KD 9S 9C 4D"), Eval("KH KD 9S 9C 4D 4H 2C"));
    }

    [Fact]
    public void FlushBeatsStraight()
    {
        Assert.True(Eval("2H 5H 7H 9H JH") > Eval("TH JD QS KC AD"));
    }

    [Fact]
    public void IdenticalHands_Tie()
    {
        Assert.Equal(Eval("AH KD 9S 7C 5D"), Eval("AS KC 9D 7H 5C"));
    }
}
