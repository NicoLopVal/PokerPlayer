using PokerPlayer.Domain.Decisions;
using PokerPlayer.Domain.Enums;

namespace PokerPlayer.Tests.Domain;

public class ActionSelectorTests
{
    private static ActionPool Pool(double fold, double check, double call, double raise, int? raiseAmount = 12) =>
        new(
        [
            new ActionOption(PlayerAction.FOLD, fold, fold > 0 ? 0 : null, null),
            new ActionOption(PlayerAction.CHECK, check, check > 0 ? 0 : null, null),
            new ActionOption(PlayerAction.CALL, call, call > 0 ? 0 : null, null),
            new ActionOption(PlayerAction.RAISE, raise, raise > 0 ? 0 : null, raise > 0 ? raiseAmount : null)
        ]);

    [Fact]
    public void Frequencies_FollowTheProbabilities()
    {
        var selector = new ActionSelector(new Random(11));
        var pool = Pool(0.2, 0, 0.5, 0.3);
        const int draws = 100_000;

        var counts = Enumerable.Range(0, draws)
            .Select(_ => selector.Select(pool).Action)
            .GroupBy(action => action)
            .ToDictionary(group => group.Key, group => (double)group.Count() / draws);

        Assert.InRange(counts[PlayerAction.FOLD], 0.19, 0.21);
        Assert.InRange(counts[PlayerAction.CALL], 0.49, 0.51);
        Assert.InRange(counts[PlayerAction.RAISE], 0.29, 0.31);
        Assert.False(counts.ContainsKey(PlayerAction.CHECK));
    }

    [Fact]
    public void ZeroProbabilityActions_AreNeverDrawn()
    {
        var selector = new ActionSelector(new Random(3));
        var pool = Pool(0, 0.999999, 0, 0.000001);

        Assert.All(Enumerable.Range(0, 10_000), _ =>
            Assert.Contains(selector.Select(pool).Action, new[] { PlayerAction.CHECK, PlayerAction.RAISE }));
    }

    [Fact]
    public void CertainAction_IsAlwaysDrawn()
    {
        var selector = new ActionSelector(new Random(5));

        Assert.All(Enumerable.Range(0, 1_000), _ => Assert.Equal(PlayerAction.CALL, selector.Select(Pool(0, 0, 1, 0)).Action));
    }

    [Fact]
    public void ProbabilitiesThatDoNotQuiteSumToOne_StillAlwaysPickAnAction()
    {
        // Rounding can leave the sum at 0.9999999; the roll is scaled to the real total.
        var selector = new ActionSelector(new Random(8));
        var pool = Pool(0.3333333, 0, 0.3333333, 0.3333333);

        Assert.All(Enumerable.Range(0, 10_000), _ => Assert.NotNull(selector.Select(pool)));
    }

    [Fact]
    public void SameSeed_SameDecisions()
    {
        var pool = Pool(0.2, 0, 0.5, 0.3);
        var first = new ActionSelector(new Random(42));
        var second = new ActionSelector(new Random(42));

        Assert.Equal(
            Enumerable.Range(0, 100).Select(_ => first.Select(pool)),
            Enumerable.Range(0, 100).Select(_ => second.Select(pool)));
    }

    [Fact]
    public void EmptyPool_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => new ActionSelector(new Random(1)).Select(Pool(0, 0, 0, 0)));
    }

    [Theory]
    [InlineData(PlayerAction.FOLD, null, "FOLD")]
    [InlineData(PlayerAction.CHECK, null, "CHECK")]
    [InlineData(PlayerAction.CALL, null, "CALL")]
    [InlineData(PlayerAction.RAISE, 12, "RAISE 12")]
    public void Decision_PrintsAsTheReply(PlayerAction action, int? amount, string expected)
    {
        Assert.Equal(expected, new Decision(action, amount).ToString());
    }

    [Fact]
    public void Raise_CarriesTheSuggestedAmount()
    {
        var decision = new ActionSelector(new Random(1)).Select(Pool(0, 0, 0, 1, raiseAmount: 37));

        Assert.Equal(new Decision(PlayerAction.RAISE, 37), decision);
        Assert.Equal("RAISE 37", decision.ToString());
    }
}
