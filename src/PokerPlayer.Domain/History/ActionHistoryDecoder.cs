using PokerPlayer.Domain.Enums;
using PokerPlayer.Domain.Exceptions;

namespace PokerPlayer.Domain.History;

/// <summary>
/// Replays a heads-up history (e.g. ["bet", "raise", "call", "check"]) to find who did what on which street.
/// Rules:
/// - The first two entries are the blind posts (SB then BB), whatever word they use.
/// - Preflop the SB acts first; on later streets the BB acts first.
/// - A round closes on a call, or on a check once the other player has already acted.
///   Exception: a preflop SB call of the big blind (a limp) gives the BB the option to act.
/// - "bet", "raise" and "allin" are all treated as RAISE.
/// Consistency checks (e.g. a check while facing a bet) are left to the validator.
/// </summary>
public static class ActionHistoryDecoder
{
    private const int BlindPosts = 2;

    public static DecodedHistory Decode(IReadOnlyList<string> history)
    {
        ArgumentNullException.ThrowIfNull(history);

        var actions = new List<DecodedAction>(history.Count);
        var street = Street.Preflop;
        Blind? toAct = Blind.SB;
        var actedThisRound = 0;
        var facingBet = true; // the small blind starts preflop facing the big blind

        for (var i = 0; i < history.Count; i++)
        {
            var raw = history[i];
            if (i < BlindPosts)
            {
                var poster = i == 0 ? Blind.SB : Blind.BB;
                actions.Add(new DecodedAction(poster, Street.Preflop, PlayerAction.RAISE, IsBlindPost: true, raw));
                continue;
            }

            if (toAct is null)
                break; // Hand already over; extra entries are the validator's concern.

            var actor = toAct.Value;
            var action = ParseAction(raw);
            actions.Add(new DecodedAction(actor, street, action, IsBlindPost: false, raw));

            var closesRound = action switch
            {
                PlayerAction.FOLD => false,
                PlayerAction.RAISE => false,
                PlayerAction.CHECK => actedThisRound > 0,
                PlayerAction.CALL => !(street == Street.Preflop && actedThisRound == 0), // limp keeps BB option
                _ => false
            };

            if (action == PlayerAction.FOLD)
            {
                toAct = null;
                break;
            }

            if (closesRound)
            {
                if (street == Street.River)
                {
                    toAct = null;
                    break;
                }

                street++;
                toAct = Blind.BB;
                actedThisRound = 0;
                facingBet = false;
                continue;
            }

            actedThisRound++;
            toAct = Other(actor);
            facingBet = action == PlayerAction.RAISE; // a limp or a check leaves nothing to call
        }

        // Fewer than two entries: the blinds are still being posted.
        if (history.Count < BlindPosts)
            toAct = history.Count == 0 ? Blind.SB : Blind.BB;

        return new DecodedHistory(actions, street, toAct, toAct is not null && facingBet);
    }

    public static PlayerAction ParseAction(string raw) =>
        raw?.Trim().Replace("-", "").Replace(" ", "").ToUpperInvariant() switch
        {
            "FOLD" => PlayerAction.FOLD,
            "CHECK" => PlayerAction.CHECK,
            "CALL" => PlayerAction.CALL,
            "RAISE" or "BET" or "ALLIN" => PlayerAction.RAISE,
            _ => throw new InvalidInputException(ErrorCodes.InvalidActionHistory,
                $"Invalid action history. '{raw}' is not a valid action, expected fold, check, call, bet, raise or allin.")
        };

    private static Blind Other(Blind blind) => blind == Blind.SB ? Blind.BB : Blind.SB;
}
