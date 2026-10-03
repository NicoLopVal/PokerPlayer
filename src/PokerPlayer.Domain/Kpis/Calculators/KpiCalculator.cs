using PokerPlayer.Domain.Enums;
using PokerPlayer.Domain.History;
using PokerPlayer.Domain.Preflop;
using PokerPlayer.Domain.Ranges;

namespace PokerPlayer.Domain.Kpis.Calculators;

/// <param name="Iterations">Monte Carlo runouts per postflop estimate.</param>
/// <param name="Seed">Fixed seed for reproducible results; null for a random seed.</param>
public sealed record KpiCalculatorOptions(int Iterations = 10_000, int? Seed = null);

/// <summary>
/// Builds the indicators relevant to the current street. Heads-up for now: the villain is the
/// only non-Hero player. The street is taken from <see cref="GameState.Street"/>; checking it
/// against the board is the validator's job.
/// </summary>
public sealed class KpiCalculator(KpiCalculatorOptions options)
{
    /// <summary>Bets are always a share of the pot, rounded to whole chips.</summary>
    public static readonly (string Label, double PotFraction)[] ReferenceSizes = [("1/3 pot", 1 / 3.0), ("2/3 pot", 2 / 3.0), ("1 pot", 1)];

    private readonly EquityEstimator _equityEstimator = new(CreateRandom(options));

