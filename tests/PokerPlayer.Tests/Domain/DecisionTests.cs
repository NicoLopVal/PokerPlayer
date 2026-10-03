using PokerPlayer.Domain.Decisions;
using PokerPlayer.Domain.Enums;
using PokerPlayer.Domain.History;
using PokerPlayer.Domain.Kpis.Calculators;
using PokerPlayer.Domain.Players;
using PokerPlayer.Domain.Preflop;
using static PokerPlayer.Tests.TestData;

namespace PokerPlayer.Tests.Domain;

public class DecisionTests
{
    private static readonly KpiCalculator Kpis = new(new KpiCalculatorOptions(Iterations: 4_000, Seed: 42));

    private static ActionPool Pool(PokerPlayer.Domain.GameState state, DecisionOptions? options = null) =>
        new ActionPoolCalculator(options ?? new DecisionOptions()).Calculate(state, Kpis.Calculate(state));

    // Legality

    [Fact]
    public void FacingBet_CheckIsForbidden_AndProbabilitiesSumToOne()
    {
        var pool = Pool(State(board: "7C JH 2D", pot: 20, betToCall: 10));

        Assert.Equal(4, pool.Options.Count);
        Assert.Equal(0, pool[PlayerAction.CHECK].Probability);
        Assert.False(pool[PlayerAction.CHECK].IsAllowed);
        Assert.True(pool[PlayerAction.FOLD].IsAllowed);
        Assert.True(pool[PlayerAction.CALL].IsAllowed);
        Assert.Equal(1.0, pool.Options.Sum(option => option.Probability), 9);
    }

    [Fact]
    public void NothingToCall_FoldAndCallAreForbidden()
    {
        var pool = Pool(State(board: "7C JH 2D", pot: 20, betToCall: 0));

        Assert.Equal(0, pool[PlayerAction.FOLD].Probability);
        Assert.Equal(0, pool[PlayerAction.CALL].Probability);
        Assert.True(pool[PlayerAction.CHECK].IsAllowed);
        Assert.Equal(1.0, pool.Options.Sum(option => option.Probability), 9);
    }

    [Fact]
    public void NoChipsBeyondTheCall_RaiseIsForbidden_CallStillAllowed()
    {
        var pool = Pool(State(board: "7C JH 2D", pot: 40, betToCall: 20, heroStack: 15));

        Assert.Equal(0, pool[PlayerAction.RAISE].Probability);
        Assert.True(pool[PlayerAction.CALL].IsAllowed);
    }

    [Fact]
    public void SuggestedRaise_IsAReferenceSizeWithinHeroStack()
    {
        var state = State(board: "7C JH 2D", pot: 30, betToCall: 5, heroStack: 20);
        var kpis = Kpis.Calculate(state);

        var raise = new ActionPoolCalculator().Calculate(state, kpis)[PlayerAction.RAISE];

        Assert.Contains(raise.SuggestedRaise!.Value, kpis.ReferenceBets.Select(bet => bet.Amount));
        Assert.True(5 + raise.SuggestedRaise <= 20);
    }

    // Behaviour

    [Fact]
    public void Nuts_OnTheRiver_PrefersRaisingOverChecking()
    {
        var pool = Pool(State(hand: "AH KH", board: "QH JH TH 2C 3D", pot: 20, betToCall: 0, heroStack: 100));

        Assert.Equal(PlayerAction.RAISE, pool.Best.Action);
    }

    [Fact]
    public void Trash_FacingABigRiverBet_PrefersFolding()
    {
        var pool = Pool(State(hand: "7C 2D", board: "AH KS QH 9S 4C", pot: 60, betToCall: 30, heroStack: 100));

        Assert.Equal(PlayerAction.FOLD, pool.Best.Action);
    }

    [Fact]
    public void LowerTemperature_ConcentratesOnTheBestAction()
    {
        var state = State(board: "7C JH 2D", pot: 20, betToCall: 10);

        var warm = Pool(state, new DecisionOptions(Temperature: 0.5));
        var cold = Pool(state, new DecisionOptions(Temperature: 0.02));

        Assert.Equal(warm.Best.Action, cold.Best.Action);
        Assert.True(cold.Best.Probability > warm.Best.Probability);
    }

    // Preflop equity heuristic

    [Fact]
    public void PreflopEquity_IsMonotonicAndBounded()
    {
        var aces = PreflopEquityModel.Equity(PreflopEquityModel.ShareOfRangeBeaten(0.0045, 1));
        var trash = PreflopEquityModel.Equity(PreflopEquityModel.ShareOfRangeBeaten(0.99, 1));
        var outsideNarrowRange = PreflopEquityModel.Equity(PreflopEquityModel.ShareOfRangeBeaten(0.30, 0.05));

        Assert.InRange(aces, 0.79, PreflopEquityModel.MaxEquity);
        Assert.InRange(trash, PreflopEquityModel.MinEquity, 0.26);
        Assert.Equal(PreflopEquityModel.MinEquity, outsideNarrowRange, 6);
    }

    // Fold probability

    private static readonly DecodedHistory VillainRaisedPreflop = ActionHistoryDecoder.Decode(["bet", "raise", "call", "raise"]);
    private static readonly DecodedHistory VillainCalledPreflop = ActionHistoryDecoder.Decode(["bet", "raise", "raise", "call"]);

    [Fact]
    public void Fold_WithoutStats_IsTheGameTheoryBaseline()
    {
        var villain = new Player("Villain", 100, Blind.BB);

        var fold = FoldProbabilityEstimator.Estimate(villain, VillainCalledPreflop, Street.Flop, pot: 10, risk: 10);

        Assert.Equal(0.5, fold, 6); // B / (P + B)
    }

    [Fact]
    public void Fold_LooseAggressiveVillainFoldsLess_TightPassiveMore()
    {
        var loose = new Player("Villain", 100, Blind.BB, stats: new PlayerStats(0.50, 0.30, 0.10, 4));
        var tight = new Player("Villain", 100, Blind.BB, stats: new PlayerStats(0.12, 0.10, 0.04, 1));

        var looseFold = FoldProbabilityEstimator.Estimate(loose, VillainCalledPreflop, Street.Flop, 10, 10);
        var tightFold = FoldProbabilityEstimator.Estimate(tight, VillainCalledPreflop, Street.Flop, 10, 10);

        Assert.True(looseFold < 0.5);
        Assert.True(tightFold > 0.5);
        Assert.InRange(looseFold, 0.5 * 0.6, 0.5 * 1.4);
    }

    [Fact]
    public void Fold_MeasuredStatsOverrideTheEstimate()
    {
        var stats = new PlayerStats(0.24, 0.20, 0.08, 2.5, foldToThreeBet: 0.65, foldToCBet: 0.30);
        var villain = new Player("Villain", 100, Blind.BB, stats: stats);

        Assert.Equal(0.30, FoldProbabilityEstimator.Estimate(villain, VillainCalledPreflop, Street.Turn, 10, 10), 6);
        Assert.Equal(0.65, FoldProbabilityEstimator.Estimate(villain, VillainRaisedPreflop, Street.Preflop, 10, 10), 6);
        // Villain has not raised, so Hero's raise is not a 3-bet: the override does not apply.
        Assert.NotEqual(0.65, FoldProbabilityEstimator.Estimate(villain, VillainCalledPreflop, Street.Preflop, 10, 10));
    }
}
