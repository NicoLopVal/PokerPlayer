using PokerPlayer.Domain.Enums;

namespace PokerPlayer.Domain.Decisions;

/// <param name="Probability">Chance of picking this action (0..1). Exactly 0 when the action is not allowed.</param>
/// <param name="ExpectedValue">EV in chips relative to folding now, after marker adjustments. Null when not allowed.</param>
/// <param name="SuggestedRaise">For RAISE: the reference size (share of the pot, in chips) with the best EV. Null otherwise.</param>
public sealed record ActionOption(PlayerAction Action, double Probability, double? ExpectedValue, int? SuggestedRaise)
{
    public bool IsAllowed => ExpectedValue is not null;
}

/// <summary>All four actions with their selection probabilities; the probabilities sum to 1.</summary>
public sealed record ActionPool(IReadOnlyList<ActionOption> Options)
{
    public ActionOption this[PlayerAction action] => Options.Single(option => option.Action == action);

    public ActionOption Best => Options.MaxBy(option => option.Probability)!;
}
