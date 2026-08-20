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
        private static readonly PhysicalCollisionEventDeduplicator CollisionDeduplicator =
            new PhysicalCollisionEventDeduplicator();
        private static readonly HashSet<string> NumericRunawayStages =
            new HashSet<string>(StringComparer.Ordinal);
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

        internal static bool RecordNumericRunawayIfPresent(
            Shot shot,
            PhysicalShotBinding binding,
            string stage)
        {
            if (shot == null || binding == null || !binding.Matches(shot))
            {
                return false;
            }

            PhysicalProjectileState state = binding.State;
            PhysicalVector3 position = ToPhysical(shot.CurrentPosition);
            PhysicalVector3 hostVelocity = ToPhysical(shot.CurrentVelocity);
            if (!PhysicalNumericRunawayDetector.IsRunaway(
                    state.TranslationalKineticEnergyJoules,
                    state.RetainedMassKilograms,
                    hostVelocity,
                    position))
            {
                return false;
            }

            string exactStage = string.IsNullOrWhiteSpace(stage) ? "unknown" : stage;
            string dedupeKey = state.ProjectileId + "|" + exactStage;
            lock (CollisionLogLock)
            {
                if (!NumericRunawayStages.Add(dedupeKey))
                {
                    return true;
                }
            }

            try
            {
                PhysicalCollisionRecord? lastCollision = state.CollisionHistory.Count > 0
                    ? state.CollisionHistory[state.CollisionHistory.Count - 1]
                    : null;
                FieldReportRuntime.RecordEvent(
                    "numeric-runaway",
                    true,
                    Field("projectileIdentity", state.ProjectileId),
                    Field("rootIdentity", state.RootShotId),
                    Field("collisionIdentity", lastCollision?.CollisionId),
                    Field("massKilograms", state.RetainedMassKilograms),
                    Field("energyJoules", state.TranslationalKineticEnergyJoules),
                    Field("diameterMetres", state.EquivalentDiameterMetres),
                    Field("assignedPhysicalSpeed", state.SpeedMetresPerSecond),
                    Field("eftBallisticCoefficient", binding.EftBallisticCoefficient),
                    Field("incomingVelocity", lastCollision?.IncomingVelocityMetresPerSecond),
                    Field("outgoingVelocity", lastCollision?.OutgoingVelocityMetresPerSecond),
                    Field("projectedVelocity", ToPhysical(binding.CreationVelocity)),
                    Field("hostMeasuredVelocity", hostVelocity),
                    Field("hostMeasuredSpeed", hostVelocity.Magnitude),
                    Field("position", position),
                    Field("deltaTimeSeconds", Math.Max(
                        0d,
                        Time.realtimeSinceStartupAsDouble - binding.CreationTimeSeconds)),
                    Field("material", lastCollision?.MaterialClass.ToString()),
                    Field("outcome", lastCollision?.Outcome.ToString()),
                    Field("stage", exactStage));
            }
            catch (Exception exception)
            {
                Plugin.LogHookFailure("Physical numeric runaway diagnostics", exception);
            }

            return true;
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
                    CollisionDeduplicator.Clear();
                    NumericRunawayStages.Clear();
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
                bool shotBindingMatched = shot != null && binding.Matches(shot);
                bool isCanonicalTerminal = TryResolveCanonicalTerminalReason(
                    eventName,
                    reason,
                    out PhysicalLifecycleTerminalReason attemptedTerminalReason);
                PhysicalLifecycleTerminalAttempt? terminalAttempt = null;
                PhysicalLifecycleSnapshot? contextSnapshot = null;
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

                    LifecycleTracker.TryGetActiveSnapshot(
                        state.ProjectileId,
                        out contextSnapshot);

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
                PhysicalLifecycleReportContext reportContext =
                    PhysicalLifecycleReportContext.Resolve(
                        shotBindingMatched,
                        shotBindingMatched && shot != null
                            ? ToPhysical(shot.CurrentPosition)
                            : PhysicalVector3.Zero,
                        shotBindingMatched && shot != null
                            ? ToPhysical(shot.CurrentVelocity)
                            : PhysicalVector3.Zero,
                        contextSnapshot,
                        ToPhysical(binding.CreationPosition),
                        ToPhysical(binding.CreationVelocity));
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
                    reportContext.Position,
                    reportContext.Velocity,
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
                    targetSurfaceIdentity,
                    reportContext.ShotBindingMatched,
                    reportContext.ContextSource);

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
            PhysicalVector3 currentPosition,
            PhysicalVector3 currentVelocity,
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
            string? targetSurface,
            bool shotBindingMatched,
            string contextSource)
        {
            PhysicalProjectileState state = binding.State;
            Shot? safeShot = shotBindingMatched ? shot : null;
            AmmoTemplate? ammunition = safeShot?.Ammo?.Template as AmmoTemplate;
            object? weapon = safeShot?.Weapon;
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
            bool? localPlayerShooter = ReadNullableBoolProperty(safeShot?.Player, "IsYourPlayer");
            string shooterAlias = FieldReportRuntime.CreateProfileAlias(safeShot?.PlayerProfileID);
            PhysicalVector3? shooterPosition = safeShot == null
                ? null
                : ToPhysical(safeShot.StartPosition);
            PhysicalVector3 approximateOrigin = safeShot == null
                ? ToPhysical(binding.CreationPosition)
                : ToPhysical(safeShot.StartPosition);
            PhysicalVector3 displacement = new PhysicalVector3(
                currentPosition.X - approximateOrigin.X,
                currentPosition.Y - approximateOrigin.Y,
                currentPosition.Z - approximateOrigin.Z);
            double approximateDistance = displacement.Magnitude;
            double? distanceTravelled = ReadNullableNonNegativeDoubleProperty(safeShot, "Distance");
            string targetCategory = safeShot?.HittedBallisticCollider?.GetType().Name ?? string.Empty;
            string targetBodyPart = safeShot?.HittedBallisticCollider is BodyPartCollider bodyPartCollider
                ? bodyPartCollider.BodyPartColliderType.ToString()
                : string.Empty;
            string armorContext = ResolveArmorContext(safeShot);
            string colliderDescriptor = safeShot?.HitCollider?.GetType().Name ?? string.Empty;
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
                currentPosition,
                currentVelocity,
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
                safeShot?.BulletState.ToString() ?? "released",
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
                replacementRelationship,
                shotBindingMatched,
                contextSource);
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
            bool matched = shot != null && binding.Matches(shot);
            Vector3 position = matched ? shot!.CurrentPosition : binding.CreationPosition;
            Vector3 velocity = matched ? shot!.CurrentVelocity : binding.CreationVelocity;
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
                return !CollisionDeduplicator.TryRecord(
                    projectileIdentity,
                    collisionIdentity,
                    phase);
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
                CollisionDeduplicator.ClearProjectile(projectileIdentity);
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
                Field("removalTimestamp", removalTimestamp),
                Field("shotBindingMatched", false),
                Field("contextSource", "tracker-snapshot"));
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

    }
}
