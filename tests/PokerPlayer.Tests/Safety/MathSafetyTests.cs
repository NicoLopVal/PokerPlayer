using System.Text.Json;
using PokerPlayer.Domain;
using PokerPlayer.Domain.Cards;
using PokerPlayer.Domain.Decisions;
using PokerPlayer.Domain.Enums;
using PokerPlayer.Domain.History;
using PokerPlayer.Domain.Kpis.Calculators;
using PokerPlayer.Domain.Players;
using PokerPlayer.Domain.Preflop;
using PokerPlayer.Domain.Ranges;
using PokerPlayer.Domain.Validation;
using static PokerPlayer.Tests.TestData;

namespace PokerPlayer.Tests.Safety;

/// <summary>
/// Division by zero, NaN and infinity. Two rules:
/// - shares and probabilities with a zero denominator are 0 (nothing bet, nothing unseen);
/// - undefined ratios and invalid settings throw instead of silently producing NaN/∞.
/// NaN/∞ matter twice: they poison the softmax, and System.Text.Json refuses to serialize them.
/// </summary>
public class MathSafetyTests
{
    [Fact]
    public void Fractions_WithZeroDenominator_AreZero()
    {
        Assert.Equal(0, PokerMath.HitNextCard(0, 0));
        Assert.Equal(0, PokerMath.HitWithinTwoCards(0, 0));
        Assert.Equal(1, PokerMath.HitWithinTwoCards(1, 1));
        Assert.Equal(0, PokerMath.OddsToProbability(0, 0));
        Assert.Equal(0, PokerMath.RequiredEquity(0, 0));
        Assert.Equal(0, PokerMath.BluffBreakEven(0, 0));
        Assert.Equal(0, PokerMath.MinimumDefenseFrequency(0, 0));
        Assert.Equal(0, PokerMath.OptimalBluffShare(0, 0));
        Assert.Equal(0, PokerMath.PotCommitment(0, 0));
    }

    [Fact]
    public void Ratios_WithNothingToDivideBy_AreNullOrDefined()
    {
        Assert.Null(PokerMath.StackToPotRatio(100, 0));
        Assert.Null(PokerMath.OddsAgainst(0, 47));
        Assert.Equal(100, PokerMath.GeometricBetSize(0, 100, 3));   // empty pot: just shove
        Assert.Equal(0, PokerMath.GeometricBetSize(10, 0, 3));      // no stack: nothing to bet
        Assert.Equal(100, PokerMath.GeometricBetSize(10, 100, 0));  // no streets left
    }

