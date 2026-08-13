#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using BallisticPenetration.Core;

namespace BallisticPenetration.Core.Physics
{
    public enum PhysicalProjectileStateFailureReason
    {
        None = 0,
        InputMissing = 1,
        ProjectileKindInvalid = 2,
        ProjectileIdMissing = 3,
        RootShotIdMissing = 4,
        ShapeClassInvalid = 5,
        ConstructionInvalid = 6,
        OriginalMassInvalid = 7,
        RetainedMassInvalid = 8,
        RetainedMassExceedsOriginal = 9,
        NominalDiameterInvalid = 10,
        DeformedDiameterInvalid = 11,
        ProjectedAreaInvalid = 12,
        LengthInvalid = 13,
        DragCoefficientInvalid = 14,
        PositionInvalid = 15,
        VelocityInvalid = 16,
        MovingStateHasZeroVelocity = 17,
        RestingStateHasVelocity = 18,
        OrientationInvalid = 19,
        YawAngleInvalid = 20,
        TumbleStateInvalid = 21,
        PenetrationCapabilityInvalid = 22,
        DamageCapabilityInvalid = 23,
        TerminalStateInvalid = 24,
        RenderStateInvalid = 25,
        RootLineageInvalid = 26,
        ChildLineageInvalid = 27,
        FragmentLineageInvalid = 28,
        CollisionHistoryEntryMissing = 29,
        DuplicateCollisionId = 30,
        DerivedValueInvalid = 31,
        MaterialOriginMismatch = 32,
        DamageCapabilityExceedsEnergy = 33,
        CollisionSequenceMismatch = 34
    }

    /// <summary>
    /// Mutable transfer object used only to construct a validated immutable physical state. A failed
    /// construction does not alter this object or any EFT state owned by the caller.
    /// </summary>
    public sealed class PhysicalProjectileStateInput
    {
        public PhysicalProjectileStateInput()
        {
            FragmentIndex = -1;
            Orientation = PhysicalOrientation.Identity;
            RenderState = PhysicalProjectileRenderState.NotRendered;
            CollisionHistory = Array.Empty<PhysicalCollisionRecord?>();
        }

        public PhysicalProjectileKind Kind { get; set; }

        public string? ProjectileId { get; set; }

        public string? RootShotId { get; set; }

        public string? ParentProjectileId { get; set; }

        public string? SourceProjectileId { get; set; }

        public string? SourceMaterialId { get; set; }

        public PhysicalMaterialClass SourceMaterialClass { get; set; }

        public string? SourceCollisionId { get; set; }

        public int FragmentIndex { get; set; }

        public int FragmentGeneration { get; set; }

        public ulong DeterministicSeed { get; set; }

        public PhysicalProjectileConstruction Construction { get; set; }

        public PhysicalProjectileShapeClass ShapeClass { get; set; }

        public double OriginalMassKilograms { get; set; }

        public double RetainedMassKilograms { get; set; }

        public double NominalDiameterMetres { get; set; }

        public double DeformedDiameterMetres { get; set; }

        public double ProjectedAreaSquareMetres { get; set; }

        public double LengthMetres { get; set; }

        /// <summary>
        /// State-specific dimensionless drag coefficient. A fragment must receive its own value.
        /// </summary>
        public double DragCoefficient { get; set; }

        public PhysicalVector3 PositionMetres { get; set; }

        public PhysicalVector3 VelocityMetresPerSecond { get; set; }

        public PhysicalOrientation Orientation { get; set; }

        /// <summary>
        /// Angle in radians between the projectile longitudinal axis and its velocity vector.
        /// </summary>
        public double YawAngleRadians { get; set; }

        public PhysicalProjectileTumbleState TumbleState { get; set; }

        /// <summary>
        /// Current physically-derived penetration capability in joules per square metre.
        /// </summary>
        public double PenetrationCapabilityJoulesPerSquareMetre { get; set; }

        /// <summary>
        /// Current physically-derived damage capability in joules available for transfer.
        /// </summary>
        public double DamageCapabilityJoules { get; set; }

        public PhysicalProjectileTerminalState TerminalState { get; set; }

        public PhysicalProjectileRenderState RenderState { get; set; }

        public IReadOnlyList<PhysicalCollisionRecord?>? CollisionHistory { get; set; }
    }

