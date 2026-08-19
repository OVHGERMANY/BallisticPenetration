#nullable enable

using System;

namespace BallisticPenetration.Core.Physics
{
    /// <summary>
    /// Stable PCG-XSH-RR 32-bit random stream for physical projectile calculations. The algorithm,
    /// seed, and stream are explicit so identical inputs remain deterministic across runtimes.
    /// </summary>
    public sealed class DeterministicProjectileRandom
    {
        private ulong _state;
        private readonly ulong _increment;

        public DeterministicProjectileRandom(ulong seed, ulong stream)
        {
            Seed = seed;
            Stream = stream;
            _increment = unchecked((stream << 1) | 1UL);
            _state = 0UL;
            NextUInt32();
            _state = unchecked(_state + seed);
            NextUInt32();
        }

        public ulong Seed { get; }

        public ulong Stream { get; }

        public uint NextUInt32()
        {
            ulong oldState = _state;
            _state = unchecked((oldState * 6364136223846793005UL) + _increment);
            uint xorShifted = unchecked((uint)(((oldState >> 18) ^ oldState) >> 27));
            int rotation = (int)(oldState >> 59);
            return (xorShifted >> rotation)
                | (xorShifted << ((-rotation) & 31));
        }

        public double NextUnitDouble()
        {
            ulong high = NextUInt32() >> 5;
            ulong low = NextUInt32() >> 6;
            return ((high << 26) + low) / 9007199254740992d;
        }

        public double NextSignedUnitDouble()
        {
            return (NextUnitDouble() * 2d) - 1d;
        }
    }
}
