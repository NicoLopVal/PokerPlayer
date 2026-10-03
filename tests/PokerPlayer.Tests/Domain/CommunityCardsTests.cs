using PokerPlayer.Domain.Cards;
using PokerPlayer.Domain.Enums;
using PokerPlayer.Domain.Exceptions;
using static PokerPlayer.Tests.TestData;

namespace PokerPlayer.Tests.Domain;

public class CommunityCardsTests
{
    private static Card?[] Slots(params string?[] slots) =>
        slots.Select(text => text is null ? (Card?)null : C(text)).ToArray();

    [Theory]
    [InlineData(null, null, null, null, null, Street.Preflop)]
    [InlineData("7C", "JH", "AS", null, null, Street.Flop)]
    [InlineData("7C", "JH", "AS", "2D", null, Street.Turn)]
    [InlineData("7C", "JH", "AS", "2D", "9S", Street.River)]
    public void ValidPatterns_AreAccepted(string? a, string? b, string? c, string? d, string? e, Street street)
    {
        var board = new CommunityCards(Slots(a, b, c, d, e));

        Assert.Equal(street, board.ImpliedStreet);
    }

    [Theory]
    [InlineData("7C", null, null, null, null)]
    [InlineData("7C", "JH", null, null, null)]
    [InlineData(null, "JH", "AS", null, null)]
    [InlineData("7C", "JH", "AS", null, "9S")]
    [InlineData(null, null, null, null, "9S")]
    public void InvalidPatterns_Throw(string? a, string? b, string? c, string? d, string? e)
    {
        Assert.Throws<InvalidInputException>(() => new CommunityCards(Slots(a, b, c, d, e)));
    }

    [Fact]
    public void WrongSlotCount_Throws()
    {
        Assert.Throws<InvalidInputException>(() => new CommunityCards(Slots("7C", "JH", "AS")));
    }

    [Fact]
    public void DuplicatedCard_Throws()
    {
        Assert.Throws<InvalidInputException>(() => new CommunityCards(Slots("7C", "7C", "AS", null, null)));
    }

    [Fact]
    public void Equality_ComparesSlotsByValue()
    {
        Assert.Equal(Board("7C JH AS"), Board("7C JH AS"));
        Assert.NotEqual(Board("7C JH AS"), Board("7C JH AD"));
    }
}
