namespace PokerPlayer.Domain.Evaluation;

/// <summary>
/// Comparable strength of a best-five-card hand. Higher wins, equal ties.
/// Layout: category in bits 20+, then up to five tie-break ranks of 4 bits each.
/// </summary>
public readonly record struct HandValue(int Value) : IComparable<HandValue>
{
    public HandCategory Category => (HandCategory)(Value >> 20);

    public int CompareTo(HandValue other) => Value.CompareTo(other.Value);

    public static bool operator >(HandValue left, HandValue right) => left.Value > right.Value;
    public static bool operator <(HandValue left, HandValue right) => left.Value < right.Value;
    public static bool operator >=(HandValue left, HandValue right) => left.Value >= right.Value;
    public static bool operator <=(HandValue left, HandValue right) => left.Value <= right.Value;

    internal static HandValue Create(HandCategory category, ReadOnlySpan<int> ranks)
    {
        var value = (int)category << 20;
        for (var i = 0; i < 5; i++)
            value |= (i < ranks.Length ? ranks[i] : 0) << (16 - 4 * i);
        return new HandValue(value);
    }

    public override string ToString() => Category.ToString();
}
