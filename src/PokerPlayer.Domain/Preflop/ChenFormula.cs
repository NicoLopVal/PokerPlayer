using PokerPlayer.Domain.Enums;

namespace PokerPlayer.Domain.Preflop;

/// <summary>
/// Bill Chen's starting hand score (from "Hold'em Excellence" / Chen &amp; Ankenman).
/// Ranges from -1 (72o) to 20 (AA). Higher is stronger.
/// </summary>
public static class ChenFormula
{
    public const int MinScore = -1;
    public const int MaxScore = 20;

    public static int Score(StartingHand hand)
    {
        // 1. Highest card.
        var score = CardPoints(hand.High);

        // 2. Pairs double the points, minimum 5.
        if (hand.IsPair)
            return (int)Math.Ceiling(Math.Max(5, score * 2));

        // 3. Suited.
        if (hand.Suited)
            score += 2;

        // 4. Gap between the cards.
        var gap = (int)hand.High - (int)hand.Low - 1;
        score -= gap switch
        {
            0 => 0,
            1 => 1,
            2 => 2,
            3 => 4,
            _ => 5
        };

        // 5. Straight bonus for connected or one-gapped cards below a queen.
        if (gap <= 1 && hand.High < Rank.Queen)
            score += 1;

        // 6. Round half points up.
        return (int)Math.Ceiling(score);
    }

    private static double CardPoints(Rank rank) => rank switch
    {
        Rank.Ace => 10,
        Rank.King => 8,
        Rank.Queen => 7,
        Rank.Jack => 6,
        _ => (int)rank / 2.0
    };
}
