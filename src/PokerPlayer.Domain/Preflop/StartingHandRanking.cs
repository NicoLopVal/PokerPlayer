using PokerPlayer.Domain.Enums;

namespace PokerPlayer.Domain.Preflop;

/// <summary>
/// The 169 starting hands ordered from strongest to weakest by Chen score,
/// used to turn a percentage (e.g. VPIP 24%) into a concrete range.
/// Ties are broken by pairs first, then higher cards, then suited before offsuit.
/// </summary>
public static class StartingHandRanking
{
    public static IReadOnlyList<StartingHand> Ordered { get; } = BuildOrdering();

    private static readonly Dictionary<StartingHand, double> TopPercentByHand = BuildTopPercent();

    /// <summary>
    /// Share of all 1,326 combos ranked at or above this hand. AA ≈ 0.005, 72o = 1.0.
    /// "Top 5%" means the hand is among the best 5% of starting hands.
    /// </summary>
    public static double TopPercent(StartingHand hand) => TopPercentByHand[hand];

    /// <summary>The strongest hands whose combos add up to roughly <paramref name="share"/> of all combos.</summary>
    public static IReadOnlyList<StartingHand> Top(double share)
    {
        var target = Math.Clamp(share, 0, 1) * StartingHand.TotalCombos;
        var result = new List<StartingHand>();
        var combos = 0;
        foreach (var hand in Ordered)
        {
            if (combos >= target && result.Count > 0)
                break;
            result.Add(hand);
            combos += hand.Combos;
        }
        return result;
    }

    private static List<StartingHand> BuildOrdering()
    {
        var ranks = Enum.GetValues<Rank>();
        var hands = new List<StartingHand>(169);
        foreach (var high in ranks)
        foreach (var low in ranks.Where(rank => rank <= high))
        {
            hands.Add(new StartingHand(high, low, suited: false));
            if (high != low)
                hands.Add(new StartingHand(high, low, suited: true));
        }

        return hands
            .OrderByDescending(ChenFormula.Score)
            .ThenByDescending(hand => hand.IsPair)
            .ThenByDescending(hand => hand.High)
            .ThenByDescending(hand => hand.Low)
            .ThenByDescending(hand => hand.Suited)
            .ToList();
    }

    private static Dictionary<StartingHand, double> BuildTopPercent()
    {
        var result = new Dictionary<StartingHand, double>();
        var combos = 0;
        foreach (var hand in Ordered)
        {
            combos += hand.Combos;
            result[hand] = (double)combos / StartingHand.TotalCombos;
        }
        return result;
    }
}
