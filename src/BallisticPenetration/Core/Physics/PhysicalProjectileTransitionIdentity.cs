#nullable enable

using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace BallisticPenetration.Core.Physics
{
    /// <summary>
    /// Builds collision identities from immutable physical state instead of a pooled host shot or
    /// its truncated random seed.
    /// </summary>
    internal static class PhysicalProjectileTransitionIdentity
    {
        internal static string CreateCollisionId(PhysicalProjectileState state)
        {
            if (state == null)
            {
                ThrowNullState(nameof(state));
            }

            return string.Concat(
                state.ProjectileId,
                "-collision-",
                state.CollisionHistory.Count.ToString(CultureInfo.InvariantCulture));
        }

        [DoesNotReturn]
        private static void ThrowNullState(string parameterName)
        {
            throw new ArgumentNullException(parameterName);
        }
    }
}
