using System;
using System.Reflection;
using EFT.Ballistics;
using HarmonyLib;
using SPT.Reflection.Patching;
using BallisticPenetration.Core;
using BallisticPenetration.Runtime.Diagnostics;
using BallisticPenetration.Runtime.State;

namespace BallisticPenetration.Runtime.Patches
{
    /// <summary>
    /// Hook B: runs at the start of Shot.CreateFragments, after HandleCollision
    /// has interpolated the impact velocity and immediately before the original
    /// fragment/penetration logic consumes Damage and PenetrationPower.
    /// </summary>
    internal sealed class FragmentFalloffPatch : ModulePatch
    {
        internal const string HarmonyOwnerId = "com.janky.ballisticpenetration.fragment-falloff";

        private readonly MethodInfo _target;

        internal FragmentFalloffPatch(MethodInfo target)
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
        private static void Prefix(Shot __instance)
        {
            CollisionContext? context = null;
            float impactSpeed = 0f;
            BallisticFalloffFactors diagnosticFactors = BallisticFalloffFactors.NeutralFallback;

            try
            {
                if (__instance == null)
                {
                    return;
                }

                if (!CollisionContextStore.TryTake(__instance, out context) || context == null)
                {
                    return;
                }

                // Stock collision degradation has already run. Save the values
                // received by this prefix.
                context.PatchInputDamage = __instance.Damage;
                context.PatchInputPenetrationPower = __instance.PenetrationPower;

                // HandleCollision has already interpolated this speed to the hit
                // point. Zero is valid and is never raised to a floor.
                impactSpeed = __instance._currentVelocity.magnitude;

                PluginConfiguration? configuration = Plugin.Configuration;
                if (configuration == null || !configuration.Enabled.Value)
                {
                    context.AdjustmentResult = CollisionAdjustmentResult.PluginDisabled;
                    RecordDiagnostic(__instance, context, impactSpeed, diagnosticFactors);
                    return;
                }

                if (context.AdjustmentResult != CollisionAdjustmentResult.None)
                {
                    RecordDiagnostic(__instance, context, impactSpeed, diagnosticFactors);
                    return;
                }

                double penetrationExponent;
                double damageExponent;
                if (!configuration.TryGetExponentValues(out penetrationExponent, out damageExponent))
                {
                    context.AdjustmentResult = CollisionAdjustmentResult.InvalidExponents;
                    RecordDiagnostic(__instance, context, impactSpeed, diagnosticFactors);
                    return;
                }

                if (!IsValidImpactSpeed(impactSpeed))
                {
                    context.AdjustmentResult = CollisionAdjustmentResult.InvalidImpactSpeed;
                    RecordDiagnostic(__instance, context, impactSpeed, diagnosticFactors);
                    return;
                }

                FalloffExponentConfiguration exponents =
                    new FalloffExponentConfiguration(penetrationExponent, damageExponent);
                BallisticFalloffFailureReason failureReason;
                if (!BallisticFalloffCalculator.TryCalculate(
                    impactSpeed,
                    context.TemplateInitialSpeed,
                    exponents,
                    out diagnosticFactors,
                    out failureReason))
                {
                    context.AdjustmentResult = CollisionAdjustmentResult.CalculationFailed;
                    RecordDiagnostic(__instance, context, impactSpeed, diagnosticFactors);
                    return;
                }

                double adjustedDamage = context.EntryDamage * diagnosticFactors.DamageFactor;
                double adjustedPenetrationPower = context.EntryPenetrationPower * diagnosticFactors.PenetrationFactor;
                float damage = (float)adjustedDamage;
                float penetrationPower = (float)adjustedPenetrationPower;
                if (!IsFiniteNonNegative(adjustedDamage)
                    || !IsFiniteNonNegative(adjustedPenetrationPower)
                    || !IsFiniteNonNegative(damage)
                    || !IsFiniteNonNegative(penetrationPower))
                {
                    context.AdjustmentResult = CollisionAdjustmentResult.CalculationFailed;
                    RecordDiagnostic(__instance, context, impactSpeed, diagnosticFactors);
                    return;
                }

                // Replace the stock result with the uncapped velocity curve.
                __instance.Damage = damage;
                __instance.PenetrationPower = penetrationPower;
                context.LocalOutputDamage = damage;
                context.LocalOutputPenetrationPower = penetrationPower;
                context.AdjustmentResult = CollisionAdjustmentResult.Applied;

                // Record the values written by this patch.
                RecordDiagnostic(__instance, context, impactSpeed, diagnosticFactors);

                if (configuration.LogAdjustments.Value)
                {
                    Plugin.LogAdjustment(
                        context.TemplateId,
                        impactSpeed,
                        context.TemplateInitialSpeed,
                        context.EntryDamage,
                        context.EntryPenetrationPower,
                        __instance.Damage,
                        __instance.PenetrationPower,
                        diagnosticFactors.DamageFactor,
                        diagnosticFactors.PenetrationFactor);
                }
            }
            catch (Exception exception)
            {
                if (context != null)
                {
                    context.AdjustmentResult = CollisionAdjustmentResult.CalculationFailed;
                    RecordDiagnostic(__instance, context, impactSpeed, diagnosticFactors);
                }

                Plugin.LogHookFailure("CreateFragments falloff prefix", exception);
            }
        }

        private static void RecordDiagnostic(
            Shot shot,
            CollisionContext context,
            float impactSpeed,
            BallisticFalloffFactors factors)
        {
            DiagnosticsRuntime.TryRecordAdjustment(shot, context, impactSpeed, factors);
        }

        private static bool IsValidImpactSpeed(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f;
        }

        private static bool IsFiniteNonNegative(double value)
        {
            return !double.IsNaN(value)
                && !double.IsInfinity(value)
                && value >= 0d;
        }

        private static bool IsFiniteNonNegative(float value)
        {
            return !float.IsNaN(value)
                && !float.IsInfinity(value)
                && value >= 0f;
        }
    }
}