    /// <summary>
    /// Validated immutable SI-unit state for an intact projectile, deformed projectile, projectile
    /// fragment, or target-generated spall component.
    /// </summary>
    public sealed class PhysicalProjectileState
    {
        public const int SchemaVersion = 1;

        private const double RestSpeedToleranceMetresPerSecond = 0.000000001d;

        private readonly ReadOnlyCollection<PhysicalCollisionRecord> _collisionHistory;

        private PhysicalProjectileState(
            PhysicalProjectileStateInput input,
            string projectileId,
            string rootShotId,
            IReadOnlyList<PhysicalCollisionRecord> collisionHistory,
            double speedMetresPerSecond,
            double translationalEnergyJoules,
            double equivalentDiameterMetres,
            double aspectRatio,
            double ballisticCoefficientKilogramsPerSquareMetre)
        {
            Kind = input.Kind;
            ProjectileId = projectileId;
            RootShotId = rootShotId;
            ParentProjectileId = input.ParentProjectileId;
            SourceProjectileId = input.SourceProjectileId;
            SourceMaterialId = input.SourceMaterialId;
            SourceMaterialClass = input.SourceMaterialClass;
            SourceCollisionId = input.SourceCollisionId;
            FragmentIndex = input.FragmentIndex;
            FragmentGeneration = input.FragmentGeneration;
            DeterministicSeed = input.DeterministicSeed;
            Construction = input.Construction;
            ShapeClass = input.ShapeClass;
            OriginalMassKilograms = input.OriginalMassKilograms;
            RetainedMassKilograms = input.RetainedMassKilograms;
            NominalDiameterMetres = input.NominalDiameterMetres;
            DeformedDiameterMetres = input.DeformedDiameterMetres;
            ProjectedAreaSquareMetres = input.ProjectedAreaSquareMetres;
            EquivalentDiameterMetres = equivalentDiameterMetres;
            LengthMetres = input.LengthMetres;
            AspectRatio = aspectRatio;
            DragCoefficient = input.DragCoefficient;
            BallisticCoefficientKilogramsPerSquareMetre = ballisticCoefficientKilogramsPerSquareMetre;
            PositionMetres = input.PositionMetres;
            VelocityMetresPerSecond = input.VelocityMetresPerSecond;
            SpeedMetresPerSecond = speedMetresPerSecond;
            MomentumKilogramMetresPerSecond = input.VelocityMetresPerSecond.Scale(input.RetainedMassKilograms);
            TranslationalKineticEnergyJoules = translationalEnergyJoules;
            Orientation = input.Orientation;
            YawAngleRadians = input.YawAngleRadians;
            TumbleState = input.TumbleState;
            PenetrationCapabilityJoulesPerSquareMetre = input.PenetrationCapabilityJoulesPerSquareMetre;
            DamageCapabilityJoules = input.DamageCapabilityJoules;
            TerminalState = input.TerminalState;
            RenderState = input.RenderState;

            var historyCopy = new PhysicalCollisionRecord[collisionHistory.Count];
            for (int index = 0; index < historyCopy.Length; index++)
            {
                historyCopy[index] = collisionHistory[index];
            }

            _collisionHistory = Array.AsReadOnly(historyCopy);
        }

        public PhysicalProjectileKind Kind { get; }

        public string ProjectileId { get; }

        public string RootShotId { get; }

        public string? ParentProjectileId { get; }

        public string? SourceProjectileId { get; }

        public string? SourceMaterialId { get; }

        public PhysicalMaterialClass SourceMaterialClass { get; }

        public string? SourceCollisionId { get; }

        public int FragmentIndex { get; }

        public int FragmentGeneration { get; }

        public ulong DeterministicSeed { get; }

        public PhysicalProjectileConstruction Construction { get; }

        public PhysicalProjectileShapeClass ShapeClass { get; }

        public double OriginalMassKilograms { get; }

        public double RetainedMassKilograms { get; }

        public double NominalDiameterMetres { get; }

        public double DeformedDiameterMetres { get; }

        public double ProjectedAreaSquareMetres { get; }

        public double EquivalentDiameterMetres { get; }

        public double LengthMetres { get; }

        public double AspectRatio { get; }

