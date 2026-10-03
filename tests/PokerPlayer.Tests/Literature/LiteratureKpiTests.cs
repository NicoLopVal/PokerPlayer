using PokerPlayer.Domain.Cards;
using PokerPlayer.Domain.Enums;
using PokerPlayer.Domain.Evaluation;
using PokerPlayer.Domain.Kpis.Calculators;
using PokerPlayer.Domain.Preflop;
using PokerPlayer.Domain.Ranges;
using static PokerPlayer.Tests.TestData;

namespace PokerPlayer.Tests.Literature;

/// <summary>
/// Indicator values checked against figures that appear throughout poker math literature
/// (outs/odds charts, bet-sizing tables in "The Mathematics of Poker" and "Modern Poker Theory",
/// Harrington's M, Chen's formula, the 5-card hand frequency table).
/// </summary>
public class LiteratureKpiTests
{
    // Outs chart: flop → turn, flop → river (two cards), turn → river.

    [Theory]
    [InlineData(4, 0.085, 0.165, 0.087)]   // gutshot
    [InlineData(8, 0.170, 0.315, 0.174)]   // open-ended straight draw
    [InlineData(9, 0.191, 0.350, 0.196)]   // flush draw
    [InlineData(12, 0.255, 0.450, 0.261)]  // flush draw + gutshot (minus overlaps)
    [InlineData(15, 0.319, 0.541, 0.326)]  // flush draw + open-ended straight draw
    public void OutsChart(int outs, double flopToTurn, double flopToRiver, double turnToRiver)
    {
        Assert.Equal(flopToTurn, PokerMath.HitNextCard(outs, 47), 3);
        Assert.Equal(flopToRiver, PokerMath.HitWithinTwoCards(outs, 47), 3);
        Assert.Equal(turnToRiver, PokerMath.HitNextCard(outs, 46), 3);
    }

    [Theory]
    [InlineData(9, 47, 4.2)]   // flush draw on the flop: ~4.2 : 1 against
    [InlineData(9, 46, 4.1)]   // flush draw on the turn: ~4.1 : 1 against
    [InlineData(8, 46, 4.75)]  // OESD on the turn: 38 : 8
    [InlineData(4, 46, 10.5)]  // gutshot on the turn: 42 : 4
    public void OddsAgainst(int outs, int unseen, double expected)
    {
        Assert.Equal(expected, PokerMath.OddsAgainst(outs, unseen)!.Value, 1);
    }

    [Theory]
    [InlineData(9, 2, 0.36, 0.350)]  // rule of 4 overestimates slightly
    [InlineData(15, 2, 0.60, 0.541)] // and more so with many outs
    [InlineData(9, 1, 0.18, 0.196)]  // rule of 2 underestimates slightly
    public void RuleOfTwoAndFour_IsCloseToExact(int outs, int cardsToCome, double rule, double exact)
    {
        Assert.Equal(rule, PokerMath.RuleOfTwoAndFour(outs, cardsToCome), 6);
        Assert.InRange(rule - exact, -0.03, 0.07);
    }

    // Pricing: villain bets B into P; Hero calls B into a pot of P + B.

    [Theory]
    // bet / pot, required equity to call, MDF, bluff break-even (alpha)
    [InlineData(1 / 3.0, 0.200, 0.750, 0.250)]
    [InlineData(1 / 2.0, 0.250, 0.667, 0.333)]
    [InlineData(2 / 3.0, 0.286, 0.600, 0.400)]
    [InlineData(3 / 4.0, 0.300, 0.571, 0.429)]
    [InlineData(1.0, 0.333, 0.500, 0.500)]
    [InlineData(1.5, 0.375, 0.400, 0.600)]
    [InlineData(2.0, 0.400, 0.333, 0.667)]
    public void BetSizingTable(double betToPot, double requiredEquity, double mdf, double alpha)
    {
        const int potBefore = 300;
        var bet = (int)Math.Round(potBefore * betToPot);

        Assert.Equal(requiredEquity, PokerMath.RequiredEquity(bet, potBefore + bet), 3);
        Assert.Equal(mdf, PokerMath.MinimumDefenseFrequency(potBefore, bet), 3);
        Assert.Equal(alpha, PokerMath.BluffBreakEven(potBefore, bet), 3);
        // Two formulas from the indicators file are the same quantity: C/(P+C) == B/(P+2B).
        Assert.Equal(PokerMath.RequiredEquity(bet, potBefore + bet), PokerMath.OptimalBluffShare(potBefore, bet), 9);
    }

