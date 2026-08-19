#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using BallisticPenetration.Core.Diagnostics;
using BallisticPenetration.Core.Physics;
using BallisticPenetration.Runtime.State;
using EFT.Ballistics;
using UnityEngine;

namespace BallisticPenetration.Runtime.Diagnostics
{
    internal static class PhysicalProjectileLifecycleDiagnostics
    {
        private const int MaximumRecordsPerProcess = 8192;
        internal const int TerminalTombstoneCapacity =
            PhysicalProjectileLifecycleTracker.DefaultTerminalTombstoneCapacity;

        private static readonly object LifecycleLock = new object();
        private static readonly PhysicalProjectileLifecycleTracker LifecycleTracker =
            new PhysicalProjectileLifecycleTracker(TerminalTombstoneCapacity);
        private static readonly object CollisionLogLock = new object();
        private static readonly Dictionary<string, HashSet<(string CollisionIdentity, string Phase)>> CollisionLogByProjectile
            = new Dictionary<string, HashSet<(string, string)>>(StringComparer.Ordinal);
        private static int _recordCount;
        private static int _limitReported;
        private static bool _shutdownStarted;

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

        internal static void RecordRemoval(
            Shot? shot,
            PhysicalShotBinding? binding,
            string removalReason)
        {
            if (binding == null)
            {
                return;
            }

            try
            {
                double now = Time.realtimeSinceStartupAsDouble;
                string projectileIdentity = binding.State.ProjectileId;
                PhysicalLifecycleMissingTerminal? missing;
                lock (LifecycleLock)
                {
                    if (_shutdownStarted)
                    {
                        return;
                    }

                    ObserveIfCurrent(shot, binding, null, null);
                    missing = LifecycleTracker.RemoveWithoutTerminal(
                        projectileIdentity,
                        removalReason,
                        now);
                }

                if (missing == null)
                {
                    return;
                }

                ClearCollisionDedupeState(projectileIdentity);
                LogMissingTerminal(missing, now);
            }
            catch (Exception exception)
            {
                Plugin.LogHookFailure("Physical projectile lifecycle removal diagnostics", exception);
            }
        }

