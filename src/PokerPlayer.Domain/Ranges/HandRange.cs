using PokerPlayer.Domain.Cards;
using PokerPlayer.Domain.Preflop;

namespace PokerPlayer.Domain.Ranges;

/// <summary>A set of concrete two-card combos a player might hold, each equally likely.</summary>
public sealed class HandRange
{
    public IReadOnlyList<(Card First, Card Second)> Combos { get; }

    /// <summary>Share of starting hands the range was built from (0..1), before card removal.</summary>
    public double Width { get; }

    private HandRange(IReadOnlyList<(Card, Card)> combos, double width)
    {
        Combos = combos;
        Width = width;
    }

    /// <summary>A range made of explicit combos, e.g. when the villain's hand is known in a hand review.</summary>
    public static HandRange FromCombos(IEnumerable<(Card First, Card Second)> combos)
    {
        var list = combos.ToList();
        return new HandRange(list, (double)list.Count / StartingHand.TotalCombos);
    }

    /// <summary>
    /// Builds the top <paramref name="width"/> share of starting hands, then removes every combo that
    /// uses a card we can see (blockers). Falls back to any two cards if nothing is left.
    /// </summary>
    public static HandRange FromTopPercent(double width, IReadOnlyCollection<Card> deadCards)
    {
        var classes = StartingHandRanking.Top(width).ToHashSet();
        var available = Deck.Without(deadCards);

        var combos = new List<(Card, Card)>();
        for (var i = 0; i < available.Count; i++)
        for (var j = i + 1; j < available.Count; j++)
            if (classes.Contains(StartingHand.From(available[i], available[j])))
                combos.Add((available[i], available[j]));

        return combos.Count > 0 || width >= 1
            ? new HandRange(combos, width)
            : FromTopPercent(1, deadCards);
    }
}
