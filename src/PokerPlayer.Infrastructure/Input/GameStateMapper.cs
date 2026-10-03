using PokerPlayer.Domain;
using PokerPlayer.Domain.Cards;
using PokerPlayer.Domain.Enums;
using PokerPlayer.Domain.Exceptions;
using PokerPlayer.Domain.Players;

namespace PokerPlayer.Infrastructure.Input;

/// <summary>Turns the raw DTO into domain objects. Empty strings on the board become unrevealed (null) cards.</summary>
public static class GameStateMapper
{
    public static GameState ToDomain(GameStateDto dto)
    {
        var handCards = Required(dto.HandCards, nameof(dto.HandCards));
        if (handCards.Length != 2)
            throw new InvalidInputException(ErrorCodes.InvalidHand,
                $"Invalid hand. HandCards must contain exactly 2 cards, got {handCards.Length}.");

        var board = Required(dto.CommunityCards, nameof(dto.CommunityCards))
            .Select(text => string.IsNullOrWhiteSpace(text) ? (Card?)null : Card.Parse(text))
            .ToArray();

        var players = Required(dto.Players, nameof(dto.Players))
            .Select((player, index) => ToDomain(player ?? throw Missing($"Players[{index}]")))
            .ToArray();

        var history = dto.ActionHistory ?? [];
        var missingAction = Array.FindIndex(history, action => action is null);
        if (missingAction >= 0)
            throw new InvalidInputException(ErrorCodes.InvalidActionHistory,
                $"Invalid action history. Entry {missingAction} is empty.");

        return new GameState(
            new Hand(Card.Parse(handCards[0]), Card.Parse(handCards[1])),
            new CommunityCards(board),
            ParseEnum<Street>(Required(dto.Street, nameof(dto.Street)), ErrorCodes.InvalidStreet, "street"),
            players,
            RequiredValue(dto.PotSize, nameof(dto.PotSize)),
            RequiredValue(dto.BetToCall, nameof(dto.BetToCall)),
            history,
            dto.BigBlind,
            dto.TotalChips);
    }

    private static Player ToDomain(PlayerDto dto) =>
        new(
            Required(dto.Name, "Players.Name"),
            RequiredValue(dto.Stack, "Players.Stack"),
            ParseEnum<Blind>(Required(dto.Blinds, "Players.Blinds"), ErrorCodes.InvalidBlinds, "blind"),
            dto.Invested,
            dto.Stats is null ? null : ToDomain(dto.Stats));

    private static PlayerStats ToDomain(PlayerStatsDto dto) =>
        new(
            RequiredValue(dto.Vpip, "Stats.VPIP"),
            RequiredValue(dto.Pfr, "Stats.PFR"),
            RequiredValue(dto.ThreeBet, "Stats.ThreeBet"),
            RequiredValue(dto.Af, "Stats.AF"),
            dto.FoldToThreeBet,
            dto.FoldToCBet);

    private static T Required<T>(T? value, string field) where T : class => value ?? throw Missing(field);

    private static T RequiredValue<T>(T? value, string field) where T : struct => value ?? throw Missing(field);

    private static InvalidInputException Missing(string field) =>
        new(ErrorCodes.MissingField, $"Missing required field. '{field}' is required.");

    private static T ParseEnum<T>(string text, string code, string name) where T : struct, Enum =>
        Enum.TryParse<T>(text.Trim(), ignoreCase: true, out var value) && Enum.IsDefined(value) && !int.TryParse(text, out _)
            ? value
            : throw new InvalidInputException(code,
                $"Invalid {name}. '{text}' is not a valid {name}, expected one of: {string.Join(", ", Enum.GetNames<T>())}.");
}
