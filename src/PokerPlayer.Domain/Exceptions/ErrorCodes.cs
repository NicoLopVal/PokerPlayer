namespace PokerPlayer.Domain.Exceptions;

/// <summary>Stable codes returned instead of an action when the input is invalid.</summary>
public static class ErrorCodes
{
    /// <summary>File missing or with an unsupported extension.</summary>
    public const string InvalidFile = "inv01";

    /// <summary>Not valid JSON, empty, or a field of the wrong type.</summary>
    public const string MalformedInput = "inv02";

    /// <summary>A required field is missing.</summary>
    public const string MissingField = "inv03";

    /// <summary>A card does not follow rank + suit, e.g. "7H".</summary>
    public const string InvalidCard = "inv04";

    /// <summary>Pot negative, or too small for the actions taken.</summary>
    public const string InvalidPot = "inv05";

    /// <summary>Bet to call negative or larger than the pot.</summary>
    public const string InvalidBetToCall = "inv06";

    /// <summary>The same card appears more than once.</summary>
    public const string DuplicatedCard = "inv07";

    /// <summary>Board slots malformed, or revealed cards do not match the street.</summary>
    public const string InvalidCommunityCards = "inv08";

    /// <summary>Hand does not have exactly two cards.</summary>
    public const string InvalidHand = "inv09";

    /// <summary>Street is not Preflop, Flop, Turn or River.</summary>
    public const string InvalidStreet = "inv10";

    /// <summary>An action in the history is not fold, check, call, bet, raise or allin.</summary>
    public const string InvalidActionHistory = "inv11";

    /// <summary>Player list is wrong: no Hero, duplicated names, fewer than two players.</summary>
    public const string InvalidPlayers = "inv12";

    /// <summary>A stack is negative.</summary>
    public const string InvalidStack = "inv13";

    /// <summary>Blind is not SB or BB, or both players hold the same blind.</summary>
    public const string InvalidBlinds = "inv14";

    /// <summary>Big blind smaller than one chip.</summary>
    public const string InvalidBigBlind = "inv15";

    /// <summary>A player stat is out of range.</summary>
    public const string InvalidStats = "inv16";

    /// <summary>Invested chips negative.</summary>
    public const string InvalidInvested = "inv17";

    /// <summary>Stacks plus pot do not add up to the total chips in play.</summary>
    public const string ChipCountMismatch = "inv18";

    /// <summary>A bug, not an input problem. Reported so the bot always replies.</summary>
    public const string UnexpectedError = "err99";
}
