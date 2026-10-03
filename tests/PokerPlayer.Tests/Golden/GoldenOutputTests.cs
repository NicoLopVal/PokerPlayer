using System.Runtime.CompilerServices;
using PokerPlayer.Application.UseCases;
using PokerPlayer.Console;
using PokerPlayer.Domain.Decisions;
using PokerPlayer.Domain.Kpis.Calculators;
using PokerPlayer.Infrastructure.Input;

namespace PokerPlayer.Tests.Golden;

/// <summary>
/// Frozen replies for reference hands. Each Cases/{name}.json is run through the console with a fixed seed
/// and compared with Cases/{name}.expected.txt. Any change in the indicators, the decision engine or the
/// validation shows up as a diff of these files.
///
/// When a change is intended, regenerate and review the diff before committing:
///   $env:POKER_UPDATE_GOLDEN = "1"; dotnet test --filter GoldenOutputTests; Remove-Item Env:POKER_UPDATE_GOLDEN
/// </summary>
public class GoldenOutputTests
{
    private const string UpdateVariable = "POKER_UPDATE_GOLDEN";

    public static TheoryData<string> Cases() => [.. Directory.GetFiles(CasesDirectory(), "*.json").Select(Path.GetFileNameWithoutExtension).Order()!];

    [Theory]
    [MemberData(nameof(Cases))]
    public void Reply_MatchesGoldenFile(string name)
    {
        var input = Path.Combine(CasesDirectory(), name + ".json");
        var expectedPath = Path.Combine(CasesDirectory(), name + ".expected.txt");

        var actual = Run(input);

        if (Environment.GetEnvironmentVariable(UpdateVariable) == "1")
        {
            File.WriteAllText(expectedPath, actual);
            return;
        }

        Assert.True(File.Exists(expectedPath), $"Missing {name}.expected.txt. Set {UpdateVariable}=1 and rerun to create it, then review it.");
        Assert.Equal(Normalize(File.ReadAllText(expectedPath)), Normalize(actual));
    }

    [Fact]
    public void EveryExpectedFile_HasAnInput()
    {
        var orphans = Directory.GetFiles(CasesDirectory(), "*.expected.txt")
            .Select(path => Path.GetFileName(path).Replace(".expected.txt", ""))
            .Where(name => !File.Exists(Path.Combine(CasesDirectory(), name + ".json")));

        Assert.Empty(orphans);
    }

    private static string Run(string inputPath)
    {
        // Fixed seeds so the simulation and the final draw, and therefore the reply, are reproducible.
        var useCase = new DecideActionUseCase(
            new JsonGameStateReader(),
            new KpiCalculator(new KpiCalculatorOptions(Seed: 20261003)),
            new ActionPoolCalculator(),
            new ActionSelector(new Random(20261003)));

        var output = new StringWriter { NewLine = "\n" };
        var exitCode = ConsoleApp.Run([inputPath, "--pool"], output, useCase);
        return $"exit {exitCode}\n{output}";
    }

    private static string Normalize(string text) => text.Replace("\r\n", "\n").TrimEnd();

    private static string CasesDirectory([CallerFilePath] string thisFile = "") =>
        Path.Combine(Path.GetDirectoryName(thisFile)!, "Cases");
}
