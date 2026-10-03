using PokerPlayer.Domain.Enums;
using PokerPlayer.Domain.Exceptions;

namespace PokerPlayer.Domain.Cards;

/// <summary>
/// The five board slots. Unrevealed slots are null, and only these patterns are valid:
/// all null (preflop), last two null (flop), last one null (turn), none null (river).
/// </summary>
public sealed record CommunityCards
{
    public const int SlotCount = 5;

    public IReadOnlyList<Card?> Slots { get; }

    public CommunityCards(IReadOnlyList<Card?> slots)
    {
        ArgumentNullException.ThrowIfNull(slots);
        if (slots.Count != SlotCount)
            throw new InvalidInputException(ErrorCodes.InvalidCommunityCards, $"Invalid community cards, expected exactly {SlotCount} slots but got {slots.Count}.");

        var revealed = slots.TakeWhile(card => card.HasValue).Count();
        if (slots.Skip(revealed).Any(card => card.HasValue))
            throw new InvalidInputException(ErrorCodes.InvalidCommunityCards, "Invalid community cards, cards must be revealed in order: empty slots can only be at the end.");
        if (revealed is 1 or 2)
            throw new InvalidInputException(ErrorCodes.InvalidCommunityCards, $"Invalid community cards, {revealed} revealed card(s) is not a valid board. Expected 0, 3, 4 or 5.");

        var revealedCards = slots.Take(revealed).Select(card => card!.Value).ToArray();
        if (revealedCards.Distinct().Count() != revealedCards.Length)
            throw new InvalidInputException(ErrorCodes.DuplicatedCard, "Duplicated card. The community cards contain the same card twice.");

        Slots = slots.ToArray();
        Revealed = revealedCards;
    }

    public static CommunityCards Empty { get; } = new([null, null, null, null, null]);

    /// <summary>Only the revealed cards, in board order.</summary>
    public IReadOnlyList<Card> Revealed { get; }

    /// <summary>The street implied by how many cards are revealed.</summary>
    public Street ImpliedStreet => Revealed.Count switch
    {
        0 => Street.Preflop,
        3 => Street.Flop,
        4 => Street.Turn,
        _ => Street.River
    };

    // Records compare collections by reference; compare the slots by value instead.
    public bool Equals(CommunityCards? other) => other is not null && Slots.SequenceEqual(other.Slots);

    public override int GetHashCode() => Slots.Aggregate(0, (hash, card) => HashCode.Combine(hash, card));

    public override string ToString() => string.Join(" ", Slots.Select(card => card?.ToString() ?? "--"));
}
