#nullable enable

using System;
using System.Globalization;
using System.Threading;
using BallisticPenetration.Core.Physics;
using BallisticPenetration.Runtime.State;
using EFT.Ballistics;
using UnityEngine;

namespace BallisticPenetration.Runtime.Diagnostics
{
    internal static class PhysicalProjectileLifecycleDiagnostics
    {
        private const int MaximumRecordsPerProcess = 8192;
        private static int _recordCount;
        private static int _limitReported;

        internal static void Record(
            string eventName,
            Shot? shot,
            PhysicalShotBinding? binding,
            string reason)
        {
            PluginConfiguration? configuration = Plugin.Configuration;
            if (configuration == null
                || !configuration.LogPhysicalProjectileLifecycle.Value
                || binding == null)
            {
                return;
            }

            int recordNumber = Interlocked.Increment(ref _recordCount);
            if (recordNumber > MaximumRecordsPerProcess)
            {
                if (Interlocked.CompareExchange(ref _limitReported, 1, 0) == 0)
                {
                    Plugin.Log?.LogWarning(
                        "Physical projectile lifecycle log reached its 8192-record process limit.");
                }

                return;
            }

            try
            {
                PhysicalProjectileState state = binding.State;
                PhysicalCollisionRecord? lastCollision = state.CollisionHistory.Count > 0
                    ? state.CollisionHistory[state.CollisionHistory.Count - 1]
                    : null;
                Vector3 currentPosition = shot != null
                    ? shot.CurrentPosition
                    : binding.CreationPosition;
                Vector3 currentVelocity = shot != null
                    ? shot.CurrentVelocity
                    : Vector3.zero;
                double now = Time.realtimeSinceStartupAsDouble;
                Plugin.Log?.LogInfo(
                    "Physical projectile lifecycle: event=" + eventName
                    + ", projectile=" + state.ProjectileId
                    + ", root=" + state.RootShotId
                    + ", kind=" + state.Kind
                    + ", fragmentIndex=" + state.FragmentIndex
                    + ", fragmentGeneration=" + state.FragmentGeneration
                    + ", createdAt=" + Format(binding.CreationTimeSeconds)
                    + ", age=" + Format(Math.Max(0d, now - binding.CreationTimeSeconds))
                    + ", creationPosition=" + Format(binding.CreationPosition)
                    + ", creationVelocity=" + Format(binding.CreationVelocity)
                    + ", currentPosition=" + Format(currentPosition)
                    + ", lastVelocity=" + Format(currentVelocity)
                    + ", lastSpeed=" + Format(currentVelocity.magnitude)
                    + ", lastCollision=" + (lastCollision?.Outcome.ToString() ?? "none")
                    + ", terminalState=" + state.TerminalState
                    + ", targetAlreadyDead=" + binding.TargetWasAlreadyDead
                    + ", shotState=" + (shot?.BulletState.ToString() ?? "released")
                    + ", reason=" + reason + ".");
            }
            catch (Exception exception)
            {
                Plugin.LogHookFailure("Physical projectile lifecycle diagnostics", exception);
            }
        }

        private static string Format(Vector3 value)
        {
            return "(" + Format(value.x) + "," + Format(value.y) + "," + Format(value.z) + ")";
        }

        private static string Format(double value)
        {
            return value.ToString("0.######", CultureInfo.InvariantCulture);
        }
    }
}
