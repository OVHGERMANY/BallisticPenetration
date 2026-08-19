#nullable enable

using System;

namespace BallisticPenetration.Core.Physics
{
    /// <summary>
    /// Maps a full physical-component seed onto the bounded random table owned by the host.
    /// The physical state keeps its original 64-bit seed; only the host-facing index is folded.
    /// </summary>
    public static class PhysicalHostRandomSeed
    {
        public static int Map(ulong deterministicSeed, int hostRandomCount)
        {
            if (hostRandomCount <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(hostRandomCount),
                    hostRandomCount,
                    "Host random count must be positive.");
            }

            return (int)(deterministicSeed % (ulong)hostRandomCount);
        }
    }
}