        internal static void ShutdownExpected()
        {
            IReadOnlyList<PhysicalLifecycleSnapshot> closed;
            int tombstonesBeforeFinalCleanup;
            int duplicateTerminalViolations;
            int missingTerminalViolations;
            double now = Time.realtimeSinceStartupAsDouble;

            lock (LifecycleLock)
            {
                if (_shutdownStarted)
                {
                    return;
                }

                _shutdownStarted = true;
                closed = LifecycleTracker.CloseActiveForShutdown(now);
                tombstonesBeforeFinalCleanup = LifecycleTracker.TombstoneCount;
                duplicateTerminalViolations = LifecycleTracker.DuplicateTerminalViolationCount;
                missingTerminalViolations = LifecycleTracker.MissingTerminalViolationCount;
            }

            try
            {
                for (int index = 0; index < closed.Count; index++)
                {
                    LogShutdownTerminal(closed[index], now);
                }

                Plugin.Log?.LogInfo(
                    "Physical projectile lifecycle: event=shutdown-cleanup-summary"
                    + ", activeEntriesClosed=" + closed.Count
                    + ", tombstonesBeforeFinalCleanup=" + tombstonesBeforeFinalCleanup
                    + ", duplicateTerminalViolations=" + duplicateTerminalViolations
                    + ", missingTerminalViolations=" + missingTerminalViolations
                    + ".");
            }
            catch (Exception exception)
            {
                Plugin.LogHookFailure("Physical projectile lifecycle shutdown diagnostics", exception);
            }
            finally
            {
                lock (LifecycleLock)
                {
                    LifecycleTracker.Clear();
                }

                lock (CollisionLogLock)
                {
                    CollisionLogByProjectile.Clear();
                }
            }
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
            if (binding == null)
            {
                return;
            }

            bool loggingEnabled = configuration != null
                && configuration.LogPhysicalProjectileLifecycle.Value;
            if (eventName == "created" && !loggingEnabled)
            {
                return;
            }

            try
            {
                PhysicalProjectileState state = binding.State;
                double now = Time.realtimeSinceStartupAsDouble;
                bool isCanonicalTerminal = TryResolveCanonicalTerminalReason(
                    eventName,
                    reason,
                    out PhysicalLifecycleTerminalReason attemptedTerminalReason);
                PhysicalLifecycleTerminalAttempt? terminalAttempt = null;
                bool creationRegistered = true;

                lock (LifecycleLock)
                {
                    if (_shutdownStarted)
                    {
                        return;
                    }

                    if (eventName == "created")
                    {
                        creationRegistered = LifecycleTracker.TryRegister(
                            CreateSnapshot(shot, binding));
                    }
                    else
                    {
                        ObserveIfCurrent(
                            shot,
                            binding,
                            collisionIdentity,
                            recordSequence.HasValue
                                ? ResolveCollisionOrdinal(recordSequence.Value)
                                : null);
                    }

                    if (isCanonicalTerminal)
                    {
                        terminalAttempt = LifecycleTracker.TryTerminate(
                            state.ProjectileId,
                            attemptedTerminalReason,
                            now);
                    }
                    else if ((eventName == "collision-observed"
                            || eventName == "collision-resolved")
                        && !LifecycleTracker.IsActive(state.ProjectileId))
                    {
                        return;
                    }
                }

                if (!creationRegistered)
                {
                    Plugin.Log?.LogWarning(
                        "Physical projectile lifecycle invariant: event=lifecycle-identity-reused"
                        + ", projectile=" + state.ProjectileId
                        + ", root=" + state.RootShotId
                        + ", kind=" + state.Kind
                        + ", fragmentIndex=" + state.FragmentIndex
                        + ", fragmentGeneration=" + state.FragmentGeneration
                        + ", timestamp=" + Format(now)
                        + ".");
                    return;
                }

                if (terminalAttempt?.Disposition == PhysicalLifecycleTerminalDisposition.Duplicate)
                {
                    LogDuplicateTerminal(
                        terminalAttempt.Tombstone,
                        attemptedTerminalReason,
                        now);
                    return;
                }

                if (isCanonicalTerminal
                    && terminalAttempt?.Disposition != PhysicalLifecycleTerminalDisposition.Canonical)
                {
                    return;
                }

                if (isCanonicalTerminal)
                {
                    ClearCollisionDedupeState(state.ProjectileId);
                }

                if (!loggingEnabled)
                {
                    return;
                }

                if ((eventName == "collision-observed" || eventName == "collision-resolved")
                    && IsDuplicateCollisionEvent(
                        state.ProjectileId,
                        collisionIdentity,
                        phase))
                {
                    return;
                }

                if (!isCanonicalTerminal)
                {
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
                }

                int resolvedSequence = recordSequence ?? state.CollisionHistory.Count;
                int collisionOrdinal = ResolveCollisionOrdinal(resolvedSequence);
                bool isResolved = resolutionKnown ?? false;
                bool isContinued = continued ?? false;
                bool isReplaced = replaced ?? false;
                Vector3 currentPosition = shot != null ? shot.CurrentPosition : binding.CreationPosition;
                Vector3 currentVelocity = shot != null ? shot.CurrentVelocity : Vector3.zero;
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

        private static PhysicalLifecycleSnapshot CreateSnapshot(
            Shot? shot,
            PhysicalShotBinding binding)
        {
            Vector3 position = shot != null ? shot.CurrentPosition : binding.CreationPosition;
            Vector3 velocity = shot != null ? shot.CurrentVelocity : binding.CreationVelocity;
            PhysicalProjectileState state = binding.State;
            PhysicalCollisionRecord? lastCollision = state.CollisionHistory.Count > 0
                ? state.CollisionHistory[state.CollisionHistory.Count - 1]
                : null;
            return new PhysicalLifecycleSnapshot(
                state.ProjectileId,
                state.RootShotId,
                state.Kind.ToString(),
                state.FragmentIndex,
                state.FragmentGeneration,
                binding.CreationTimeSeconds,
                ToPhysical(position),
                ToPhysical(velocity),
                lastCollision?.CollisionId ?? string.Empty,
                lastCollision?.Sequence ?? 0);
        }

        private static void ObserveIfCurrent(
            Shot? shot,
            PhysicalShotBinding binding,
            string? collisionIdentity,
            int? collisionOrdinal)
        {
            if (shot == null || !binding.Matches(shot))
            {
                return;
            }

            LifecycleTracker.TryObserve(
                binding.State.ProjectileId,
                ToPhysical(shot.CurrentPosition),
                ToPhysical(shot.CurrentVelocity),
                collisionIdentity,
                collisionOrdinal);
        }

        private static bool TryResolveCanonicalTerminalReason(
            string eventName,
            string reason,
            out PhysicalLifecycleTerminalReason terminalReason)
        {
            terminalReason = PhysicalLifecycleTerminalReason.Stopped;
            if (eventName != "retired")
            {
                return false;
            }

            if (reason == "terminal-stop")
            {
                terminalReason = PhysicalLifecycleTerminalReason.Stopped;
                return true;
            }

            if (reason == "collision-replaced")
            {
                terminalReason = PhysicalLifecycleTerminalReason.Replaced;
                return true;
            }

            if (reason == "transaction-abort")
            {
                terminalReason = PhysicalLifecycleTerminalReason.Aborted;
                return true;
            }

            return false;
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

            return eventName == "retired"
                && (reason == "terminal-stop"
                    || reason == "collision-replaced"
                    || reason == "transaction-abort");
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

            lock (CollisionLogLock)
            {
                if (!CollisionLogByProjectile.TryGetValue(
                        projectileIdentity,
                        out HashSet<(string CollisionIdentity, string Phase)>? recordedPhases))
                {
                    recordedPhases = new HashSet<(string CollisionIdentity, string Phase)>(
                        CollisionRecordTupleComparer.Instance);
                    CollisionLogByProjectile[projectileIdentity] = recordedPhases;
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

            lock (CollisionLogLock)
            {
                CollisionLogByProjectile.Remove(projectileIdentity);
            }
        }

        private static void LogDuplicateTerminal(
            PhysicalLifecycleTombstone? firstTerminal,
            PhysicalLifecycleTerminalReason attemptedReason,
            double duplicateTimestamp)
        {
            if (firstTerminal == null)
            {
                return;
            }

            PhysicalLifecycleSnapshot snapshot = firstTerminal.Snapshot;
            Plugin.Log?.LogWarning(
                "Physical projectile lifecycle invariant: event=terminal-duplicate"
                + ", projectile=" + snapshot.ProjectileIdentity
                + ", root=" + snapshot.RootIdentity
                + ", kind=" + snapshot.ProjectileKind
                + ", fragmentIndex=" + snapshot.FragmentIndex
                + ", fragmentGeneration=" + snapshot.FragmentGeneration
                + ", firstTerminalReason=" + FormatTerminalReason(firstTerminal)
                + ", attemptedTerminalReason=" + FormatTerminalReason(attemptedReason)
                + ", firstTerminalTimestamp=" + Format(firstTerminal.TerminalTimestamp)
                + ", duplicateTimestamp=" + Format(duplicateTimestamp)
                + ".");
        }

        private static void LogMissingTerminal(
            PhysicalLifecycleMissingTerminal missing,
            double removalTimestamp)
        {
            PhysicalLifecycleSnapshot snapshot = missing.Snapshot;
            Plugin.Log?.LogWarning(
                "Physical projectile lifecycle invariant: event=terminal-missing"
                + ", projectile=" + snapshot.ProjectileIdentity
                + ", root=" + snapshot.RootIdentity
                + ", kind=" + snapshot.ProjectileKind
                + ", fragmentIndex=" + snapshot.FragmentIndex
                + ", fragmentGeneration=" + snapshot.FragmentGeneration
                + ", removalPath=" + missing.RemovalReason
                + ", createdAt=" + Format(snapshot.CreationTimestamp)
                + ", lastPosition=" + Format(snapshot.LastKnownPosition)
                + ", lastVelocity=" + Format(snapshot.LastKnownVelocity)
                + ", lastSpeed=" + Format(snapshot.LastKnownSpeed)
                + ", lastCollisionIdentity=" + FormatOptional(snapshot.LastCollisionIdentity)
                + ", lastCollisionOrdinal=" + snapshot.LastCollisionOrdinal
                + ", removalTimestamp=" + Format(removalTimestamp)
                + ".");
        }

        private static void LogShutdownTerminal(
            PhysicalLifecycleSnapshot snapshot,
            double shutdownTimestamp)
        {
            Plugin.Log?.LogInfo(
                "Physical projectile lifecycle: event=retired"
                + ", projectile=" + snapshot.ProjectileIdentity
                + ", root=" + snapshot.RootIdentity
                + ", kind=" + snapshot.ProjectileKind
                + ", fragmentIndex=" + snapshot.FragmentIndex
                + ", fragmentGeneration=" + snapshot.FragmentGeneration
                + ", createdAt=" + Format(snapshot.CreationTimestamp)
                + ", age=" + Format(Math.Max(0d, shutdownTimestamp - snapshot.CreationTimestamp))
                + ", currentPosition=" + Format(snapshot.LastKnownPosition)
                + ", lastVelocity=" + Format(snapshot.LastKnownVelocity)
                + ", lastSpeed=" + Format(snapshot.LastKnownSpeed)
                + ", lastCollisionIdentity=" + FormatOptional(snapshot.LastCollisionIdentity)
                + ", lastCollisionOrdinal=" + snapshot.LastCollisionOrdinal
                + ", ballisticTerminal=false"
                + ", lifecycleTerminal=true"
                + ", lifecycleEndReason=shutdown"
                + ", reason=shutdown-cleanup.");
        }

        private static string FormatTerminalReason(PhysicalLifecycleTombstone tombstone)
        {
            return tombstone.TerminalReason.HasValue
                ? FormatTerminalReason(tombstone.TerminalReason.Value)
                : "missing";
        }

        private static string FormatTerminalReason(PhysicalLifecycleTerminalReason terminalReason)
        {
            return terminalReason.ToString().ToLowerInvariant();
        }

        private static string FormatOptional(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "none" : value;
        }

        private static int ResolveCollisionOrdinal(int recordSequence)
        {
            return recordSequence;
        }

        private static PhysicalVector3 ToPhysical(Vector3 value)
        {
            return new PhysicalVector3(value.x, value.y, value.z);
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
