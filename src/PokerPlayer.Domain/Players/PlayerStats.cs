using PokerPlayer.Domain.Exceptions;

namespace PokerPlayer.Domain.Players;

/// <summary>Long-run tendencies of a player, collected from previous hands.</summary>
public sealed record PlayerStats
{
    /// <summary>Typical stats of a regular player, used when no stats are provided.</summary>
    public static PlayerStats Average { get; } = new(vpip: 0.25, pfr: 0.18, threeBet: 0.07, af: 2.0);

    /// <summary>Voluntarily put money in pot: share of hands the player plays (0..1).</summary>
    public double Vpip { get; }

    /// <summary>Preflop raise: share of hands the player raises preflop (0..1).</summary>
    public double Pfr { get; }

    /// <summary>Share of 3-bet opportunities the player re-raises (0..1).</summary>
    public double ThreeBet { get; }

    /// <summary>Aggression factor: (bets + raises) / calls. Zero or greater.</summary>
    public double Af { get; }

    /// <summary>Share of times the player folds to a 3-bet (0..1). Optional; overrides the preflop fold estimate.</summary>
    public double? FoldToThreeBet { get; }

    /// <summary>Share of times the player folds to a continuation bet (0..1). Optional; overrides the postflop fold estimate.</summary>
    public double? FoldToCBet { get; }

    public PlayerStats(double vpip, double pfr, double threeBet, double af, double? foldToThreeBet = null, double? foldToCBet = null)
    {
        Vpip = EnsureFraction(vpip, nameof(Vpip));
        Pfr = EnsureFraction(pfr, nameof(Pfr));
        ThreeBet = EnsureFraction(threeBet, nameof(ThreeBet));
        // Every preflop raise is also voluntary money in the pot, so raising more often than playing is impossible.
        if (pfr > vpip)
            throw new InvalidInputException(ErrorCodes.InvalidStats,
                FormattableString.Invariant($"Invalid player stats. PFR ({pfr}) cannot be greater than VPIP ({vpip}), every preflop raise also counts as voluntarily playing the hand."));
        if (!double.IsFinite(af) || af < 0)
            throw new InvalidInputException(ErrorCodes.InvalidStats, FormattableString.Invariant($"Invalid player stats. {af} is not a valid AF, it cannot be negative."));
        Af = af;
        FoldToThreeBet = foldToThreeBet is { } foldTo3Bet ? EnsureFraction(foldTo3Bet, nameof(FoldToThreeBet)) : null;
        FoldToCBet = foldToCBet is { } foldToCBetValue ? EnsureFraction(foldToCBetValue, nameof(FoldToCBet)) : null;
    }

    private static double EnsureFraction(double value, string name) =>
        value is >= 0 and <= 1 ? value : throw new InvalidInputException(ErrorCodes.InvalidStats, FormattableString.Invariant($"Invalid player stats. {value} is not a valid {name}, it must be between 0 and 1."));
}
