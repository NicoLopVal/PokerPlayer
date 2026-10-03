using PokerPlayer.Domain.Cards;
using PokerPlayer.Domain.Enums;
using PokerPlayer.Domain.Exceptions;
using PokerPlayer.Domain.Players;

namespace PokerPlayer.Domain;

/// <summary>
/// Everything known at the moment Hero has to act.
/// The constructor enforces structural rules only; cross-field consistency
/// (street vs board, pot vs stacks, history vs street) belongs to the validator.
/// </summary>
public sealed record GameState
{
    public const int DefaultBigBlind = 2;

    public Hand Hand { get; }
    public CommunityCards CommunityCards { get; }
    public Street Street { get; }
    public IReadOnlyList<Player> Players { get; }

    /// <summary>All chips in the middle, including the bet Hero is facing.</summary>
    public int PotSize { get; }

    /// <summary>Chips Hero must add to call. Zero means Hero can check.</summary>
    public int BetToCall { get; }

    public int BigBlind { get; }

    /// <summary>Raw actions of the hand, starting with the two blind posts.</summary>
    public IReadOnlyList<string> ActionHistory { get; }

    /// <summary>All chips at the table (every stack plus the pot), when known. One chip is one unit.</summary>
    public int? TotalChips { get; }

    public GameState(
        Hand hand,
        CommunityCards communityCards,
        Street street,
        IReadOnlyList<Player> players,
        int potSize,
        int betToCall,
        IReadOnlyList<string> actionHistory,
        int? bigBlind = null,
        int? totalChips = null)
    {
        ArgumentNullException.ThrowIfNull(hand);
        ArgumentNullException.ThrowIfNull(communityCards);
        ArgumentNullException.ThrowIfNull(players);
        ArgumentNullException.ThrowIfNull(actionHistory);

        if (potSize < 0)
            throw new InvalidInputException(ErrorCodes.InvalidPot, $"Invalid pot value. {potSize} is not a valid pot value, it cannot be negative.");
        if (betToCall < 0)
            throw new InvalidInputException(ErrorCodes.InvalidBetToCall, $"Invalid bet to call. {betToCall} is not a valid bet to call, it cannot be negative.");
        if (bigBlind is < 1)
            throw new InvalidInputException(ErrorCodes.InvalidBigBlind, $"Invalid big blind. {bigBlind} is not a valid big blind, it must be at least 1 chip.");
        if (totalChips is < 0)
            throw new InvalidInputException(ErrorCodes.ChipCountMismatch, $"Invalid chip count. {totalChips} is not a valid total of chips, it cannot be negative.");

        var heroes = players.Count(player => player.IsHero);
        if (heroes != 1)
            throw new InvalidInputException(ErrorCodes.InvalidPlayers, $"Invalid players. Exactly one player must be named '{Player.HeroName}', found {heroes}.");
        if (players.Count < 2)
            throw new InvalidInputException(ErrorCodes.InvalidPlayers, "Invalid players. A game needs at least two players.");
        if (players.Select(player => player.Name.ToUpperInvariant()).Distinct().Count() != players.Count)
            throw new InvalidInputException(ErrorCodes.InvalidPlayers, "Invalid players. Player names must be unique.");

        var visible = hand.Cards.Concat(communityCards.Revealed).ToArray();
        var duplicated = visible.GroupBy(card => card).FirstOrDefault(group => group.Count() > 1);
        if (duplicated is not null)
            throw new InvalidInputException(ErrorCodes.DuplicatedCard, $"Duplicated card. {duplicated.Key} appears in both the hand and the community cards.");

        Hand = hand;
        CommunityCards = communityCards;
        Street = street;
        Players = players.ToArray();
        PotSize = potSize;
        BetToCall = betToCall;
        BigBlind = bigBlind ?? DefaultBigBlind;
        ActionHistory = actionHistory.ToArray();
        TotalChips = totalChips;
    }

    public Player Hero => Players.Single(player => player.IsHero);

    public IEnumerable<Player> Villains => Players.Where(player => !player.IsHero);

    /// <summary>Small blind amount, taken as half the big blind (at least 1 chip).</summary>
    public int SmallBlind => Math.Max(1, BigBlind / 2);

    /// <summary>Cards Hero can see: hand plus revealed board.</summary>
    public IReadOnlyList<Card> KnownCards => [.. Hand.Cards, .. CommunityCards.Revealed];
}
