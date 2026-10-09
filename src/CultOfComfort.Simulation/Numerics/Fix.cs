using System;
using System.Globalization;

namespace CultOfComfort.Simulation.Numerics;

/// <summary>
/// Q47.16 fixed-point number. The simulation uses it instead of float/double so the Unity client
/// (IL2CPP, ARM) and the .NET server produce bit-identical results. See ADR 0002.
/// </summary>
public readonly struct Fix : IEquatable<Fix>, IComparable<Fix>
{
    public const int FractionalBits = 16;
    private const long OneRaw = 1L << FractionalBits;

    public static readonly Fix Zero = new(0);
    public static readonly Fix One = new(OneRaw);
    public static readonly Fix Half = new(OneRaw >> 1);

    private Fix(long raw) => Raw = raw;

    public long Raw { get; }

    public static Fix FromRaw(long raw) => new(raw);

    public static Fix FromInt(int value) => new((long)value << FractionalBits);

    public static Fix Ratio(int numerator, int denominator)
    {
        if (denominator == 0)
        {
            throw new DivideByZeroException();
        }

        return new(((long)numerator << FractionalBits) / denominator);
    }

    public int FloorToInt() => (int)(Raw >> FractionalBits);

    public Fix Floor() => new(Raw & ~(OneRaw - 1));

    public Fix Abs() => Raw < 0 ? new(-Raw) : this;

    /// <summary>Presentation only. Never feed the result back into the simulation.</summary>
    public double ToDouble() => (double)Raw / OneRaw;

    public static Fix Min(Fix a, Fix b) => a.Raw <= b.Raw ? a : b;

    public static Fix Max(Fix a, Fix b) => a.Raw >= b.Raw ? a : b;

    public static Fix Clamp(Fix value, Fix min, Fix max) => Min(Max(value, min), max);

    public static Fix operator +(Fix a, Fix b) => new(a.Raw + b.Raw);
    public static Fix operator -(Fix a, Fix b) => new(a.Raw - b.Raw);
    public static Fix operator -(Fix a) => new(-a.Raw);
    public static Fix operator *(Fix a, Fix b) => new((a.Raw * b.Raw) >> FractionalBits);
    public static Fix operator /(Fix a, Fix b) => new((a.Raw << FractionalBits) / b.Raw);
    public static Fix operator *(Fix a, int b) => new(a.Raw * b);
    public static Fix operator /(Fix a, int b) => new(a.Raw / b);

    public static bool operator ==(Fix a, Fix b) => a.Raw == b.Raw;
    public static bool operator !=(Fix a, Fix b) => a.Raw != b.Raw;
    public static bool operator <(Fix a, Fix b) => a.Raw < b.Raw;
    public static bool operator >(Fix a, Fix b) => a.Raw > b.Raw;
    public static bool operator <=(Fix a, Fix b) => a.Raw <= b.Raw;
    public static bool operator >=(Fix a, Fix b) => a.Raw >= b.Raw;

    public bool Equals(Fix other) => Raw == other.Raw;

    public override bool Equals(object? obj) => obj is Fix other && Equals(other);

    public override int GetHashCode() => Raw.GetHashCode();

    public int CompareTo(Fix other) => Raw.CompareTo(other.Raw);

    public override string ToString() => ToDouble().ToString("0.####", CultureInfo.InvariantCulture);
}
