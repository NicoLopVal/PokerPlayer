using PokerPlayer.Domain.Enums;
using PokerPlayer.Domain.Preflop;

namespace PokerPlayer.Tests.Domain;

public class PreflopTests
{
    [Theory]
    [InlineData(Rank.Ace, Rank.Ace, false, 20)]
    [InlineData(Rank.King, Rank.King, false, 16)]
    [InlineData(Rank.Two, Rank.Two, false, 5)]
    [InlineData(Rank.Five, Rank.Five, false, 5)]
    [InlineData(Rank.Six, Rank.Six, false, 6)]
    [InlineData(Rank.Ace, Rank.King, true, 12)]
    [InlineData(Rank.Ace, Rank.King, false, 10)]
    [InlineData(Rank.Jack, Rank.Ten, true, 9)]
    [InlineData(Rank.Ten, Rank.Nine, true, 8)]
    [InlineData(Rank.Seven, Rank.Two, false, -1)]
    public void ChenScore_MatchesKnownValues(Rank first, Rank second, bool suited, int expected)
    {
        Assert.Equal(expected, ChenFormula.Score(new StartingHand(first, second, suited)));
    }

    [Fact]
    public void Ranking_Covers169HandsAnd1326Combos()
    {
        Assert.Equal(169, StartingHandRanking.Ordered.Distinct().Count());
        Assert.Equal(1326, StartingHandRanking.Ordered.Sum(hand => hand.Combos));
        Assert.Equal("AA", StartingHandRanking.Ordered[0].ToString());
    }

    [Fact]
    public void TopPercent_IsMonotonicFromBestToWorst()
    {
        var aces = StartingHandRanking.TopPercent(new StartingHand(Rank.Ace, Rank.Ace, false));
        var sevenTwo = StartingHandRanking.TopPercent(new StartingHand(Rank.Seven, Rank.Two, false));
        var last = StartingHandRanking.TopPercent(StartingHandRanking.Ordered[^1]);

        Assert.Equal(6 / 1326.0, aces, 6);
        Assert.InRange(sevenTwo, 0.97, 1.0); // shares the lowest Chen score (-1) with a few other hands
        Assert.Equal(1.0, last, 6);
        Assert.Equal(StartingHandRanking.Ordered.Select(StartingHandRanking.TopPercent).Order(),
            StartingHandRanking.Ordered.Select(StartingHandRanking.TopPercent));
    }

    [Fact]
    public void Top_ReturnsAboutTheRequestedShare()
    {
        var combos = StartingHandRanking.Top(0.24).Sum(hand => hand.Combos) / 1326.0;

        Assert.InRange(combos, 0.24, 0.27);
    }
}
