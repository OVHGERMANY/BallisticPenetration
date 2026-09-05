using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using HarmonyLib;
using BallisticPenetration.Core;
using BallisticPenetration.Core.Physics;
using BallisticPenetration.Runtime;
using BallisticPenetration.Runtime.Diagnostics;
using BallisticPenetration.Runtime.Patches;
using BallisticPenetration.Runtime.Rendering;
using BallisticPenetration.Runtime.State;
using EFT.Ballistics;

namespace BallisticPenetration
{
    // BepInEx GUID fields are reverse-domain plugin identifiers, not System.Guid values.
#pragma warning disable CA2243
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInProcess("EscapeFromTarkov.exe")]
    [BepInDependency(
        SptVersionCompatibility.CorePluginGuid,
        SptVersionCompatibility.SupportedCoreVersionText)]
#pragma warning restore CA2243
    public sealed class Plugin : BaseUnityPlugin
    {
        internal const string PluginGuid = "com.janky.ballisticpenetration";
        internal const string PluginName = "Janky-BallisticPenetration";
        internal const string PluginVersion = "1.3.1";

        private CollisionSnapshotPatch? _collisionSnapshotPatch;
        private FragmentFalloffPatch? _fragmentFalloffPatch;
        private BodyPartColliderPostmortemArmorPatch? _bodyPartColliderPostmortemArmorPatch;
        private ArmorPlateColliderPostmortemArmorPatch? _armorPlateColliderPostmortemArmorPatch;
        private bool _isShuttingDown;

        internal static PluginConfiguration? Configuration { get; private set; }

        internal static ManualLogSource? Log { get; private set; }

        private void Awake()
        {
            Log = Logger;
            Configuration = new PluginConfiguration(Config);

            try
            {
                EnsureExactSptCoreVersion();
                FieldReportRuntime.Initialize(Configuration);

                // Resolve and verify every signature before any Harmony mutation.
                MethodInfo handleCollisionTarget = TargetMethodResolver.ResolveHandleCollision();
                MethodInfo createFragmentsTarget = TargetMethodResolver.ResolveCreateFragments();
                MethodInfo bodyPartColliderApplyHitTarget =
                    TargetMethodResolver.ResolveBodyPartColliderApplyHit();
                MethodInfo armorPlateColliderApplyHitTarget =
                    TargetMethodResolver.ResolveArmorPlateColliderApplyHit();

                CollisionSnapshotPatch collisionSnapshotPatch =
                    new CollisionSnapshotPatch(handleCollisionTarget);
                FragmentFalloffPatch fragmentFalloffPatch =
                    new FragmentFalloffPatch(createFragmentsTarget);
                BodyPartColliderPostmortemArmorPatch bodyPartColliderPostmortemArmorPatch =
                    new BodyPartColliderPostmortemArmorPatch(bodyPartColliderApplyHitTarget);
                ArmorPlateColliderPostmortemArmorPatch armorPlateColliderPostmortemArmorPatch =
                    new ArmorPlateColliderPostmortemArmorPatch(armorPlateColliderApplyHitTarget);
                _collisionSnapshotPatch = collisionSnapshotPatch;
                _fragmentFalloffPatch = fragmentFalloffPatch;
                _bodyPartColliderPostmortemArmorPatch = bodyPartColliderPostmortemArmorPatch;
                _armorPlateColliderPostmortemArmorPatch = armorPlateColliderPostmortemArmorPatch;

                WarnAboutCompetingPatchOwners(
                    handleCollisionTarget,
                    "Shot.HandleCollision(float, Vector3, Vector3)",
                    collisionSnapshotPatch.HarmonyId);
                WarnAboutCompetingPatchOwners(
                    createFragmentsTarget,
                    "Shot.CreateFragments()",
                    fragmentFalloffPatch.HarmonyId);
                WarnAboutCompetingPatchOwners(
                    bodyPartColliderApplyHitTarget,
                    "BodyPartCollider.ApplyHit(DamageInfo, ShotId)",
                    bodyPartColliderPostmortemArmorPatch.HarmonyId);
                WarnAboutCompetingPatchOwners(
                    armorPlateColliderApplyHitTarget,
                    "ArmorPlateCollider.ApplyHit(DamageInfo, ShotId)",
                    armorPlateColliderPostmortemArmorPatch.HarmonyId);

                EnablePatchesTransactionally();
                Logger.LogInfo(PluginName + " " + BuildVersion + " loaded for SPT "
                    + SptVersionCompatibility.SupportedCoreVersionText
                    + ". Compatibility update; gameplay behavior unchanged.");
            }
            catch (Exception exception)
            {
                FieldReportRuntime.RecordRuntimeError("plugin-startup", exception);
                Logger.LogError(PluginName + " failed to load; its patches were disabled. " + exception);
                throw;
            }
        }

        private void OnDestroy()
        {
            _isShuttingDown = true;
            PhysicalProjectileLifecycleDiagnostics.ShutdownExpected();
            PhysicalProjectileVisualRuntime.Shutdown();
            // Remove the optional overlay and trace objects.
            DiagnosticsRuntime.Shutdown();
            FieldReportRuntime.Shutdown();
        }

        private void Update()
        {
            if (_isShuttingDown)
            {
                return;
            }

            // Unity objects are created from this main-thread callback.
            PhysicalProjectileVisualRuntime.UpdatePresentation();
            DiagnosticsRuntime.UpdatePresentation();
            PluginConfiguration? configuration = Configuration;
            if (configuration != null)
            {
                FieldReportRuntime.UpdateIssueMarker(configuration);
            }
        }

        [SuppressMessage(
            "Design",
            "CA1031:Do not catch general exception types",
            Justification = "Logging must never replace an exception raised by the game or another patch.")]
        internal static void LogHookFailure(string hookName, Exception exception)
        {
            try
            {
                ManualLogSource? logger = Log;
                if (logger != null)
                {
                    logger.LogWarning(
                        hookName
                        + " failed; BallisticPenetration will make no further changes to this shot. "
                        + exception);
                }

                FieldReportRuntime.RecordRuntimeError(hookName, exception);
            }
            catch
            {
                // Preserve the game's original exception.
            }
        }

        private void EnsureExactSptCoreVersion()
        {
            PluginInfo corePluginInfo;
            if (!Chainloader.PluginInfos.TryGetValue(
                    SptVersionCompatibility.CorePluginGuid,
                    out corePluginInfo)
                || corePluginInfo == null
                || corePluginInfo.Metadata == null
                || corePluginInfo.Metadata.Version == null)
            {
                string missingMessage =
                    "Compatibility check failed: loaded BepInEx plugin "
                    + SptVersionCompatibility.CorePluginGuid
                    + " has a missing version; required exact version is "
                    + SptVersionCompatibility.SupportedCoreVersionText
                    + ". Refusing to enable any Harmony patch.";
                Logger.LogError(missingMessage);
                throw new InvalidOperationException(missingMessage);
            }

            Version actualVersion = corePluginInfo.Metadata.Version;
            if (!SptVersionCompatibility.IsExactSupportedCoreVersion(actualVersion))
            {
                string mismatchMessage =
                    "Compatibility check failed: loaded BepInEx plugin "
                    + SptVersionCompatibility.CorePluginGuid
                    + " is version " + actualVersion
                    + "; required exact version is "
                    + SptVersionCompatibility.SupportedCoreVersionText
                    + ". Refusing to enable any Harmony patch.";
                Logger.LogError(mismatchMessage);
                throw new InvalidOperationException(mismatchMessage);
            }

            Logger.LogInfo(
                "Compatibility check passed: loaded BepInEx plugin "
                + SptVersionCompatibility.CorePluginGuid
                + " is exactly version " + actualVersion + ".");
        }

        [SuppressMessage(
            "Design",
            "CA1031:Do not catch general exception types",
            Justification = "Optional collision logging must fail open for every logger implementation failure.")]
        internal static void LogAdjustment(
            string? ammoTemplateId,
            float impactSpeed,
            float templateSpeed,
            float preCollisionDamage,
            float preCollisionPenetrationPower,
            float adjustedDamage,
            float adjustedPenetrationPower,
            double damageFactor,
            double penetrationFactor,
            string componentId,
            string rootShotId,
            string collisionId,
            int collisionOrdinal,
            double previousDamageFactor,
            double previousPenetrationFactor,
            double appliedDamageRatio,
            double appliedPenetrationRatio)
        {
            try
            {
                ManualLogSource? logger = Log;
                if (logger != null)
                {
                    logger.LogInfo(
                        "Applied ballistic falloff: ammo=" + ammoTemplateId
                        + ", component=" + componentId
                        + ", root=" + rootShotId
                        + ", transition=" + collisionId
                        + ", ordinal=" + collisionOrdinal
                        + ", impact=" + impactSpeed
                        + ", template=" + templateSpeed
                        + ", damage=" + preCollisionDamage + " -> " + adjustedDamage
                        + " (previous " + previousDamageFactor
                        + ", current " + damageFactor
                        + ", applied " + appliedDamageRatio + ")"
                        + ", penetration=" + preCollisionPenetrationPower + " -> " + adjustedPenetrationPower
                        + " (previous " + previousPenetrationFactor
                        + ", current " + penetrationFactor
                        + ", applied " + appliedPenetrationRatio + ").");
                }
            }
            catch
            {
                // Optional diagnostics must not affect a live collision.
            }
        }

        internal static void LogPhysicalTransitionPrepared(
            Shot shot,
            PhysicalRuntimeCollisionState collisionState)
        {
            ManualLogSource? logger = Log;
            PluginConfiguration? configuration = Configuration;
            if (logger == null
                || configuration == null
                || !configuration.LogAdjustments.Value
                || shot == null
                || collisionState == null)
            {
                return;
            }

            PhysicalProjectileState parent = collisionState.ParentState;
            string normalizationOwnership = "unbound";
            double previousDamageFactor = 1d;
            double previousPenetrationFactor = 1d;
            if (ShotNormalizationBindingStore.TryGet(
                    shot,
                    out ShotNormalizationBinding? normalizationBinding)
                && normalizationBinding != null)
            {
                normalizationOwnership = normalizationBinding.State.Ownership.ToString();
                previousDamageFactor = normalizationBinding.State.RepresentedDamageFactor;
                previousPenetrationFactor =
                    normalizationBinding.State.RepresentedPenetrationFactor;
            }

            BallisticFalloffFactors currentFactors = CalculateAbsoluteSpeedFactors(shot);
            logger.LogInfo(
                "Physical transition prepared: transition=" + collisionState.TransitionId
                + ", projectile=" + parent.ProjectileId
                + ", root=" + parent.RootShotId
                + ", ordinal=" + parent.CollisionHistory.Count
                + ", preparedVelocity=" + FormatVector(parent.VelocityMetresPerSecond)
                + ", preparedSpeed=" + FormatDouble(parent.SpeedMetresPerSecond)
                + ", preparedEnergy=" + FormatDouble(parent.TranslationalKineticEnergyJoules)
                + ", retainedMass=" + FormatDouble(parent.RetainedMassKilograms)
                + ", capturedVelocity=("
                + FormatDouble(shot.CurrentVelocity.x) + ","
                + FormatDouble(shot.CurrentVelocity.y) + ","
                + FormatDouble(shot.CurrentVelocity.z) + ")"
                + ", capturedSpeed=" + FormatDouble(shot.CurrentVelocity.magnitude)
                + ", damage=" + FormatDouble(collisionState.ParentEftDamage)
                + ", penetration=" + FormatDouble(
                    collisionState.ParentEftPenetrationPower)
                + ", normalizationOwnership=" + normalizationOwnership
                + ", previousDamageFactor=" + FormatDouble(previousDamageFactor)
                + ", currentDamageFactor=" + FormatDouble(currentFactors.DamageFactor)
                + ", previousPenetrationFactor=" + FormatDouble(
                    previousPenetrationFactor)
                + ", currentPenetrationFactor=" + FormatDouble(
                    currentFactors.PenetrationFactor)
                + ".");
        }

        internal static void LogPhysicalComponentProjected(
            PhysicalRuntimeCollisionState collisionState,
            PhysicalProjectileState component,
            PhysicalEftProjectileProjection projection,
            Shot child)
        {
            ManualLogSource? logger = Log;
            PluginConfiguration? configuration = Configuration;
            if (logger == null
                || configuration == null
                || !configuration.LogAdjustments.Value
                || collisionState == null
                || component == null
                || projection == null
                || child == null)
            {
                return;
            }

            BallisticFalloffFactors componentFactors = CalculateAbsoluteSpeedFactors(child);
            logger.LogInfo(
                "Physical component projected: transition=" + collisionState.TransitionId
                + ", projectile=" + component.ProjectileId
                + ", root=" + component.RootShotId
                + ", ordinal=" + component.CollisionHistory.Count
                + ", kind=" + component.Kind
                + ", resolvedVelocity=" + FormatVector(component.VelocityMetresPerSecond)
                + ", resolvedSpeed=" + FormatDouble(component.SpeedMetresPerSecond)
                + ", resolvedEnergy=" + FormatDouble(
                    component.TranslationalKineticEnergyJoules)
                + ", retainedMass=" + FormatDouble(component.RetainedMassKilograms)
                + ", projectedSpeed=" + FormatDouble(projection.SpeedMetresPerSecond)
                + ", shotVelocity=("
                + FormatDouble(child.CurrentVelocity.x) + ","
                + FormatDouble(child.CurrentVelocity.y) + ","
                + FormatDouble(child.CurrentVelocity.z) + ")"
                + ", shotSpeed=" + FormatDouble(child.CurrentVelocity.magnitude)
                + ", damage=" + FormatDouble(child.Damage)
                + ", penetration=" + FormatDouble(child.PenetrationPower)
                + ", normalizationOwnership="
                + BallisticNormalizationOwnership.PhysicalCapability
                + ", baselineDamageFactor=" + FormatDouble(componentFactors.DamageFactor)
                + ", baselinePenetrationFactor=" + FormatDouble(
                    componentFactors.PenetrationFactor)
                + ".");
        }

        internal static void LogPhysicalBridgeFallback(string stage, Shot shot)
        {
            ManualLogSource? logger = Log;
            PluginConfiguration? configuration = Configuration;
            if (logger == null
                || configuration == null
                || !configuration.LogAdjustments.Value
                || shot == null)
            {
                return;
            }

            string componentId = "unbound";
            string rootId = "unbound";
            int ordinal = 0;
            if (ShotNormalizationBindingStore.TryGet(
                    shot,
                    out ShotNormalizationBinding? normalizationBinding)
                && normalizationBinding != null)
            {
                componentId = normalizationBinding.State.ComponentId;
                rootId = normalizationBinding.State.RootShotId;
                ordinal = normalizationBinding.State.CollisionOrdinal;
            }

            logger.LogInfo(
                "Physical bridge fallback: stage=" + stage
                + ", component=" + componentId
                + ", root=" + rootId
                + ", ordinal=" + ordinal
                + ", fireIndex=" + shot.FireIndex
                + ", randomSeed=" + shot.RandomSeed
                + ", fragmentIndex=" + shot.FragmentIndex
                + ", capturedSpeed=" + FormatDouble(shot.CurrentVelocity.magnitude)
                + ", damage=" + FormatDouble(shot.Damage)
                + ", penetration=" + FormatDouble(shot.PenetrationPower)
                + ".");
        }

        private static string FormatVector(PhysicalVector3 value)
        {
            return "("
                + FormatDouble(value.X) + ","
                + FormatDouble(value.Y) + ","
                + FormatDouble(value.Z) + ")";
        }

        private static BallisticFalloffFactors CalculateAbsoluteSpeedFactors(Shot shot)
        {
            PluginConfiguration? configuration = Configuration;
            EFT.InventoryLogic.AmmoTemplate? template =
                shot.Ammo?.Template as EFT.InventoryLogic.AmmoTemplate;
            if (configuration == null
                || template == null
                || !configuration.TryGetExponentValues(
                    out double penetrationExponent,
                    out double damageExponent)
                || !BallisticFalloffCalculator.TryCalculate(
                    shot.CurrentVelocity.magnitude,
                    template.InitialSpeed,
                    new FalloffExponentConfiguration(
                        penetrationExponent,
                        damageExponent),
                    out BallisticFalloffFactors factors,
                    out _))
            {
                return BallisticFalloffFactors.NeutralFallback;
            }

            return factors;
        }

        private static string FormatDouble(double value)
        {
            return value.ToString("R", CultureInfo.InvariantCulture);
        }

        private void EnablePatchesTransactionally()
        {
            CollisionSnapshotPatch collisionSnapshotPatch = _collisionSnapshotPatch
                ?? throw new InvalidOperationException("Collision snapshot patch was not created.");
            FragmentFalloffPatch fragmentFalloffPatch = _fragmentFalloffPatch
                ?? throw new InvalidOperationException("Fragment falloff patch was not created.");
            BodyPartColliderPostmortemArmorPatch bodyPartColliderPostmortemArmorPatch =
                _bodyPartColliderPostmortemArmorPatch
                ?? throw new InvalidOperationException("Body-part armor patch was not created.");
            ArmorPlateColliderPostmortemArmorPatch armorPlateColliderPostmortemArmorPatch =
                _armorPlateColliderPostmortemArmorPatch
                ?? throw new InvalidOperationException("Armor-plate patch was not created.");

            try
            {
                collisionSnapshotPatch.Enable();
                fragmentFalloffPatch.Enable();
                bodyPartColliderPostmortemArmorPatch.Enable();
                armorPlateColliderPostmortemArmorPatch.Enable();
            }
            catch
            {
                // ModulePatch can fail after assigning a target, so roll back all
                // patch owners rather than relying solely on IsActive.
                DisableForRollback(armorPlateColliderPostmortemArmorPatch);
                DisableForRollback(bodyPartColliderPostmortemArmorPatch);
                DisableForRollback(fragmentFalloffPatch);
                DisableForRollback(collisionSnapshotPatch);
                throw;
            }
        }

        [SuppressMessage(
            "Design",
            "CA1031:Do not catch general exception types",
            Justification = "Transactional rollback must continue disabling remaining patch owners after any cleanup failure.")]
        private static void DisableForRollback(SPT.Reflection.Patching.ModulePatch? patch)
        {
            if (patch == null || patch.TargetMethod == null)
            {
                return;
            }

            try
            {
                patch.Disable();
            }
            catch (Exception cleanupException)
            {
                Log?.LogError("Failed while rolling back " + patch.GetType().Name + ": " + cleanupException);
            }
        }

        private void WarnAboutCompetingPatchOwners(MethodBase target, string targetName, string ownHarmonyId)
        {
            HarmonyLib.Patches patchInfo = Harmony.GetPatchInfo(target);
            if (patchInfo == null)
            {
                return;
            }

            List<string> competingOwners = patchInfo.Owners
                .Where(owner => !string.IsNullOrEmpty(owner) && !string.Equals(owner, ownHarmonyId, StringComparison.Ordinal))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(owner => owner, StringComparer.Ordinal)
                .ToList();

            if (competingOwners.Count > 0)
            {
                Logger.LogWarning(
                    targetName
                    + " already has Harmony patch owner(s): "
                    + string.Join(", ", competingOwners)
                    + ". " + PluginName + " will continue with Priority.Last prefixes.");
            }
        }

        internal const string BuildVersion = "1.3.1";
    }
}
