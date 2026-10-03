using PokerPlayer.Domain.Enums;
using PokerPlayer.Domain.Exceptions;

namespace PokerPlayer.Domain.Players;

/// <summary>A seat at the table.</summary>
public sealed record Player
{
    public const string HeroName = "Hero";

    public string Name { get; }

    /// <summary>Chips the player still has behind (not yet in the pot).</summary>
    public int Stack { get; }

    public Blind Blind { get; }

    /// <summary>Chips already put in the pot this hand, when known.</summary>
    public int? Invested { get; }

    /// <summary>Tendencies, when known. Usually only available for opponents.</summary>
    public PlayerStats? Stats { get; }

    public Player(string name, int stack, Blind blind, int? invested = null, PlayerStats? stats = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new InvalidInputException(ErrorCodes.InvalidPlayers, "Invalid players. Every player must have a name.");
        if (stack < 0)
            throw new InvalidInputException(ErrorCodes.InvalidStack, $"Invalid stack. {stack} is not a valid stack for '{name}', it cannot be negative.");
        if (invested < 0)
            throw new InvalidInputException(ErrorCodes.InvalidInvested, $"Invalid invested value. {invested} is not a valid invested value for '{name}', it cannot be negative.");

        Name = name.Trim();
        Stack = stack;
        Blind = blind;
        Invested = invested;
        Stats = stats;
    }

    public bool IsHero => string.Equals(Name, HeroName, StringComparison.OrdinalIgnoreCase);
}