    [Fact]
    public void PotOdds_ClassicExample()
    {
        // $100 pot, villain bets $50: Hero calls $50 to win $150 → 3 : 1, needs 25%.
        Assert.Equal(3.0, PokerMath.PotOddsRatio(50, 150), 9);
        Assert.Equal(0.25, PokerMath.RequiredEquity(50, 150), 9);
        Assert.Equal(0.25, PokerMath.OddsToProbability(3, 1), 9);
    }

    [Fact]
    public void PotSizedRiverBet_OneThirdBluffs()
    {
        // Chen & Ankenman: a pot-sized bet should be 1/3 bluffs, 2/3 value (value : bluff = 2 : 1).
        Assert.Equal(1 / 3.0, PokerMath.OptimalBluffShare(100, 100), 9);
    }

    // Expected value

    [Fact]
    public void ExpectedValue_TextbookExamples()
    {
        // Calling 20 into a 100 pot (bet included) with 20% equity: 0.2 · 120 - 20 = +4.
        Assert.Equal(4.0, ExpectedValue.Call(0.20, 100, 20), 9);
        // Half-pot bluff that works 40% of the time: 0.4 · 100 - 0.6 · 50 = +10.
        Assert.Equal(10.0, ExpectedValue.PureBluff(0.40, 100, 50), 9);
        // At exactly alpha = B / (P + B) the bluff breaks even.
        Assert.Equal(0.0, ExpectedValue.PureBluff(50 / 150.0, 100, 50), 9);
        // Calling with exactly the required equity breaks even.
        Assert.Equal(0.0, ExpectedValue.Call(PokerMath.RequiredEquity(50, 150), 150, 50), 9);
    }

    // Stack depth

    [Fact]
    public void Harrington_MRatio()
    {
        // "Harrington on Hold'em": 3,000 chips with 50/100 blinds and no antes → M = 20.
        Assert.Equal(20.0, PokerMath.MRatio(3000, 50, 100), 9);
        Assert.Equal(15.0, PokerMath.MRatio(3000, 50, 100, antes: 50), 9);
    }

    [Fact]
    public void StackToPotRatio_Example()
    {
        // 100 behind into a 20 pot → SPR 5.
        Assert.Equal(5.0, PokerMath.StackToPotRatio(100, 20)!.Value, 9);
    }

    [Fact]
    public void GeometricSizing_TwoStreets()
    {
        // Pot 100, 300 behind, two streets: ~82% pot per street gets all-in by the river.
        Assert.Equal(82.3, PokerMath.GeometricBetSize(100, 300, 2), 1);
    }

    // Combinatorics

    [Fact]
    public void StartingHandCombinations()
    {
        Assert.Equal(1326, PokerMath.Combinations(52, 2));
        Assert.Equal(2_598_960, PokerMath.Combinations(52, 5));
        Assert.Equal(19_600, PokerMath.Combinations(50, 3));                    // possible flops for one hand
        Assert.Equal(6, new StartingHand(Rank.Ace, Rank.Ace, false).Combos);
        Assert.Equal(4, new StartingHand(Rank.Ace, Rank.King, true).Combos);
        Assert.Equal(12, new StartingHand(Rank.Ace, Rank.King, false).Combos);
    }

    [Fact]
    public void Blockers_OneAceOnBoard()
    {
        // With one ace visible: AA drops from 6 to 3 combos, AK from 16 to 12.
        var board = Cards("AS 7D 2C");
        var any = HandRange.FromTopPercent(1, board).Combos;

        Assert.Equal(3, any.Count(c => c.First.Rank == Rank.Ace && c.Second.Rank == Rank.Ace));
        Assert.Equal(12, any.Count(c => (c.First.Rank, c.Second.Rank) is (Rank.Ace, Rank.King) or (Rank.King, Rank.Ace)));
        Assert.Equal((int)PokerMath.Combinations(49, 2), any.Count);
    }

    // Chen formula examples (Chen & Ankenman, "Hold'em Excellence")

    [Theory]
    [InlineData("AS", "KS", 12)]
    [InlineData("TC", "TS", 10)]
    [InlineData("5H", "7H", 6)]
    [InlineData("2D", "2S", 5)]
    [InlineData("AC", "AD", 20)]
    [InlineData("7S", "2D", -1)]
    public void ChenFormula_PublishedExamples(string first, string second, int expected)
    {
        Assert.Equal(expected, ChenFormula.Score(StartingHand.From(C(first), C(second))));
    }