    private static Random CreateRandom(KpiCalculatorOptions options)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(options.Iterations, 1, nameof(options.Iterations));
        return options.Seed is { } seed ? new Random(seed) : new Random();
    }

    public KpiCalculator() : this(new KpiCalculatorOptions()) { }

    public StreetKpis Calculate(GameState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        return state.Street switch
        {
            Street.Preflop => Preflop(state),
            Street.Flop => Flop(state),
            Street.Turn => Turn(state),
            Street.River => River(state),
            _ => throw new ArgumentOutOfRangeException(nameof(state), state.Street, "Unknown street.")
        };
    }

    private PreflopKpis Preflop(GameState state)
    {
        var stack = Stack(state);
        var startingHand = StartingHand.From(state.Hand);
        var topPercent = StartingHandRanking.TopPercent(startingHand);
        var rangeWidth = VillainRangeWidth(state);
        var shareBeaten = PreflopEquityModel.ShareOfRangeBeaten(topPercent, rangeWidth);
        var equity = PreflopEquityModel.Equity(shareBeaten);
        return new PreflopKpis
        {
            Street = Street.Preflop,
            UnseenCards = UnseenCards(state),
            Stack = stack,
            FacingBet = FacingBet(state),
            ReferenceBets = ReferenceBets(state, stack.EffectiveStack, equity),
            HandStrength = new PreflopHandStrengthKpis(
                startingHand.ToString(),
                ChenFormula.Score(startingHand),
                topPercent),
            Equity = new PreflopEquityKpis(rangeWidth, shareBeaten, equity, CallExpectedValue(state, equity)),
            GeometricBetSize = PokerMath.GeometricBetSize(state.PotSize, stack.EffectiveStack, StreetsLeft(Street.Preflop))
        };
    }

    private FlopKpis Flop(GameState state)
    {
        var stack = Stack(state);
        var equity = Equity(state);
        return new FlopKpis
        {
            Street = Street.Flop,
            UnseenCards = UnseenCards(state),
            Stack = stack,
            FacingBet = FacingBet(state),
            ReferenceBets = ReferenceBets(state, stack.EffectiveStack, equity.Equity),
            Draws = Draws(state, cardsToCome: 2),
            Equity = equity,
            GeometricBetSize = PokerMath.GeometricBetSize(state.PotSize, stack.EffectiveStack, StreetsLeft(Street.Flop))
        };
    }

    private TurnKpis Turn(GameState state)
    {
        var stack = Stack(state);
        var equity = Equity(state);
        return new TurnKpis
        {
            Street = Street.Turn,
            UnseenCards = UnseenCards(state),
            Stack = stack,
            FacingBet = FacingBet(state),
            ReferenceBets = ReferenceBets(state, stack.EffectiveStack, equity.Equity),
            Draws = Draws(state, cardsToCome: 1),
            Equity = equity,
            GeometricBetSize = PokerMath.GeometricBetSize(state.PotSize, stack.EffectiveStack, StreetsLeft(Street.Turn))
        };
    }

    private RiverKpis River(GameState state)
    {
        var stack = Stack(state);
        var equity = Equity(state);
        return new RiverKpis
        {
            Street = Street.River,
            UnseenCards = UnseenCards(state),
            Stack = stack,
            FacingBet = FacingBet(state),
            ReferenceBets = ReferenceBets(state, stack.EffectiveStack, equity.Equity),
            Equity = equity
        };
    }

    private static int UnseenCards(GameState state) => PokerMath.UnseenCards(state.KnownCards.Count);

    private static int StreetsLeft(Street street) => Street.River - street + 1;

    private static StackKpis Stack(GameState state)
    {
        var hero = state.Hero;
        var effective = Math.Min(hero.Stack, state.Villains.Max(villain => villain.Stack));
        return new StackKpis(
            effective,
            PokerMath.StackInBigBlinds(effective, state.BigBlind),
            PokerMath.MRatio(effective, state.SmallBlind, state.BigBlind),
            PokerMath.StackToPotRatio(effective, state.PotSize),
            hero.Invested is { } invested ? PokerMath.PotCommitment(invested, hero.Stack) : null);
    }

    private static FacingBetKpis? FacingBet(GameState state)
    {
        if (state.BetToCall == 0)
            return null;

        var call = state.BetToCall;
        var pot = state.PotSize;
        // Heads-up, the bet Hero faces is approximately what Hero has to call.
        var potBeforeBet = Math.Max(0, pot - call);
        return new FacingBetKpis(
            call,
            PokerMath.RequiredEquity(call, pot),
            PokerMath.PotOddsRatio(call, pot),
            PokerMath.MinimumDefenseFrequency(potBeforeBet, call));
    }

    /// <summary>
    /// Three reference sizes as a share of the pot (see <see cref="ReferenceSizes"/>). Each amount is added on top of
    /// any call, is at least 1 chip, and is capped so Hero never risks more than the effective stack.
    /// </summary>
    private static IReadOnlyList<ReferenceBetKpis> ReferenceBets(GameState state, int effectiveStack, double equity)
    {
        var pot = state.PotSize;
        var call = state.BetToCall;
        // Effective stack never exceeds Hero's stack, so call + raise can never be more than Hero owns.
        var maxRaise = effectiveStack - call;
        if (maxRaise < 1)
            return [];

        return ReferenceSizes
            .Select(size => (size.Label, Amount: Math.Clamp((int)Math.Round(size.PotFraction * pot), 1, maxRaise)))
            .DistinctBy(size => size.Amount)
            .Select(size =>
            {
                var risk = call + size.Amount;
                var whenCalled = ExpectedValue.WhenCalled(equity, pot, risk, size.Amount);
                return new ReferenceBetKpis(
                    size.Label,
                    size.Amount,
                    PokerMath.BluffBreakEven(pot, risk),
                    PokerMath.OptimalBluffShare(pot + call, size.Amount),
                    whenCalled,
                    ExpectedValue.BreakEvenFoldFrequency(pot, whenCalled));
            })
            .ToList();
    }

    private static DrawKpis Draws(GameState state, int cardsToCome)
    {
        var outs = OutsCounter.Count(state.Hand, state.CommunityCards.Revealed);
        var unseen = UnseenCards(state);
        return new DrawKpis(
            outs,
            PokerMath.HitNextCard(outs, unseen),
            cardsToCome >= 2 ? PokerMath.HitWithinTwoCards(outs, unseen) : null,
            PokerMath.RuleOfTwoAndFour(outs, cardsToCome),
            PokerMath.OddsAgainst(outs, unseen));
    }

    private EquityKpis Equity(GameState state)
    {
        var range = HandRange.FromTopPercent(VillainRangeWidth(state), state.KnownCards.ToArray());

        var estimate = _equityEstimator.Estimate(state.Hand, state.CommunityCards.Revealed, range, options.Iterations);
        return new EquityKpis(
            estimate.CurrentHand.ToString(),
            estimate.Equity,
            estimate.HandStrength,
            estimate.PositivePotential,
            estimate.NegativePotential,
            estimate.EffectiveHandStrength,
            range.Width,
            estimate.Samples,
            CallExpectedValue(state, estimate.Equity));
    }

    private static double? CallExpectedValue(GameState state, double equity) =>
        state.BetToCall > 0 ? ExpectedValue.Call(equity, state.PotSize, state.BetToCall) : null;

    private static double VillainRangeWidth(GameState state) =>
        VillainRangeEstimator.EstimateWidth(state.Villains.First(), ActionHistoryDecoder.Decode(state.ActionHistory));
}
