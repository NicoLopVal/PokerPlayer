using PokerPlayer.Domain.Enums;
using PokerPlayer.Domain.History;
using PokerPlayer.Domain.Players;

namespace PokerPlayer.Domain.Ranges;

/// <summary>
/// Rough estimate of how wide the villain's range is, from their preflop actions and stats:
/// - re-raised Hero's raise (3-bet)  → top ThreeBet %
/// - raised first                    → top PFR %
/// - called / limped                 → top VPIP %
/// - no voluntary chips (e.g. BB checked its option) → any two cards
/// Without stats the villain is treated as an average player.
/// Postflop narrowing is intentionally left out for now.
/// </summary>
public static class VillainRangeEstimator
{
    /// <summary>Smallest width used, so a tiny stat never produces an empty or single-hand range.</summary>
    public const double MinimumWidth = 0.02;

    public static double EstimateWidth(Player villain, DecodedHistory history)
    {
        var stats = villain.Stats ?? PlayerStats.Average;

        var preflop = history.Actions.Where(action => action.Street == Street.Preflop && !action.IsBlindPost).ToList();
        var villainActions = preflop.Where(action => action.Actor == villain.Blind).ToList();

        var villainRaised = false;
        var reRaised = false;
        var heroRaisedBefore = false;
        foreach (var action in preflop)
        {
            if (action.Action != PlayerAction.RAISE)
                continue;
            if (action.Actor == villain.Blind)
            {
                villainRaised = true;
                reRaised |= heroRaisedBefore;
            }
            else
            {
                heroRaisedBefore = true;
            }
        }

        double width;
        if (reRaised)
            width = stats.ThreeBet;
        else if (villainRaised)
            width = stats.Pfr;
        else if (villainActions.Any(action => action.Action == PlayerAction.CALL))
            width = stats.Vpip;
        else
            width = 1;

        return Math.Clamp(width, MinimumWidth, 1);
    }
}
