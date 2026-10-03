using PokerPlayer.Domain.Enums;
using PokerPlayer.Domain.Exceptions;

namespace PokerPlayer.Domain.Cards;

/// <summary>A single playing card, written as rank + suit, e.g. "7H", "TS", "AD".</summary>
public readonly record struct Card(Rank Rank, Suit Suit)
{
    private const string RankSymbols = "23456789TJQKA";
    private const string SuitSymbols = "DHSC";

    public static Card Parse(string text)
    {
        if (!TryParse(text, out var card))
            throw new InvalidInputException(ErrorCodes.InvalidCard,
                $"Invalid card format. '{text}' is not a valid card, expected rank (2-9, T, J, Q, K, A) followed by suit (D, H, S, C), e.g. '7H'.");
        return card;
    }

    public static bool TryParse(string? text, out Card card)
    {
        card = default;
        if (text is null || text.Trim().Length != 2)
            return false;

        var trimmed = text.Trim().ToUpperInvariant();
        var rankIndex = RankSymbols.IndexOf(trimmed[0]);
        var suitIndex = SuitSymbols.IndexOf(trimmed[1]);
        if (rankIndex < 0 || suitIndex < 0)
            return false;

        card = new Card((Rank)(rankIndex + 2), (Suit)suitIndex);
        return true;
    }

    /// <summary>Unique index in 0..51, handy for bitmasks and lookups.</summary>
    public int Index => ((int)Rank - 2) * 4 + (int)Suit;

    public override string ToString() => $"{RankSymbols[(int)Rank - 2]}{SuitSymbols[(int)Suit]}";
}
