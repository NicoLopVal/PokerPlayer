using System.Text.Json.Nodes;
using PokerPlayer.Domain.Enums;
using PokerPlayer.Domain.Exceptions;
using PokerPlayer.Domain.History;
using PokerPlayer.Domain.Validation;
using PokerPlayer.Infrastructure.Input;

namespace PokerPlayer.Tests.Validation;

/// <summary>
/// Every way the input can be wrong must produce "{code}: {reason}" instead of an action.
/// Each case starts from a valid flop state and breaks exactly one thing.
/// </summary>
public class InvalidInputTests
{
    private const string ValidJson = """
        {
          "HandCards": ["AH", "KH"],
          "CommunityCards": ["7C", "JH", "2S", "", ""],
          "Street": "Flop",
          "PotSize": 20,
          "BetToCall": 5,
          "BigBlind": 2,
          "ActionHistory": ["bet", "raise", "raise", "call", "raise"],
          "Players": [
            { "Name": "Hero", "Stack": 50, "Blinds": "SB" },
            { "Name": "Villain", "Stack": 65, "Blinds": "BB",
              "Stats": { "VPIP": 0.24, "PFR": 0.20, "ThreeBet": 0.08, "AF": 2.5 } }
          ]
        }
        """;

    /// <summary>Parse + validate, exactly as the application does, returning the bot's reply.</summary>
    private static string Reply(string json)
    {
        try
        {
            GameStateValidator.Validate(JsonGameStateReader.Parse(json));
            return "OK";
        }
        catch (InvalidInputException exception)
        {
            return exception.Message;
        }
    }

    private static string Reply(Action<JsonObject> mutate)
    {
        var root = JsonNode.Parse(ValidJson)!.AsObject();
        mutate(root);
        return Reply(root.ToJsonString());
    }

    private static JsonObject Hero(JsonObject root) => root["Players"]![0]!.AsObject();
    private static JsonObject Villain(JsonObject root) => root["Players"]![1]!.AsObject();
    private static JsonObject Stats(JsonObject root) => Villain(root)["Stats"]!.AsObject();

    private static JsonArray Array(params string[] values) => new(values.Select(value => (JsonNode?)JsonValue.Create(value)).ToArray());

    [Fact]
    public void BaseInput_IsValid()
    {
        Assert.Equal("OK", Reply(ValidJson));
    }

    // 1.1 Card format

    [Theory]
    [InlineData("1H")]
    [InlineData("10H")]
    [InlineData("AX")]
    [InlineData("A")]
    [InlineData("AHH")]
    [InlineData("HA")]
    [InlineData("")]
    public void HandCard_WithInvalidFormat_IsInv04(string card)
    {
        var reply = Reply(root => root["HandCards"] = Array(card, "KH"));

        Assert.StartsWith("inv04: Invalid card format.", reply);
        Assert.Contains($"'{card}'", reply);
    }

    [Theory]
    [InlineData("ZZ")]
    [InlineData("7c7")]
    [InlineData("11C")]
    public void CommunityCard_WithInvalidFormat_IsInv04(string card)
    {
        Assert.StartsWith("inv04:", Reply(root => root["CommunityCards"] = Array("7C", card, "2S", "", "")));
    }

    [Fact]
    public void Card_LowerCase_IsAccepted()
    {
        Assert.Equal("OK", Reply(root => root["HandCards"] = Array("ah", "kh")));
    }

    [Fact]
    public void Card_AsNumber_IsInv02()
    {
        Assert.StartsWith("inv02:", Reply(root => root["HandCards"] = new JsonArray(7, "KH")));
    }

    // 1.2 Structure: required fields, types and values that make sense

    [Theory]
    [InlineData("HandCards")]
    [InlineData("CommunityCards")]
    [InlineData("Street")]
    [InlineData("PotSize")]
    [InlineData("BetToCall")]
    [InlineData("Players")]
    public void MissingTopLevelField_IsInv03(string field)
    {
        var reply = Reply(root => root.Remove(field));

        Assert.Equal($"inv03: Missing required field. '{field}' is required.", reply);
    }

    [Theory]
    [InlineData("Name", "Players.Name")]
    [InlineData("Stack", "Players.Stack")]
    [InlineData("Blinds", "Players.Blinds")]
    public void MissingPlayerField_IsInv03(string field, string path)
    {
        Assert.Equal($"inv03: Missing required field. '{path}' is required.", Reply(root => Hero(root).Remove(field)));
    }

