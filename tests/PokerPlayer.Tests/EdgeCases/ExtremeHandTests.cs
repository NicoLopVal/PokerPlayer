using PokerPlayer.Domain.Cards;
using PokerPlayer.Domain.Decisions;
using PokerPlayer.Domain.Enums;
using PokerPlayer.Domain.Evaluation;
using PokerPlayer.Domain.History;
using PokerPlayer.Domain.Kpis;
using PokerPlayer.Domain.Kpis.Calculators;
using PokerPlayer.Domain.Players;
using PokerPlayer.Domain.Preflop;
using static PokerPlayer.Tests.TestData;

namespace PokerPlayer.Tests.EdgeCases;

/// <summary>Best and worst possible situations: the numbers must sit at their sensible extremes.</summary>
public class ExtremeHandTests
{
    private static readonly KpiCalculator Kpis = new(new KpiCalculatorOptions(Iterations: 4_000, Seed: 9));

    private static ActionPool Pool(PokerPlayer.Domain.GameState state, DecisionOptions? options = null) =>
        new ActionPoolCalculator(options ?? new DecisionOptions()).Calculate(state, Kpis.Calculate(state));

    // Preflop: strongest and weakest starting hands

    [Fact]
    public void Aces_AreTheStrongestStartingHand()
    {
        var aces = new StartingHand(Rank.Ace, Rank.Ace, false);

        Assert.Equal(ChenFormula.MaxScore, ChenFormula.Score(aces));
        Assert.Equal(aces, StartingHandRanking.Ordered[0]);
        Assert.All(StartingHandRanking.Ordered, hand => Assert.True(StartingHandRanking.TopPercent(aces) <= StartingHandRanking.TopPercent(hand)));
    }

    [Fact]
    public void ChenScores_StayWithinPublishedBounds()
    {
        var scores = StartingHandRanking.Ordered.Select(ChenFormula.Score).ToList();

        Assert.Equal(ChenFormula.MaxScore, scores.Max());
        Assert.Equal(ChenFormula.MinScore, scores.Min());
    }

    [Fact]
    public void PreflopEquity_IsHighestForAces_LowestForTheWorstHand()
    {
        var aces = Assert.IsType<PreflopKpis>(Kpis.Calculate(State(hand: "AH AD")));
        var worst = StartingHandRanking.Ordered[^1];
        var worstHand = $"{new Card(worst.High, Suit.Clubs)} {new Card(worst.Low, Suit.Diamonds)}";
        var trash = Assert.IsType<PreflopKpis>(Kpis.Calculate(State(hand: worstHand)));

        Assert.InRange(aces.Equity.EstimatedEquity, 0.79, PreflopEquityModel.MaxEquity);
        Assert.Equal(PreflopEquityModel.MinEquity, trash.Equity.EstimatedEquity, 6);
    }

    [Fact]
    public void Aces_FacingTheBigBlind_NearlyNeverFold()
    {
        var pool = Pool(State(hand: "AH AD", pot: 3, betToCall: 1));

        Assert.True(pool[PlayerAction.FOLD].Probability < 0.001);
    }

    // Evaluator: best and worst five-card hands

    [Fact]
    public void RoyalFlush_BeatsEverything_SevenFiveHigh_LosesToEverything()
    {
        var royal = HandEvaluator.Evaluate(Cards("AS KS QS JS TS"));
        var worst = HandEvaluator.Evaluate(Cards("7C 5D 4H 3S 2C"));
        var random = new Random(4);

        for (var i = 0; i < 2_000; i++)
        {
            var hand = HandEvaluator.Evaluate(Deck.All.OrderBy(_ => random.Next()).Take(5).ToArray());
            Assert.True(royal >= hand);
            Assert.True(worst <= hand);
        }
    }

    // River: certain win, certain tie, certain loss

    [Fact]
    public void Nuts_OnTheRiver_EquityOne_RaisesAndNeverFolds()
    {
        var checkSpot = Pool(State(hand: "AH KH", board: "QH JH TH 2C 3D", pot: 20, betToCall: 0, heroStack: 100));
        var betSpot = Pool(State(hand: "AH KH", board: "QH JH TH 2C 3D", pot: 40, betToCall: 20, heroStack: 100));

        Assert.Equal(PlayerAction.RAISE, checkSpot.Best.Action);
        Assert.Equal(PlayerAction.RAISE, betSpot.Best.Action);
        Assert.True(betSpot[PlayerAction.FOLD].Probability < 0.001);
    }

    [Fact]
    public void BoardPlays_EveryoneTies_EquityIsExactlyHalf()
    {
        var kpis = Assert.IsType<RiverKpis>(Kpis.Calculate(State(hand: "2C 3D", board: "AS KS QS JS TS", betToCall: 0)));

        Assert.Equal(0.5, kpis.Equity.Equity, 9);
        Assert.Equal(0.5, kpis.Equity.HandStrength, 9);
    }