    [Fact]
    public void UndefinedRatios_Throw()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => PokerMath.PotOddsRatio(0, 100));
        Assert.Throws<ArgumentOutOfRangeException>(() => PokerMath.StackInBigBlinds(100, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => PokerMath.MRatio(100, 0, 0));
    }

    [Fact]
    public void ExpectedValue_EdgeDenominators()
    {
        Assert.Equal(1, ExpectedValue.BreakEvenFoldFrequency(0, -10));  // empty pot: villain must always fold
        Assert.Equal(0, ExpectedValue.BreakEvenFoldFrequency(0, 0));
        Assert.Equal(0, ExpectedValue.Call(0.5, 0, 0));
    }

    [Fact]
    public void PreflopEquity_WithEmptyRange_DoesNotDivideByZero()
    {
        Assert.Equal(0, PreflopEquityModel.ShareOfRangeBeaten(0.1, 0));
        Assert.Equal(PreflopEquityModel.MinEquity, PreflopEquityModel.Equity(double.NegativeInfinity));
    }

    [Fact]
    public void FoldEstimate_ZeroPot_IsCapped()
    {
        var villain = new Player("Villain", 100, Blind.BB);
        var history = ActionHistoryDecoder.Decode([]);

        var fold = FoldProbabilityEstimator.Estimate(villain, history, Street.Flop, pot: 0, risk: 10);

        Assert.Equal(FoldProbabilityEstimator.MaxFoldProbability, fold);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ZeroIterations_Throws(int iterations)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new KpiCalculator(new KpiCalculatorOptions(iterations)));
        var range = HandRange.FromTopPercent(1, Cards("AH KH 7C JD 2S"));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new EquityEstimator(new Random(1)).Estimate(Hand("AH KH"), Cards("7C JD 2S"), range, iterations));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-0.1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void InvalidTemperature_Throws(double temperature)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ActionPoolCalculator(new DecisionOptions(Temperature: temperature)));
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void NonFiniteStats_AreRejected(double value)
    {
        Assert.Throws<PokerPlayer.Domain.Exceptions.InvalidInputException>(() => new PlayerStats(value, 0.2, 0.1, 2));
        Assert.Throws<PokerPlayer.Domain.Exceptions.InvalidInputException>(() => new PlayerStats(0.2, 0.2, 0.1, value));
    }

    [Fact]
    public void ZeroPotAndEmptyStacks_ProduceFiniteOutput()
    {
        // Hero all-in with nothing behind, empty pot, no history: every denominator is at its worst.
        var state = State(board: "7C JD 2S", pot: 0, betToCall: 0, heroStack: 0, villainStack: 0, history: []);

        AssertFiniteAndValid(state);
    }

    /// <summary>
    /// Randomized check over many valid states: every indicator is a finite number (serializes to JSON),
    /// every share is within [0, 1], and the action pool is a proper probability distribution.
    /// </summary>
    [Fact]
    public void RandomValidStates_ProduceFiniteIndicatorsAndValidPools()
    {
        var random = new Random(2024);
        for (var i = 0; i < 400; i++)
            AssertFiniteAndValid(RandomState(random));
    }

    private static readonly KpiCalculator Kpis = new(new KpiCalculatorOptions(Iterations: 300, Seed: 1));
    private static readonly ActionPoolCalculator Pool = new();

    private static readonly string[] Shares =
    [
        "Equity", "HandStrength", "PositivePotential", "NegativePotential", "EffectiveHandStrength", "EstimatedEquity",
        "ShareOfRangeBeaten", "VillainRangeWidth", "TopPercent", "RequiredEquity", "MinimumDefenseFrequency",
        "FoldEquityNeeded", "OptimalBluffShare", "BreakEvenFoldFrequency", "HitNextCard", "HitByRiver",
        "RuleOfTwoAndFour", "PotCommitment"
    ];

    private static void AssertFiniteAndValid(GameState state)
    {
        GameStateValidator.Validate(state);
        var kpis = Kpis.Calculate(state);

        // Throws on NaN / Infinity.
        var json = JsonSerializer.Serialize(kpis, kpis.GetType());
        foreach (var (name, value) in Numbers(JsonDocument.Parse(json).RootElement))
            if (Shares.Contains(name))
                Assert.True(value is >= 0 and <= 1, $"{name} = {value} is outside [0, 1] for {Describe(state)}");

        var pool = Pool.Calculate(state, kpis);
        Assert.Equal(1.0, pool.Options.Sum(option => option.Probability), 9);
        Assert.All(pool.Options, option =>
        {
            Assert.InRange(option.Probability, 0, 1);
            Assert.True(option.ExpectedValue is null || double.IsFinite(option.ExpectedValue.Value));
            if (!option.IsAllowed)
                Assert.Equal(0, option.Probability);
        });
        Assert.Equal(state.BetToCall > 0, pool[PlayerAction.FOLD].IsAllowed);
        Assert.Equal(state.BetToCall > 0, pool[PlayerAction.CALL].IsAllowed);
        Assert.Equal(state.BetToCall == 0, pool[PlayerAction.CHECK].IsAllowed);
        if (pool[PlayerAction.RAISE].SuggestedRaise is { } raise)
            Assert.True(state.BetToCall + raise <= state.Hero.Stack, $"Raise {raise} exceeds stack for {Describe(state)}");
    }

    private static IEnumerable<(string Name, double Value)> Numbers(JsonElement element, string name = "")
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                foreach (var number in Numbers(property.Value, property.Name))
                    yield return number;
                break;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                foreach (var number in Numbers(item, name))
                    yield return number;
                break;
            case JsonValueKind.Number:
                yield return (name, element.GetDouble());
                break;
        }
    }

    /// <summary>Histories that leave Hero a decision; street, Hero's blind and facing a bet are derived from them.</summary>
    private static readonly string[][] Histories =
    [
        [], ["bet", "raise"], ["bet", "raise", "call"], ["bet", "raise", "allin"],
        ["bet", "raise", "raise", "call", "check"], ["bet", "raise", "raise", "raise", "call", "raise"],
        ["bet", "raise", "raise", "call", "check", "check", "raise"],
        ["bet", "raise", "call", "check", "check", "check", "check", "check", "check"]
    ];

    private static GameState RandomState(Random random)
    {
        var history = Histories[random.Next(Histories.Length)];
        var decoded = ActionHistoryDecoder.Decode(history);
        var street = history.Length == 0 ? (Street)random.Next(4) : decoded.CurrentStreet;
        var heroBlind = history.Length == 0 ? (random.Next(2) == 0 ? Blind.SB : Blind.BB) : decoded.NextToAct!.Value;

        var deck = Deck.All.OrderBy(_ => random.Next()).ToArray();
        var boardCount = street switch { Street.Preflop => 0, Street.Flop => 3, Street.Turn => 4, _ => 5 };
        var board = deck.Skip(2).Take(boardCount).Select(card => (Card?)card)
            .Concat(Enumerable.Repeat<Card?>(null, 5 - boardCount)).ToArray();

        int? bigBlind = random.Next(4) == 0 ? null : random.Next(2, 50);
        var big = bigBlind ?? GameState.DefaultBigBlind;
        var small = Math.Max(1, big / 2);
        var pot = (history.Length == 0 ? 0 : small + big) + random.Next(0, 400);
        var facesBet = history.Length == 0 ? random.Next(2) == 0 : decoded.NextToActFacesBet;
        var betToCall = facesBet && pot > 0 ? random.Next(1, pot + 1) : 0;

        var vpip = random.NextDouble();
        var stats = random.Next(3) == 0 ? null : new PlayerStats(vpip, vpip * random.NextDouble(), random.NextDouble(), random.NextDouble() * 6,
            random.Next(2) == 0 ? random.NextDouble() : null, random.Next(2) == 0 ? random.NextDouble() : null);
        var heroStack = random.Next(0, 300);
        var villainStack = random.Next(0, 300);

        return new GameState(
            new Hand(deck[0], deck[1]),
            new CommunityCards(board),
            street,
            [
                new Player("Hero", heroStack, heroBlind, random.Next(2) == 0 ? random.Next(0, 100) : null),
                new Player("Villain", villainStack, heroBlind == Blind.SB ? Blind.BB : Blind.SB, stats: stats)
            ],
            pot,
            betToCall,
            history,
            bigBlind,
            random.Next(2) == 0 ? heroStack + villainStack + pot : null);
    }

    private static string Describe(GameState state) =>
        $"{state.Hand} | {state.CommunityCards} | pot {state.PotSize} call {state.BetToCall} | stacks {state.Hero.Stack}/{state.Villains.First().Stack}";
}