    [Theory]
    [InlineData("VPIP")]
    [InlineData("PFR")]
    [InlineData("ThreeBet")]
    [InlineData("AF")]
    public void MissingStat_IsInv03(string stat)
    {
        Assert.StartsWith("inv03:", Reply(root => Stats(root).Remove(stat)));
    }

    [Fact]
    public void OptionalFields_CanBeOmitted()
    {
        var reply = Reply(root =>
        {
            root.Remove("BigBlind");
            root.Remove("ActionHistory");
            Villain(root).Remove("Stats");
            root["PotSize"] = 0;
            root["BetToCall"] = 0;
        });

        Assert.Equal("OK", reply);
    }

    [Fact]
    public void NullPlayerEntry_IsInv03()
    {
        Assert.Equal("inv03: Missing required field. 'Players[1]' is required.", Reply(root => root["Players"]![1] = null));
    }

    [Theory]
    [InlineData("PotSize", "\"twenty\"")]
    [InlineData("PotSize", "20.5")]
    [InlineData("BetToCall", "true")]
    [InlineData("HandCards", "\"AH KH\"")]
    [InlineData("Players", "{}")]
    public void FieldWithWrongType_IsInv02(string field, string rawValue)
    {
        var reply = Reply(root => root[field] = JsonNode.Parse(rawValue));

        Assert.StartsWith("inv02: Malformed input.", reply);
        Assert.Contains(field, reply);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("null")]
    [InlineData("{ not json")]
    [InlineData("[1, 2, 3]")]
    public void NotAGameStateDocument_IsInv02(string json)
    {
        Assert.StartsWith("inv02:", Reply(json));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    public void HandWithWrongCardCount_IsInv09(int count)
    {
        var cards = new[] { "AH", "KH", "QH" }.Take(count).ToArray();

        Assert.Equal($"inv09: Invalid hand. HandCards must contain exactly 2 cards, got {count}.", Reply(root => root["HandCards"] = Array(cards)));
    }

    [Theory]
    [InlineData("Showdown")]
    [InlineData("1")]
    [InlineData("")]
    public void UnknownStreet_IsInv10(string street)
    {
        Assert.StartsWith("inv10: Invalid street.", Reply(root => root["Street"] = street));
    }

    [Fact]
    public void Street_IsCaseInsensitive()
    {
        Assert.Equal("OK", Reply(root => root["Street"] = "FLOP"));
    }

    [Fact]
    public void NoHero_IsInv12()
    {
        Assert.Equal("inv12: Invalid players. Exactly one player must be named 'Hero', found 0.", Reply(root => Hero(root)["Name"] = "Alice"));
    }

    [Fact]
    public void TwoHeroes_IsInv12()
    {
        Assert.StartsWith("inv12:", Reply(root => Villain(root)["Name"] = "hero"));
    }

    [Fact]
    public void SinglePlayer_IsInv12()
    {
        Assert.StartsWith("inv12:", Reply(root => root["Players"]!.AsArray().RemoveAt(1)));
    }

    [Fact]
    public void EmptyPlayerName_IsInv12()
    {
        Assert.StartsWith("inv12:", Reply(root => Villain(root)["Name"] = " "));
    }

    [Fact]
    public void NegativeStack_IsInv13()
    {
        Assert.Equal("inv13: Invalid stack. -1 is not a valid stack for 'Villain', it cannot be negative.", Reply(root => Villain(root)["Stack"] = -1));
    }

    [Fact]
    public void ZeroStack_IsAllowed_HeroIsAllIn()
    {
        Assert.Equal("OK", Reply(root => Hero(root)["Stack"] = 0));
    }

    [Theory]
    [InlineData("UTG")]
    [InlineData("BTN")]
    [InlineData("0")]
    public void UnknownBlind_IsInv14(string blind)
    {
        Assert.StartsWith("inv14: Invalid blind.", Reply(root => Hero(root)["Blinds"] = blind));
    }

    [Fact]
    public void BothPlayersSameBlind_IsInv14()
    {
        Assert.Equal("inv14: Invalid blinds. Only one player can be BB, found 2.", Reply(root => Hero(root)["Blinds"] = "BB"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-2)]
    public void BigBlindBelowOneChip_IsInv15(int bigBlind)
    {
        Assert.StartsWith("inv15: Invalid big blind.", Reply(root => root["BigBlind"] = bigBlind));
    }

    [Theory]
    [InlineData("VPIP", 1.5)]
    [InlineData("PFR", -0.1)]
    [InlineData("ThreeBet", 2)]
    [InlineData("AF", -1)]
    [InlineData("FoldToCBet", 1.01)]
    [InlineData("FoldToThreeBet", -0.5)]
    public void StatOutOfRange_IsInv16(string stat, double value)
    {
        Assert.StartsWith("inv16: Invalid player stats.", Reply(root => Stats(root)[stat] = value));
    }

    [Fact]
    public void NegativeInvested_IsInv17()
    {
        Assert.StartsWith("inv17:", Reply(root => Hero(root)["Invested"] = -3));
    }

    [Fact]
    public void NegativeBetToCall_IsInv06()
    {
        Assert.Equal("inv06: Invalid bet to call. -1 is not a valid bet to call, it cannot be negative.", Reply(root => root["BetToCall"] = -1));
    }

    [Fact]
    public void BetToCallLargerThanPot_IsInv06()
    {
        Assert.StartsWith("inv06: Invalid bet to call. 25 is not a valid bet to call, it cannot exceed the pot (20)",
            Reply(root => root["BetToCall"] = 25));
    }

    [Fact]
    public void SameCardInHandAndBoard_IsInv07()
    {
        Assert.Equal("inv07: Duplicated card. AH appears in both the hand and the community cards.",
            Reply(root => root["CommunityCards"] = Array("7C", "AH", "2S", "", "")));
    }

    [Fact]
    public void SameCardTwiceInHand_IsInv07()
    {
        Assert.StartsWith("inv07:", Reply(root => root["HandCards"] = Array("AH", "AH")));
    }

    [Fact]
    public void SameCardTwiceOnBoard_IsInv07()
    {
        Assert.StartsWith("inv07:", Reply(root => root["CommunityCards"] = Array("7C", "7C", "2S", "", "")));
    }

    // 1.3 Community cards vs street

    [Fact]
    public void Preflop_WithRevealedCards_IsInv08()
    {
        var reply = Reply(root => root["Street"] = "Preflop");

        Assert.Equal("inv08: Invalid community cards, stage preflop cannot have any community card available, found 3.", reply);
    }

    [Theory]
    [InlineData("Flop", new[] { "", "", "", "", "" }, 3, 0)]
    [InlineData("Flop", new[] { "7C", "JH", "2S", "9D", "" }, 3, 4)]
    [InlineData("Turn", new[] { "7C", "JH", "2S", "", "" }, 4, 3)]
    [InlineData("River", new[] { "7C", "JH", "2S", "9D", "" }, 5, 4)]
    [InlineData("River", new[] { "", "", "", "", "" }, 5, 0)]
    public void BoardNotMatchingStreet_IsInv08(string street, string[] board, int expected, int found)
    {
        var reply = Reply(root =>
        {
            root["Street"] = street;
            root["CommunityCards"] = Array(board);
        });

        Assert.Equal($"inv08: Invalid community cards, stage {street.ToLowerInvariant()} must have exactly {expected} community cards available, found {found}.", reply);
    }

    [Theory]
    [InlineData("7C,,2S,,")]          // gap
    [InlineData("7C,JH,,,")]          // two cards is not a board
    [InlineData(",,,,9D")]            // revealed out of order
    [InlineData("7C,JH,2S,")]         // four slots
    [InlineData("7C,JH,2S,,,")]       // six slots
    public void MalformedBoard_IsInv08(string board)
    {
        Assert.StartsWith("inv08: Invalid community cards", Reply(root => root["CommunityCards"] = Array(board.Split(','))));
    }

    [Fact]
    public void NullBoardSlots_AreTreatedAsUnrevealed()
    {
        Assert.Equal("OK", Reply(root => root["CommunityCards"] = new JsonArray("7C", "JH", "2S", null, null)));
    }

    // 1.4 Action history

    [Theory]
    [InlineData("dance")]
    [InlineData("")]
    [InlineData("raise 20")]
    [InlineData("chek")]
    public void UnknownAction_IsInv11(string action)
    {
        var reply = Reply(root => root["ActionHistory"] = Array("bet", "raise", "raise", action));

        Assert.Equal($"inv11: Invalid action history. '{action}' is not a valid action, expected fold, check, call, bet, raise or allin.", reply);
    }

    [Fact]
    public void NullAction_IsInv11()
    {
        Assert.Equal("inv11: Invalid action history. Entry 2 is empty.",
            Reply(root => root["ActionHistory"] = new JsonArray("bet", "raise", null, "call")));
    }

    [Theory]
    [InlineData("fold", PlayerAction.FOLD)]
    [InlineData("CHECK", PlayerAction.CHECK)]
    [InlineData("Call", PlayerAction.CALL)]
    [InlineData("raise", PlayerAction.RAISE)]
    [InlineData("bet", PlayerAction.RAISE)]
    [InlineData("allin", PlayerAction.RAISE)]
    [InlineData("All-In", PlayerAction.RAISE)]
    [InlineData("all in", PlayerAction.RAISE)]
    public void KnownActions_AreRecognized(string raw, PlayerAction expected)
    {
        Assert.Equal(expected, ActionHistoryDecoder.ParseAction(raw));
    }

    // 1.4 History vs street: the history must describe the decision being asked

    /// <summary>A state on the given street, Hero holding the given blind, with matching board cards.</summary>
    private static string ReplyFor(string history, string street, string heroBlind, int betToCall, int? bigBlind = 2)
    {
        var board = street switch
        {
            "Preflop" => Array("", "", "", "", ""),
            "Flop" => Array("7C", "JH", "2S", "", ""),
            "Turn" => Array("7C", "JH", "2S", "9D", ""),
            _ => Array("7C", "JH", "2S", "9D", "4S")
        };

        return Reply(root =>
        {
            root["ActionHistory"] = Array(history.Split(','));
            root["Street"] = street;
            root["CommunityCards"] = board;
            root["BetToCall"] = betToCall;
            root["BigBlind"] = bigBlind;
            Hero(root)["Blinds"] = heroBlind;
            Villain(root)["Blinds"] = heroBlind == "SB" ? "BB" : "SB";
        });
    }

    [Theory]
    [InlineData("bet,raise", "Preflop", "SB", 1)]                                  // SB faces the big blind
    [InlineData("bet,raise,call", "Preflop", "BB", 0)]                             // SB limped: BB has the option
    [InlineData("bet,raise,raise", "Preflop", "BB", 4)]                            // SB raised
    [InlineData("bet,raise,allin", "Preflop", "BB", 15)]                           // all-in counts as a raise
    [InlineData("bet,raise,call,check", "Flop", "BB", 0)]                          // BB acts first after the flop
    [InlineData("bet,raise,raise,call,check", "Flop", "SB", 0)]                    // BB checked to Hero
    [InlineData("bet,raise,raise,call,bet", "Flop", "SB", 5)]                      // BB bet into Hero
    [InlineData("bet,raise,raise,call,check,check", "Turn", "BB", 0)]              // flop checked through
    [InlineData("bet,raise,call,check,check,check,check,check,check", "River", "SB", 0)]
    public void ConsistentHistories_AreAccepted(string history, string street, string heroBlind, int betToCall)
    {
        Assert.Equal("OK", ReplyFor(history, street, heroBlind, betToCall));
    }

    [Fact]
    public void BigBlindOfOneChip_SmallBlindCompletesForFree()
    {
        // BB 1 → SB 1: the small blind has nothing to call on its first action.
        Assert.Equal("OK", ReplyFor("bet,raise", "Preflop", "SB", 0, bigBlind: 1));
    }

    [Fact]
    public void HistoryEndingOnAnotherStreet_IsInv11()
    {
        Assert.Equal("inv11: Invalid action history. The actions end on the preflop, but the street is flop.",
            ReplyFor("bet,raise", "Flop", "SB", 1));
    }

    [Fact]
    public void NotHerosTurn_IsInv11()
    {
        Assert.Equal("inv11: Invalid action history. After these actions it is SB's turn, but Hero is BB.",
            ReplyFor("bet,raise", "Preflop", "BB", 1));
    }

    [Fact]
    public void HandAlreadyFolded_IsInv11()
    {
        Assert.Equal("inv11: Invalid action history. The hand is already over, SB folded. There is no decision left for Hero.",
            ReplyFor("bet,raise,fold", "Preflop", "BB", 0));
    }

    [Fact]
    public void RiverAlreadyClosed_IsInv11()
    {
        Assert.Equal("inv11: Invalid action history. The hand is already over, the river betting round is closed. There is no decision left for Hero.",
            ReplyFor("bet,raise,call,check,check,check,check,check,check,check", "River", "SB", 0));
    }

    [Fact]
    public void ActionsAfterTheHandEnded_IsInv11()
    {
        Assert.Equal("inv11: Invalid action history. The hand ended at action 3 ('fold' by SB on the preflop), but 2 more action(s) follow.",
            ReplyFor("bet,raise,fold,call,check", "Preflop", "BB", 0));
    }

    [Fact]
    public void OnlyOneBlindPosted_IsInv11()
    {
        Assert.Equal("inv11: Invalid action history. It must start with both blind posts (small blind, then big blind), found only 1 entry.",
            ReplyFor("bet", "Preflop", "BB", 1));
    }

    [Fact]
    public void FacingABetWithNothingToCall_IsInv06()
    {
        Assert.Equal("inv06: Invalid bet to call. 0 is not a valid bet to call, the action history shows Hero is facing a bet.",
            ReplyFor("bet,raise,raise,call,bet", "Flop", "SB", 0));
    }

    [Fact]
    public void SomethingToCallWhenCheckedTo_IsInv06()
    {
        Assert.Equal("inv06: Invalid bet to call. 5 is not a valid bet to call, the action history shows Hero is not facing a bet.",
            ReplyFor("bet,raise,raise,call,check", "Flop", "SB", 5));
    }

    // Stat consistency

    [Fact]
    public void PfrAboveVpip_IsInv16()
    {
        var reply = Reply(root =>
        {
            Stats(root)["VPIP"] = 0.20;
            Stats(root)["PFR"] = 0.30;
        });

        Assert.Equal("inv16: Invalid player stats. PFR (0.3) cannot be greater than VPIP (0.2), every preflop raise also counts as voluntarily playing the hand.", reply);
    }

    [Fact]
    public void PfrEqualToVpip_IsAllowed()
    {
        Assert.Equal("OK", Reply(root => Stats(root)["PFR"] = 0.24));
    }

    // Chip conservation: one chip is one chip; stacks + pot must equal the chips at the table

    [Fact]
    public void ChipCount_Matching_IsAccepted()
    {
        Assert.Equal("OK", Reply(root => root["TotalChips"] = 50 + 65 + 20));
    }

    [Fact]
    public void ChipCount_Mismatch_IsInv18()
    {
        Assert.Equal("inv18: Invalid chip count. Stacks (50 + 65) plus pot (20) add up to 135, but the total chips in play is 140.",
            Reply(root => root["TotalChips"] = 140));
    }

    [Fact]
    public void ChipCount_Negative_IsInv18()
    {
        Assert.StartsWith("inv18:", Reply(root => root["TotalChips"] = -1));
    }

    [Fact]
    public void ChipCount_Omitted_IsNotChecked()
    {
        Assert.Equal("OK", Reply(ValidJson));
    }

    // 1.5 Pot

    [Fact]
    public void NegativePot_IsInv05()
    {
        Assert.Equal("inv05: Invalid pot value. -5 is not a valid pot value, it cannot be negative.", Reply(root => root["PotSize"] = -5));
    }

    [Fact]
    public void ZeroPot_WithHistory_IsInv05()
    {
        var reply = Reply(root =>
        {
            root["PotSize"] = 0;
            root["BetToCall"] = 0;
        });

        Assert.Equal("inv05: Invalid pot value. 0 is not a valid pot value when the action history has 5 action(s).", reply);
    }

    [Fact]
    public void PotSmallerThanPostedBlinds_IsInv05()
    {
        var reply = Reply(root =>
        {
            root["PotSize"] = 2;
            root["BetToCall"] = 0;
        });

        Assert.Equal("inv05: Invalid pot value. 2 is not a valid pot value, the posted blinds alone add up to 3.", reply);
    }

    [Fact]
    public void ZeroPot_WithoutHistory_IsAllowed()
    {
        var reply = Reply(root =>
        {
            root["ActionHistory"] = new JsonArray();
            root["PotSize"] = 0;
            root["BetToCall"] = 0;
        });

        Assert.Equal("OK", reply);
    }

    // Codes are unique and documented

    [Fact]
    public void ErrorCodes_AreUnique()
    {
        var codes = typeof(ErrorCodes).GetFields().Select(field => (string)field.GetValue(null)!).ToList();

        Assert.Equal(codes.Count, codes.Distinct().Count());
        Assert.All(codes, code => Assert.Matches("^(inv|err)[0-9]{2}$", code));
    }
}
