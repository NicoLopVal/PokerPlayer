using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using PokerPlayer.Application.UseCases;
using PokerPlayer.Domain.Decisions;
using PokerPlayer.Domain.Exceptions;
using PokerPlayer.Domain.Kpis.Calculators;
using PokerPlayer.Infrastructure.Input;

namespace PokerPlayer.Console;

/// <summary>
/// Console entry logic, kept out of Program.cs so it can be tested.
/// Usage: PokerPlayer.Console &lt;game-state.json|.txt&gt; [--pool] [--kpis]
/// Prints the decision ("FOLD", "CHECK", "CALL" or "RAISE {amount}"), or a single "{code}: {reason}" line
/// instead when the input is invalid. --pool also shows every action's probability, --kpis the indicators.
/// </summary>
public static class ConsoleApp
{
    public const int Success = 0;
    public const int UsageError = 1;
    public const int InvalidInput = 2;
    public const int UnexpectedError = 3;

    public static int Run(string[] args, TextWriter output, DecideActionUseCase? useCase = null)
    {
        // The reply must read the same on every machine, whatever its language settings.
        var culture = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        try
        {
            return RunInvariant(args, output, useCase);
        }
        finally
        {
            CultureInfo.CurrentCulture = culture;
        }
    }

    private static int RunInvariant(string[] args, TextWriter output, DecideActionUseCase? useCase)
    {
        var paths = args.Where(arg => !arg.StartsWith("--")).ToArray();
        if (paths.Length != 1)
        {
            output.WriteLine("Usage: PokerPlayer.Console <game-state.json|game-state.txt> [--pool] [--kpis]");
            return UsageError;
        }

        useCase ??= new DecideActionUseCase(new JsonGameStateReader(), new KpiCalculator(), new ActionPoolCalculator(), new ActionSelector());

        try
        {
            var result = useCase.Execute(paths[0]);

            if (args.Contains("--kpis"))
            {
                var json = new JsonSerializerOptions { WriteIndented = true, Converters = { new JsonStringEnumConverter() } };
                output.WriteLine(JsonSerializer.Serialize(result.Kpis, result.Kpis.GetType(), json));
            }

            if (args.Contains("--pool"))
                foreach (var option in result.Pool.Options)
                    output.WriteLine(Describe(option));

            // The reply: always the last line.
            output.WriteLine(result.Decision);
            return Success;
        }
        catch (InvalidInputException exception)
        {
            output.WriteLine(exception.Message);
            return InvalidInput;
        }
        catch (Exception exception)
        {
            // A bug, not bad input: still answer with a code so the caller never gets a stack trace instead of a reply.
            output.WriteLine($"{ErrorCodes.UnexpectedError}: Unexpected error. {exception.Message}");
            return UnexpectedError;
        }
    }

    private static string Describe(ActionOption option)
    {
        var name = option.SuggestedRaise is { } raise ? $"{option.Action} {raise}" : option.Action.ToString();
        // Invariant culture: the reply must not change with the machine's language (71.1% vs 71,1 %).
        var ev = option.ExpectedValue is { } value
            ? string.Format(CultureInfo.InvariantCulture, "EV {0,7:+0.00;-0.00}", value)
            : "not allowed";
        return string.Format(CultureInfo.InvariantCulture, "{0,-10} {1,7:0.0%}   {2}", name, option.Probability, ev);
    }
}
