using System;

namespace CultOfComfort.Simulation.Numerics;

/// <summary>
/// PCG32 (pcg32_srandom_r / pcg32_random_r). Platform-independent, so a seed produces the same
/// sequence in Unity and on the server. Never use System.Random or UnityEngine.Random in the sim.
/// </summary>
public sealed class PcgRandom
{
    private const ulong Multiplier = 6364136223846793005UL;

    private readonly ulong _increment;
    private ulong _state;

    public PcgRandom(ulong seed, ulong stream)
    {
        _increment = (stream << 1) | 1UL;
        _state = 0;
        NextUInt();
        _state += seed;
        NextUInt();
    }

    public uint NextUInt()
    {
        var old = _state;
        _state = unchecked(old * Multiplier + _increment);
        var xorShifted = (uint)(((old >> 18) ^ old) >> 27);
        var rotation = (int)(old >> 59);
        return (xorShifted >> rotation) | (xorShifted << (-rotation & 31));
    }

    /// <summary>Returns a value in [minInclusive, maxExclusive).</summary>
    public int Next(int minInclusive, int maxExclusive)
    {
        if (maxExclusive <= minInclusive)
        {
            throw new ArgumentOutOfRangeException(nameof(maxExclusive), "maxExclusive must be greater than minInclusive.");
        }

        var range = (uint)(maxExclusive - minInclusive);
        return minInclusive + (int)(NextUInt() % range);
    }
}

/// <summary>Independent PRNG streams per concern, so adding randomness to one system never shifts another.</summary>
internal static class RandomStreams
{
    public const ulong Waves = 1;
    public const ulong Combat = 2;
    public const ulong World = 3;
    public const ulong Placement = 4;
    public const ulong Bots = 5;
}