        public double DragCoefficient { get; }

        /// <summary>
        /// Physical sectional ballistic coefficient m/(Cd*A), in kilograms per square metre. It is
        /// calculated from this component's retained mass, area, and drag coefficient; it is never
        /// inherited from a whole parent projectile.
        /// </summary>
        public double BallisticCoefficientKilogramsPerSquareMetre { get; }

        public PhysicalVector3 PositionMetres { get; }

        public PhysicalVector3 VelocityMetresPerSecond { get; }

        public double SpeedMetresPerSecond { get; }

        public PhysicalVector3 MomentumKilogramMetresPerSecond { get; }

        public double TranslationalKineticEnergyJoules { get; }

        public PhysicalOrientation Orientation { get; }

        public double YawAngleRadians { get; }

        public PhysicalProjectileTumbleState TumbleState { get; }

        public double PenetrationCapabilityJoulesPerSquareMetre { get; }

        public double DamageCapabilityJoules { get; }

        public PhysicalProjectileTerminalState TerminalState { get; }

        public PhysicalProjectileRenderState RenderState { get; }

        public IReadOnlyList<PhysicalCollisionRecord> CollisionHistory
        {
            get { return _collisionHistory; }
        }

        public bool IsProjectileDerivedMass
        {
            get { return Kind != PhysicalProjectileKind.TargetSpall; }
        }

