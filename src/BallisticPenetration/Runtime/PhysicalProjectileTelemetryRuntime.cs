#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using BallisticPenetration.Core.Physics;
using EFT.Ballistics;
using EFT.InventoryLogic;

namespace BallisticPenetration.Runtime
{
    /// <summary>
    /// Converts validated runtime state into host-free telemetry only while an observer is present.
    /// </summary>
    internal static class PhysicalProjectileTelemetryRuntime
    {
        private const int MaximumParentDepth = 64;

        [SuppressMessage(
            "Design",
            "CA1031:Do not catch general exception types",
            Justification = "Optional observation must never interrupt a validated collision transaction.")]
        internal static void PublishPrepared(
            Shot shot,
            PhysicalRuntimeCollisionState collisionState)
        {
            if (!PhysicalProjectileTelemetry.HasSubscribers)
            {
                return;
            }

            try
            {
                if (TryCreateHostIdentity(shot, out PhysicalTelemetryHostIdentity? host)
                    && host != null)
                {
                    PhysicalProjectileTelemetry.Publish(
                        PhysicalProjectileTelemetryFactory.CreatePrepared(
                            collisionState.TransitionId,
                            host,
                            CreateImpact(collisionState),
                            collisionState.ParentState));
                }
            }
            catch (Exception exception)
            {
                Plugin.LogHookFailure("Physical collision telemetry preparation", exception);
            }
        }

        internal static void PublishResolvedStopped(
            Shot shot,
            PhysicalRuntimeCollisionState collisionState,
            PhysicalProjectileState stoppedState,
            PhysicalLossBudget lossBudget)
        {
            if (!PhysicalProjectileTelemetry.HasSubscribers)
            {
                return;
            }

            PublishResolved(
                shot,
                collisionState,
                PhysicalCollisionOutcome.Stopped,
                new[] { stoppedState },
                lossBudget);
        }

        [SuppressMessage(
            "Design",
            "CA1031:Do not catch general exception types",
            Justification = "Optional observation must never interrupt a validated collision transaction.")]
        internal static void PublishResolved(
            Shot shot,
            PhysicalRuntimeCollisionState collisionState,
            PhysicalCollisionOutcome outcome,
            IReadOnlyList<PhysicalProjectileState> outputs,
            PhysicalLossBudget lossBudget)
        {
            if (!PhysicalProjectileTelemetry.HasSubscribers)
            {
                return;
            }

            try
            {
                if (TryCreateHostIdentity(shot, out PhysicalTelemetryHostIdentity? host)
                    && host != null
                    && PhysicalProjectileTelemetryFactory.TryCreateResolved(
                        collisionState.TransitionId,
                        outcome,
                        host,
                        CreateImpact(collisionState),
                        collisionState.ParentState,
                        outputs,
                        lossBudget,
                        out PhysicalProjectileTelemetryEvent? telemetryEvent)
                    && telemetryEvent != null)
                {
                    PhysicalProjectileTelemetry.Publish(telemetryEvent);
                }
            }
            catch (Exception exception)
            {
                Plugin.LogHookFailure("Physical collision telemetry resolution", exception);
            }
        }

        private static PhysicalTelemetryImpact CreateImpact(
            PhysicalRuntimeCollisionState collisionState)
        {
            PhysicalTargetMaterialProfile target = collisionState.TargetProfile;
            PhysicalImpactGeometry geometry = collisionState.Geometry;
            return new PhysicalTelemetryImpact(
                geometry.Position,
                geometry.SurfaceNormal,
                geometry.PhysicalThicknessMetres,
                geometry.EffectivePathLengthMetres,
                target.ProfileId,
                target.MaterialClass,
                target.DensityKilogramsPerCubicMetre,
                target.EffectiveResistancePressurePascals,
                target.ProjectileDeformationCoupling,
                target.ProjectileFractureCoupling,
                target.HeatLossFraction);
        }

        private static bool TryCreateHostIdentity(
            Shot shot,
            out PhysicalTelemetryHostIdentity? identity)
        {
            identity = null;
            if (shot == null)
            {
                return false;
            }

            Shot root = shot;
            int parentDepth = 0;
            while (root.Parent != null)
            {
                root = root.Parent;
                parentDepth++;
                if (parentDepth > MaximumParentDepth)
                {
                    return false;
                }
            }

            AmmoTemplate? template = shot.Ammo?.Template as AmmoTemplate;
            string rootProfileId = root.Player != null
                ? root.Player.iPlayer.ProfileId
                : root.PlayerProfileID;
            identity = new PhysicalTelemetryHostIdentity(
                root.FireIndex,
                root.RandomSeed,
                shot.FireIndex,
                shot.RandomSeed,
                shot.FragmentIndex,
                parentDepth,
                rootProfileId ?? string.Empty,
                template?.StringId ?? string.Empty,
                template?.Name ?? string.Empty);
            return true;
        }
    }
}
