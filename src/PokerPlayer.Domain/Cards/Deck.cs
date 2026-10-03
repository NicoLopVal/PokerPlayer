using PokerPlayer.Domain.Enums;

namespace PokerPlayer.Domain.Cards;

public static class Deck
{
    public const int Size = 52;

    /// <summary>All 52 cards, ordered by <see cref="Card.Index"/>.</summary>
    public static IReadOnlyList<Card> All { get; } =
        Enum.GetValues<Rank>()
            .SelectMany(rank => Enum.GetValues<Suit>().Select(suit => new Card(rank, suit)))
            .ToArray();

    /// <summary>Cards not present in <paramref name="known"/>.</summary>
    public static List<Card> Without(IEnumerable<Card> known)
    {
        var excluded = new HashSet<Card>(known);
        return All.Where(card => !excluded.Contains(card)).ToList();
    }
}
