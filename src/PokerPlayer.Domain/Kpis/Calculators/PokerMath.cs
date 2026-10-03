using PokerPlayer.Domain.Cards;

namespace PokerPlayer.Domain.Kpis.Calculators;

/// <summary>Closed-form poker formulas. Pure and cheap; P = pot, B = bet, C = call, o = outs, N = unseen cards, S = stack.</summary>
public static class PokerMath
{
    // Probability and draws

    public static int UnseenCards(int knownCards) => Deck.Size - knownCards;

    public static double HitNextCard(int outs, int unseen) => Fraction(outs, unseen);

    /// <summary>1 - [(N-o)/N]·[(N-o-1)/(N-1)]: hit on at least one of the next two cards.</summary>
    public static double HitWithinTwoCards(int outs, int unseen) =>
        unseen < 2 ? HitNextCard(outs, unseen) : 1 - (double)(unseen - outs) / unseen * (unseen - outs - 1) / (unseen - 1);

    /// <summary>~4·o% with two cards to come, ~2·o% with one.</summary>
    public static double RuleOfTwoAndFour(int outs, int cardsToCome) =>
        Math.Min(1, outs * (cardsToCome >= 2 ? 4 : 2) / 100.0);

    /// <summary>(N-o) : o, returned as the left side of "x : 1". Null with no outs.</summary>
    public static double? OddsAgainst(int outs, int unseen) => outs == 0 ? null : (double)(unseen - outs) / outs;

    /// <summary>Odds a : b against, to probability b / (a + b).</summary>
    public static double OddsToProbability(double against, double @for) => Fraction(@for, against + @for);

    /// <summary>n! / (k!(n-k)!).</summary>
    public static long Combinations(int n, int k)
    {
        if (k < 0 || k > n)
            return 0;
        k = Math.Min(k, n - k);
        long result = 1;
        for (var i = 1; i <= k; i++)
            result = result * (n - k + i) / i;
        return result;
    }

    // Pricing

    /// <summary>C / (P + C), where P already includes the bet faced.</summary>
    public static double RequiredEquity(int call, int pot) => Fraction(call, pot + call);

    /// <summary>P : C, returned as the left side of "x : 1". Only defined when there is something to call.</summary>
    public static double PotOddsRatio(int call, int pot) =>
        call > 0 ? (double)pot / call : throw new ArgumentOutOfRangeException(nameof(call), call, "Pot odds need a bet to call.");

    // Bluffing and game theory

    /// <summary>B / (P + B): fold equity needed for a pure bluff (alpha).</summary>
    public static double BluffBreakEven(double potBeforeBet, double bet) => Fraction(bet, potBeforeBet + bet);

    /// <summary>P / (P + B): share of range to defend against a bet B into P.</summary>
    public static double MinimumDefenseFrequency(double potBeforeBet, double bet) => Fraction(potBeforeBet, potBeforeBet + bet);

    /// <summary>B / (P + 2B): bluff share of a balanced betting range.</summary>
    public static double OptimalBluffShare(double potBeforeBet, double bet) => Fraction(bet, potBeforeBet + 2 * bet);

    // Stack depth

    public static double StackInBigBlinds(int stack, int bigBlind) =>
        bigBlind > 0 ? (double)stack / bigBlind : throw new ArgumentOutOfRangeException(nameof(bigBlind), bigBlind, "Big blind must be positive.");

    public static double MRatio(int stack, int smallBlind, int bigBlind, int antes = 0) =>
        smallBlind + bigBlind + antes > 0
            ? (double)stack / (smallBlind + bigBlind + antes)
            : throw new ArgumentOutOfRangeException(nameof(bigBlind), bigBlind, "Blinds must add up to a positive amount.");

    public static double? StackToPotRatio(int stack, int pot) => pot == 0 ? null : (double)stack / pot;

    public static double PotCommitment(int invested, int remainingStack) => Fraction(invested, invested + remainingStack);

    /// <summary>½·P·[((P + 2S)/P)^(1/n) - 1]: equal pot-fraction bet per street to be all-in after n streets.</summary>
    public static double GeometricBetSize(int pot, int stack, int streetsLeft)
    {
        if (pot <= 0 || streetsLeft <= 0)
            return stack;
        return 0.5 * pot * (Math.Pow((pot + 2.0 * stack) / pot, 1.0 / streetsLeft) - 1);
    }

    /// <summary>Numerator / denominator for shares and probabilities; 0 when the denominator is 0 (nothing bet, nothing unseen).</summary>
    private static double Fraction(double numerator, double denominator) => denominator == 0 ? 0 : numerator / denominator;
}
