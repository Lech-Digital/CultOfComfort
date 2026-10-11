namespace CultOfComfort.Client
{
    /// <summary>Which state icons float over a raider: at most two, most important first (spec: Status icons).</summary>
    public static class RaiderStatus
    {
        public const int MaxIcons = 2;

        /// <returns>How many names were written into <paramref name="into"/> (allocation-free; call per frame).</returns>
        public static int Select(bool muddy, bool slowed, bool frightened, bool fighting, string[] into)
        {
            var count = 0;
            Add(muddy, "mud", into, ref count);
            Add(slowed, "speed", into, ref count);
            Add(frightened, "voice", into, ref count);
            Add(fighting, "swords", into, ref count);
            return count;
        }

        private static void Add(bool active, string icon, string[] into, ref int count)
        {
            if (active && count < MaxIcons)
            {
                into[count++] = icon;
            }
        }
    }
}
