using PokerPlayer.Domain.Enums;

namespace PokerPlayer.Domain.History;

/// <param name="Actions">Decoded entries, in order.</param>
/// <param name="CurrentStreet">Street the hand is on after replaying the history.</param>
/// <param name="NextToAct">Who acts next, or null when the hand is over (a fold or the river closed).</param>
/// <param name="NextToActFacesBet">True when the next player has chips to call (assumes the big blind is larger than the small blind).</param>
public sealed record DecodedHistory(IReadOnlyList<DecodedAction> Actions, Street CurrentStreet, Blind? NextToAct, bool NextToActFacesBet)
{
    /// <summary>True when the hand ended (fold or river closed) before every entry was used.</summary>
    public bool HasEntriesAfterHandEnded(int entries) => Actions.Count < entries;

    public IEnumerable<DecodedAction> VoluntaryActions(Blind actor, Street street) =>
        Actions.Where(action => !action.IsBlindPost && action.Actor == actor && action.Street == street);
}
