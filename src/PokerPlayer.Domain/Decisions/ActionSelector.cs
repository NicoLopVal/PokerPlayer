using PokerPlayer.Domain.Enums;

namespace PokerPlayer.Domain.Decisions;

/// <summary>The action to play. Prints as the bot's reply: "FOLD", "CHECK", "CALL" or "RAISE {amount}".</summary>
/// <param name="Amount">For RAISE: chips added on top of any call. Null for other actions.</param>
public sealed record Decision(PlayerAction Action, int? Amount = null)
{
    public override string ToString() => Action == PlayerAction.RAISE ? $"RAISE {Amount}" : Action.ToString();
}

/// <summary>
/// Picks one action from the pool at random, weighted by its probability.
/// One roll in [0, 1) is laid over the cumulative probabilities: with FOLD 0.2, CALL 0.5, RAISE 0.3,
/// a roll below 0.2 folds, below 0.7 calls, otherwise raises. Actions with probability 0 are never picked.
/// The random source is injected so results can be reproduced in tests.
/// </summary>
public sealed class ActionSelector(Random random)
{
    public ActionSelector() : this(new Random()) { }

    public Decision Select(ActionPool pool)
    {
        ArgumentNullException.ThrowIfNull(pool);

        var candidates = pool.Options.Where(option => option.Probability > 0).ToList();
        if (candidates.Count == 0)
            throw new InvalidOperationException("The action pool has no action with a positive probability.");

        // Scale by the actual total so tiny rounding errors in the probabilities can never skip every action.
        var roll = random.NextDouble() * candidates.Sum(option => option.Probability);
        var cumulative = 0.0;
        foreach (var option in candidates)
        {
            cumulative += option.Probability;
            if (roll < cumulative)
                return ToDecision(option);
        }

        return ToDecision(candidates[^1]);
    }

    private static Decision ToDecision(ActionOption option) =>
        new(option.Action, option.Action == PlayerAction.RAISE ? option.SuggestedRaise : null);
}
