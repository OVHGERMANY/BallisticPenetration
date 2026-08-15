using System;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using EFT.Ballistics;
using EFT.InventoryLogic;
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
        [SuppressMessage(
            "Design",
            "CA1031:Do not catch general exception types",
            Justification = "The Harmony prefix must preserve vanilla values for every host or compatibility failure.")]
        private static void Prefix(
            Shot __instance,
            out PhysicalRuntimeCollisionState? __state)
        {
            __state = null;
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

                if (configuration.EnableExperimentalPhysicalProjectiles.Value)
                {
                    PhysicalBoundFlightResult physicalResult =
                        PhysicalProjectileRuntime.TryApplyBoundFlight(
                            __instance,
                            out PhysicalRuntimeCollisionState? physicalState,
                            out diagnosticFactors);
                    if (physicalResult == PhysicalBoundFlightResult.Rejected)
                    {
                        context.AdjustmentResult = CollisionAdjustmentResult.CalculationFailed;
                        RecordDiagnostic(__instance, context, impactSpeed, diagnosticFactors);
                        return;
                    }

                    if (physicalResult == PhysicalBoundFlightResult.Applied)
                    {
                        __state = physicalState;
                        context.LocalOutputDamage = __instance.Damage;
                        context.LocalOutputPenetrationPower = __instance.PenetrationPower;
                        context.AdjustmentResult = CollisionAdjustmentResult.Applied;
                        RecordDiagnostic(__instance, context, impactSpeed, diagnosticFactors);
                        return;
                    }
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

                if (!ShotNormalizationBindingStore.TryGetOrCreateRoot(
                        __instance,
                        out ShotNormalizationBinding? normalizationBinding)
                    || normalizationBinding == null)
                {
                    context.AdjustmentResult = CollisionAdjustmentResult.CalculationFailed;
                    RecordDiagnostic(__instance, context, impactSpeed, diagnosticFactors);
                    return;
                }

                string collisionIdentity = ShotNormalizationBindingStore.CreateCollisionIdentity(
                    __instance,
                    normalizationBinding.State);
                if (!BallisticNormalizationCalculator.TryAdvance(
                        normalizationBinding.State,
                        collisionIdentity,
                        context.EntryDamage,
                        context.EntryPenetrationPower,
                        diagnosticFactors,
                        out BallisticNormalizationTransition? normalizationTransition,
                        out _)
                    || normalizationTransition == null)
                {
                    context.AdjustmentResult = CollisionAdjustmentResult.CalculationFailed;
                    RecordDiagnostic(__instance, context, impactSpeed, diagnosticFactors);
                    return;
                }

                CaptureNormalization(context, normalizationTransition, collisionIdentity);
                if (normalizationTransition.Disposition
                    == BallisticNormalizationDisposition.Duplicate)
                {
                    context.LocalOutputDamage = __instance.Damage;
                    context.LocalOutputPenetrationPower = __instance.PenetrationPower;
                    context.AdjustmentResult = CollisionAdjustmentResult.DuplicateNormalization;
                    RecordDiagnostic(__instance, context, impactSpeed, diagnosticFactors);
                    return;
                }

                if (normalizationTransition.Disposition
                    == BallisticNormalizationDisposition.PhysicalCapabilityBypass)
                {
                    if (!ShotNormalizationBindingStore.TryCommit(
                            __instance,
                            normalizationBinding,
                            normalizationTransition.NextState,
                            out _))
                    {
                        context.AdjustmentResult = CollisionAdjustmentResult.CalculationFailed;
                        RecordDiagnostic(__instance, context, impactSpeed, diagnosticFactors);
                        return;
                    }

                    context.LocalOutputDamage = __instance.Damage;
                    context.LocalOutputPenetrationPower = __instance.PenetrationPower;
                    context.AdjustmentResult = CollisionAdjustmentResult.PhysicalCapabilityOwned;
                    RecordDiagnostic(__instance, context, impactSpeed, diagnosticFactors);
                    return;
                }

                double adjustedDamage = normalizationTransition.OutputDamage;
                double adjustedPenetrationPower = normalizationTransition.OutputPenetrationPower;
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

                // Replace only the speed-curve portion already represented in the cumulative
                // statistics. Armor CF, material loss, and child multipliers remain intact.
                __instance.Damage = damage;
                __instance.PenetrationPower = penetrationPower;
                if (!ShotNormalizationBindingStore.TryCommit(
                        __instance,
                        normalizationBinding,
                        normalizationTransition.NextState,
                        out _))
                {
                    __instance.Damage = context.PatchInputDamage;
                    __instance.PenetrationPower = context.PatchInputPenetrationPower;
                    context.AdjustmentResult = CollisionAdjustmentResult.CalculationFailed;
                    RecordDiagnostic(__instance, context, impactSpeed, diagnosticFactors);
                    return;
                }

                context.LocalOutputDamage = damage;
                context.LocalOutputPenetrationPower = penetrationPower;
                context.AdjustmentResult = CollisionAdjustmentResult.Applied;

                if (configuration.EnableExperimentalPhysicalProjectiles.Value)
                {
                    bool prepared = PhysicalProjectileRuntime.TryPrepareRootCollision(
                        __instance,
                        out __state);
                    if (!prepared)
                    {
                        Plugin.LogPhysicalBridgeFallback("root-prepare", __instance);
                    }
                }

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
                        diagnosticFactors.PenetrationFactor,
                        context.NormalizationComponentId,
                        context.NormalizationRootShotId,
                        context.NormalizationCollisionId,
                        context.NormalizationCollisionOrdinal,
                        context.PreviousDamageFactor,
                        context.PreviousPenetrationFactor,
                        context.AppliedDamageRatio,
                        context.AppliedPenetrationRatio);
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

        [PatchPostfix]
        [HarmonyPriority(Priority.First)]
        [SuppressMessage(
            "Design",
            "CA1031:Do not catch general exception types",
            Justification = "The Harmony postfix must retain EFT's original child list for every experimental bridge failure.")]
        private static void Postfix(
            Shot __instance,
            PhysicalRuntimeCollisionState? __state)
        {
            try
            {
                PluginConfiguration? configuration = Plugin.Configuration;
                if (__instance == null
                    || configuration == null
                    || !configuration.Enabled.Value)
                {
                    return;
                }

                if (__state != null
                    && configuration.EnableExperimentalPhysicalProjectiles.Value)
                {
                    bool applied = PhysicalProjectileRuntime.TryApplyObservedOutcome(
                        __instance,
                        __state);
                    if (!applied)
                    {
                        Plugin.LogPhysicalBridgeFallback("outcome-apply", __instance);
                    }
                }

                PropagateNormalizationToChildren(__instance, configuration);
            }
            catch (Exception exception)
            {
                Plugin.LogHookFailure("CreateFragments physical postfix", exception);
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

        private static void CaptureNormalization(
            CollisionContext context,
            BallisticNormalizationTransition transition,
            string collisionIdentity)
        {
            context.NormalizationComponentId = transition.NextState.ComponentId;
            context.NormalizationRootShotId = transition.NextState.RootShotId;
            context.NormalizationCollisionId = collisionIdentity;
            context.NormalizationCollisionOrdinal = transition.CollisionOrdinal;
            context.PreviousDamageFactor = transition.PreviousDamageFactor;
            context.PreviousPenetrationFactor = transition.PreviousPenetrationFactor;
            context.CurrentDamageFactor = transition.CurrentDamageFactor;
            context.CurrentPenetrationFactor = transition.CurrentPenetrationFactor;
            context.AppliedDamageRatio = transition.AppliedDamageRatio;
            context.AppliedPenetrationRatio = transition.AppliedPenetrationRatio;
        }

        private static void PropagateNormalizationToChildren(
            Shot parent,
            PluginConfiguration configuration)
        {
            if (!ShotNormalizationBindingStore.TryGet(
                    parent,
                    out ShotNormalizationBinding? parentBinding)
                || parentBinding == null)
            {
                return;
            }

            for (int index = 0; index < parent.Fragments.Count; index++)
            {
                Shot child = parent.Fragments[index];
                if (child == null)
                {
                    continue;
                }

                if (PhysicalShotBindingStore.TryGet(
                        child,
                        out PhysicalShotBinding? physicalBinding)
                    && physicalBinding != null)
                {
                    BallisticFalloffFactors baselineFactors =
                        CalculatePhysicalBaseline(child, configuration);
                    ShotNormalizationBindingStore.TrySetPhysicalComponent(
                        child,
                        physicalBinding.State,
                        baselineFactors);
                }
                else
                {
                    ShotNormalizationBindingStore.TrySetDerivedChild(
                        child,
                        parentBinding.State);
                }
            }
        }

        private static BallisticFalloffFactors CalculatePhysicalBaseline(
            Shot child,
            PluginConfiguration configuration)
        {
            AmmoTemplate? template = child.Ammo?.Template as AmmoTemplate;
            if (template == null
                || !configuration.TryGetExponentValues(
                    out double penetrationExponent,
                    out double damageExponent)
                || !BallisticFalloffCalculator.TryCalculate(
                    child.CurrentVelocity.magnitude,
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
