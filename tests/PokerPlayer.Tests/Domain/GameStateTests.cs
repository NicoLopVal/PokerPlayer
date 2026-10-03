using PokerPlayer.Domain;
using PokerPlayer.Domain.Enums;
using PokerPlayer.Domain.Exceptions;
using PokerPlayer.Domain.Players;
using static PokerPlayer.Tests.TestData;

namespace PokerPlayer.Tests.Domain;

public class GameStateTests
{
    [Fact]
    public void BigBlind_DefaultsToTwo()
    {
        var state = State(bigBlind: null);

        Assert.Equal(2, state.BigBlind);
        Assert.Equal(1, state.SmallBlind);
    }

    [Fact]
    public void BetToCall_ZeroIsAllowed()
    {
        Assert.Equal(0, State(betToCall: 0).BetToCall);
    }

    [Fact]
    public void BetToCall_NegativeThrows()
    {
        Assert.Throws<InvalidInputException>(() => State(betToCall: -1));
    }

    [Fact]
    public void CardOnBothHandAndBoard_Throws()
    {
        Assert.Throws<InvalidInputException>(() => State(hand: "AH KH", board: "AH 7C 2D"));
    }

    [Fact]
    public void ExactlyOneHeroIsRequired()
    {
        var players = new[]
        {
            new Player("Villain", 50, Blind.SB),
            new Player("Other", 50, Blind.BB)
        };

        Assert.Throws<InvalidInputException>(() =>
            new GameState(Hand("AH KH"), Board(""), Street.Preflop, players, 3, 1, ["bet", "raise"]));
    }

    [Fact]
    public void Hero_AndVillains_AreResolvedByName()
    {
        var state = State(heroStack: 40, villainStack: 70);

        Assert.Equal(40, state.Hero.Stack);
        Assert.Equal(70, Assert.Single(state.Villains).Stack);
    }

    [Theory]
    [InlineData(-0.1, 0.2, 0.1, 1)]
    [InlineData(0.2, 1.1, 0.1, 1)]
    [InlineData(0.2, 0.2, 0.1, -1)]
    public void PlayerStats_OutOfRange_Throw(double vpip, double pfr, double threeBet, double af)
    {
        Assert.Throws<InvalidInputException>(() => new PlayerStats(vpip, pfr, threeBet, af));
    }
}
