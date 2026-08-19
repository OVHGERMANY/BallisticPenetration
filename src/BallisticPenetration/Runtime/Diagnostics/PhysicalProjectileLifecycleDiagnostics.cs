#nullable enable

using System;
using System.Collections.Generic;
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
        private static readonly object _collisionLogLock = new object();
        private static readonly Dictionary<string, HashSet<(string CollisionIdentity, string Phase)>> _collisionLogByProjectile
            = new Dictionary<string, HashSet<(string, string)>>(StringComparer.Ordinal);

        internal static void Record(
            string eventName,
            Shot? shot,
            PhysicalShotBinding? binding,
            string reason)
        {
            RecordInternal(
                eventName,
                shot,
                binding,
                reason,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null);
        }

        internal static void RecordCollisionObserved(
            Shot? shot,
            PhysicalShotBinding? binding,
            string collisionIdentity,
            int recordSequence,
            string targetSurfaceIdentity)
        {
            RecordInternal(
                "collision-observed",
                shot,
                binding,
                "bound-flight-capture",
                collisionIdentity,
                recordSequence,
                "observed",
                false,
                false,
                "none",
                false,
                false,
                null,
                null,
                targetSurfaceIdentity,
                null);
        }

        internal static void RecordCollisionResolved(
            Shot? shot,
            PhysicalShotBinding? binding,
            PhysicalCollisionRecord collisionRecord,
            string collisionIdentity,
            bool continued,
            bool replaced,
            bool targetWasAlreadyDead,
            string targetSurfaceIdentity)
        {
            bool ballisticTerminal = collisionRecord.Outcome == PhysicalCollisionOutcome.Stopped;
            RecordInternal(
                "collision-resolved",
                shot,
                binding,
                ballisticTerminal ? "resolved-stopped" : "resolved-continuation",
                collisionIdentity,
                collisionRecord.Sequence,
                "resolved",
                true,
                ballisticTerminal,
                "none",
                false,
                continued,
                replaced,
                targetWasAlreadyDead,
                targetSurfaceIdentity,
                collisionRecord);
        }

        private static void RecordInternal(
            string eventName,
            Shot? shot,
            PhysicalShotBinding? binding,
            string reason,
            string? collisionIdentity,
            int? recordSequence,
            string? phase,
            bool? resolutionKnown,
            bool? ballisticTerminalOverride,
            string? lifecycleEndReasonOverride,
            bool? lifecycleTerminalOverride,
            bool? continued,
            bool? replaced,
            bool? targetWasAlreadyDead,
            string? targetSurfaceIdentity,
            PhysicalCollisionRecord? collisionRecord)
        {
            PluginConfiguration? configuration = Plugin.Configuration;
            if (configuration == null
                || !configuration.LogPhysicalProjectileLifecycle.Value
                || binding == null)
            {
                return;
            }

            if ((eventName == "collision-observed" || eventName == "collision-resolved")
                && IsDuplicateCollisionEvent(
                    binding.State.ProjectileId,
                    collisionIdentity,
                    phase))
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
                if (eventName == "retired"
                    && (reason == "terminal-stop"
                        || reason == "collision-replaced"
                        || reason == "transaction-abort"))
                {
                    ClearCollisionDedupeState(state.ProjectileId);
                }

                int resolvedSequence = recordSequence ?? state.CollisionHistory.Count;
                int collisionOrdinal = ResolveCollisionOrdinal(resolvedSequence);
                bool isResolved = resolutionKnown ?? false;
                bool isContinued = continued ?? false;
                bool isReplaced = replaced ?? false;
                Vector3 currentPosition = shot != null ? shot.CurrentPosition : binding.CreationPosition;
                Vector3 currentVelocity = shot != null ? shot.CurrentVelocity : Vector3.zero;
                double now = Time.realtimeSinceStartupAsDouble;
                bool ballisticTerminal = ResolveBallisticTerminalState(
                    eventName,
                    reason,
                    ballisticTerminalOverride);
                bool lifecycleTerminal = ResolveLifecycleTerminalState(
                    eventName,
                    reason,
                    lifecycleTerminalOverride);
                string lifecycleEndReason = ResolveLifecycleEndReason(
                    eventName,
                    reason,
                    lifecycleEndReasonOverride,
                    state.TerminalState);
                PhysicalCollisionRecord? resolvedCollision = collisionRecord;
                PhysicalVector3 incomingPhysical = resolvedCollision == null
                    ? PhysicalVector3.Zero
                    : resolvedCollision.IncomingVelocityMetresPerSecond;
                PhysicalVector3 outgoingPhysical = resolvedCollision == null
                    ? PhysicalVector3.Zero
                    : resolvedCollision.OutgoingVelocityMetresPerSecond;
                double incomingSpeed = incomingPhysical.Magnitude;
                double outgoingSpeed = outgoingPhysical.Magnitude;
                string outcome = resolvedCollision?.Outcome.ToString() ?? "pending";
                string materialId = resolvedCollision?.MaterialId ?? "pending";
                string materialClass = resolvedCollision == null
                    ? "pending"
                    : resolvedCollision.MaterialClass.ToString();

                Plugin.Log?.LogInfo(
                    "Physical projectile lifecycle: event=" + eventName
                    + ", projectile=" + state.ProjectileId
                    + ", root=" + state.RootShotId
                    + ", kind=" + state.Kind
                    + ", fragmentIndex=" + state.FragmentIndex
                    + ", fragmentGeneration=" + state.FragmentGeneration
                    + ", recordSequence=" + resolvedSequence
                    + ", collisionOrdinal=" + collisionOrdinal
                    + ", phase=" + (string.IsNullOrWhiteSpace(phase) ? "none" : phase)
                    + ", resolutionKnown=" + isResolved.ToString().ToLowerInvariant()
                    + ", createdAt=" + Format(binding.CreationTimeSeconds)
                    + ", age=" + Format(Math.Max(0d, now - binding.CreationTimeSeconds))
                    + ", creationPosition=" + Format(binding.CreationPosition)
                    + ", creationVelocity=" + Format(binding.CreationVelocity)
                    + ", currentPosition=" + Format(currentPosition)
                    + ", lastVelocity=" + Format(currentVelocity)
                    + ", lastSpeed=" + Format(currentVelocity.magnitude)
                    + ", collisionOutcome=" + outcome
                    + ", materialId=" + materialId
                    + ", materialClass=" + materialClass
                    + ", incoming=" + Format(incomingPhysical)
                    + ", incomingSpeed=" + Format(incomingSpeed)
                    + ", outgoing=" + Format(outgoingPhysical)
                    + ", outgoingSpeed=" + Format(outgoingSpeed)
                    + ", continued=" + isContinued.ToString().ToLowerInvariant()
                    + ", replaced=" + isReplaced.ToString().ToLowerInvariant()
                    + ", ballisticTerminal=" + ballisticTerminal.ToString().ToLowerInvariant()
                    + ", lifecycleTerminal=" + lifecycleTerminal.ToString().ToLowerInvariant()
                    + ", lifecycleEndReason=" + lifecycleEndReason
                    + ", targetSurface=" + (string.IsNullOrWhiteSpace(targetSurfaceIdentity)
                        ? "none"
                        : targetSurfaceIdentity)
                    + ", targetAlreadyDead=" + (targetWasAlreadyDead ?? false).ToString().ToLowerInvariant()
                    + ", terminalState=" + state.TerminalState
                    + ", shotState=" + (shot?.BulletState.ToString() ?? "released")
                    + ", reason=" + reason + ".");
            }
            catch (Exception exception)
            {
                Plugin.LogHookFailure("Physical projectile lifecycle diagnostics", exception);
            }
        }

        private static bool ResolveBallisticTerminalState(
            string eventName,
            string reason,
            bool? terminalOverride)
        {
            if (terminalOverride.HasValue)
            {
                return terminalOverride.Value;
            }

            return eventName == "retired" && reason == "terminal-stop";
        }

        private static bool ResolveLifecycleTerminalState(
            string eventName,
            string reason,
            bool? terminalOverride)
        {
            if (terminalOverride.HasValue)
            {
                return terminalOverride.Value;
            }

            if (eventName == "retired"
                && (reason == "terminal-stop"
                    || reason == "collision-replaced"
                    || reason == "transaction-abort"))
            {
                return true;
            }

            return false;
        }

        private static string ResolveLifecycleEndReason(
            string eventName,
            string reason,
            string? reasonOverride,
            PhysicalProjectileTerminalState terminalState)
        {
            if (!string.IsNullOrWhiteSpace(reasonOverride))
            {
                return reasonOverride;
            }

            if (eventName == "retired")
            {
                if (reason == "terminal-stop")
                {
                    return "stopped";
                }

                if (reason == "collision-replaced")
                {
                    return "replaced";
                }

                if (reason == "transaction-abort")
                {
                    return "aborted";
                }

                // Preserve prior terminal state semantics for unexpected retired states only.
                if (terminalState == PhysicalProjectileTerminalState.Stopped)
                {
                    return "stopped";
                }
            }

            return "none";
        }

        private static bool IsDuplicateCollisionEvent(
            string projectileIdentity,
            string? collisionIdentity,
            string? phase)
        {
            if (string.IsNullOrWhiteSpace(projectileIdentity)
                || string.IsNullOrWhiteSpace(collisionIdentity)
                || string.IsNullOrWhiteSpace(phase))
            {
                return false;
            }

            lock (_collisionLogLock)
            {
                if (!_collisionLogByProjectile.TryGetValue(
                        projectileIdentity,
                        out HashSet<(string CollisionIdentity, string Phase)>? recordedPhases))
                {
                    recordedPhases = new HashSet<(string CollisionIdentity, string Phase)>(
                        CollisionRecordTupleComparer.Instance);
                    _collisionLogByProjectile[projectileIdentity] = recordedPhases;
                }

                return !recordedPhases.Add((collisionIdentity, phase));
            }
        }

        private static void ClearCollisionDedupeState(string projectileIdentity)
        {
            if (string.IsNullOrWhiteSpace(projectileIdentity))
            {
                return;
            }

            lock (_collisionLogLock)
            {
                _collisionLogByProjectile.Remove(projectileIdentity);
            }
        }

        private static int ResolveCollisionOrdinal(int? recordSequence)
        {
            return recordSequence ?? 0;
        }

        private static string Format(Vector3 value)
        {
            return "(" + Format(value.x) + "," + Format(value.y) + "," + Format(value.z) + ")";
        }

        private static string Format(PhysicalVector3 value)
        {
            return "(" + Format(value.X) + "," + Format(value.Y) + "," + Format(value.Z) + ")";
        }

        private static string Format(double value)
        {
            return value.ToString("0.######", CultureInfo.InvariantCulture);
        }

        private sealed class CollisionRecordTupleComparer : IEqualityComparer<(string CollisionIdentity, string Phase)>
        {
            internal static readonly CollisionRecordTupleComparer Instance = new CollisionRecordTupleComparer();

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
                return ((StringComparer.Ordinal.GetHashCode(obj.CollisionIdentity) * 397)
                        ^ StringComparer.Ordinal.GetHashCode(obj.Phase));
            }
        }
    }
}
