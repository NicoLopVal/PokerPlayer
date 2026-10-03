using PokerPlayer.Domain.Cards;

namespace PokerPlayer.Domain.Evaluation;

/// <summary>Finds the best five-card hand among 1 to 7 cards.</summary>
public static class HandEvaluator
{
    private const int AceLowStraightMask = (1 << 14) | 0b111100; // A,5,4,3,2

    public static HandValue Evaluate(IEnumerable<Card> cards)
    {
        Span<Card> buffer = stackalloc Card[7];
        var count = 0;
        foreach (var card in cards)
        {
            if (count == buffer.Length)
                throw new ArgumentException("At most 7 cards can be evaluated.", nameof(cards));
            buffer[count++] = card;
        }
        return Evaluate(buffer[..count]);
    }

    public static HandValue Evaluate(Card[] cards) => Evaluate(cards.AsSpan());

    public static HandValue Evaluate(ReadOnlySpan<Card> cards)
    {
        if (cards.Length is 0 or > 7)
            throw new ArgumentException("Between 1 and 7 cards are required.", nameof(cards));

        Span<int> rankCounts = stackalloc int[15];
        Span<int> suitMasks = stackalloc int[4];
        Span<int> suitCounts = stackalloc int[4];
        var rankMask = 0;

        foreach (var card in cards)
        {
            var rank = (int)card.Rank;
            rankCounts[rank]++;
            rankMask |= 1 << rank;
            suitMasks[(int)card.Suit] |= 1 << rank;
            suitCounts[(int)card.Suit]++;
        }

        var flushSuit = -1;
        for (var suit = 0; suit < 4; suit++)
            if (suitCounts[suit] >= 5)
                flushSuit = suit;

        Span<int> ranks = stackalloc int[5];

        if (flushSuit >= 0)
        {
            var straightFlushHigh = StraightHigh(suitMasks[flushSuit]);
            if (straightFlushHigh > 0)
                return HandValue.Create(HandCategory.StraightFlush, [straightFlushHigh]);
        }

        // Group ranks by count, highest rank first within each group.
        int quads = 0, trips = 0, secondTrips = 0, pairHigh = 0, pairLow = 0;
        for (var rank = 14; rank >= 2; rank--)
        {
            switch (rankCounts[rank])
            {
                case 4: quads = rank; break;
                case 3 when trips == 0: trips = rank; break;
                case 3: secondTrips = rank; break;
                case 2 when pairHigh == 0: pairHigh = rank; break;
                case 2 when pairLow == 0: pairLow = rank; break;
            }
        }

        if (quads > 0)
            return HandValue.Create(HandCategory.FourOfAKind, [quads, HighestExcluding(rankMask, quads)]);

        if (trips > 0 && (secondTrips > 0 || pairHigh > 0))
            return HandValue.Create(HandCategory.FullHouse, [trips, Math.Max(secondTrips, pairHigh)]);

        if (flushSuit >= 0)
        {
            TopRanks(suitMasks[flushSuit], ranks);
            return HandValue.Create(HandCategory.Flush, ranks);
        }

        var straightHigh = StraightHigh(rankMask);
        if (straightHigh > 0)
            return HandValue.Create(HandCategory.Straight, [straightHigh]);

        if (trips > 0)
        {
            var kickers = TopRanks(rankMask & ~(1 << trips), ranks[1..3]);
            ranks[0] = trips;
            return HandValue.Create(HandCategory.ThreeOfAKind, ranks[..(1 + kickers)]);
        }

        if (pairHigh > 0 && pairLow > 0)
            return HandValue.Create(HandCategory.TwoPair,
                [pairHigh, pairLow, HighestExcluding(rankMask & ~(1 << pairLow), pairHigh)]);

        if (pairHigh > 0)
        {
            var kickers = TopRanks(rankMask & ~(1 << pairHigh), ranks[1..4]);
            ranks[0] = pairHigh;
            return HandValue.Create(HandCategory.Pair, ranks[..(1 + kickers)]);
        }

        var highCards = TopRanks(rankMask, ranks);
        return HandValue.Create(HandCategory.HighCard, ranks[..highCards]);
    }

    /// <summary>Highest card of a straight in the rank mask, or 0 if none.</summary>
    private static int StraightHigh(int mask)
    {
        for (var high = 14; high >= 6; high--)
        {
            var window = 0b11111 << (high - 4);
            if ((mask & window) == window)
                return high;
        }
        return (mask & AceLowStraightMask) == AceLowStraightMask ? 5 : 0;
    }

    /// <summary>Writes the highest ranks of the mask into the destination; returns how many were written.</summary>
    private static int TopRanks(int mask, Span<int> destination)
    {
        var written = 0;
        for (var rank = 14; rank >= 2 && written < destination.Length; rank--)
            if ((mask & (1 << rank)) != 0)
                destination[written++] = rank;
        return written;
    }

    private static int HighestExcluding(int mask, int excludedRank)
    {
        Span<int> top = stackalloc int[1];
        return TopRanks(mask & ~(1 << excludedRank), top) == 1 ? top[0] : 0;
    }
}
