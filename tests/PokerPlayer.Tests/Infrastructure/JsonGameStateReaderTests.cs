using PokerPlayer.Domain.Enums;
using PokerPlayer.Domain.Exceptions;
using PokerPlayer.Infrastructure.Input;

namespace PokerPlayer.Tests.Infrastructure;

public class JsonGameStateReaderTests
{
    private const string ValidJson = """
        {
          "HandCards": ["2D", "7H"],
          "CommunityCards": ["7C", "JH", "AS", "", ""],
          "Street": "Flop",
          "PotSize": 25,
          "BetToCall": 0,
          "ActionHistory": ["bet", "raise", "raise", "call"],
          "Players": [
            { "Name": "Hero", "Stack": 50, "Blinds": "SB" },
            { "Name": "Villain", "Stack": 65, "Blinds": "BB",
              "Stats": { "VPIP": 0.24, "PFR": 0.20, "ThreeBet": 0.08, "AF": 2.5 } }
          ]
        }
        """;

    [Fact]
    public void Parse_MapsAllFields()
    {
        var state = JsonGameStateReader.Parse(ValidJson);

        Assert.Equal("2D 7H", state.Hand.ToString());
        Assert.Equal(3, state.CommunityCards.Revealed.Count);
        Assert.Null(state.CommunityCards.Slots[3]);
        Assert.Equal(Street.Flop, state.Street);
        Assert.Equal(25, state.PotSize);
        Assert.Equal(2, state.BigBlind); // default
        Assert.Equal(Blind.SB, state.Hero.Blind);
        Assert.Equal(0.08, Assert.Single(state.Villains).Stats!.ThreeBet);
        Assert.Null(state.Villains.Single().Stats!.FoldToCBet);
    }

    [Fact]
    public void Parse_ReadsOptionalFoldStats()
    {
        var json = ValidJson.Replace("\"AF\": 2.5", "\"AF\": 2.5, \"FoldToThreeBet\": 0.6, \"FoldToCBet\": 0.4");

        var stats = JsonGameStateReader.Parse(json).Villains.Single().Stats!;

        Assert.Equal(0.6, stats.FoldToThreeBet);
        Assert.Equal(0.4, stats.FoldToCBet);
    }

    [Fact]
    public void Read_AcceptsJsonAndTxt_RejectsOtherExtensions()
    {
        var directory = Directory.CreateTempSubdirectory();
        try
        {
            foreach (var extension in new[] { ".json", ".txt" })
            {
                var path = Path.Combine(directory.FullName, "state" + extension);
                File.WriteAllText(path, ValidJson);
                Assert.Equal(25, new JsonGameStateReader().Read(path).PotSize);
            }

            var xml = Path.Combine(directory.FullName, "state.xml");
            File.WriteAllText(xml, ValidJson);
            Assert.Throws<InvalidInputException>(() => new JsonGameStateReader().Read(xml));
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public void InvalidCard_ThrowsInvalidInputException()
    {
        Assert.Throws<InvalidInputException>(() => JsonGameStateReader.Parse(ValidJson.Replace("\"2D\"", "\"1X\"")));
    }

    [Fact]
    public void MissingField_ThrowsInvalidInput()
    {
        Assert.Throws<InvalidInputException>(() => JsonGameStateReader.Parse(ValidJson.Replace("\"PotSize\": 25,", "")));
    }

    [Fact]
    public void MalformedJson_ThrowsInvalidInput()
    {
        Assert.Throws<InvalidInputException>(() => JsonGameStateReader.Parse("{ not json"));
    }
}
