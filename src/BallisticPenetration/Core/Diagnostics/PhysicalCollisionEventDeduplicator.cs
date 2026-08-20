#nullable enable

using System;
using System.Collections.Generic;

namespace BallisticPenetration.Core.Diagnostics
{
    /// <summary>
    /// Production collision-phase deduplication shared by the runtime and validation runner.
    /// Observed and resolved phases intentionally remain distinct keys.
    /// </summary>
    internal sealed class PhysicalCollisionEventDeduplicator
    {
        private readonly Dictionary<string, HashSet<(string CollisionIdentity, string Phase)>> _byProjectile
            = new Dictionary<string, HashSet<(string, string)>>(StringComparer.Ordinal);

        internal bool TryRecord(
            string projectileIdentity,
            string? collisionIdentity,
            string? phase)
        {
            if (string.IsNullOrWhiteSpace(projectileIdentity)
                || string.IsNullOrWhiteSpace(collisionIdentity)
                || string.IsNullOrWhiteSpace(phase))
            {
                return true;
            }

            if (!_byProjectile.TryGetValue(
                    projectileIdentity,
                    out HashSet<(string CollisionIdentity, string Phase)>? recordedPhases))
            {
                recordedPhases = new HashSet<(string CollisionIdentity, string Phase)>(
                    CollisionRecordTupleComparer.Instance);
                _byProjectile[projectileIdentity] = recordedPhases;
            }

            return recordedPhases.Add((collisionIdentity, phase));
        }

        internal void ClearProjectile(string projectileIdentity)
        {
            if (!string.IsNullOrWhiteSpace(projectileIdentity))
            {
                _byProjectile.Remove(projectileIdentity);
            }
        }

        internal void Clear()
        {
            _byProjectile.Clear();
        }

        private sealed class CollisionRecordTupleComparer :
            IEqualityComparer<(string CollisionIdentity, string Phase)>
        {
            internal static readonly CollisionRecordTupleComparer Instance =
                new CollisionRecordTupleComparer();

            public bool Equals(
                (string CollisionIdentity, string Phase) left,
                (string CollisionIdentity, string Phase) right)
            {
                return string.Equals(
                    left.CollisionIdentity,
                    right.CollisionIdentity,
                    StringComparison.Ordinal)
                    && string.Equals(left.Phase, right.Phase, StringComparison.Ordinal);
            }

            public int GetHashCode((string CollisionIdentity, string Phase) obj)
            {
                unchecked
                {
                    return (StringComparer.Ordinal.GetHashCode(obj.CollisionIdentity) * 397)
                        ^ StringComparer.Ordinal.GetHashCode(obj.Phase);
                }
            }
        }
    }
}
