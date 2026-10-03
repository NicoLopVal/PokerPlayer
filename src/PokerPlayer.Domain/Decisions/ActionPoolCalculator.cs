using PokerPlayer.Domain.Enums;
using PokerPlayer.Domain.History;
using PokerPlayer.Domain.Kpis;
using PokerPlayer.Domain.Kpis.Calculators;
using PokerPlayer.Domain.Preflop;

namespace PokerPlayer.Domain.Decisions;

/// <param name="Temperature">
/// Softmax temperature, in pot units. Lower = almost always the best action; higher = more mixing.
/// With 0.1, an action worth 10% of the pot more than another is picked ~2.7x as often.
/// </param>
/// <param name="OutOfPositionRealization">Share of equity Hero realizes when acting first after the flop (EQR).</param>
/// <param name="CommitmentSpr">Below this stack-to-pot ratio, strong hands are pushed toward raising.</param>
/// <param name="CommitmentStrength">Minimum hand strength (EHS postflop, equity preflop) for the commitment push.</param>
/// <param name="CommitmentBonus">Bonus added to the raise EV when committed, in pot units.</param>
/// <param name="SemiBluffWeight">Bonus added to the raise EV per unit of positive potential, in pot units (flop and turn).</param>
public sealed record DecisionOptions(
    double Temperature = 0.1,
    double OutOfPositionRealization = 0.9,
    double CommitmentSpr = 2,
    double CommitmentStrength = 0.6,
    double CommitmentBonus = 0.1,
    double SemiBluffWeight = 0.1);

/// <summary>
/// Turns the indicators into a pool of actions with selection probabilities:
/// 1. Decide which actions are allowed. CHECK only with nothing to call; FOLD and CALL only when facing a bet;
///    RAISE only when Hero has chips left after calling (sizes are capped by the stack).
/// 2. Compute each allowed action's EV in chips, relative to folding now:
///    FOLD 0 · CHECK eq·EQR·P · CALL eq·EQR·(P + C) - C ·
///    RAISE f·P + (1 - f)·[eq_call·(P + risk + B) - risk], best of the reference sizes, where eq_call is
///    Hero's equity against the hands that call (the villain folds their weakest hands first).
/// 3. Adjust the raise EV with markers: commitment at low SPR, semi-bluff potential.
/// 4. Softmax over EV / pot. Disallowed actions get probability 0.
/// </summary>
public sealed class ActionPoolCalculator
{
    private readonly DecisionOptions _options;

    public ActionPoolCalculator() : this(new DecisionOptions()) { }

    public ActionPoolCalculator(DecisionOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (!double.IsFinite(options.Temperature) || options.Temperature <= 0)
            throw new ArgumentOutOfRangeException(nameof(options), options.Temperature, "Temperature must be a positive number.");
        _options = options;
    }

    public ActionPool Calculate(GameState state, StreetKpis kpis)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(kpis);

        var pot = state.PotSize;
        var equity = HeroEquity(kpis);
        var realization = EquityRealization(state);
        var candidates = new Dictionary<PlayerAction, (double Ev, int? Raise)>();

        if (state.BetToCall > 0)
        {
            var call = Math.Min(state.BetToCall, state.Hero.Stack); // short stack calls all-in for less
            candidates[PlayerAction.FOLD] = (ExpectedValue.Fold, null);
            candidates[PlayerAction.CALL] = (ExpectedValue.Call(equity * realization, pot, call), null);
        }
        else
        {
            candidates[PlayerAction.CHECK] = (equity * realization * pot, null);
        }

        if (BestRaise(state, kpis, equity) is { } raise)
            candidates[PlayerAction.RAISE] = (raise.Ev, raise.Amount);

        var probabilities = Softmax(candidates.ToDictionary(pair => pair.Key, pair => pair.Value.Ev), Math.Max(pot, state.BigBlind));