    [Fact]
    public void DrawingDead_OnTheRiver_CallIsTheLeastLikelyAllowedAction()
    {
        // Ten-high against a river where any pair wins: equity near zero, facing a pot-sized bet.
        var pool = Pool(State(hand: "3C 2D", board: "AH KS QD 8S 7C", pot: 60, betToCall: 30, heroStack: 100));

        Assert.True(pool[PlayerAction.CALL].Probability < 0.02);
        Assert.True(pool[PlayerAction.FOLD].Probability > pool[PlayerAction.CALL].Probability);
    }

    // Draws: most and fewest outs

    [Fact]
    public void ComboDraw_HasFifteenOuts_AndAboutFiftyFourPercentByRiver()
    {
        // 6♥5♥ on 7♥ 8♣ K♥: flush draw (9) + open-ended straight draw (4s and 9s not already counted, 6).
        var kpis = Assert.IsType<FlopKpis>(Kpis.Calculate(State(hand: "6H 5H", board: "7H 8C KH", betToCall: 0)));

        Assert.Equal(15, kpis.Draws.Outs);
        Assert.Equal(0.541, kpis.Draws.HitByRiver!.Value, 3);
    }

    [Fact]
    public void NutFlush_HasNoOuts_AndOddsAgainstIsUndefined()
    {
        var kpis = Assert.IsType<FlopKpis>(Kpis.Calculate(State(hand: "AH KH", board: "QH 7H 2H", betToCall: 0)));

        Assert.Equal(0, kpis.Draws.Outs);
        Assert.Null(kpis.Draws.OddsAgainst);
        Assert.Equal(0, kpis.Draws.HitByRiver);
    }

    // Stacks: all-in and short stack

    [Fact]
    public void CallingPutsHeroAllIn_RaiseIsForbidden()
    {
        var pool = Pool(State(board: "7C JD 2S", pot: 40, betToCall: 20, heroStack: 20));

        Assert.Equal(0, pool[PlayerAction.RAISE].Probability);
        Assert.Null(pool[PlayerAction.RAISE].SuggestedRaise);
        Assert.True(pool[PlayerAction.CALL].IsAllowed);
    }

    [Fact]
    public void HeroWithNoChips_CanOnlyCheckOrCallForNothing()
    {
        var checkPool = Pool(State(board: "7C JD 2S", pot: 40, betToCall: 0, heroStack: 0));

        Assert.Equal(1.0, checkPool[PlayerAction.CHECK].Probability, 9);
    }

    [Fact]
    public void ShortStack_RaiseIsCappedAtWhatHeroHas()
    {
        var pool = Pool(State(hand: "AH AD", board: "AC 7D 2S", pot: 300, betToCall: 10, heroStack: 25, villainStack: 500));

        Assert.Equal(15, pool[PlayerAction.RAISE].SuggestedRaise);
    }

    // Probabilities: softmax extremes

    [Fact]
    public void TinyTemperature_PicksTheBestActionAlmostSurely()
    {
        var pool = Pool(State(board: "7C JD 2S", pot: 20, betToCall: 10), new DecisionOptions(Temperature: 0.001));

        Assert.True(pool.Best.Probability > 0.999);
    }

    [Fact]
    public void HugeTemperature_SpreadsEvenlyOverAllowedActions()
    {
        var pool = Pool(State(board: "7C JD 2S", pot: 20, betToCall: 10), new DecisionOptions(Temperature: 1_000_000));

        Assert.Equal(0, pool[PlayerAction.CHECK].Probability);
        Assert.All(new[] { PlayerAction.FOLD, PlayerAction.CALL, PlayerAction.RAISE },
            action => Assert.Equal(1 / 3.0, pool[action].Probability, 3));
    }

    // Fold probability extremes

    [Fact]
    public void FoldProbability_IsCappedEvenAgainstTheTightestPlayer()
    {
        var rock = new Player("Villain", 100, Blind.BB, stats: new PlayerStats(0, 0, 0, 0, foldToCBet: 1));
        var history = ActionHistoryDecoder.Decode(["bet", "raise", "raise", "call"]);

        Assert.Equal(FoldProbabilityEstimator.MaxFoldProbability, FoldProbabilityEstimator.Estimate(rock, history, Street.Flop, 10, 1_000));
    }

    [Fact]
    public void FoldProbability_StyleShiftIsBounded()
    {
        var maniac = new Player("Villain", 100, Blind.BB, stats: new PlayerStats(1, 1, 1, 50));
        var rock = new Player("Villain", 100, Blind.BB, stats: new PlayerStats(0, 0, 0, 0));
        var history = ActionHistoryDecoder.Decode(["bet", "raise", "raise", "call"]);
        var baseline = PokerMath.BluffBreakEven(10, 10);

        Assert.Equal(baseline * (1 - FoldProbabilityEstimator.MaxStyleShift), FoldProbabilityEstimator.Estimate(maniac, history, Street.Flop, 10, 10), 9);
        Assert.Equal(baseline * (1 + FoldProbabilityEstimator.MaxStyleShift), FoldProbabilityEstimator.Estimate(rock, history, Street.Flop, 10, 10), 9);
    }
}
