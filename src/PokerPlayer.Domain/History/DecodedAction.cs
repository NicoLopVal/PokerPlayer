using PokerPlayer.Domain.Enums;

namespace PokerPlayer.Domain.History;

/// <summary>One history entry with the actor and street it belongs to.</summary>
/// <param name="IsBlindPost">True for the two forced bets that open the hand.</param>
public sealed record DecodedAction(Blind Actor, Street Street, PlayerAction Action, bool IsBlindPost, string Raw);
