using PokerPlayer.Domain.Cards;
using PokerPlayer.Domain.Enums;
using PokerPlayer.Domain.Exceptions;
using static PokerPlayer.Tests.TestData;

namespace PokerPlayer.Tests.Domain;

public class CardTests
{
    [Theory]
    [InlineData("2D", Rank.Two, Suit.Diamonds)]
    [InlineData("7H", Rank.Seven, Suit.Hearts)]
    [InlineData("TS", Rank.Ten, Suit.Spades)]
    [InlineData("ac", Rank.Ace, Suit.Clubs)]
    public void Parse_ReadsRankAndSuit(string text, Rank rank, Suit suit)
    {
        Assert.Equal(new Card(rank, suit), Card.Parse(text));
    }

    [Theory]
    [InlineData("")]
    [InlineData("1H")]
    [InlineData("10H")]
    [InlineData("AX")]
    [InlineData("A")]
    public void Parse_RejectsInvalidText(string text)
    {
        Assert.Throws<InvalidInputException>(() => Card.Parse(text));
    }

    [Fact]
    public void Deck_HasFiftyTwoUniqueCardsThatRoundTrip()
    {
        Assert.Equal(52, Deck.All.Distinct().Count());
        Assert.Equal(Enumerable.Range(0, 52), Deck.All.Select(card => card.Index));
        Assert.All(Deck.All, card => Assert.Equal(card, Card.Parse(card.ToString())));
    }

    [Fact]
    public void Hand_RejectsTheSameCardTwice()
    {
        Assert.Throws<InvalidInputException>(() => new Hand(C("AS"), C("AS")));
    }

    [Fact]
    public void Hand_EqualsAnotherHandWithTheSameCards()
    {
        Assert.Equal(Hand("AS KD"), Hand("AS KD"));
    }
}
