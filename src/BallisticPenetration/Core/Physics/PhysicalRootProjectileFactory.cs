#nullable enable

using System;
using BallisticPenetration.Core;

namespace BallisticPenetration.Core.Physics
{
    public enum PhysicalRootProjectileFailureReason
    {
        None = 0,
        InputMissing = 1,
        ProjectileIdMissing = 2,
        RootShotIdMissing = 3,
        ConstructionInvalid = 4,
        ShapeInvalid = 5,
        MassInvalid = 6,
        DiameterInvalid = 7,
        DensityInvalid = 8,
        DragInvalid = 9,
        PositionInvalid = 10,
        VelocityInvalid = 11,
        GeometryInvalid = 12,
        OrientationInvalid = 13,
        CapabilityInvalid = 14,
        StateCreationFailed = 15,
        DesignInvalid = 16
    }

    public sealed class PhysicalRootProjectileInput
    {
        public string? ProjectileId { get; set; }

        public string? RootShotId { get; set; }

        public ulong DeterministicSeed { get; set; }

        public PhysicalProjectileConstruction Construction { get; set; }

        public PhysicalProjectileDesignClass DesignClass { get; set; }

        public PhysicalProjectileShapeClass ShapeClass { get; set; }

        public double MassKilograms { get; set; }

        public double NominalDiameterMetres { get; set; }

        public double MaterialDensityKilogramsPerCubicMetre { get; set; }

        public double DragCoefficient { get; set; }

        public PhysicalVector3 PositionMetres { get; set; }

        public PhysicalVector3 VelocityMetresPerSecond { get; set; }
    }

