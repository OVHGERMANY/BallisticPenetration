#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Threading;
using BallisticPenetration.Core.Diagnostics;
using BallisticPenetration.Core.Physics;
using BallisticPenetration.Runtime.State;
using EFT.Ballistics;
using EFT.InventoryLogic;
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
                FieldReportRuntime.RecordEvent(
                    "shutdown-cleanup-summary",
                    true,
                    Field("activeEntriesClosed", closed.Count),
                    Field("tombstonesBeforeFinalCleanup", tombstonesBeforeFinalCleanup),
                    Field("duplicateTerminalViolations", duplicateTerminalViolations),
                    Field("missingTerminalViolations", missingTerminalViolations));
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
            bool fieldRecordingEnabled = FieldReportRuntime.IsEnabled;
            if (eventName == "created" && !loggingEnabled && !fieldRecordingEnabled)
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

                if (!loggingEnabled && !fieldRecordingEnabled)
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

                bool humanLoggingAllowed = loggingEnabled;
                if (!isCanonicalTerminal && humanLoggingAllowed)
                {
                    int recordNumber = Interlocked.Increment(ref _recordCount);
                    if (recordNumber > MaximumRecordsPerProcess)
                    {
                        if (Interlocked.CompareExchange(ref _limitReported, 1, 0) == 0)
                        {
                            Plugin.Log?.LogWarning(
                                "Physical projectile lifecycle log reached its 8192-record process limit.");
                        }

                        humanLoggingAllowed = false;
                    }
                }

                if (!humanLoggingAllowed && !fieldRecordingEnabled)
                {
                    return;
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
                string outcome = resolvedCollision?.Outcome.ToString() ?? "pending";
                string materialId = resolvedCollision?.MaterialId ?? "pending";
                string materialClass = resolvedCollision == null
                    ? "pending"
                    : resolvedCollision.MaterialClass.ToString();

                FieldReportLifecycleEventSnapshot reportSnapshot = CreateReportSnapshot(
                    eventName,
                    shot,
                    binding,
                    reason,
                    collisionIdentity,
                    resolvedSequence,
                    collisionOrdinal,
                    phase,
                    isResolved,
                    now,
                    currentPosition,
                    currentVelocity,
                    incomingPhysical,
                    outgoingPhysical,
                    outcome,
                    materialId,
                    materialClass,
                    isContinued,
                    isReplaced,
                    ballisticTerminal,
                    lifecycleTerminal,
                    lifecycleEndReason,
                    targetWasAlreadyDead ?? false,
                    targetSurfaceIdentity);

                if (fieldRecordingEnabled)
                {
                    FieldReportRuntime.RecordLifecycle(reportSnapshot);
                }

                if (!humanLoggingAllowed)
                {
                    return;
                }

                Plugin.Log?.LogInfo(
                    "Physical projectile lifecycle: event=" + reportSnapshot.EventName
                    + ", projectile=" + reportSnapshot.ProjectileIdentity
                    + ", root=" + reportSnapshot.RootIdentity
                    + ", kind=" + reportSnapshot.ProjectileKind
                    + ", fragmentIndex=" + reportSnapshot.FragmentIndex
                    + ", fragmentGeneration=" + reportSnapshot.FragmentGeneration
                    + ", recordSequence=" + reportSnapshot.RecordSequence
                    + ", collisionOrdinal=" + reportSnapshot.CollisionOrdinal
                    + ", phase=" + (string.IsNullOrWhiteSpace(reportSnapshot.Phase) ? "none" : reportSnapshot.Phase)
                    + ", resolutionKnown=" + reportSnapshot.ResolutionKnown.ToString().ToLowerInvariant()
                    + ", createdAt=" + Format(binding.CreationTimeSeconds)
                    + ", age=" + Format(Math.Max(0d, now - binding.CreationTimeSeconds))
                    + ", creationPosition=" + Format(reportSnapshot.CreationPosition)
                    + ", creationVelocity=" + Format(reportSnapshot.CreationVelocity)
                    + ", currentPosition=" + Format(reportSnapshot.CurrentPosition)
                    + ", lastVelocity=" + Format(reportSnapshot.LastVelocity)
                    + ", lastSpeed=" + Format(reportSnapshot.LastSpeed)
                    + ", collisionOutcome=" + reportSnapshot.CollisionOutcome
                    + ", materialId=" + reportSnapshot.MaterialId
                    + ", materialClass=" + reportSnapshot.MaterialClass
                    + ", incoming=" + Format(reportSnapshot.IncomingVelocity)
                    + ", incomingSpeed=" + Format(reportSnapshot.IncomingSpeed)
                    + ", outgoing=" + Format(reportSnapshot.OutgoingVelocity)
                    + ", outgoingSpeed=" + Format(reportSnapshot.OutgoingSpeed)
                    + ", continued=" + reportSnapshot.Continued.ToString().ToLowerInvariant()
                    + ", replaced=" + reportSnapshot.Replaced.ToString().ToLowerInvariant()
                    + ", ballisticTerminal=" + reportSnapshot.BallisticTerminal.ToString().ToLowerInvariant()
                    + ", lifecycleTerminal=" + reportSnapshot.LifecycleTerminal.ToString().ToLowerInvariant()
                    + ", lifecycleEndReason=" + reportSnapshot.LifecycleEndReason
                    + ", targetSurface=" + (string.IsNullOrWhiteSpace(reportSnapshot.TargetSurface)
                        ? "none"
                        : reportSnapshot.TargetSurface)
                    + ", targetAlreadyDead=" + reportSnapshot.TargetWasAlreadyDead.ToString().ToLowerInvariant()
                    + ", terminalState=" + reportSnapshot.TerminalState
                    + ", shotState=" + reportSnapshot.ShotState
                    + ", reason=" + reportSnapshot.Reason + ".");
            }
            catch (Exception exception)
            {
                Plugin.LogHookFailure("Physical projectile lifecycle diagnostics", exception);
            }
        }

        private static FieldReportLifecycleEventSnapshot CreateReportSnapshot(
            string eventName,
            Shot? shot,
            PhysicalShotBinding binding,
            string reason,
            string? collisionIdentity,
            int recordSequence,
            int collisionOrdinal,
            string? phase,
            bool resolutionKnown,
            double now,
            Vector3 currentPosition,
            Vector3 currentVelocity,
            PhysicalVector3 incomingVelocity,
            PhysicalVector3 outgoingVelocity,
            string collisionOutcome,
            string materialId,
            string materialClass,
            bool continued,
            bool replaced,
            bool ballisticTerminal,
            bool lifecycleTerminal,
            string lifecycleEndReason,
            bool targetWasAlreadyDead,
            string? targetSurface)
        {
            PhysicalProjectileState state = binding.State;
            AmmoTemplate? ammunition = shot?.Ammo?.Template as AmmoTemplate;
            object? weapon = shot?.Weapon;
            object? weaponTemplate = ReadProperty(weapon, "Template");
            string ammunitionTemplateId = ammunition?.StringId ?? string.Empty;
            string ammunitionName = ammunition?.Name ?? string.Empty;
            string caliber = ReadStringProperty(ammunition, "Caliber");
            string weaponTemplateId = ReadStringProperty(weapon, "TemplateId");
            if (string.IsNullOrWhiteSpace(weaponTemplateId))
            {
                weaponTemplateId = ReadStringProperty(weaponTemplate, "StringId");
            }

            string weaponDisplayName = ReadStringProperty(weaponTemplate, "Name");
            bool? localPlayerShooter = ReadNullableBoolProperty(shot?.Player, "IsYourPlayer");
            string shooterAlias = FieldReportRuntime.CreateProfileAlias(shot?.PlayerProfileID);
            PhysicalVector3? shooterPosition = shot == null
                ? null
                : ToPhysical(shot.StartPosition);
            Vector3 approximateOrigin = shot?.StartPosition ?? binding.CreationPosition;
            PhysicalVector3 displacement = new PhysicalVector3(
                currentPosition.x - approximateOrigin.x,
                currentPosition.y - approximateOrigin.y,
                currentPosition.z - approximateOrigin.z);
            double approximateDistance = displacement.Magnitude;
            double? distanceTravelled = ReadNullableNonNegativeDoubleProperty(shot, "Distance");
            string targetCategory = shot?.HittedBallisticCollider?.GetType().Name ?? string.Empty;
            string targetBodyPart = shot?.HittedBallisticCollider is BodyPartCollider bodyPartCollider
                ? bodyPartCollider.BodyPartColliderType.ToString()
                : string.Empty;
            string armorContext = ResolveArmorContext(shot);
            string colliderDescriptor = shot?.HitCollider?.GetType().Name ?? string.Empty;
            string replacementRelationship = string.IsNullOrWhiteSpace(state.SourceCollisionId)
                ? string.Empty
                : "source-collision:" + state.SourceCollisionId;

            return new FieldReportLifecycleEventSnapshot(
                eventName,
                DateTimeOffset.Now,
                state.ProjectileId,
                state.RootShotId,
                state.Kind.ToString(),
                state.FragmentIndex,
                state.FragmentGeneration,
                recordSequence,
                collisionOrdinal,
                phase ?? string.Empty,
                resolutionKnown,
                binding.CreationTimeSeconds,
                lifecycleTerminal ? now : 0d,
                ToPhysical(binding.CreationPosition),
                ToPhysical(binding.CreationVelocity),
                ToPhysical(currentPosition),
                ToPhysical(currentVelocity),
                collisionIdentity ?? string.Empty,
                materialId,
                materialClass,
                incomingVelocity,
                outgoingVelocity,
                collisionOutcome,
                continued,
                replaced,
                ballisticTerminal,
                lifecycleTerminal,
                lifecycleEndReason,
                targetWasAlreadyDead,
                targetSurface ?? string.Empty,
                state.TerminalState.ToString(),
                shot?.BulletState.ToString() ?? "released",
                reason,
                localPlayerShooter,
                shooterAlias,
                weaponTemplateId,
                weaponDisplayName,
                ammunitionTemplateId,
                ammunitionName,
                caliber,
                ammunition == null ? null : ammunition.InitialSpeed,
                shooterPosition,
                targetCategory,
                targetBodyPart,
                armorContext,
                colliderDescriptor,
                distanceTravelled,
                approximateDistance,
                replacementRelationship);
        }

        private static string ResolveArmorContext(Shot? shot)
        {
            string material = shot?.HittedBallisticCollider?.TypeOfMaterial.ToString() ?? string.Empty;
            if (material == "BodyArmor")
            {
                return "body-armor";
            }

            if (material == "Helmet" || material == "HelmetRicochet")
            {
                return "helmet";
            }

            return string.Empty;
        }

        private static object? ReadProperty(object? instance, string propertyName)
        {
            try
            {
                return instance?.GetType().GetProperty(
                    propertyName,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    ?.GetValue(instance, null);
            }
            catch
            {
                return null;
            }
        }

        private static string ReadStringProperty(object? instance, string propertyName)
        {
            return ReadProperty(instance, propertyName)?.ToString() ?? string.Empty;
        }

        private static bool? ReadNullableBoolProperty(object? instance, string propertyName)
        {
            object? value = ReadProperty(instance, propertyName);
            return value is bool boolean ? boolean : (bool?)null;
        }

        private static double? ReadNullableNonNegativeDoubleProperty(
            object? instance,
            string propertyName)
        {
            object? value = ReadProperty(instance, propertyName);
            if (value == null)
            {
                return null;
            }

            try
            {
                double number = Convert.ToDouble(value, CultureInfo.InvariantCulture);
                return double.IsNaN(number) || double.IsInfinity(number) || number < 0d
                    ? null
                    : number;
            }
            catch
            {
                return null;
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
            FieldReportRuntime.RecordEvent(
                "terminal-duplicate",
                true,
                Field("projectileIdentity", snapshot.ProjectileIdentity),
                Field("rootIdentity", snapshot.RootIdentity),
                Field("projectileKind", snapshot.ProjectileKind),
                Field("fragmentIndex", snapshot.FragmentIndex),
                Field("fragmentGeneration", snapshot.FragmentGeneration),
                Field("firstTerminalReason", FormatTerminalReason(firstTerminal)),
                Field("attemptedTerminalReason", FormatTerminalReason(attemptedReason)),
                Field("firstTerminalTimestamp", firstTerminal.TerminalTimestamp),
                Field("duplicateTimestamp", duplicateTimestamp));
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
            FieldReportRuntime.RecordEvent(
                "terminal-missing",
                true,
                Field("projectileIdentity", snapshot.ProjectileIdentity),
                Field("rootIdentity", snapshot.RootIdentity),
                Field("projectileKind", snapshot.ProjectileKind),
                Field("fragmentIndex", snapshot.FragmentIndex),
                Field("fragmentGeneration", snapshot.FragmentGeneration),
                Field("removalPath", missing.RemovalReason),
                Field("creationTimestamp", snapshot.CreationTimestamp),
                Field("lastPosition", snapshot.LastKnownPosition),
                Field("lastVelocity", snapshot.LastKnownVelocity),
                Field("lastSpeed", snapshot.LastKnownSpeed),
                Field("lastCollisionIdentity", string.IsNullOrWhiteSpace(snapshot.LastCollisionIdentity)
                    ? null
                    : snapshot.LastCollisionIdentity),
                Field("lastCollisionOrdinal", snapshot.LastCollisionOrdinal),
                Field("removalTimestamp", removalTimestamp));
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
            FieldReportRuntime.RecordEvent(
                "shutdown-cleanup",
                true,
                Field("projectileIdentity", snapshot.ProjectileIdentity),
                Field("rootIdentity", snapshot.RootIdentity),
                Field("projectileKind", snapshot.ProjectileKind),
                Field("fragmentIndex", snapshot.FragmentIndex),
                Field("fragmentGeneration", snapshot.FragmentGeneration),
                Field("creationTimestamp", snapshot.CreationTimestamp),
                Field("terminalTimestamp", shutdownTimestamp),
                Field("position", snapshot.LastKnownPosition),
                Field("lastVelocity", snapshot.LastKnownVelocity),
                Field("lastSpeed", snapshot.LastKnownSpeed),
                Field("lastCollisionIdentity", string.IsNullOrWhiteSpace(snapshot.LastCollisionIdentity)
                    ? null
                    : snapshot.LastCollisionIdentity),
                Field("lastCollisionOrdinal", snapshot.LastCollisionOrdinal),
                Field("ballisticTerminal", false),
                Field("lifecycleTerminal", true),
                Field("lifecycleEndReason", "shutdown"),
                Field("reason", "shutdown-cleanup"));
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

        private static KeyValuePair<string, object?> Field(string name, object? value)
        {
            return new KeyValuePair<string, object?>(name, value);
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
