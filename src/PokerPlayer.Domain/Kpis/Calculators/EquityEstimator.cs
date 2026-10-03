using PokerPlayer.Domain.Cards;
using PokerPlayer.Domain.Evaluation;
using PokerPlayer.Domain.Ranges;

namespace PokerPlayer.Domain.Kpis.Calculators;

/// <param name="PositivePotential">Null on the river.</param>
/// <param name="NegativePotential">Null on the river.</param>
public sealed record EquityEstimate(
    HandCategory CurrentHand,
    double Equity,
    double HandStrength,
    double? PositivePotential,
    double? NegativePotential,
    double EffectiveHandStrength,
    int Samples);

/// <summary>
/// Estimates Hero's equity against a villain range.
/// - Hand strength (HS) is exact: every villain combo is compared on the current board.
/// - With cards to come, equity, PPot and NPot are estimated by random runouts (Monte Carlo).
/// - On the river equity is exactly HS.
/// The random source is injected so results are reproducible in tests.
/// </summary>
public sealed class EquityEstimator(Random random)
{
    private const int Ahead = 0, Tied = 1, Behind = 2;

    public EquityEstimate Estimate(Hand hand, IReadOnlyList<Card> board, HandRange villainRange, int iterations)
    {
        if (board.Count is < 3 or > 5)
            throw new ArgumentException("Equity is estimated postflop only (3 to 5 board cards).", nameof(board));
        if (villainRange.Combos.Count == 0)
            throw new ArgumentException("Villain range is empty.", nameof(villainRange));
        ArgumentOutOfRangeException.ThrowIfLessThan(iterations, 1);

        var combos = villainRange.Combos;
        var heroNow = HandEvaluator.Evaluate([.. hand.Cards, .. board]);

        // Exact hand strength, remembering each combo's state for the potential tables.
        var stateNow = new int[combos.Count];
        var countNow = new int[3];
        for (var i = 0; i < combos.Count; i++)
        {
            var villainNow = HandEvaluator.Evaluate([combos[i].First, combos[i].Second, .. board]);
            stateNow[i] = Compare(heroNow, villainNow);
            countNow[stateNow[i]]++;
        }
        var handStrength = (countNow[Ahead] + countNow[Tied] / 2.0) / combos.Count;

        if (board.Count == 5)
            return new EquityEstimate(heroNow.Category, handStrength, handStrength, null, null, handStrength, combos.Count);

        // Monte Carlo runouts: pick a villain combo, deal the rest of the board, compare at showdown.
        var cardsToCome = 5 - board.Count;
        var deck = Deck.Without([.. hand.Cards, .. board]);
        var transitions = new int[3, 3]; // [state now, state at showdown]
        var heroCards = new Card[7];
        var villainCards = new Card[7];
        var runout = new Card[cardsToCome];

        for (var n = 0; n < iterations; n++)
        {
            var comboIndex = random.Next(combos.Count);
            var (villainFirst, villainSecond) = combos[comboIndex];
            DealRunout(deck, villainFirst, villainSecond, runout);

            heroCards[0] = hand.First;
            heroCards[1] = hand.Second;
            villainCards[0] = villainFirst;
            villainCards[1] = villainSecond;
            for (var i = 0; i < board.Count; i++)
                heroCards[i + 2] = villainCards[i + 2] = board[i];
            for (var i = 0; i < cardsToCome; i++)
                heroCards[board.Count + 2 + i] = villainCards[board.Count + 2 + i] = runout[i];

            var final = Compare(HandEvaluator.Evaluate(heroCards), HandEvaluator.Evaluate(villainCards));
            transitions[stateNow[comboIndex], final]++;
        }

        var wins = transitions[Ahead, Ahead] + transitions[Tied, Ahead] + transitions[Behind, Ahead];
        var ties = transitions[Ahead, Tied] + transitions[Tied, Tied] + transitions[Behind, Tied];
        var equity = (wins + ties / 2.0) / iterations;

        // Billings et al. potential formulas, ties counted as half.
        double Row(int state) => transitions[state, Ahead] + transitions[state, Tied] + transitions[state, Behind];
        var positiveDenominator = Row(Behind) + Row(Tied) / 2;
        var positive = positiveDenominator == 0 ? 0 :
            (transitions[Behind, Ahead] + transitions[Behind, Tied] / 2.0 + transitions[Tied, Ahead] / 2.0) / positiveDenominator;
        var negativeDenominator = Row(Ahead) + Row(Tied) / 2;
        var negative = negativeDenominator == 0 ? 0 :
            (transitions[Ahead, Behind] + transitions[Tied, Behind] / 2.0 + transitions[Ahead, Tied] / 2.0) / negativeDenominator;

        var effective = handStrength * (1 - negative) + (1 - handStrength) * positive;
        return new EquityEstimate(heroNow.Category, equity, handStrength, positive, negative, effective, iterations);
    }

    private void DealRunout(List<Card> deck, Card villainFirst, Card villainSecond, Card[] runout)
    {
        var dealt = 0;
        while (dealt < runout.Length)
        {
            var card = deck[random.Next(deck.Count)];
            if (card == villainFirst || card == villainSecond || runout.AsSpan(0, dealt).Contains(card))
                continue;
            runout[dealt++] = card;
        }
    }

    private static int Compare(HandValue hero, HandValue villain) =>
        hero > villain ? Ahead : hero == villain ? Tied : Behind;
}
