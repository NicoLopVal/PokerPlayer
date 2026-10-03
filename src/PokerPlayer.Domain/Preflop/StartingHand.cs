using PokerPlayer.Domain.Cards;
using PokerPlayer.Domain.Enums;

namespace PokerPlayer.Domain.Preflop;

/// <summary>
/// One of the 169 strategically distinct starting hands, e.g. "AKs", "T9o", "77".
/// Suits only matter as suited vs offsuit before the flop.
/// </summary>
public readonly record struct StartingHand
{
    public const int TotalCombos = 1326; // C(52, 2)

    public Rank High { get; }
    public Rank Low { get; }
    public bool Suited { get; }

    public StartingHand(Rank first, Rank second, bool suited)
    {
        High = first >= second ? first : second;
        Low = first >= second ? second : first;
        Suited = suited && High != Low;
    }

    public static StartingHand From(Hand hand) => new(hand.First.Rank, hand.Second.Rank, hand.IsSuited);

    public static StartingHand From(Card first, Card second) => new(first.Rank, second.Rank, first.Suit == second.Suit);

    public bool IsPair => High == Low;

    /// <summary>Card combinations of this hand in a full deck: pair 6, suited 4, offsuit 12.</summary>
    public int Combos => IsPair ? 6 : Suited ? 4 : 12;

    public override string ToString()
    {
        var high = new Card(High, Suit.Spades).ToString()[0];
        var low = new Card(Low, Suit.Spades).ToString()[0];
        return IsPair ? $"{high}{low}" : $"{high}{low}{(Suited ? 's' : 'o')}";
    }
}
