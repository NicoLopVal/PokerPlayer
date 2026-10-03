using PokerPlayer.Domain.Cards;
using PokerPlayer.Domain.Evaluation;

namespace PokerPlayer.Domain.Kpis.Calculators;

/// <summary>
/// Counts outs by trying every unseen card. A card is an out when it:
/// 1. moves Hero to a better hand category,
/// 2. that the board alone (plus that card) does not already give everyone, and
/// 3. if the improvement is only to one pair, it is top pair or better (no bottom-pair "outs").
/// This catches flush and straight draws, overcards, sets and two pair without special-casing each draw.
/// It does not discount "dirty" outs that also improve the villain; equity covers that.
/// </summary>
public static class OutsCounter
{
    public static int Count(Hand hand, IReadOnlyList<Card> board)
    {
        var known = hand.Cards.Concat(board).ToArray();
        var current = HandEvaluator.Evaluate(known).Category;
        var topBoardRank = board.Count == 0 ? 0 : board.Max(card => (int)card.Rank);

        Span<Card> withCard = stackalloc Card[known.Length + 1];
        known.CopyTo(withCard);
        Span<Card> boardWithCard = stackalloc Card[board.Count + 1];
        for (var i = 0; i < board.Count; i++)
            boardWithCard[i] = board[i];

        var outs = 0;
        foreach (var card in Deck.Without(known))
        {
            withCard[^1] = card;
            boardWithCard[^1] = card;

            var improved = HandEvaluator.Evaluate(withCard);
            if (improved.Category <= current)
                continue;
            if (improved.Category <= HandEvaluator.Evaluate(boardWithCard).Category)
                continue;
            if (improved.Category == HandCategory.Pair && PairRank(improved) < Math.Max(topBoardRank, (int)card.Rank))
                continue;

            outs++;
        }

        return outs;
    }

    private static int PairRank(HandValue value) => (value.Value >> 16) & 0xF;
}
