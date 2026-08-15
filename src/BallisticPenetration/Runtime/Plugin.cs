using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using HarmonyLib;
using BallisticPenetration.Core;
using BallisticPenetration.Runtime;
using BallisticPenetration.Runtime.Diagnostics;
using BallisticPenetration.Runtime.Patches;
using BallisticPenetration.Runtime.Rendering;

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
        internal const string PluginVersion = "1.3.0";

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
                Logger.LogInfo(PluginName + " loaded for SPT 4.1.2.");
            }
            catch (Exception exception)
            {
                Logger.LogError(PluginName + " failed to load; its patches were disabled. " + exception);
                throw;
            }
        }

        private void OnDestroy()
        {
            _isShuttingDown = true;
            PhysicalProjectileVisualRuntime.Shutdown();
            // Remove the optional overlay and trace objects.
            DiagnosticsRuntime.Shutdown();
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
            double penetrationFactor)
        {
            try
            {
                ManualLogSource? logger = Log;
                if (logger != null)
                {
                    logger.LogInfo(
                        "Applied ballistic falloff: ammo=" + ammoTemplateId
                        + ", impact=" + impactSpeed
                        + ", template=" + templateSpeed
                        + ", damage=" + preCollisionDamage + " -> " + adjustedDamage
                        + " (factor " + damageFactor + ")"
                        + ", penetration=" + preCollisionPenetrationPower + " -> " + adjustedPenetrationPower
                        + " (factor " + penetrationFactor + ").");
                }
            }
            catch
            {
                // Optional diagnostics must not affect a live collision.
            }
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
    }
}
