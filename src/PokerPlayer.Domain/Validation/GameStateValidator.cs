using PokerPlayer.Domain.Enums;
using PokerPlayer.Domain.Exceptions;
using PokerPlayer.Domain.History;

namespace PokerPlayer.Domain.Validation;

/// <summary>
/// Cross-field consistency checks. Single-field rules (card format, negative values, ...) are already
/// enforced when the domain objects are built; this covers rules that need several fields together.
/// Checks run from the most concrete (board, pot, chips) to the most derived (the replayed history),
/// so the reply names the simplest problem first.
/// </summary>
public static class GameStateValidator
{
    private const int BlindPosts = 2;

    private static readonly Dictionary<Street, int> BoardCardsByStreet = new()
    {
        [Street.Preflop] = 0,
        [Street.Flop] = 3,
        [Street.Turn] = 4,
        [Street.River] = 5
    };

    /// <exception cref="InvalidInputException">The first rule the state breaks.</exception>
    public static void Validate(GameState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        ValidateBoardMatchesStreet(state);
        ValidateBlinds(state);
        ValidatePot(state);
        ValidateBetToCall(state);
        ValidateChipCount(state);
        ValidateHistory(state);
    }

    private static void ValidateBoardMatchesStreet(GameState state)
    {
        var expected = BoardCardsByStreet[state.Street];
        var revealed = state.CommunityCards.Revealed.Count;
        if (revealed == expected)
            return;

        var street = Name(state.Street);
        throw new InvalidInputException(ErrorCodes.InvalidCommunityCards, expected == 0
            ? $"Invalid community cards, stage {street} cannot have any community card available, found {revealed}."
            : $"Invalid community cards, stage {street} must have exactly {expected} community cards available, found {revealed}.");
    }

    private static void ValidateBlinds(GameState state)
    {
        var duplicated = state.Players.GroupBy(player => player.Blind).FirstOrDefault(group => group.Count() > 1);
        if (duplicated is not null)
            throw new InvalidInputException(ErrorCodes.InvalidBlinds,
                $"Invalid blinds. Only one player can be {duplicated.Key}, found {duplicated.Count()}.");
    }

    /// <summary>Once actions were taken the pot cannot be empty, and it holds at least the posted blinds.</summary>
    private static void ValidatePot(GameState state)
    {
        var actions = state.ActionHistory.Count;
        if (actions == 0)
            return;

        var minimum = actions == 1 ? state.SmallBlind : state.SmallBlind + state.BigBlind;
        if (state.PotSize == 0)
            throw new InvalidInputException(ErrorCodes.InvalidPot,
                $"Invalid pot value. 0 is not a valid pot value when the action history has {actions} action(s).");
        if (state.PotSize < minimum)
            throw new InvalidInputException(ErrorCodes.InvalidPot,
                $"Invalid pot value. {state.PotSize} is not a valid pot value, the posted blinds alone add up to {minimum}.");
    }

    /// <summary>The pot already includes the bet Hero faces, so the call can never exceed it.</summary>
    private static void ValidateBetToCall(GameState state)
    {
        if (state.BetToCall > state.PotSize)
            throw new InvalidInputException(ErrorCodes.InvalidBetToCall,
                $"Invalid bet to call. {state.BetToCall} is not a valid bet to call, it cannot exceed the pot ({state.PotSize}), which includes the bet faced.");
    }

    /// <summary>Chips are only moved, never created: every stack plus the pot must equal the chips at the table.</summary>
    private static void ValidateChipCount(GameState state)
    {
        if (state.TotalChips is not { } total)
            return;

        var stacks = state.Players.Sum(player => player.Stack);
        if (stacks + state.PotSize == total)
            return;

        var stackList = string.Join(" + ", state.Players.Select(player => player.Stack));
        throw new InvalidInputException(ErrorCodes.ChipCountMismatch,
            $"Invalid chip count. Stacks ({stackList}) plus pot ({state.PotSize}) add up to {stacks + state.PotSize}, but the total chips in play is {total}.");
    }

    /// <summary>
    /// Replays the history (unknown actions throw inv11) and checks it describes the decision being asked:
    /// the hand is still running, it is on the stated street, it is Hero's turn, and Hero faces a bet
    /// exactly when there is something to call. An empty history means it was not provided and is skipped.
    /// </summary>
    private static void ValidateHistory(GameState state)
    {
        var entries = state.ActionHistory.Count;
        var history = ActionHistoryDecoder.Decode(state.ActionHistory);
        if (entries == 0)
            return;

        if (entries < BlindPosts)
            throw InvalidHistory("It must start with both blind posts (small blind, then big blind), found only 1 entry.");

        if (history.HasEntriesAfterHandEnded(entries))
        {
            var last = history.Actions[^1];
            throw InvalidHistory(
                $"The hand ended at action {history.Actions.Count} ('{last.Raw}' by {last.Actor} on the {Name(last.Street)}), but {entries - history.Actions.Count} more action(s) follow.");
        }

        if (history.NextToAct is not { } nextToAct)
        {
            var last = history.Actions[^1];
            throw InvalidHistory(last.Action == PlayerAction.FOLD
                ? $"The hand is already over, {last.Actor} folded. There is no decision left for Hero."
                : "The hand is already over, the river betting round is closed. There is no decision left for Hero.");
        }

        if (history.CurrentStreet != state.Street)
            throw InvalidHistory($"The actions end on the {Name(history.CurrentStreet)}, but the street is {Name(state.Street)}.");

        var hero = state.Hero;
        if (nextToAct != hero.Blind)
            throw InvalidHistory($"After these actions it is {nextToAct}'s turn, but Hero is {hero.Blind}.");

        var facesBet = history.NextToActFacesBet && !(SmallBlindCompletesForFree(state, history));
        if (facesBet && state.BetToCall == 0)
            throw new InvalidInputException(ErrorCodes.InvalidBetToCall,
                "Invalid bet to call. 0 is not a valid bet to call, the action history shows Hero is facing a bet.");
        if (!facesBet && state.BetToCall > 0)
            throw new InvalidInputException(ErrorCodes.InvalidBetToCall,
                $"Invalid bet to call. {state.BetToCall} is not a valid bet to call, the action history shows Hero is not facing a bet.");
    }

    /// <summary>With a 1-chip big blind the small blind is also 1 chip, so its first preflop action costs nothing.</summary>
    private static bool SmallBlindCompletesForFree(GameState state, DecodedHistory history) =>
        state.SmallBlind >= state.BigBlind
        && history.CurrentStreet == Street.Preflop
        && history.Actions.All(action => action.IsBlindPost);

    private static InvalidInputException InvalidHistory(string reason) =>
        new(ErrorCodes.InvalidActionHistory, $"Invalid action history. {reason}");

    private static string Name(Street street) => street.ToString().ToLowerInvariant();
}
