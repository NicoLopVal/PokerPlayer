using PokerPlayer.Domain.Enums;
using PokerPlayer.Domain.Exceptions;
using PokerPlayer.Domain.History;
using PokerPlayer.Domain.Players;
using PokerPlayer.Domain.Ranges;
using static PokerPlayer.Tests.TestData;

namespace PokerPlayer.Tests.Domain;

public class ActionHistoryTests
{
    [Fact]
    public void FirstTwoEntries_AreBlindPosts_ThenSmallBlindActs()
    {
        var history = ActionHistoryDecoder.Decode(["bet", "raise"]);

        Assert.All(history.Actions, action => Assert.True(action.IsBlindPost));
        Assert.Equal([Blind.SB, Blind.BB], history.Actions.Select(action => action.Actor));
        Assert.Equal(Street.Preflop, history.CurrentStreet);
        Assert.Equal(Blind.SB, history.NextToAct);
    }

    [Fact]
    public void Limp_GivesBigBlindTheOption_AndCheckClosesPreflop()
    {
        var afterLimp = ActionHistoryDecoder.Decode(["bet", "raise", "call"]);
        Assert.Equal(Street.Preflop, afterLimp.CurrentStreet);
        Assert.Equal(Blind.BB, afterLimp.NextToAct);

        var afterCheck = ActionHistoryDecoder.Decode(["bet", "raise", "call", "check"]);
        Assert.Equal(Street.Flop, afterCheck.CurrentStreet);
        Assert.Equal(Blind.BB, afterCheck.NextToAct);
    }

    [Fact]
    public void RaiseAndCall_ClosesPreflop_BigBlindActsFirstOnFlop()
    {
        var history = ActionHistoryDecoder.Decode(["bet", "raise", "raise", "call", "check"]);

        Assert.Equal(Street.Flop, history.CurrentStreet);
        Assert.Equal(Blind.SB, history.NextToAct);
        Assert.Equal(new[] { Blind.SB, Blind.BB, Blind.BB }, history.Actions.Skip(2).Select(action => action.Actor));
    }

    [Fact]
    public void CheckCheck_MovesToNextStreet()
    {
        var history = ActionHistoryDecoder.Decode(["bet", "raise", "call", "check", "check", "check"]);

        Assert.Equal(Street.Turn, history.CurrentStreet);
        Assert.Equal(Blind.BB, history.NextToAct);
    }

    [Fact]
    public void Fold_EndsTheHand()
    {
        var history = ActionHistoryDecoder.Decode(["bet", "raise", "fold"]);

        Assert.Null(history.NextToAct);
    }

    [Theory]
    [InlineData("allin")]
    [InlineData("All-In")]
    [InlineData("BET")]
    public void BetAndAllIn_AreTreatedAsRaise(string raw)
    {
        Assert.Equal(PlayerAction.RAISE, ActionHistoryDecoder.ParseAction(raw));
    }

    [Fact]
    public void UnknownAction_Throws()
    {
        Assert.Throws<InvalidInputException>(() => ActionHistoryDecoder.Decode(["bet", "raise", "dance"]));
    }

    [Theory]
    [InlineData(new[] { "bet", "raise", "call", "raise" }, 0.20)]          // SB limps, villain (BB) raises: PFR
    [InlineData(new[] { "bet", "raise", "raise", "raise" }, 0.08)]         // Hero raises, villain re-raises: 3-bet
    [InlineData(new[] { "bet", "raise", "raise", "call" }, 0.24)]          // Hero raises, villain calls: VPIP
    [InlineData(new[] { "bet", "raise", "call", "check" }, 1.0)]           // Villain checks the option: any two
    public void VillainRange_FollowsPreflopAction(string[] actions, double expected)
    {
        var villain = new Player("Villain", 50, Blind.BB, stats: DefaultStats);

        var width = VillainRangeEstimator.EstimateWidth(villain, ActionHistoryDecoder.Decode(actions));

        Assert.Equal(expected, width, 6);
    }

    [Fact]
    public void VillainRange_WithoutStats_UsesAveragePlayer()
    {
        var villain = new Player("Villain", 50, Blind.BB);

        Assert.Equal(PlayerStats.Average.ThreeBet,
            VillainRangeEstimator.EstimateWidth(villain, ActionHistoryDecoder.Decode(["bet", "raise", "raise", "raise"])), 6);
    }
}
