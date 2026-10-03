using PokerPlayer.Console;

namespace PokerPlayer.Tests.EndToEnd;

/// <summary>The full pipeline from a file on disk to the console reply.</summary>
public sealed class ConsoleAppTests : IDisposable
{
    private readonly DirectoryInfo _directory = Directory.CreateTempSubdirectory("poker-tests");

    public void Dispose() => _directory.Delete(recursive: true);

    private (int ExitCode, string[] Lines) Run(string fileName, string? content, params string[] extraArgs)
    {
        var path = Path.Combine(_directory.FullName, fileName);
        if (content is not null)
            File.WriteAllText(path, content);

        var output = new StringWriter();
        var exitCode = ConsoleApp.Run([path, .. extraArgs], output);
        return (exitCode, output.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries));
    }

    private static string SampleFile(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "samples", name));

    [Theory]
    [InlineData("preflop.json")]
    [InlineData("flop.txt")]
    public void Samples_ReplyWithASingleDecision(string sample)
    {
        var (exitCode, lines) = Run(sample, SampleFile(sample));

        Assert.Equal(ConsoleApp.Success, exitCode);
        Assert.Matches("^(FOLD|CHECK|CALL|RAISE [0-9]+)$", Assert.Single(lines));
    }

    [Fact]
    public void PoolFlag_PrintsEveryActionThenTheDecision()
    {
        var (exitCode, lines) = Run("flop.txt", SampleFile("flop.txt"), "--pool");

        Assert.Equal(ConsoleApp.Success, exitCode);
        Assert.Equal(5, lines.Length);
        Assert.Equal(["FOLD", "CHECK", "CALL", "RAISE"], lines.Take(4).Select(line => line.Split(' ')[0]));
        Assert.Matches("^(FOLD|CALL|RAISE [0-9]+)$", lines[^1]); // facing a bet: CHECK is never drawn
    }

    [Fact]
    public void KpisFlag_AlsoPrintsIndicators()
    {
        var (exitCode, lines) = Run("flop.txt", SampleFile("flop.txt"), "--kpis");

        Assert.Equal(ConsoleApp.Success, exitCode);
        Assert.Contains(lines, line => line.Contains("\"Equity\""));
    }

    [Fact]
    public void InvalidPot_RepliesWithCodeInsteadOfAction()
    {
        var (exitCode, lines) = Run("bad.json", SampleFile("preflop.json").Replace("\"PotSize\": 3", "\"PotSize\": -5"));

        Assert.Equal(ConsoleApp.InvalidInput, exitCode);
        Assert.Equal("inv05: Invalid pot value. -5 is not a valid pot value, it cannot be negative.", Assert.Single(lines));
    }

    [Fact]
    public void PreflopWithBoardCards_RepliesInv08()
    {
        var json = SampleFile("preflop.json").Replace("[\"\", \"\", \"\", \"\", \"\"]", "[\"7C\", \"JD\", \"2S\", \"\", \"\"]");

        var (exitCode, lines) = Run("bad.json", json);

        Assert.Equal(ConsoleApp.InvalidInput, exitCode);
        Assert.Equal("inv08: Invalid community cards, stage preflop cannot have any community card available, found 3.", Assert.Single(lines));
    }

    [Fact]
    public void MissingFile_RepliesInv01()
    {
        var (exitCode, lines) = Run("missing.json", content: null);

        Assert.Equal(ConsoleApp.InvalidInput, exitCode);
        Assert.StartsWith("inv01: Invalid file.", Assert.Single(lines));
    }

    [Fact]
    public void UnsupportedExtension_RepliesInv01()
    {
        var (_, lines) = Run("state.xml", SampleFile("preflop.json"));

        Assert.StartsWith("inv01: Invalid file. '.xml' is not a supported file type", Assert.Single(lines));
    }

    [Fact]
    public void NoArguments_PrintsUsage()
    {
        var output = new StringWriter();

        Assert.Equal(ConsoleApp.UsageError, ConsoleApp.Run([], output));
        Assert.StartsWith("Usage:", output.ToString());
    }
}
