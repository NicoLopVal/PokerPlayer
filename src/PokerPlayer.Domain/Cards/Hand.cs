using PokerPlayer.Domain.Exceptions;

namespace PokerPlayer.Domain.Cards;

/// <summary>The two private (hole) cards of a player.</summary>
public sealed record Hand
{
    public Card First { get; }
    public Card Second { get; }

    public Hand(Card first, Card second)
    {
        if (first == second)
            throw new InvalidInputException(ErrorCodes.DuplicatedCard, $"Duplicated card. The hand contains {first} twice.");

        First = first;
        Second = second;
    }

    public IReadOnlyList<Card> Cards => [First, Second];

    public bool IsSuited => First.Suit == Second.Suit;

    public bool IsPair => First.Rank == Second.Rank;

    public override string ToString() => $"{First} {Second}";
}