        public static bool TryCreate(
            PhysicalProjectileStateInput? input,
            out PhysicalProjectileState? state,
            out PhysicalProjectileStateFailureReason failureReason)
        {
            state = null;
            if (input == null)
            {
                failureReason = PhysicalProjectileStateFailureReason.InputMissing;
                return false;
            }

            if (input.Kind < PhysicalProjectileKind.IntactProjectile
                || input.Kind > PhysicalProjectileKind.TargetSpall)
            {
                failureReason = PhysicalProjectileStateFailureReason.ProjectileKindInvalid;
                return false;
            }

            string? projectileId = input.ProjectileId;
            if (string.IsNullOrWhiteSpace(projectileId))
            {
                failureReason = PhysicalProjectileStateFailureReason.ProjectileIdMissing;
                return false;
            }

            string? rootShotId = input.RootShotId;
            if (string.IsNullOrWhiteSpace(rootShotId))
            {
                failureReason = PhysicalProjectileStateFailureReason.RootShotIdMissing;
                return false;
            }

            if (input.ShapeClass < PhysicalProjectileShapeClass.Spitzer
                || input.ShapeClass > PhysicalProjectileShapeClass.TargetSpallChunk)
            {
                failureReason = PhysicalProjectileStateFailureReason.ShapeClassInvalid;
                return false;
            }

            if (input.Construction < PhysicalProjectileConstruction.Unknown
                || input.Construction > PhysicalProjectileConstruction.TargetMaterial)
            {
                failureReason = PhysicalProjectileStateFailureReason.ConstructionInvalid;
                return false;
            }

            bool isTargetSpall = input.Kind == PhysicalProjectileKind.TargetSpall;
            bool hasTargetMaterialConstruction = input.Construction
                == PhysicalProjectileConstruction.TargetMaterial;
            bool hasTargetSpallShape = input.ShapeClass
                == PhysicalProjectileShapeClass.TargetSpallFlake
                || input.ShapeClass == PhysicalProjectileShapeClass.TargetSpallChunk;
            if (isTargetSpall != hasTargetMaterialConstruction
                || isTargetSpall != hasTargetSpallShape)
            {
                failureReason = PhysicalProjectileStateFailureReason.MaterialOriginMismatch;
                return false;
            }

            if (!IsFinitePositive(input.OriginalMassKilograms))
            {
                failureReason = PhysicalProjectileStateFailureReason.OriginalMassInvalid;
                return false;
            }

            if (!IsFinitePositive(input.RetainedMassKilograms))
            {
                failureReason = PhysicalProjectileStateFailureReason.RetainedMassInvalid;
                return false;
            }

            if (input.RetainedMassKilograms > input.OriginalMassKilograms)
            {
                failureReason = PhysicalProjectileStateFailureReason.RetainedMassExceedsOriginal;
                return false;
            }

            if (!IsFinitePositive(input.NominalDiameterMetres))
            {
                failureReason = PhysicalProjectileStateFailureReason.NominalDiameterInvalid;
                return false;
            }

            if (!IsFinitePositive(input.DeformedDiameterMetres))
            {
                failureReason = PhysicalProjectileStateFailureReason.DeformedDiameterInvalid;
                return false;
            }

            if (!IsFinitePositive(input.ProjectedAreaSquareMetres))
            {
                failureReason = PhysicalProjectileStateFailureReason.ProjectedAreaInvalid;
                return false;
            }

            if (!IsFinitePositive(input.LengthMetres))
            {
                failureReason = PhysicalProjectileStateFailureReason.LengthInvalid;
                return false;
            }

            if (!IsFinitePositive(input.DragCoefficient))
            {
                failureReason = PhysicalProjectileStateFailureReason.DragCoefficientInvalid;
                return false;
            }

            if (!input.PositionMetres.IsFinite)
            {
                failureReason = PhysicalProjectileStateFailureReason.PositionInvalid;
                return false;
            }

            if (!input.VelocityMetresPerSecond.IsFinite)
            {
                failureReason = PhysicalProjectileStateFailureReason.VelocityInvalid;
                return false;
            }

            double speedMetresPerSecond = input.VelocityMetresPerSecond.Magnitude;
            if (!FiniteDouble.IsFinite(speedMetresPerSecond))
            {
                failureReason = PhysicalProjectileStateFailureReason.VelocityInvalid;
                return false;
            }

            if (input.TerminalState == PhysicalProjectileTerminalState.Continuing
                || input.TerminalState == PhysicalProjectileTerminalState.Exited)
            {
                if (speedMetresPerSecond <= RestSpeedToleranceMetresPerSecond)
                {
                    failureReason = PhysicalProjectileStateFailureReason.MovingStateHasZeroVelocity;
                    return false;
                }
            }
            else if (input.TerminalState == PhysicalProjectileTerminalState.Embedded
                || input.TerminalState == PhysicalProjectileTerminalState.Stopped)
            {
                if (speedMetresPerSecond > RestSpeedToleranceMetresPerSecond)
                {
                    failureReason = PhysicalProjectileStateFailureReason.RestingStateHasVelocity;
                    return false;
                }
            }
            else
            {
                failureReason = PhysicalProjectileStateFailureReason.TerminalStateInvalid;
                return false;
            }

            if (!input.Orientation.IsUnit)
            {
                failureReason = PhysicalProjectileStateFailureReason.OrientationInvalid;
                return false;
            }

            if (!FiniteDouble.IsFinite(input.YawAngleRadians)
                || input.YawAngleRadians < 0d
                || input.YawAngleRadians > Math.PI)
            {
                failureReason = PhysicalProjectileStateFailureReason.YawAngleInvalid;
                return false;
            }

            if (input.TumbleState < PhysicalProjectileTumbleState.Stable
                || input.TumbleState > PhysicalProjectileTumbleState.Tumbling)
            {
                failureReason = PhysicalProjectileStateFailureReason.TumbleStateInvalid;
                return false;
            }

            if (!IsFiniteNonNegative(input.PenetrationCapabilityJoulesPerSquareMetre))
            {
                failureReason = PhysicalProjectileStateFailureReason.PenetrationCapabilityInvalid;
                return false;
            }

            if (!IsFiniteNonNegative(input.DamageCapabilityJoules))
            {
                failureReason = PhysicalProjectileStateFailureReason.DamageCapabilityInvalid;
                return false;
            }

            if (input.RenderState < PhysicalProjectileRenderState.NotRendered
                || input.RenderState > PhysicalProjectileRenderState.Expired)
            {
                failureReason = PhysicalProjectileStateFailureReason.RenderStateInvalid;
                return false;
            }

            bool hasParent = !string.IsNullOrWhiteSpace(input.ParentProjectileId);
            if (!hasParent)
            {
                if (input.FragmentGeneration != 0
                    || input.FragmentIndex != -1
                    || input.Kind == PhysicalProjectileKind.ProjectileFragment
                    || input.Kind == PhysicalProjectileKind.TargetSpall)
                {
                    failureReason = PhysicalProjectileStateFailureReason.RootLineageInvalid;
                    return false;
                }
            }
            else
            {
                if (string.IsNullOrWhiteSpace(input.SourceProjectileId)
                    || string.IsNullOrWhiteSpace(input.SourceMaterialId)
                    || string.IsNullOrWhiteSpace(input.SourceCollisionId)
                    || input.SourceMaterialClass <= PhysicalMaterialClass.Unknown
                    || input.SourceMaterialClass > PhysicalMaterialClass.Other
                    || input.FragmentGeneration <= 0)
                {
                    failureReason = PhysicalProjectileStateFailureReason.ChildLineageInvalid;
                    return false;
                }

                if (input.FragmentIndex < 0)
                {
                    failureReason = PhysicalProjectileStateFailureReason.FragmentLineageInvalid;
                    return false;
                }
            }

            IReadOnlyList<PhysicalCollisionRecord?>? collisionHistory = input.CollisionHistory;
            if (collisionHistory == null)
            {
                failureReason = PhysicalProjectileStateFailureReason.CollisionHistoryEntryMissing;
                return false;
            }

            var collisionIds = new HashSet<string>(StringComparer.Ordinal);
            var validatedHistory = new List<PhysicalCollisionRecord>(collisionHistory.Count);
            for (int index = 0; index < collisionHistory.Count; index++)
            {
                PhysicalCollisionRecord? record = collisionHistory[index];
                if (record == null)
                {
                    failureReason = PhysicalProjectileStateFailureReason.CollisionHistoryEntryMissing;
                    return false;
                }

                if (!collisionIds.Add(record.CollisionId))
                {
                    failureReason = PhysicalProjectileStateFailureReason.DuplicateCollisionId;
                    return false;
                }

                if (record.Sequence != index)
                {
                    failureReason = PhysicalProjectileStateFailureReason.CollisionSequenceMismatch;
                    return false;
                }

                validatedHistory.Add(record);
            }

            double translationalEnergyJoules = 0.5d
                * input.RetainedMassKilograms
                * speedMetresPerSecond
                * speedMetresPerSecond;
            double equivalentDiameterMetres;
            if (!FiniteDouble.IsFinite(translationalEnergyJoules)
                || translationalEnergyJoules < 0d
                || !PhysicalProjectileGeometry.TryCalculateEquivalentDiameterMetres(
                    input.ProjectedAreaSquareMetres,
                    out equivalentDiameterMetres))
            {
                failureReason = PhysicalProjectileStateFailureReason.DerivedValueInvalid;
                return false;
            }

            double aspectRatio = input.LengthMetres / equivalentDiameterMetres;
            double ballisticCoefficientKilogramsPerSquareMetre = input.RetainedMassKilograms
                / (input.DragCoefficient * input.ProjectedAreaSquareMetres);
            PhysicalVector3 momentum = input.VelocityMetresPerSecond.Scale(input.RetainedMassKilograms);
            if (!FiniteDouble.IsFinite(aspectRatio)
                || aspectRatio <= 0d
                || !FiniteDouble.IsFinite(ballisticCoefficientKilogramsPerSquareMetre)
                || ballisticCoefficientKilogramsPerSquareMetre <= 0d
                || !momentum.IsFinite)
            {
                failureReason = PhysicalProjectileStateFailureReason.DerivedValueInvalid;
                return false;
            }


            double damageEnergyTolerance = Math.Max(1d, translationalEnergyJoules)
                * 0.000000001d;
            if (input.DamageCapabilityJoules > translationalEnergyJoules + damageEnergyTolerance)
            {
                failureReason = PhysicalProjectileStateFailureReason.DamageCapabilityExceedsEnergy;
                return false;
            }

            state = new PhysicalProjectileState(
                input,
                projectileId,
                rootShotId,
                validatedHistory,
                speedMetresPerSecond,
                translationalEnergyJoules,
                equivalentDiameterMetres,
                aspectRatio,
                ballisticCoefficientKilogramsPerSquareMetre);
            failureReason = PhysicalProjectileStateFailureReason.None;
            return true;
        }

        private static bool IsFinitePositive(double value)
        {
            return FiniteDouble.IsFinite(value) && value > 0d;
        }

        private static bool IsFiniteNonNegative(double value)
        {
            return FiniteDouble.IsFinite(value) && value >= 0d;
        }
    }
}