    /// <summary>
    /// Constructs the first immutable physical state from measured shot geometry and velocity. The
    /// equivalent cylinder length is derived from mass, profile density, and frontal area; no EFT
    /// damage or penetration stat is used as a substitute for physical geometry or energy.
    /// </summary>
    public static class PhysicalRootProjectileFactory
    {
        public static bool TryCreate(
            PhysicalRootProjectileInput? input,
            out PhysicalProjectileState? state,
            out PhysicalRootProjectileFailureReason failureReason)
        {
            state = null;
            if (input == null)
            {
                failureReason = PhysicalRootProjectileFailureReason.InputMissing;
                return false;
            }

            if (string.IsNullOrWhiteSpace(input.ProjectileId))
            {
                failureReason = PhysicalRootProjectileFailureReason.ProjectileIdMissing;
                return false;
            }

            if (string.IsNullOrWhiteSpace(input.RootShotId))
            {
                failureReason = PhysicalRootProjectileFailureReason.RootShotIdMissing;
                return false;
            }

            if (input.Construction <= PhysicalProjectileConstruction.Unknown
                || input.Construction == PhysicalProjectileConstruction.TargetMaterial
                || input.Construction > PhysicalProjectileConstruction.MonolithicLead)
            {
                failureReason = PhysicalRootProjectileFailureReason.ConstructionInvalid;
                return false;
            }

            if (input.DesignClass <= PhysicalProjectileDesignClass.Unknown
                || input.DesignClass == PhysicalProjectileDesignClass.Payload
                || input.DesignClass > PhysicalProjectileDesignClass.Flechette)
            {
                failureReason = PhysicalRootProjectileFailureReason.DesignInvalid;
                return false;
            }

            if (input.ShapeClass <= PhysicalProjectileShapeClass.Unknown
                || input.ShapeClass == PhysicalProjectileShapeClass.TargetSpallFlake
                || input.ShapeClass == PhysicalProjectileShapeClass.TargetSpallChunk
                || input.ShapeClass > PhysicalProjectileShapeClass.Flechette)
            {
                failureReason = PhysicalRootProjectileFailureReason.ShapeInvalid;
                return false;
            }

            if (!IsFinitePositive(input.MassKilograms))
            {
                failureReason = PhysicalRootProjectileFailureReason.MassInvalid;
                return false;
            }

            if (!IsFinitePositive(input.NominalDiameterMetres))
            {
                failureReason = PhysicalRootProjectileFailureReason.DiameterInvalid;
                return false;
            }

            if (!IsFinitePositive(input.MaterialDensityKilogramsPerCubicMetre))
            {
                failureReason = PhysicalRootProjectileFailureReason.DensityInvalid;
                return false;
            }

            if (!IsFinitePositive(input.DragCoefficient))
            {
                failureReason = PhysicalRootProjectileFailureReason.DragInvalid;
                return false;
            }

            if (!input.PositionMetres.IsFinite)
            {
                failureReason = PhysicalRootProjectileFailureReason.PositionInvalid;
                return false;
            }

            if (!input.VelocityMetresPerSecond.IsFinite
                || !input.VelocityMetresPerSecond.TryNormalize(out PhysicalVector3 direction))
            {
                failureReason = PhysicalRootProjectileFailureReason.VelocityInvalid;
                return false;
            }

            double area = Math.PI
                * input.NominalDiameterMetres
                * input.NominalDiameterMetres
                * 0.25d;
            double volume = input.MassKilograms / input.MaterialDensityKilogramsPerCubicMetre;
            double length = volume / area;
            if (!IsFinitePositive(area)
                || !IsFinitePositive(volume)
                || !IsFinitePositive(length))
            {
                failureReason = PhysicalRootProjectileFailureReason.GeometryInvalid;
                return false;
            }

            if (!PhysicalOrientation.TryFromForward(
                    direction,
                    out PhysicalOrientation orientation))
            {
                failureReason = PhysicalRootProjectileFailureReason.OrientationInvalid;
                return false;
            }

            double speed = input.VelocityMetresPerSecond.Magnitude;
            double energy = 0.5d * input.MassKilograms * speed * speed;
            double penetrationCapability = energy / area;
            if (!IsFinitePositive(energy) || !IsFinitePositive(penetrationCapability))
            {
                failureReason = PhysicalRootProjectileFailureReason.CapabilityInvalid;
                return false;
            }

            var stateInput = new PhysicalProjectileStateInput
            {
                Kind = PhysicalProjectileKind.IntactProjectile,
                ProjectileId = input.ProjectileId,
                RootShotId = input.RootShotId,
                FragmentIndex = -1,
                FragmentGeneration = 0,
                DeterministicSeed = input.DeterministicSeed,
                Construction = input.Construction,
                DesignClass = input.DesignClass,
                ShapeClass = input.ShapeClass,
                OriginalMassKilograms = input.MassKilograms,
                RetainedMassKilograms = input.MassKilograms,
                NominalDiameterMetres = input.NominalDiameterMetres,
                DeformedDiameterMetres = input.NominalDiameterMetres,
                ProjectedAreaSquareMetres = area,
                LengthMetres = length,
                DragCoefficient = input.DragCoefficient,
                PositionMetres = input.PositionMetres,
                VelocityMetresPerSecond = input.VelocityMetresPerSecond,
                Orientation = orientation,
                YawAngleRadians = 0d,
                TumbleState = PhysicalProjectileTumbleState.Stable,
                PenetrationCapabilityJoulesPerSquareMetre = penetrationCapability,
                DamageCapabilityJoules = energy,
                TerminalState = PhysicalProjectileTerminalState.Continuing,
                RenderState = PhysicalProjectileRenderState.NotRendered,
                CollisionHistory = Array.Empty<PhysicalCollisionRecord?>()
            };
            if (!PhysicalProjectileState.TryCreate(stateInput, out state, out _)
                || state == null)
            {
                failureReason = PhysicalRootProjectileFailureReason.StateCreationFailed;
                return false;
            }

            failureReason = PhysicalRootProjectileFailureReason.None;
            return true;
        }

        private static bool IsFinitePositive(double value)
        {
            return FiniteDouble.IsFinite(value) && value > 0d;
        }
    }
}