        return new ActionPool(Enum.GetValues<PlayerAction>()
            .Select(action => candidates.TryGetValue(action, out var candidate)
                ? new ActionOption(action, probabilities[action], candidate.Ev, candidate.Raise)
                : new ActionOption(action, 0, null, null))
            .ToList());
    }

    private (double Ev, int Amount)? BestRaise(GameState state, StreetKpis kpis, double equity)
    {
        if (kpis.ReferenceBets.Count == 0)
            return null;

        var pot = state.PotSize;
        var villain = state.Villains.First();
        var history = ActionHistoryDecoder.Decode(state.ActionHistory);
        var bonus = MarkerBonus(kpis, equity) * pot;

        return kpis.ReferenceBets
            .Select(bet =>
            {
                var risk = state.BetToCall + bet.Amount;
                var fold = FoldProbabilityEstimator.Estimate(villain, history, state.Street, pot, risk);
                var equityWhenCalled = EquityWhenCalled(kpis, equity, fold);
                var whenCalled = ExpectedValue.WhenCalled(equityWhenCalled, pot, risk, bet.Amount);
                return (Ev: ExpectedValue.BetOrRaise(fold, pot, whenCalled) + bonus, bet.Amount);
            })
            .MaxBy(raise => raise.Ev);
    }

    /// <summary>
    /// Hero's equity against the part of the range that calls, assuming the villain folds their weakest
    /// hands first. Postflop Hero beats roughly the bottom `eq` of the range, so after the bottom `f` folds
    /// the equity is (eq - f) / (1 - f). Preflop the calling range is the top W·(1 - f) of starting hands.
    /// </summary>
    private static double EquityWhenCalled(StreetKpis kpis, double equity, double fold)
    {
        if (kpis is PreflopKpis preflop)
        {
            var callingWidth = preflop.Equity.VillainRangeWidth * (1 - fold);
            var share = PreflopEquityModel.ShareOfRangeBeaten(preflop.HandStrength.TopPercent, callingWidth);
            return PreflopEquityModel.Equity(share);
        }

        return fold >= 1 ? equity : Math.Max(0, equity - fold) / (1 - fold);
    }

    /// <summary>Extra raise EV in pot units from markers the EV formula does not capture.</summary>
    private double MarkerBonus(StreetKpis kpis, double equity)
    {
        var bonus = 0.0;

        var strength = kpis switch
        {
            FlopKpis flop => flop.Equity.EffectiveHandStrength,
            TurnKpis turn => turn.Equity.EffectiveHandStrength,
            _ => equity
        };
        if (kpis.Stack.StackToPotRatio < _options.CommitmentSpr && strength >= _options.CommitmentStrength)
            bonus += _options.CommitmentBonus;

        var positivePotential = kpis switch
        {
            FlopKpis flop => flop.Equity.PositivePotential,
            TurnKpis turn => turn.Equity.PositivePotential,
            _ => null
        };
        bonus += _options.SemiBluffWeight * (positivePotential ?? 0);

        return bonus;
    }

    /// <summary>
    /// Heads-up, the small blind acts last on every street after the flop, so it realizes its full equity.
    /// On the river there is nothing left to realize.
    /// </summary>
    private double EquityRealization(GameState state) =>
        state.Street == Street.River || state.Hero.Blind == Blind.SB ? 1 : _options.OutOfPositionRealization;

    private static double HeroEquity(StreetKpis kpis) => kpis switch
    {
        PreflopKpis preflop => preflop.Equity.EstimatedEquity,
        FlopKpis flop => flop.Equity.Equity,
        TurnKpis turn => turn.Equity.Equity,
        RiverKpis river => river.Equity.Equity,
        _ => throw new ArgumentOutOfRangeException(nameof(kpis), kpis.GetType().Name, "Unknown street indicators.")
    };

    private Dictionary<PlayerAction, double> Softmax(Dictionary<PlayerAction, double> evs, double scale)
    {
        var scores = evs.ToDictionary(pair => pair.Key, pair => pair.Value / scale / _options.Temperature);
        var max = scores.Values.Max();
        var weights = scores.ToDictionary(pair => pair.Key, pair => Math.Exp(pair.Value - max));
        var total = weights.Values.Sum();
        return weights.ToDictionary(pair => pair.Key, pair => pair.Value / total);
    }
}
