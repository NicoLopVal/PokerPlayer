namespace PokerPlayer.Domain.Preflop;

/// <summary>
/// Cheap preflop equity estimate from hand strength alone, no simulation.
/// 1. Share of the villain range Hero's hand ranks above: the villain holds the top W of hands and
///    Hero sits at the top t, so Hero outranks (W - t) / W of that range (0 when t ≥ W).
/// 2. That share is mapped linearly onto [MinEquity, MaxEquity]. Preflop equities are compressed:
///    AA is only ~85% against a random hand and the worst hands still keep ~30%.
/// This is a heuristic: it ignores domination and suitedness effects beyond the ranking itself.
/// </summary>
public static class PreflopEquityModel
{
    public const double MinEquity = 0.25;
    public const double MaxEquity = 0.80;

    /// <param name="heroTopPercent">Hero's <see cref="StartingHandRanking.TopPercent"/>.</param>
    /// <param name="villainRangeWidth">Share of starting hands in the villain range (0..1].</param>
    public static double ShareOfRangeBeaten(double heroTopPercent, double villainRangeWidth)
    {
        if (villainRangeWidth <= 0)
            return 0;
        return Math.Clamp((villainRangeWidth - heroTopPercent) / villainRangeWidth, 0, 1);
    }

    public static double Equity(double shareOfRangeBeaten) =>
        MinEquity + (MaxEquity - MinEquity) * Math.Clamp(shareOfRangeBeaten, 0, 1);
}
