namespace PokerPlayer.Infrastructure.Input;

/// <summary>Raw shape of the input file. Everything is nullable so missing fields can be reported clearly.</summary>
public sealed class GameStateDto
{
    public string[]? HandCards { get; set; }
    public string?[]? CommunityCards { get; set; }
    public string? Street { get; set; }
    public int? PotSize { get; set; }
    public int? BetToCall { get; set; }
    public int? BigBlind { get; set; }
    public int? TotalChips { get; set; }
    public string[]? ActionHistory { get; set; }
    public PlayerDto[]? Players { get; set; }
}

public sealed class PlayerDto
{
    public string? Name { get; set; }
    public int? Stack { get; set; }
    public string? Blinds { get; set; }
    public int? Invested { get; set; }
    public PlayerStatsDto? Stats { get; set; }
}

public sealed class PlayerStatsDto
{
    public double? Vpip { get; set; }
    public double? Pfr { get; set; }
    public double? ThreeBet { get; set; }
    public double? Af { get; set; }
    public double? FoldToThreeBet { get; set; }
    public double? FoldToCBet { get; set; }
}