    // Hand evaluator: frequency of each category among all 2,598,960 five-card hands.

    [Fact]
    public void Evaluator_FiveCardHandFrequencies()
    {
        var counts = new long[Enum.GetValues<HandCategory>().Length];
        var deck = Deck.All.ToArray();
        Span<Card> hand = stackalloc Card[5];

        for (var a = 0; a < 52; a++)
        for (var b = a + 1; b < 52; b++)
        for (var c = b + 1; c < 52; c++)
        for (var d = c + 1; d < 52; d++)
        for (var e = d + 1; e < 52; e++)
        {
            hand[0] = deck[a]; hand[1] = deck[b]; hand[2] = deck[c]; hand[3] = deck[d]; hand[4] = deck[e];
            counts[(int)HandEvaluator.Evaluate(hand).Category]++;
        }

        Assert.Equal(40, counts[(int)HandCategory.StraightFlush]);       // includes 4 royal flushes
        Assert.Equal(624, counts[(int)HandCategory.FourOfAKind]);
        Assert.Equal(3_744, counts[(int)HandCategory.FullHouse]);
        Assert.Equal(5_108, counts[(int)HandCategory.Flush]);
        Assert.Equal(10_200, counts[(int)HandCategory.Straight]);
        Assert.Equal(54_912, counts[(int)HandCategory.ThreeOfAKind]);
        Assert.Equal(123_552, counts[(int)HandCategory.TwoPair]);
        Assert.Equal(1_098_240, counts[(int)HandCategory.Pair]);
        Assert.Equal(1_302_540, counts[(int)HandCategory.HighCard]);
    }

    /// <summary>Simulation tolerance: ±0.01 is about 5 standard deviations at 40,000 runouts.</summary>
    private static void AssertClose(double expected, double actual) => Assert.InRange(actual, expected - 0.01, expected + 0.01);

    // Equity against a known hand

    [Fact]
    public void Equity_FlushDrawOnTheTurn_AgainstOverpair_IsNineOutsOfFortyFour()
    {
        // Hero K♥J♥ on 7♥ 2♥ 9♣ 3♦ vs A♣A♦: only the 9 remaining hearts win (a K or J pair still loses to aces).
        // 44 cards unseen → 9/44 ≈ 20.5%.
        var hero = Hand("KH JH");
        var board = Cards("7H 2H 9C 3D");
        var villain = HandRange.FromCombos([(C("AC"), C("AD"))]);

        var estimate = new EquityEstimator(new Random(3)).Estimate(hero, board, villain, 40_000);

        AssertClose(9 / 44.0, estimate.Equity);
        Assert.Equal(0, estimate.HandStrength);
        AssertClose(9 / 44.0, estimate.PositivePotential!.Value);
        Assert.Equal(0, estimate.NegativePotential!.Value);
    }

    [Fact]
    public void Equity_SetVersusFlushDrawOnTheTurn()
    {
        // Hero 9♠9♦ (set) on 7♥ 2♥ 9♣ 3♦ vs A♥T♥: villain needs one of the 9 remaining hearts, but 9♥ gives
        // Hero quads and 3♥ gives Hero a full house, so villain has 7 clean outs of 44 → Hero 37/44 ≈ 84.1%.
        var hero = Hand("9S 9D");
        var board = Cards("7H 2H 9C 3D");
        var villain = HandRange.FromCombos([(C("AH"), C("TH"))]);

        var estimate = new EquityEstimator(new Random(5)).Estimate(hero, board, villain, 40_000);

        AssertClose(1 - 7 / 44.0, estimate.Equity);
        Assert.Equal(1, estimate.HandStrength);
    }

    [Fact]
    public void Equity_RiverIsExact()
    {
        // Top pair vs two specific hands, one beaten and one ahead: exactly 50%.
        var hero = Hand("AS KD");
        var board = Cards("AH 8C 5D 3S 2C");
        var villain = HandRange.FromCombos([(C("QH"), C("QC")), (C("8S"), C("8D"))]);

        var estimate = new EquityEstimator(new Random(1)).Estimate(hero, board, villain, iterations: 1);

        Assert.Equal(0.5, estimate.Equity, 9);
        Assert.Equal(2, estimate.Samples);
    }
}
