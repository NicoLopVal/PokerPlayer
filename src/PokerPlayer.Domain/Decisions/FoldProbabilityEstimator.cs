using PokerPlayer.Domain.Enums;
using PokerPlayer.Domain.History;
using PokerPlayer.Domain.Kpis.Calculators;
using PokerPlayer.Domain.Players;

namespace PokerPlayer.Domain.Decisions;

/// <summary>
/// Estimates how often the villain folds to a Hero bet/raise (f).
/// 1. A measured stat wins when present: FoldToThreeBet when Hero re-raises a villain preflop raise,
///    FoldToCBet for bets after the flop.
/// 2. Otherwise start from the game-theory baseline: a villain who defends exactly the minimum
///    defense frequency folds risk / (pot + risk), which makes a pure bluff break even.
/// 3. Shift that baseline by the villain's style compared to an average player: loose players (high VPIP)
///    and aggressive players (high AF) fold less. The shift is capped at ±40%.
/// Without stats the villain is treated as <see cref="PlayerStats.Average"/>.
/// </summary>
public static class FoldProbabilityEstimator
{
    public const double MaxFoldProbability = 0.95;
    public const double MaxStyleShift = 0.4;

    /// <summary>Fold-rate change per point of VPIP above average (e.g. +10% VPIP → 8% fewer folds).</summary>
    public const double LoosenessWeight = 0.8;

    /// <summary>Fold-rate change per unit of AF above average.</summary>
    public const double AggressionWeight = 0.1;

    public static double Estimate(Player villain, DecodedHistory history, Street street, int pot, int risk)
    {
        var stats = villain.Stats ?? PlayerStats.Average;

        if (street == Street.Preflop)
        {
            var villainRaised = history.VoluntaryActions(villain.Blind, Street.Preflop)
                .Any(action => action.Action == PlayerAction.RAISE);
            if (villainRaised && stats.FoldToThreeBet is { } foldToThreeBet)
                return Math.Min(foldToThreeBet, MaxFoldProbability);
        }
        else if (stats.FoldToCBet is { } foldToCBet)
        {
            return Math.Min(foldToCBet, MaxFoldProbability);
        }

        var baseline = PokerMath.BluffBreakEven(pot, risk);
        var average = PlayerStats.Average;
        var style = 1
            - LoosenessWeight * (stats.Vpip - average.Vpip)
            - AggressionWeight * (stats.Af - average.Af);

        return Math.Clamp(baseline * Math.Clamp(style, 1 - MaxStyleShift, 1 + MaxStyleShift), 0, MaxFoldProbability);
    }
}
