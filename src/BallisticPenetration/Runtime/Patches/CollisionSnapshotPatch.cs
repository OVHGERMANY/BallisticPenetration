using System;
using System.Reflection;
using EFT.Ballistics;
using EFT.InventoryLogic;
using HarmonyLib;
using SPT.Reflection.Patching;
using UnityEngine;
using BallisticPenetration.Runtime.State;

namespace BallisticPenetration.Runtime.Patches
{
    /// <summary>
    /// Saves damage and penetration before HandleCollision runs. Diagnostic mode
    /// also keeps skipped hits so the overlay can explain them.
    /// </summary>
    internal sealed class CollisionSnapshotPatch : ModulePatch
    {
        internal const string HarmonyOwnerId = "com.janky.ballisticpenetration.collision-snapshot";

        private readonly MethodInfo _target;

        internal CollisionSnapshotPatch(MethodInfo target)
            : base(HarmonyOwnerId)
        {
            _target = target ?? throw new ArgumentNullException(nameof(target));
        }

        protected override MethodBase GetTargetMethod()
        {
            return _target;
        }

        [PatchPrefix]
        [HarmonyPriority(Priority.Last)]
        private static void Prefix(
            Shot __instance,
            Vector3 prevVector3,
            out CollisionContext? __state)
        {
            __state = null;

            try
            {
                if (__instance == null)
                {
                    return;
                }

                PluginConfiguration? configuration = Plugin.Configuration;
                if (configuration == null)
                {
                    return;
                }

                bool pluginEnabled = configuration.Enabled.Value;
                bool diagnosticsEnabled = configuration.EnableInGameDiagnostics.Value;
                bool isForwardHit = __instance.IsForwardHit;

                // Enabled forward hits always need saved values. Diagnostics also
                // keep hits that this plugin will skip.
                if (!diagnosticsEnabled && (!pluginEnabled || !isForwardHit))
                {
                    return;
                }

                float damage = __instance.Damage;
                float penetrationPower = __instance.PenetrationPower;
                AmmoTemplate? ammoTemplate = __instance.Ammo?.Template as AmmoTemplate;
                float templateInitialSpeed = ammoTemplate != null ? ammoTemplate.InitialSpeed : 0f;

                CollisionAdjustmentResult result = DetermineInitialResult(
                    pluginEnabled,
                    isForwardHit,
                    ammoTemplate,
                    damage,
                    penetrationPower,
                    templateInitialSpeed);

                string? templateId = ammoTemplate != null ? ammoTemplate.StringId : null;
                string? templateName = ammoTemplate != null ? ammoTemplate.Name : null;
                bool hasPreviousFramePosition = diagnosticsEnabled && IsFiniteVector3(prevVector3);

                CollisionContext context = new CollisionContext(
                    damage,
                    penetrationPower,
                    templateInitialSpeed,
                    templateId,
                    templateName,
                    hasPreviousFramePosition,
                    prevVector3,
                    result);

                CollisionContextStore.Set(__instance, context);
                __state = context;
            }
            catch (Exception exception)
            {
                Plugin.LogHookFailure("HandleCollision snapshot prefix", exception);
            }
        }

        [PatchFinalizer]
        private static Exception? Finalizer(
            Shot __instance,
            CollisionContext? __state,
            Exception? __exception)
        {
            try
            {
                if (__instance != null && __state != null)
                {
                    CollisionContextStore.RemoveIfSame(__instance, __state);
                }
            }
            catch (Exception cleanupException)
            {
                Plugin.LogHookFailure("HandleCollision snapshot finalizer", cleanupException);
            }

            return __exception;
        }

        private static CollisionAdjustmentResult DetermineInitialResult(
            bool pluginEnabled,
            bool isForwardHit,
            AmmoTemplate? ammoTemplate,
            float damage,
            float penetrationPower,
            float templateInitialSpeed)
        {
            if (!pluginEnabled)
            {
                return CollisionAdjustmentResult.PluginDisabled;
            }

            if (!isForwardHit)
            {
                return CollisionAdjustmentResult.NonForwardHit;
            }

            if (ammoTemplate == null)
            {
                return CollisionAdjustmentResult.MissingTemplate;
            }

            if (!IsFiniteNonNegative(damage)
                || !IsFiniteNonNegative(penetrationPower)
                || !IsFinitePositive(templateInitialSpeed))
            {
                return CollisionAdjustmentResult.InputInvalid;
            }

            return CollisionAdjustmentResult.None;
        }

        private static bool IsFinitePositive(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;
        }

        private static bool IsFiniteNonNegative(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f;
        }

        private static bool IsFiniteVector3(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
