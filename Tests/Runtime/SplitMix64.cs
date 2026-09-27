namespace fMath.Tests
{
    /// <summary>Tiny deterministic PRNG for reproducible test sweeps.</summary>
    internal struct SplitMix64
    {
        ulong _state;

        public SplitMix64(ulong seed) { _state = seed; }

        public ulong Next()
        {
            ulong z = _state += 0x9E3779B97F4A7C15UL;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }

        /// <summary>Uniform int in [min, max] (inclusive).</summary>
        public int Range(int min, int max) => (int)(min + (long)(Next() % (ulong)((long)max - min + 1)));
    }
}
