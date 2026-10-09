namespace CultOfComfort.Simulation.Numerics;

/// <summary>FNV-1a 64-bit. Used for layout hashes and the rolling battle state hash.</summary>
internal sealed class Fnv1aHasher
{
    private const ulong OffsetBasis = 14695981039346656037UL;
    private const ulong Prime = 1099511628211UL;

    public ulong Value { get; private set; } = OffsetBasis;

    public void Add(long value)
    {
        var hash = Value;
        for (var i = 0; i < 8; i++)
        {
            hash ^= (byte)(value >> (i * 8));
            hash = unchecked(hash * Prime);
        }

        Value = hash;
    }

    public void Add(int value) => Add((long)value);
}
