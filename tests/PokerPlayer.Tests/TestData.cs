using PokerPlayer.Domain;
using PokerPlayer.Domain.Cards;
using PokerPlayer.Domain.Enums;
using PokerPlayer.Domain.Players;

namespace PokerPlayer.Tests;

internal static class TestData
{
    public static Card C(string text) => Card.Parse(text);

    public static Card[] Cards(string text) => text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(Card.Parse).ToArray();

    public static Hand Hand(string text)
    {
        var cards = Cards(text);
        return new Hand(cards[0], cards[1]);
    }

    public static CommunityCards Board(string text)
    {
        var cards = Cards(text).Select(card => (Card?)card).ToList();
        while (cards.Count < CommunityCards.SlotCount)
            cards.Add(null);
        return new CommunityCards(cards);
    }

    public static PlayerStats DefaultStats => new(0.24, 0.20, 0.08, 2.5);

    public static GameState State(
        string hand = "AH KH",
        string board = "",
        Street? street = null,
        int pot = 3,
        int betToCall = 1,
        int heroStack = 49,
        int villainStack = 63,
        string[]? history = null,
        int? bigBlind = null,
        int? heroInvested = null,
        PlayerStats? villainStats = null)
    {
        var communityCards = Board(board);
        return new GameState(
            Hand(hand),
            communityCards,
            street ?? communityCards.ImpliedStreet,
            [
                new Player("Hero", heroStack, Blind.SB, heroInvested),
                new Player("Villain", villainStack, Blind.BB, stats: villainStats ?? DefaultStats)
            ],
            pot,
            betToCall,
            history ?? ["bet", "raise"],
            bigBlind);
    }
}
