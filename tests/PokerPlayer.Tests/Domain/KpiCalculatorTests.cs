using PokerPlayer.Domain.Enums;
using PokerPlayer.Domain.Kpis;
using PokerPlayer.Domain.Kpis.Calculators;
using PokerPlayer.Domain.Ranges;
using static PokerPlayer.Tests.TestData;

namespace PokerPlayer.Tests.Domain;

public class KpiCalculatorTests
{
    private static readonly KpiCalculator Calculator = new(new KpiCalculatorOptions(Iterations: 4_000, Seed: 42));

    [Fact]
    public void Preflop_ReturnsHandStrengthAndPricing()
    {
        var kpis = Assert.IsType<PreflopKpis>(Calculator.Calculate(State(hand: "AH KH", pot: 3, betToCall: 1)));

        Assert.Equal("AKs", kpis.HandStrength.HandClass);
        Assert.Equal(12, kpis.HandStrength.ChenScore);
        Assert.Equal(50, kpis.UnseenCards);
        Assert.Equal(0.25, kpis.FacingBet!.RequiredEquity, 6);
        Assert.Equal([1, 2, 3], kpis.ReferenceBets.Select(bet => bet.Amount)); // 1/3, 2/3 and 1 pot of 3 chips
        Assert.Equal(1.0, kpis.Equity.VillainRangeWidth, 6); // BB has not acted voluntarily: any two cards
        Assert.InRange(kpis.Equity.EstimatedEquity, 0.75, 0.80);
        Assert.NotNull(kpis.Equity.CallExpectedValue);
    }

    [Fact]
    public void ReferenceBets_NeverExceedHeroStack()
    {
        var kpis = Calculator.Calculate(State(pot: 100, betToCall: 10, heroStack: 25, villainStack: 200));

        Assert.All(kpis.ReferenceBets, bet => Assert.True(10 + bet.Amount <= 25));
        Assert.Contains(kpis.ReferenceBets, bet => bet.Amount == 15);
    }

    [Fact]
    public void ReferenceBets_EmptyWhenCallingTakesWholeStack()
    {
        Assert.Empty(Calculator.Calculate(State(pot: 20, betToCall: 10, heroStack: 10)).ReferenceBets);
    }

    [Fact]
    public void NoBetToCall_HasNoFacingBetSection()
    {
        Assert.Null(Calculator.Calculate(State(betToCall: 0)).FacingBet);
    }

    [Fact]
    public void Stack_UsesSmallerStackAndOptionalInvested()
    {
        var withoutInvested = Calculator.Calculate(State(heroStack: 49, villainStack: 30)).Stack;
        Assert.Equal(30, withoutInvested.EffectiveStack);
        Assert.Equal(15, withoutInvested.StackInBigBlinds, 6);
        Assert.Null(withoutInvested.PotCommitment);

        var withInvested = Calculator.Calculate(State(heroStack: 40, heroInvested: 10)).Stack;
        Assert.Equal(0.2, withInvested.PotCommitment!.Value, 6);
    }

    [Fact]
    public void Flop_FlushDrawHasDrawsAndEquity()
    {
        var state = State(hand: "AH 5H", board: "KH 9H 2C", pot: 14, betToCall: 5,
            history: ["bet", "raise", "raise", "call", "raise"]);

        var kpis = Assert.IsType<FlopKpis>(Calculator.Calculate(state));

        Assert.True(kpis.Draws.Outs >= 9);
        Assert.NotNull(kpis.Draws.HitByRiver);
        Assert.InRange(kpis.Equity.Equity, 0.3, 0.7);
        Assert.NotNull(kpis.Equity.PositivePotential);
        Assert.NotNull(kpis.Equity.CallExpectedValue);
        Assert.Equal(0.24, kpis.Equity.VillainRangeWidth, 6); // villain only called preflop
    }

    [Fact]
    public void Turn_HasNoHitByRiver()
    {
        var kpis = Assert.IsType<TurnKpis>(Calculator.Calculate(State(hand: "AH 5H", board: "KH 9H 2C 3D", betToCall: 0)));

        Assert.Null(kpis.Draws.HitByRiver);
    }

    [Fact]
    public void River_EquityEqualsHandStrength_AndNutsAlwaysWin()
    {
        var kpis = Assert.IsType<RiverKpis>(Calculator.Calculate(State(hand: "AH KH", board: "QH JH TH 2C 3D", betToCall: 0)));

        Assert.Equal(1.0, kpis.Equity.Equity, 6);
        Assert.Equal(kpis.Equity.HandStrength, kpis.Equity.Equity, 6);
        Assert.Null(kpis.Equity.PositivePotential);
        Assert.Equal("StraightFlush", kpis.Equity.CurrentHand);
    }

    [Fact]
    public void ReferenceBets_AreCappedByEffectiveStack()
    {
        var kpis = Calculator.Calculate(State(board: "2C 7D 9S", pot: 100, betToCall: 0, heroStack: 20));

        var bet = Assert.Single(kpis.ReferenceBets, size => size.Amount == 20);
        Assert.True(kpis.ReferenceBets.All(size => size.Amount <= 20));
        Assert.Equal(20 / 120.0, bet.FoldEquityNeeded, 6);
    }

    [Fact]
    public void Equity_IsReproducibleWithSeed()
    {
        var state = State(hand: "AH 5H", board: "KH 9H 2C", betToCall: 0);

        var first = new KpiCalculator(new KpiCalculatorOptions(2_000, Seed: 7)).Calculate(state);
        var second = new KpiCalculator(new KpiCalculatorOptions(2_000, Seed: 7)).Calculate(state);

        Assert.Equal(((FlopKpis)first).Equity, ((FlopKpis)second).Equity);
    }

    [Fact]
    public void Equity_SetIsFarAheadOfRandomRange()
    {
        var estimator = new EquityEstimator(new Random(1));
        var hand = Hand("9C 9D");
        var board = Cards("9S 4H 2C");
        var range = HandRange.FromTopPercent(1, [.. hand.Cards, .. board]);

        var estimate = estimator.Estimate(hand, board, range, 5_000);

        Assert.InRange(estimate.Equity, 0.85, 1.0);
        Assert.InRange(estimate.HandStrength, 0.95, 1.0);
    }

    [Fact]
    public void Range_ExcludesVisibleCards()
    {
        var dead = Cards("AH AD KS");
        var range = HandRange.FromTopPercent(0.02, dead);

        Assert.DoesNotContain(range.Combos, combo => dead.Contains(combo.First) || dead.Contains(combo.Second));
        Assert.NotEmpty(range.Combos);
    }
}
