using PokerPlayer.Domain.Enums;

namespace PokerPlayer.Domain.Kpis;

/// <summary>Indicators shared by every street. Each street adds its own on top.</summary>
public abstract record StreetKpis
{
    public required Street Street { get; init; }

    /// <summary>Cards Hero cannot see: 52 minus hand and revealed board.</summary>
    public required int UnseenCards { get; init; }

    public required StackKpis Stack { get; init; }

    /// <summary>Pricing of the bet Hero faces; null when Hero can check.</summary>
    public required FacingBetKpis? FacingBet { get; init; }

    /// <summary>Bluff and value maths for three reference bet/raise sizes.</summary>
    public required IReadOnlyList<ReferenceBetKpis> ReferenceBets { get; init; }
}

public sealed record PreflopKpis : StreetKpis
{
    public required PreflopHandStrengthKpis HandStrength { get; init; }

    public required PreflopEquityKpis Equity { get; init; }

    /// <summary>Bet per street that gets all chips in by the river with equal pot-fraction bets.</summary>
    public required double GeometricBetSize { get; init; }
}

public sealed record FlopKpis : StreetKpis
{
    public required DrawKpis Draws { get; init; }
    public required EquityKpis Equity { get; init; }
    public required double GeometricBetSize { get; init; }
}

public sealed record TurnKpis : StreetKpis
{
    public required DrawKpis Draws { get; init; }
    public required EquityKpis Equity { get; init; }
    public required double GeometricBetSize { get; init; }
}

public sealed record RiverKpis : StreetKpis
{
    /// <summary>On the river equity equals hand strength; there are no cards to come.</summary>
    public required EquityKpis Equity { get; init; }
}
