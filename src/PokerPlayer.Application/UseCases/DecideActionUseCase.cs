using PokerPlayer.Application.Abstractions;
using PokerPlayer.Domain.Decisions;
using PokerPlayer.Domain.Kpis;
using PokerPlayer.Domain.Kpis.Calculators;
using PokerPlayer.Domain.Validation;

namespace PokerPlayer.Application.UseCases;

/// <param name="Kpis">Indicators for the street.</param>
/// <param name="Pool">Every action with its probability.</param>
/// <param name="Decision">The action drawn from the pool.</param>
public sealed record DecisionResult(StreetKpis Kpis, ActionPool Pool, Decision Decision);

/// <summary>
/// Full turn: read and validate the game state, build its indicators, turn them into an action pool,
/// and draw the action to play from it.
/// </summary>
public sealed class DecideActionUseCase(
    IGameStateReader reader,
    KpiCalculator kpiCalculator,
    ActionPoolCalculator poolCalculator,
    ActionSelector selector)
{
    public DecisionResult Execute(string inputPath)
    {
        var state = reader.Read(inputPath);
        GameStateValidator.Validate(state);
        var kpis = kpiCalculator.Calculate(state);
        var pool = poolCalculator.Calculate(state, kpis);
        return new DecisionResult(kpis, pool, selector.Select(pool));
    }
}
