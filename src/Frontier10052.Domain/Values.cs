using System.Text.Json.Serialization;

namespace Frontier10052.Domain;

public readonly record struct Credits : IComparable<Credits>
{
    [JsonConstructor]
    public Credits(long value)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value);
        Value = value;
    }

    public long Value { get; }

    public int CompareTo(Credits other) => Value.CompareTo(other.Value);

    public static Credits operator +(Credits left, Credits right) => new(checked(left.Value + right.Value));

    public static Credits operator -(Credits left, Credits right)
    {
        if (right.Value > left.Value)
        {
            throw new ArgumentOutOfRangeException(nameof(right), "Credits cannot become negative.");
        }

        return new Credits(left.Value - right.Value);
    }

    public static Credits operator *(Credits unitPrice, Tonnes quantity) => new(checked(unitPrice.Value * quantity.Value));
    public static bool operator <(Credits left, Credits right) => left.Value < right.Value;
    public static bool operator >(Credits left, Credits right) => left.Value > right.Value;
    public static bool operator <=(Credits left, Credits right) => left.Value <= right.Value;
    public static bool operator >=(Credits left, Credits right) => left.Value >= right.Value;
    public override string ToString() => $"{Value:N0} cr";
}

public readonly record struct Tonnes : IComparable<Tonnes>
{
    [JsonConstructor]
    public Tonnes(int value)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value);
        Value = value;
    }

    public int Value { get; }

    public int CompareTo(Tonnes other) => Value.CompareTo(other.Value);
    public static Tonnes operator +(Tonnes left, Tonnes right) => new(checked(left.Value + right.Value));

    public static Tonnes operator -(Tonnes left, Tonnes right)
    {
        if (right.Value > left.Value)
        {
            throw new ArgumentOutOfRangeException(nameof(right), "Cargo mass cannot become negative.");
        }

        return new Tonnes(left.Value - right.Value);
    }

    public static bool operator <(Tonnes left, Tonnes right) => left.Value < right.Value;
    public static bool operator >(Tonnes left, Tonnes right) => left.Value > right.Value;
    public static bool operator <=(Tonnes left, Tonnes right) => left.Value <= right.Value;
    public static bool operator >=(Tonnes left, Tonnes right) => left.Value >= right.Value;
    public override string ToString() => $"{Value} t";
}

public readonly record struct GameTime : IComparable<GameTime>
{
    [JsonConstructor]
    public GameTime(long hoursSinceStart)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(hoursSinceStart);
        HoursSinceStart = hoursSinceStart;
    }

    public long HoursSinceStart { get; }

    public GameTime AddHours(long hours)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(hours);
        return new GameTime(checked(HoursSinceStart + hours));
    }

    public int CompareTo(GameTime other) => HoursSinceStart.CompareTo(other.HoursSinceStart);
    public override string ToString() => $"T+{HoursSinceStart}h";
}
