using PokerPlayer.Application.Abstractions;
using PokerPlayer.Domain.Kpis;
using PokerPlayer.Domain.Kpis.Calculators;
using PokerPlayer.Domain.Validation;

namespace PokerPlayer.Application.UseCases;

/// <summary>Reads and validates a game state and builds the indicators for its street.</summary>
public sealed class EvaluateKpisUseCase(IGameStateReader reader, KpiCalculator calculator)
{
    public StreetKpis Execute(string inputPath)
    {
        var state = reader.Read(inputPath);
        GameStateValidator.Validate(state);
        return calculator.Calculate(state);
    }
}
