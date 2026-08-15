#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using BallisticPenetration.Core;

namespace BallisticPenetration.Core.Physics
{
    public enum PhysicalFragmentationProfileFailureReason
    {
        None = 0,
        InputMissing = 1,
        ProjectileCountInvalid = 2,
        ProjectileMassInvalid = 3,
        ProjectileConeInvalid = 4,
        ProjectileAspectRatioInvalid = 5,
        ProjectileDragMultiplierInvalid = 6,
        ProjectilePenetrationEfficiencyInvalid = 7,
        TargetSpallFractionInvalid = 8,
        TargetSpallMassInvalid = 9,
        TargetSpallCountInvalid = 10,
        TargetSpallConeInvalid = 11,
        TargetSpallAspectRatioInvalid = 12,
        TargetSpallDragCoefficientInvalid = 13,
        TargetSpallPenetrationEfficiencyInvalid = 14
    }

    public sealed class PhysicalFragmentationProfileInput
    {
        public int MaximumProjectileFragmentCount { get; set; }

        public double MinimumProjectileFragmentMassKilograms { get; set; }

        public double ProjectileConeHalfAngleRadians { get; set; }

        public double MinimumProjectileAspectRatio { get; set; }

        public double MaximumProjectileAspectRatio { get; set; }

        public double MinimumProjectileDragMultiplier { get; set; }

        public double MaximumProjectileDragMultiplier { get; set; }

        public double ProjectilePenetrationEfficiency { get; set; }

        public double TargetSpallEjectedMassFraction { get; set; }

        public double TargetSpallKineticEnergyFraction { get; set; }

        public double NominalTargetSpallMassKilograms { get; set; }

        public int MaximumTargetSpallCount { get; set; }

        public double TargetSpallConeHalfAngleRadians { get; set; }

        public double MinimumTargetSpallAspectRatio { get; set; }

        public double MaximumTargetSpallAspectRatio { get; set; }

        public double MinimumTargetSpallDragCoefficient { get; set; }

        public double MaximumTargetSpallDragCoefficient { get; set; }

        public double TargetSpallPenetrationEfficiency { get; set; }
    }

    /// <summary>
    /// Calibrated geometric limits for resolving an already-confirmed fragmentation outcome.
    /// The profile never decides whether fragmentation happened.
    /// </summary>
    public sealed class PhysicalFragmentationProfile
    {
        private PhysicalFragmentationProfile(PhysicalFragmentationProfileInput input)
        {
            MaximumProjectileFragmentCount = input.MaximumProjectileFragmentCount;
            MinimumProjectileFragmentMassKilograms = input.MinimumProjectileFragmentMassKilograms;
            ProjectileConeHalfAngleRadians = input.ProjectileConeHalfAngleRadians;
            MinimumProjectileAspectRatio = input.MinimumProjectileAspectRatio;
            MaximumProjectileAspectRatio = input.MaximumProjectileAspectRatio;
            MinimumProjectileDragMultiplier = input.MinimumProjectileDragMultiplier;
            MaximumProjectileDragMultiplier = input.MaximumProjectileDragMultiplier;
            ProjectilePenetrationEfficiency = input.ProjectilePenetrationEfficiency;
            TargetSpallEjectedMassFraction = input.TargetSpallEjectedMassFraction;
            TargetSpallKineticEnergyFraction = input.TargetSpallKineticEnergyFraction;
            NominalTargetSpallMassKilograms = input.NominalTargetSpallMassKilograms;
            MaximumTargetSpallCount = input.MaximumTargetSpallCount;
            TargetSpallConeHalfAngleRadians = input.TargetSpallConeHalfAngleRadians;
            MinimumTargetSpallAspectRatio = input.MinimumTargetSpallAspectRatio;
            MaximumTargetSpallAspectRatio = input.MaximumTargetSpallAspectRatio;
            MinimumTargetSpallDragCoefficient = input.MinimumTargetSpallDragCoefficient;
            MaximumTargetSpallDragCoefficient = input.MaximumTargetSpallDragCoefficient;
            TargetSpallPenetrationEfficiency = input.TargetSpallPenetrationEfficiency;
        }

        public int MaximumProjectileFragmentCount { get; }

        public double MinimumProjectileFragmentMassKilograms { get; }

        public double ProjectileConeHalfAngleRadians { get; }

        public double MinimumProjectileAspectRatio { get; }

        public double MaximumProjectileAspectRatio { get; }

        public double MinimumProjectileDragMultiplier { get; }

        public double MaximumProjectileDragMultiplier { get; }

        public double ProjectilePenetrationEfficiency { get; }

        public double TargetSpallEjectedMassFraction { get; }

        public double TargetSpallKineticEnergyFraction { get; }

        public double NominalTargetSpallMassKilograms { get; }

        public int MaximumTargetSpallCount { get; }

        public double TargetSpallConeHalfAngleRadians { get; }

        public double MinimumTargetSpallAspectRatio { get; }

        public double MaximumTargetSpallAspectRatio { get; }

        public double MinimumTargetSpallDragCoefficient { get; }

        public double MaximumTargetSpallDragCoefficient { get; }

        public double TargetSpallPenetrationEfficiency { get; }

        public bool ProducesTargetSpall
        {
            get
            {
                return TargetSpallEjectedMassFraction > 0d
                    && TargetSpallKineticEnergyFraction > 0d;
            }
        }

        public static bool TryCreate(
            PhysicalFragmentationProfileInput? input,
            out PhysicalFragmentationProfile? profile,
            out PhysicalFragmentationProfileFailureReason failureReason)
        {
            profile = null;
            if (input == null)
            {
                failureReason = PhysicalFragmentationProfileFailureReason.InputMissing;
                return false;
            }

            if (input.MaximumProjectileFragmentCount < 1
                || input.MaximumProjectileFragmentCount > 256)
            {
                failureReason = PhysicalFragmentationProfileFailureReason.ProjectileCountInvalid;
                return false;
            }

            if (!IsFinitePositive(input.MinimumProjectileFragmentMassKilograms))
            {
                failureReason = PhysicalFragmentationProfileFailureReason.ProjectileMassInvalid;
                return false;
            }

            if (!IsValidCone(input.ProjectileConeHalfAngleRadians))
            {
                failureReason = PhysicalFragmentationProfileFailureReason.ProjectileConeInvalid;
                return false;
            }

            if (!IsValidRange(
                input.MinimumProjectileAspectRatio,
                input.MaximumProjectileAspectRatio))
            {
                failureReason = PhysicalFragmentationProfileFailureReason.ProjectileAspectRatioInvalid;
                return false;
            }

            if (!IsValidRange(
                input.MinimumProjectileDragMultiplier,
                input.MaximumProjectileDragMultiplier))
            {
                failureReason = PhysicalFragmentationProfileFailureReason.ProjectileDragMultiplierInvalid;
                return false;
            }

            if (!IsUnitInterval(input.ProjectilePenetrationEfficiency))
            {
                failureReason = PhysicalFragmentationProfileFailureReason.ProjectilePenetrationEfficiencyInvalid;
                return false;
            }

            bool hasSpallMass = input.TargetSpallEjectedMassFraction > 0d;
            bool hasSpallEnergy = input.TargetSpallKineticEnergyFraction > 0d;
            if (!IsUnitInterval(input.TargetSpallEjectedMassFraction)
                || !IsUnitInterval(input.TargetSpallKineticEnergyFraction)
                || hasSpallMass != hasSpallEnergy)
            {
                failureReason = PhysicalFragmentationProfileFailureReason.TargetSpallFractionInvalid;
                return false;
            }

            if (hasSpallMass)
            {
                if (!IsFinitePositive(input.NominalTargetSpallMassKilograms))
                {
                    failureReason = PhysicalFragmentationProfileFailureReason.TargetSpallMassInvalid;
                    return false;
                }

                if (input.MaximumTargetSpallCount < 1 || input.MaximumTargetSpallCount > 256)
                {
                    failureReason = PhysicalFragmentationProfileFailureReason.TargetSpallCountInvalid;
                    return false;
                }

                if (!IsValidCone(input.TargetSpallConeHalfAngleRadians))
                {
                    failureReason = PhysicalFragmentationProfileFailureReason.TargetSpallConeInvalid;
                    return false;
                }

                if (!IsValidRange(
                    input.MinimumTargetSpallAspectRatio,
                    input.MaximumTargetSpallAspectRatio))
                {
                    failureReason = PhysicalFragmentationProfileFailureReason.TargetSpallAspectRatioInvalid;
                    return false;
                }

                if (!IsValidRange(
                    input.MinimumTargetSpallDragCoefficient,
                    input.MaximumTargetSpallDragCoefficient))
                {
                    failureReason = PhysicalFragmentationProfileFailureReason.TargetSpallDragCoefficientInvalid;
                    return false;
                }

                if (!IsUnitInterval(input.TargetSpallPenetrationEfficiency))
                {
                    failureReason = PhysicalFragmentationProfileFailureReason.TargetSpallPenetrationEfficiencyInvalid;
                    return false;
                }
            }

            profile = new PhysicalFragmentationProfile(input);
            failureReason = PhysicalFragmentationProfileFailureReason.None;
            return true;
        }

        private static bool IsFinitePositive(double value)
        {
            return FiniteDouble.IsFinite(value) && value > 0d;
        }

        private static bool IsUnitInterval(double value)
        {
            return FiniteDouble.IsFinite(value) && value >= 0d && value <= 1d;
        }

        private static bool IsValidCone(double value)
        {
            return FiniteDouble.IsFinite(value) && value >= 0d && value <= Math.PI * 0.5d;
        }

        private static bool IsValidRange(double minimum, double maximum)
        {
            return IsFinitePositive(minimum)
                && IsFinitePositive(maximum)
                && maximum >= minimum;
        }
    }

    public enum PhysicalFragmentationFailureReason
    {
        None = 0,
        InputMissing = 1,
        ParentMissing = 2,
        DeformationResponseMissing = 3,
        ProjectileProfileMissing = 4,
        TargetProfileMissing = 5,
        FragmentationProfileMissing = 6,
        ProjectileConstructionMismatch = 7,
        FragmentationOutcomeMissing = 8,
        DeformationResponseMismatch = 9,
        ObservedFragmentCountInvalid = 10,
        ProjectileIdPrefixMissing = 11,
        TargetSpallIdPrefixMissing = 12,
        DirectionInvalid = 13,
        ProjectileReservationInvalid = 14,
        TargetSpallReservationInvalid = 15,
        EffectiveLossBudgetInvalid = 16,
        ProjectileGeometryInvalid = 17,
        TargetSpallGeometryInvalid = 18,
        ProjectileStateInvalid = 19,
        TargetSpallStateInvalid = 20,
        ConservationValidationFailed = 21,
        FragmentGenerationOverflow = 22
    }

    public sealed class PhysicalFragmentationInput
    {
        public PhysicalProjectileState? Parent { get; set; }

        public PhysicalDeformationResponse? DeformationResponse { get; set; }

        public PhysicalProjectileMaterialProfile? ProjectileProfile { get; set; }

        public PhysicalTargetMaterialProfile? TargetProfile { get; set; }

        public PhysicalFragmentationProfile? FragmentationProfile { get; set; }

        /// <summary>
        /// Count already selected by the host. Zero remains observable, but physical closure emits
        /// one fragment when the confirmed outcome reserved nonzero fragment mass and energy.
        /// </summary>
        public int ObservedProjectileFragmentCount { get; set; }

        public string? ProjectileIdPrefix { get; set; }

        public string? TargetSpallIdPrefix { get; set; }
    }

    public sealed class PhysicalFragmentationResponse
    {
        private readonly ReadOnlyCollection<PhysicalProjectileState> _projectileFragments;
        private readonly ReadOnlyCollection<PhysicalProjectileState> _targetSpall;
        private readonly ReadOnlyCollection<PhysicalProjectileState> _allSecondaryComponents;

        internal PhysicalFragmentationResponse(
            PhysicalProjectileState? primaryState,
            IReadOnlyList<PhysicalProjectileState> projectileFragments,
            IReadOnlyList<PhysicalProjectileState> targetSpall,
            int observedProjectileFragmentCount,
            PhysicalLossBudget effectiveLossBudget,
            PhysicalConservationResult conservationResult,
            double targetSpallMassKilograms,
            double targetSpallEnergyJoules)
        {
            PrimaryState = primaryState;
            ObservedProjectileFragmentCount = observedProjectileFragmentCount;
            EffectiveLossBudget = effectiveLossBudget;
            ConservationResult = conservationResult;
            TargetSpallMassKilograms = targetSpallMassKilograms;
            TargetSpallEnergyJoules = targetSpallEnergyJoules;

            var projectileCopy = new PhysicalProjectileState[projectileFragments.Count];
            var spallCopy = new PhysicalProjectileState[targetSpall.Count];
            var allCopy = new PhysicalProjectileState[projectileCopy.Length + spallCopy.Length];
            for (int index = 0; index < projectileCopy.Length; index++)
            {
                projectileCopy[index] = projectileFragments[index];
                allCopy[index] = projectileCopy[index];
            }

            for (int index = 0; index < spallCopy.Length; index++)
            {
                spallCopy[index] = targetSpall[index];
                allCopy[projectileCopy.Length + index] = spallCopy[index];
            }

            _projectileFragments = Array.AsReadOnly(projectileCopy);
            _targetSpall = Array.AsReadOnly(spallCopy);
            _allSecondaryComponents = Array.AsReadOnly(allCopy);
        }

        public PhysicalProjectileState? PrimaryState { get; }

        public IReadOnlyList<PhysicalProjectileState> ProjectileFragments
        {
            get { return _projectileFragments; }
        }

        public IReadOnlyList<PhysicalProjectileState> TargetSpall
        {
            get { return _targetSpall; }
        }

        public IReadOnlyList<PhysicalProjectileState> AllSecondaryComponents
        {
            get { return _allSecondaryComponents; }
        }

        public int ObservedProjectileFragmentCount { get; }

        public int ProducedProjectileFragmentCount
        {
            get { return _projectileFragments.Count; }
        }

        public PhysicalLossBudget EffectiveLossBudget { get; }

        public PhysicalConservationResult ConservationResult { get; }

        public double TargetSpallMassKilograms { get; }

        public double TargetSpallEnergyJoules { get; }
    }

    public enum PhysicalTargetSpallFailureReason
    {
        None = 0,
        InputMissing = 1,
        ParentMissing = 2,
        DeformationResponseMissing = 3,
        TargetProfileMissing = 4,
        FragmentationProfileMissing = 5,
        TargetSpallDisabled = 6,
        NonPenetratingOutcome = 7,
        FragmentationOutcomeOwnedByFragmentationSolver = 8,
        DeformationResponseMismatch = 9,
        TargetProfileMismatch = 10,
        TargetSpallIdPrefixMissing = 11,
        DirectionInvalid = 12,
        TargetSpallReservationInvalid = 13,
        EffectiveLossBudgetInvalid = 14,
        TargetSpallStateInvalid = 15,
        ConservationValidationFailed = 16,
        FragmentGenerationOverflow = 17
    }

    public sealed class PhysicalTargetSpallInput
    {
        public PhysicalProjectileState? Parent { get; set; }

        public PhysicalDeformationResponse? DeformationResponse { get; set; }

        public PhysicalTargetMaterialProfile? TargetProfile { get; set; }

        public PhysicalFragmentationProfile? FragmentationProfile { get; set; }

        public string? TargetSpallIdPrefix { get; set; }
    }

    /// <summary>
    /// Target material ejected by a nonfragmenting penetration. Its mass is never charged to the
    /// projectile; its kinetic energy is reclassified from work already spent on the target.
    /// </summary>
    public sealed class PhysicalTargetSpallResponse
    {
        private readonly ReadOnlyCollection<PhysicalProjectileState> _components;

        internal PhysicalTargetSpallResponse(
            IReadOnlyList<PhysicalProjectileState> components,
            PhysicalLossBudget effectiveLossBudget,
            PhysicalConservationResult conservationResult,
            double massKilograms,
            double energyJoules)
        {
            var copy = new PhysicalProjectileState[components.Count];
            for (int index = 0; index < copy.Length; index++)
            {
                copy[index] = components[index];
            }

            _components = Array.AsReadOnly(copy);
            EffectiveLossBudget = effectiveLossBudget;
            ConservationResult = conservationResult;
            MassKilograms = massKilograms;
            EnergyJoules = energyJoules;
        }

        public IReadOnlyList<PhysicalProjectileState> Components
        {
            get { return _components; }
        }

        public PhysicalLossBudget EffectiveLossBudget { get; }

        public PhysicalConservationResult ConservationResult { get; }

        public double MassKilograms { get; }

        public double EnergyJoules { get; }
    }

    /// <summary>
    /// Deterministically resolves mass, energy, geometry, and trajectory only after the host has
    /// confirmed fragmentation. It consumes the host count and never calls host decision or random
    /// methods.
    /// </summary>
    public static class PhysicalFragmentationSolver
    {
        private const double RelativeTolerance = 0.000000001d;
        private const ulong ProjectileStream = 0x50524F4A46524147UL;
        private const ulong TargetSpallStream = 0x5350414C4C535452UL;

        public static bool TrySolve(
            PhysicalFragmentationInput? input,
            out PhysicalFragmentationResponse? response,
            out PhysicalFragmentationFailureReason failureReason)
        {
            response = null;
            if (input == null)
            {
                failureReason = PhysicalFragmentationFailureReason.InputMissing;
                return false;
            }

            PhysicalProjectileState? parent = input.Parent;
            if (parent == null)
            {
                failureReason = PhysicalFragmentationFailureReason.ParentMissing;
                return false;
            }

            PhysicalDeformationResponse? deformation = input.DeformationResponse;
            if (deformation == null)
            {
                failureReason = PhysicalFragmentationFailureReason.DeformationResponseMissing;
                return false;
            }

            PhysicalProjectileMaterialProfile? projectileProfile = input.ProjectileProfile;
            if (projectileProfile == null)
            {
                failureReason = PhysicalFragmentationFailureReason.ProjectileProfileMissing;
                return false;
            }

            PhysicalTargetMaterialProfile? targetProfile = input.TargetProfile;
            if (targetProfile == null)
            {
                failureReason = PhysicalFragmentationFailureReason.TargetProfileMissing;
                return false;
            }

            PhysicalFragmentationProfile? fragmentationProfile = input.FragmentationProfile;
            if (fragmentationProfile == null)
            {
                failureReason = PhysicalFragmentationFailureReason.FragmentationProfileMissing;
                return false;
            }

            if (projectileProfile.Construction != parent.Construction)
            {
                failureReason = PhysicalFragmentationFailureReason.ProjectileConstructionMismatch;
                return false;
            }

            if (parent.FragmentGeneration == int.MaxValue)
            {
                failureReason = PhysicalFragmentationFailureReason.FragmentGenerationOverflow;
                return false;
            }

            if (!deformation.RequiresFragmentation
                || deformation.CollisionRecord.Outcome != PhysicalCollisionOutcome.Fragmented)
            {
                failureReason = PhysicalFragmentationFailureReason.FragmentationOutcomeMissing;
                return false;
            }

            PhysicalConservationResult deformationConservation;
            PhysicalConservationFailureReason deformationReason;
            if (!PhysicalProjectileConservation.TryValidateDeformationResponse(
                parent,
                deformation.PrimaryState,
                deformation.CollisionRecord,
                deformation.AvailableFragmentMassKilograms,
                deformation.AvailableFragmentEnergyJoules,
                deformation.LossBudget,
                out deformationConservation,
                out deformationReason))
            {
                failureReason = PhysicalFragmentationFailureReason.DeformationResponseMismatch;
                return false;
            }

            if (input.ObservedProjectileFragmentCount < 0)
            {
                failureReason = PhysicalFragmentationFailureReason.ObservedFragmentCountInvalid;
                return false;
            }

            string? projectileIdPrefix = input.ProjectileIdPrefix;
            if (string.IsNullOrWhiteSpace(projectileIdPrefix))
            {
                failureReason = PhysicalFragmentationFailureReason.ProjectileIdPrefixMissing;
                return false;
            }

            string? targetSpallIdPrefix = input.TargetSpallIdPrefix;
            if (fragmentationProfile.ProducesTargetSpall
                && string.IsNullOrWhiteSpace(targetSpallIdPrefix))
            {
                failureReason = PhysicalFragmentationFailureReason.TargetSpallIdPrefixMissing;
                return false;
            }

            string resolvedTargetSpallIdPrefix = targetSpallIdPrefix ?? string.Empty;

            PhysicalVector3 projectileAxis;
            if (!deformation.OutgoingDirection.TryNormalize(out projectileAxis))
            {
                failureReason = PhysicalFragmentationFailureReason.DirectionInvalid;
                return false;
            }

            PhysicalVector3 spallAxis;
            PhysicalVector3 spallDrive = deformation.OutgoingDirection.Scale(0.75d)
                .Add(deformation.SurfaceNormal.Negate().Scale(0.25d));
            if (!spallDrive.TryNormalize(out spallAxis))
            {
                failureReason = PhysicalFragmentationFailureReason.DirectionInvalid;
                return false;
            }

            double fragmentMassKilograms = deformation.AvailableFragmentMassKilograms;
            double fragmentEnergyJoules = deformation.AvailableFragmentEnergyJoules;
            double massTolerance = Math.Max(
                0.000000000001d,
                parent.RetainedMassKilograms * RelativeTolerance);
            double energyTolerance = Math.Max(
                0.000000001d,
                parent.TranslationalKineticEnergyJoules * RelativeTolerance);
            if (!IsFinitePositive(fragmentMassKilograms)
                || !IsFinitePositive(fragmentEnergyJoules)
                || fragmentMassKilograms <= massTolerance
                || fragmentEnergyJoules <= energyTolerance)
            {
                failureReason = PhysicalFragmentationFailureReason.ProjectileReservationInvalid;
                return false;
            }

            double countByMassValue = Math.Floor(
                fragmentMassKilograms
                    / fragmentationProfile.MinimumProjectileFragmentMassKilograms);
            int countByMass = countByMassValue >= fragmentationProfile.MaximumProjectileFragmentCount
                ? fragmentationProfile.MaximumProjectileFragmentCount
                : Math.Max(1, (int)countByMassValue);
            int requestedCount = Math.Max(1, input.ObservedProjectileFragmentCount);
            int projectileFragmentCount = Math.Min(
                requestedCount,
                Math.Min(fragmentationProfile.MaximumProjectileFragmentCount, countByMass));

            ulong collisionSeed = StableHash64(deformation.CollisionRecord.CollisionId);
            ulong projectileSeed = parent.DeterministicSeed
                ^ collisionSeed
                ^ unchecked((ulong)(parent.FragmentGeneration + 1));
            var projectileRandom = new DeterministicProjectileRandom(
                projectileSeed,
                ProjectileStream);
            double[] projectileMasses = PartitionMass(
                fragmentMassKilograms,
                projectileFragmentCount,
                fragmentationProfile.MinimumProjectileFragmentMassKilograms,
                projectileRandom);
            double[] projectileEnergies = PartitionEnergy(
                fragmentEnergyJoules,
                projectileMasses,
                projectileRandom);
            var projectileFragments = new List<PhysicalProjectileState>(
                projectileFragmentCount);
            for (int index = 0; index < projectileFragmentCount; index++)
            {
                PhysicalProjectileState? fragment;
                if (!TryCreateProjectileFragment(
                    parent,
                    deformation,
                    projectileProfile,
                    fragmentationProfile,
                    projectileIdPrefix,
                    index,
                    projectileMasses[index],
                    projectileEnergies[index],
                    projectileAxis,
                    projectileRandom,
                    out fragment)
                    || fragment == null)
                {
                    failureReason = PhysicalFragmentationFailureReason.ProjectileStateInvalid;
                    return false;
                }

                projectileFragments.Add(fragment);
            }

            double targetSpallMassKilograms = 0d;
            double targetSpallEnergyJoules = 0d;
            var targetSpall = new List<PhysicalProjectileState>();
            PhysicalLossBudget effectiveLossBudget = deformation.LossBudget;
            if (fragmentationProfile.ProducesTargetSpall)
            {
                double availableTargetMassKilograms = deformation.SweptVolumeCubicMetres
                    * targetProfile.DensityKilogramsPerCubicMetre;
                targetSpallMassKilograms = availableTargetMassKilograms
                    * fragmentationProfile.TargetSpallEjectedMassFraction;
                targetSpallEnergyJoules = deformation.LossBudget.PenetrationLossJoules
                    * fragmentationProfile.TargetSpallKineticEnergyFraction;
                if (!IsFinitePositive(targetSpallMassKilograms)
                    || !IsFinitePositive(targetSpallEnergyJoules)
                    || targetSpallEnergyJoules
                        > deformation.LossBudget.PenetrationLossJoules)
                {
                    failureReason = PhysicalFragmentationFailureReason.TargetSpallReservationInvalid;
                    return false;
                }

                effectiveLossBudget = new PhysicalLossBudget(
                    deformation.LossBudget.PenetrationLossJoules - targetSpallEnergyJoules,
                    deformation.LossBudget.DeformationLossJoules,
                    deformation.LossBudget.FractureLossJoules,
                    deformation.LossBudget.HeatLossJoules,
                    deformation.LossBudget.OtherLossJoules);
                PhysicalLossBudgetFailureReason lossReason;
                if (!effectiveLossBudget.IsValid(out lossReason))
                {
                    failureReason = PhysicalFragmentationFailureReason.EffectiveLossBudgetInvalid;
                    return false;
                }

                double targetSpallCountValue = Math.Ceiling(
                    targetSpallMassKilograms
                        / fragmentationProfile.NominalTargetSpallMassKilograms);
                int targetSpallCount = targetSpallCountValue
                        >= fragmentationProfile.MaximumTargetSpallCount
                    ? fragmentationProfile.MaximumTargetSpallCount
                    : Math.Max(1, (int)targetSpallCountValue);
                var spallRandom = new DeterministicProjectileRandom(
                    projectileSeed ^ StableHash64(targetProfile.ProfileId),
                    TargetSpallStream);
                double[] spallMasses = PartitionMass(
                    targetSpallMassKilograms,
                    targetSpallCount,
                    0d,
                    spallRandom);
                double[] spallEnergies = PartitionEnergy(
                    targetSpallEnergyJoules,
                    spallMasses,
                    spallRandom);
                targetSpall.Capacity = targetSpallCount;
                for (int index = 0; index < targetSpallCount; index++)
                {
                    PhysicalProjectileState? component;
                    if (!TryCreateTargetSpall(
                        parent,
                        deformation,
                        targetProfile,
                        fragmentationProfile,
                        resolvedTargetSpallIdPrefix,
                        projectileFragmentCount + index,
                        spallMasses[index],
                        spallEnergies[index],
                        spallAxis,
                        spallRandom,
                        out component)
                        || component == null)
                    {
                        failureReason = PhysicalFragmentationFailureReason.TargetSpallStateInvalid;
                        return false;
                    }

                    targetSpall.Add(component);
                }
            }

            var allSecondaries = new List<PhysicalProjectileState?>(
                projectileFragments.Count + targetSpall.Count);
            for (int index = 0; index < projectileFragments.Count; index++)
            {
                allSecondaries.Add(projectileFragments[index]);
            }

            for (int index = 0; index < targetSpall.Count; index++)
            {
                allSecondaries.Add(targetSpall[index]);
            }

            PhysicalConservationResult conservationResult;
            PhysicalConservationFailureReason conservationReason;
            if (!PhysicalProjectileConservation.TryValidateFragmentationResolution(
                parent,
                deformation.PrimaryState,
                deformation.CollisionRecord,
                allSecondaries,
                fragmentMassKilograms,
                fragmentEnergyJoules,
                targetSpallMassKilograms,
                targetSpallEnergyJoules,
                deformation.LossBudget,
                effectiveLossBudget,
                out conservationResult,
                out conservationReason))
            {
                failureReason = PhysicalFragmentationFailureReason.ConservationValidationFailed;
                return false;
            }

            response = new PhysicalFragmentationResponse(
                deformation.PrimaryState,
                projectileFragments,
                targetSpall,
                input.ObservedProjectileFragmentCount,
                effectiveLossBudget,
                conservationResult,
                targetSpallMassKilograms,
                targetSpallEnergyJoules);
            failureReason = PhysicalFragmentationFailureReason.None;
            return true;
        }

        public static bool TrySolveTargetSpall(
            PhysicalTargetSpallInput? input,
            out PhysicalTargetSpallResponse? response,
            out PhysicalTargetSpallFailureReason failureReason)
        {
            response = null;
            if (input == null)
            {
                failureReason = PhysicalTargetSpallFailureReason.InputMissing;
                return false;
            }

            PhysicalProjectileState? parent = input.Parent;
            if (parent == null)
            {
                failureReason = PhysicalTargetSpallFailureReason.ParentMissing;
                return false;
            }

            PhysicalDeformationResponse? deformation = input.DeformationResponse;
            if (deformation == null)
            {
                failureReason = PhysicalTargetSpallFailureReason.DeformationResponseMissing;
                return false;
            }

            PhysicalTargetMaterialProfile? targetProfile = input.TargetProfile;
            if (targetProfile == null)
            {
                failureReason = PhysicalTargetSpallFailureReason.TargetProfileMissing;
                return false;
            }

            PhysicalFragmentationProfile? fragmentationProfile = input.FragmentationProfile;
            if (fragmentationProfile == null)
            {
                failureReason = PhysicalTargetSpallFailureReason.FragmentationProfileMissing;
                return false;
            }

            if (!fragmentationProfile.ProducesTargetSpall)
            {
                failureReason = PhysicalTargetSpallFailureReason.TargetSpallDisabled;
                return false;
            }

            if (deformation.CollisionRecord.Outcome == PhysicalCollisionOutcome.Fragmented
                || deformation.RequiresFragmentation)
            {
                failureReason =
                    PhysicalTargetSpallFailureReason.FragmentationOutcomeOwnedByFragmentationSolver;
                return false;
            }

            if (deformation.CollisionRecord.Outcome != PhysicalCollisionOutcome.Penetrated
                && deformation.CollisionRecord.Outcome != PhysicalCollisionOutcome.Deviated)
            {
                failureReason = PhysicalTargetSpallFailureReason.NonPenetratingOutcome;
                return false;
            }

            if (parent.FragmentGeneration == int.MaxValue)
            {
                failureReason = PhysicalTargetSpallFailureReason.FragmentGenerationOverflow;
                return false;
            }

            if (!string.Equals(
                    deformation.CollisionRecord.MaterialId,
                    targetProfile.ProfileId,
                    StringComparison.Ordinal)
                || deformation.CollisionRecord.MaterialClass != targetProfile.MaterialClass)
            {
                failureReason = PhysicalTargetSpallFailureReason.TargetProfileMismatch;
                return false;
            }

            if (!PhysicalProjectileConservation.TryValidateDeformationResponse(
                    parent,
                    deformation.PrimaryState,
                    deformation.CollisionRecord,
                    deformation.AvailableFragmentMassKilograms,
                    deformation.AvailableFragmentEnergyJoules,
                    deformation.LossBudget,
                    out _,
                    out _))
            {
                failureReason = PhysicalTargetSpallFailureReason.DeformationResponseMismatch;
                return false;
            }

            string? targetSpallIdPrefix = input.TargetSpallIdPrefix;
            if (string.IsNullOrWhiteSpace(targetSpallIdPrefix))
            {
                failureReason = PhysicalTargetSpallFailureReason.TargetSpallIdPrefixMissing;
                return false;
            }

            PhysicalVector3 spallDrive = deformation.OutgoingDirection.Scale(0.75d)
                .Add(deformation.SurfaceNormal.Negate().Scale(0.25d));
            if (!spallDrive.TryNormalize(out PhysicalVector3 spallAxis))
            {
                failureReason = PhysicalTargetSpallFailureReason.DirectionInvalid;
                return false;
            }

            double availableTargetMassKilograms = deformation.SweptVolumeCubicMetres
                * targetProfile.DensityKilogramsPerCubicMetre;
            double targetSpallMassKilograms = availableTargetMassKilograms
                * fragmentationProfile.TargetSpallEjectedMassFraction;
            double targetSpallEnergyJoules = deformation.LossBudget.PenetrationLossJoules
                * fragmentationProfile.TargetSpallKineticEnergyFraction;
            if (!IsFinitePositive(targetSpallMassKilograms)
                || !IsFinitePositive(targetSpallEnergyJoules)
                || targetSpallEnergyJoules > deformation.LossBudget.PenetrationLossJoules)
            {
                failureReason = PhysicalTargetSpallFailureReason.TargetSpallReservationInvalid;
                return false;
            }

            var effectiveLossBudget = new PhysicalLossBudget(
                deformation.LossBudget.PenetrationLossJoules - targetSpallEnergyJoules,
                deformation.LossBudget.DeformationLossJoules,
                deformation.LossBudget.FractureLossJoules,
                deformation.LossBudget.HeatLossJoules,
                deformation.LossBudget.OtherLossJoules);
            if (!effectiveLossBudget.IsValid(out _))
            {
                failureReason = PhysicalTargetSpallFailureReason.EffectiveLossBudgetInvalid;
                return false;
            }

            double targetSpallCountValue = Math.Ceiling(
                targetSpallMassKilograms
                    / fragmentationProfile.NominalTargetSpallMassKilograms);
            int targetSpallCount = targetSpallCountValue
                    >= fragmentationProfile.MaximumTargetSpallCount
                ? fragmentationProfile.MaximumTargetSpallCount
                : Math.Max(1, (int)targetSpallCountValue);
            ulong collisionSeed = StableHash64(deformation.CollisionRecord.CollisionId);
            ulong componentSeed = parent.DeterministicSeed
                ^ collisionSeed
                ^ unchecked((ulong)(parent.FragmentGeneration + 1));
            var spallRandom = new DeterministicProjectileRandom(
                componentSeed ^ StableHash64(targetProfile.ProfileId),
                TargetSpallStream);
            double[] spallMasses = PartitionMass(
                targetSpallMassKilograms,
                targetSpallCount,
                0d,
                spallRandom);
            double[] spallEnergies = PartitionEnergy(
                targetSpallEnergyJoules,
                spallMasses,
                spallRandom);
            var targetSpall = new List<PhysicalProjectileState>(targetSpallCount);
            for (int index = 0; index < targetSpallCount; index++)
            {
                if (!TryCreateTargetSpall(
                        parent,
                        deformation,
                        targetProfile,
                        fragmentationProfile,
                        targetSpallIdPrefix,
                        index,
                        spallMasses[index],
                        spallEnergies[index],
                        spallAxis,
                        spallRandom,
                        out PhysicalProjectileState? component)
                    || component == null)
                {
                    failureReason = PhysicalTargetSpallFailureReason.TargetSpallStateInvalid;
                    return false;
                }

                targetSpall.Add(component);
            }

            var nullableComponents = new List<PhysicalProjectileState?>(targetSpall.Count);
            for (int index = 0; index < targetSpall.Count; index++)
            {
                nullableComponents.Add(targetSpall[index]);
            }

            if (!PhysicalProjectileConservation.TryValidateFragmentationResolution(
                    parent,
                    deformation.PrimaryState,
                    deformation.CollisionRecord,
                    nullableComponents,
                    0d,
                    0d,
                    targetSpallMassKilograms,
                    targetSpallEnergyJoules,
                    deformation.LossBudget,
                    effectiveLossBudget,
                    out PhysicalConservationResult conservationResult,
                    out _))
            {
                failureReason = PhysicalTargetSpallFailureReason.ConservationValidationFailed;
                return false;
            }

            response = new PhysicalTargetSpallResponse(
                targetSpall,
                effectiveLossBudget,
                conservationResult,
                targetSpallMassKilograms,
                targetSpallEnergyJoules);
            failureReason = PhysicalTargetSpallFailureReason.None;
            return true;
        }

        private static bool TryCreateProjectileFragment(
            PhysicalProjectileState parent,
            PhysicalDeformationResponse deformation,
            PhysicalProjectileMaterialProfile projectileProfile,
            PhysicalFragmentationProfile fragmentationProfile,
            string projectileIdPrefix,
            int fragmentIndex,
            double massKilograms,
            double energyJoules,
            PhysicalVector3 axis,
            DeterministicProjectileRandom random,
            out PhysicalProjectileState? state)
        {
            state = null;
            double aspectRatio = Lerp(
                fragmentationProfile.MinimumProjectileAspectRatio,
                fragmentationProfile.MaximumProjectileAspectRatio,
                random.NextUnitDouble());
            double diameterMetres;
            double lengthMetres;
            double projectedAreaSquareMetres;
            double yawAngleRadians;
            if (!TryCalculateComponentGeometry(
                massKilograms,
                projectileProfile.DensityKilogramsPerCubicMetre,
                aspectRatio,
                random,
                out diameterMetres,
                out lengthMetres,
                out projectedAreaSquareMetres,
                out yawAngleRadians))
            {
                return false;
            }

            PhysicalVector3 direction;
            if (!TrySampleConeDirection(
                axis,
                fragmentationProfile.ProjectileConeHalfAngleRadians,
                random,
                out direction))
            {
                return false;
            }

            PhysicalVector3 longitudinalAxis;
            if (!TrySampleAngularOffset(
                direction,
                yawAngleRadians,
                random,
                out longitudinalAxis))
            {
                return false;
            }

            double speedMetresPerSecond = Math.Sqrt((2d * energyJoules) / massKilograms);
            double dragMultiplier = Lerp(
                fragmentationProfile.MinimumProjectileDragMultiplier,
                fragmentationProfile.MaximumProjectileDragMultiplier,
                random.NextUnitDouble());
            double dragCoefficient = parent.DragCoefficient
                * dragMultiplier
                * (1d + Square(Math.Sin(yawAngleRadians)));
            double penetrationCapability = energyJoules
                / projectedAreaSquareMetres
                * fragmentationProfile.ProjectilePenetrationEfficiency
                * (0.15d + (0.85d * Math.Abs(Math.Cos(yawAngleRadians))));
            double parentDamageFraction = Math.Min(
                1d,
                parent.DamageCapabilityJoules / parent.TranslationalKineticEnergyJoules);
            double damageCapability = Math.Min(
                energyJoules,
                energyJoules * parentDamageFraction);
            bool fragmentsTargetMaterial = parent.Construction
                == PhysicalProjectileConstruction.TargetMaterial;
            PhysicalProjectileKind componentKind = fragmentsTargetMaterial
                ? PhysicalProjectileKind.TargetSpallFragment
                : PhysicalProjectileKind.ProjectileFragment;
            PhysicalProjectileShapeClass componentShape = fragmentsTargetMaterial
                ? (lengthMetres / diameterMetres < 0.75d
                    ? PhysicalProjectileShapeClass.TargetSpallFlake
                    : PhysicalProjectileShapeClass.TargetSpallChunk)
                : PhysicalProjectileShapeClass.IrregularProjectileFragment;
            return TryCreateSecondaryState(
                parent,
                deformation,
                componentKind,
                projectileIdPrefix + "-" + fragmentIndex.ToString(System.Globalization.CultureInfo.InvariantCulture),
                fragmentIndex,
                CombineSeed(parent.DeterministicSeed, random.NextUInt32(), random.NextUInt32()),
                parent.Construction,
                PhysicalProjectileDesignClass.Fragment,
                componentShape,
                massKilograms,
                diameterMetres,
                projectedAreaSquareMetres,
                lengthMetres,
                dragCoefficient,
                direction.Scale(speedMetresPerSecond),
                longitudinalAxis,
                yawAngleRadians,
                penetrationCapability,
                damageCapability,
                out state);
        }

        private static bool TryCreateTargetSpall(
            PhysicalProjectileState parent,
            PhysicalDeformationResponse deformation,
            PhysicalTargetMaterialProfile targetProfile,
            PhysicalFragmentationProfile fragmentationProfile,
            string targetSpallIdPrefix,
            int fragmentIndex,
            double massKilograms,
            double energyJoules,
            PhysicalVector3 axis,
            DeterministicProjectileRandom random,
            out PhysicalProjectileState? state)
        {
            state = null;
            double aspectRatio = Lerp(
                fragmentationProfile.MinimumTargetSpallAspectRatio,
                fragmentationProfile.MaximumTargetSpallAspectRatio,
                random.NextUnitDouble());
            double diameterMetres;
            double lengthMetres;
            double projectedAreaSquareMetres;
            double yawAngleRadians;
            if (!TryCalculateComponentGeometry(
                massKilograms,
                targetProfile.DensityKilogramsPerCubicMetre,
                aspectRatio,
                random,
                out diameterMetres,
                out lengthMetres,
                out projectedAreaSquareMetres,
                out yawAngleRadians))
            {
                return false;
            }

            PhysicalVector3 direction;
            if (!TrySampleConeDirection(
                axis,
                fragmentationProfile.TargetSpallConeHalfAngleRadians,
                random,
                out direction))
            {
                return false;
            }

            PhysicalVector3 longitudinalAxis;
            if (!TrySampleAngularOffset(
                direction,
                yawAngleRadians,
                random,
                out longitudinalAxis))
            {
                return false;
            }

            double speedMetresPerSecond = Math.Sqrt((2d * energyJoules) / massKilograms);
            double dragCoefficient = Lerp(
                fragmentationProfile.MinimumTargetSpallDragCoefficient,
                fragmentationProfile.MaximumTargetSpallDragCoefficient,
                random.NextUnitDouble())
                * (1d + Square(Math.Sin(yawAngleRadians)));
            double penetrationCapability = energyJoules
                / projectedAreaSquareMetres
                * fragmentationProfile.TargetSpallPenetrationEfficiency
                * (0.1d + (0.9d * Math.Abs(Math.Cos(yawAngleRadians))));
            PhysicalProjectileShapeClass shapeClass = aspectRatio <= 0.35d
                ? PhysicalProjectileShapeClass.TargetSpallFlake
                : PhysicalProjectileShapeClass.TargetSpallChunk;
            return TryCreateSecondaryState(
                parent,
                deformation,
                PhysicalProjectileKind.TargetSpall,
                targetSpallIdPrefix + "-" + fragmentIndex.ToString(System.Globalization.CultureInfo.InvariantCulture),
                fragmentIndex,
                CombineSeed(parent.DeterministicSeed, random.NextUInt32(), random.NextUInt32()),
                PhysicalProjectileConstruction.TargetMaterial,
                PhysicalProjectileDesignClass.Fragment,
                shapeClass,
                massKilograms,
                diameterMetres,
                projectedAreaSquareMetres,
                lengthMetres,
                dragCoefficient,
                direction.Scale(speedMetresPerSecond),
                longitudinalAxis,
                yawAngleRadians,
                penetrationCapability,
                energyJoules,
                out state);
        }

        private static bool TryCreateSecondaryState(
            PhysicalProjectileState parent,
            PhysicalDeformationResponse deformation,
            PhysicalProjectileKind kind,
            string projectileId,
            int fragmentIndex,
            ulong deterministicSeed,
            PhysicalProjectileConstruction construction,
            PhysicalProjectileDesignClass designClass,
            PhysicalProjectileShapeClass shapeClass,
            double massKilograms,
            double diameterMetres,
            double projectedAreaSquareMetres,
            double lengthMetres,
            double dragCoefficient,
            PhysicalVector3 velocity,
            PhysicalVector3 longitudinalAxis,
            double yawAngleRadians,
            double penetrationCapability,
            double damageCapability,
            out PhysicalProjectileState? state)
        {
            state = null;
            if (!IsFinitePositive(massKilograms)
                || !IsFinitePositive(diameterMetres)
                || !IsFinitePositive(projectedAreaSquareMetres)
                || !IsFinitePositive(lengthMetres)
                || !IsFinitePositive(dragCoefficient)
                || !velocity.IsFinite
                || !IsFiniteNonNegative(penetrationCapability)
                || !IsFiniteNonNegative(damageCapability))
            {
                return false;
            }

            PhysicalOrientation orientation;
            if (!PhysicalOrientation.TryFromForward(longitudinalAxis, out orientation))
            {
                return false;
            }

            PhysicalCollisionRecord[] history = CreateInheritedHistory(
                parent,
                deformation.CollisionRecord);
            bool preservesTargetMaterialOrigin = kind
                == PhysicalProjectileKind.TargetSpallFragment;
            var stateInput = new PhysicalProjectileStateInput
            {
                Kind = kind,
                ProjectileId = projectileId,
                RootShotId = parent.RootShotId,
                ParentProjectileId = parent.ProjectileId,
                SourceProjectileId = parent.ProjectileId,
                SourceMaterialId = preservesTargetMaterialOrigin
                    ? parent.SourceMaterialId
                    : deformation.CollisionRecord.MaterialId,
                SourceMaterialClass = preservesTargetMaterialOrigin
                    ? parent.SourceMaterialClass
                    : deformation.CollisionRecord.MaterialClass,
                SourceCollisionId = deformation.CollisionRecord.CollisionId,
                FragmentIndex = fragmentIndex,
                FragmentGeneration = parent.FragmentGeneration + 1,
                DeterministicSeed = deterministicSeed,
                Construction = construction,
                DesignClass = designClass,
                ShapeClass = shapeClass,
                OriginalMassKilograms = massKilograms,
                RetainedMassKilograms = massKilograms,
                NominalDiameterMetres = diameterMetres,
                DeformedDiameterMetres = diameterMetres,
                ProjectedAreaSquareMetres = projectedAreaSquareMetres,
                LengthMetres = lengthMetres,
                DragCoefficient = dragCoefficient,
                PositionMetres = deformation.OutputPositionMetres,
                VelocityMetresPerSecond = velocity,
                Orientation = orientation,
                YawAngleRadians = yawAngleRadians,
                TumbleState = PhysicalProjectileTumbleState.Tumbling,
                PenetrationCapabilityJoulesPerSquareMetre = penetrationCapability,
                DamageCapabilityJoules = damageCapability,
                TerminalState = PhysicalProjectileTerminalState.Exited,
                RenderState = PhysicalProjectileRenderState.NotRendered,
                CollisionHistory = history
            };
            PhysicalProjectileStateFailureReason stateReason;
            return PhysicalProjectileState.TryCreate(stateInput, out state, out stateReason);
        }

        private static PhysicalCollisionRecord[] CreateInheritedHistory(
            PhysicalProjectileState parent,
            PhysicalCollisionRecord collision)
        {
            var history = new PhysicalCollisionRecord[parent.CollisionHistory.Count + 1];
            for (int index = 0; index < parent.CollisionHistory.Count; index++)
            {
                history[index] = parent.CollisionHistory[index];
            }

            history[history.Length - 1] = collision;
            return history;
        }

        private static double[] PartitionMass(
            double totalMassKilograms,
            int count,
            double minimumMassKilograms,
            DeterministicProjectileRandom random)
        {
            var result = new double[count];
            var weights = new double[count];
            double baseMass = count == 1
                ? 0d
                : Math.Min(minimumMassKilograms, totalMassKilograms / count);
            double remainingMass = totalMassKilograms - (baseMass * count);
            double totalWeight = 0d;
            for (int index = 0; index < count; index++)
            {
                weights[index] = 0.5d + random.NextUnitDouble();
                totalWeight += weights[index];
            }

            double assignedMass = 0d;
            for (int index = 0; index < count - 1; index++)
            {
                result[index] = baseMass + (remainingMass * weights[index] / totalWeight);
                assignedMass += result[index];
            }

            result[count - 1] = totalMassKilograms - assignedMass;
            return result;
        }

        private static double[] PartitionEnergy(
            double totalEnergyJoules,
            double[] masses,
            DeterministicProjectileRandom random)
        {
            var result = new double[masses.Length];
            var weights = new double[masses.Length];
            double totalWeight = 0d;
            for (int index = 0; index < masses.Length; index++)
            {
                weights[index] = masses[index] * (0.75d + (0.5d * random.NextUnitDouble()));
                totalWeight += weights[index];
            }

            double assignedEnergy = 0d;
            for (int index = 0; index < masses.Length - 1; index++)
            {
                result[index] = totalEnergyJoules * weights[index] / totalWeight;
                assignedEnergy += result[index];
            }

            result[masses.Length - 1] = totalEnergyJoules - assignedEnergy;
            return result;
        }

        private static bool TryCalculateComponentGeometry(
            double massKilograms,
            double densityKilogramsPerCubicMetre,
            double aspectRatio,
            DeterministicProjectileRandom random,
            out double diameterMetres,
            out double lengthMetres,
            out double projectedAreaSquareMetres,
            out double yawAngleRadians)
        {
            diameterMetres = 0d;
            lengthMetres = 0d;
            projectedAreaSquareMetres = 0d;
            yawAngleRadians = 0d;
            if (!IsFinitePositive(massKilograms)
                || !IsFinitePositive(densityKilogramsPerCubicMetre)
                || !IsFinitePositive(aspectRatio))
            {
                return false;
            }

            double volumeCubicMetres = massKilograms / densityKilogramsPerCubicMetre;
            double diameterCubed = (4d * volumeCubicMetres) / (Math.PI * aspectRatio);
            diameterMetres = Math.Pow(diameterCubed, 1d / 3d);
            lengthMetres = diameterMetres * aspectRatio;
            yawAngleRadians = Lerp(
                Math.PI / 6d,
                Math.PI * 0.5d,
                random.NextUnitDouble());
            double frontalAreaSquareMetres;
            if (!PhysicalProjectileGeometry.TryCalculateCircularAreaSquareMetres(
                diameterMetres,
                out frontalAreaSquareMetres))
            {
                return false;
            }

            double sideAreaSquareMetres = diameterMetres * lengthMetres;
            projectedAreaSquareMetres = (frontalAreaSquareMetres
                * Math.Abs(Math.Cos(yawAngleRadians)))
                + (sideAreaSquareMetres * Math.Abs(Math.Sin(yawAngleRadians)));
            return IsFinitePositive(volumeCubicMetres)
                && IsFinitePositive(diameterMetres)
                && IsFinitePositive(lengthMetres)
                && IsFinitePositive(projectedAreaSquareMetres);
        }

        private static bool TrySampleConeDirection(
            PhysicalVector3 axis,
            double halfAngleRadians,
            DeterministicProjectileRandom random,
            out PhysicalVector3 direction)
        {
            direction = PhysicalVector3.Zero;
            PhysicalVector3 unitAxis;
            if (!axis.TryNormalize(out unitAxis))
            {
                return false;
            }

            PhysicalVector3 reference = Math.Abs(unitAxis.Z) < 0.9d
                ? new PhysicalVector3(0d, 0d, 1d)
                : new PhysicalVector3(0d, 1d, 0d);
            PhysicalVector3 right;
            if (!reference.Cross(unitAxis).TryNormalize(out right))
            {
                return false;
            }

            PhysicalVector3 up;
            if (!unitAxis.Cross(right).TryNormalize(out up))
            {
                return false;
            }

            double cosineMaximumAngle = Math.Cos(halfAngleRadians);
            double cosineAngle = 1d
                - (random.NextUnitDouble() * (1d - cosineMaximumAngle));
            double sineAngle = Math.Sqrt(Math.Max(0d, 1d - (cosineAngle * cosineAngle)));
            double azimuthRadians = Math.PI * 2d * random.NextUnitDouble();
            PhysicalVector3 candidate = unitAxis.Scale(cosineAngle)
                .Add(right.Scale(sineAngle * Math.Cos(azimuthRadians)))
                .Add(up.Scale(sineAngle * Math.Sin(azimuthRadians)));
            return candidate.TryNormalize(out direction);
        }

        private static bool TrySampleAngularOffset(
            PhysicalVector3 axis,
            double angleRadians,
            DeterministicProjectileRandom random,
            out PhysicalVector3 direction)
        {
            direction = PhysicalVector3.Zero;
            if (!FiniteDouble.IsFinite(angleRadians)
                || angleRadians < 0d
                || angleRadians > Math.PI)
            {
                return false;
            }

            PhysicalVector3 unitAxis;
            if (!axis.TryNormalize(out unitAxis))
            {
                return false;
            }

            PhysicalVector3 reference = Math.Abs(unitAxis.Z) < 0.9d
                ? new PhysicalVector3(0d, 0d, 1d)
                : new PhysicalVector3(0d, 1d, 0d);
            PhysicalVector3 right;
            if (!reference.Cross(unitAxis).TryNormalize(out right))
            {
                return false;
            }

            PhysicalVector3 up;
            if (!unitAxis.Cross(right).TryNormalize(out up))
            {
                return false;
            }

            double azimuthRadians = Math.PI * 2d * random.NextUnitDouble();
            double sineAngle = Math.Sin(angleRadians);
            PhysicalVector3 candidate = unitAxis.Scale(Math.Cos(angleRadians))
                .Add(right.Scale(sineAngle * Math.Cos(azimuthRadians)))
                .Add(up.Scale(sineAngle * Math.Sin(azimuthRadians)));
            return candidate.TryNormalize(out direction);
        }

        private static ulong StableHash64(string value)
        {
            const ulong offset = 14695981039346656037UL;
            const ulong prime = 1099511628211UL;
            ulong hash = offset;
            for (int index = 0; index < value.Length; index++)
            {
                char character = value[index];
                hash = unchecked((hash ^ (byte)character) * prime);
                hash = unchecked((hash ^ (byte)(character >> 8)) * prime);
            }

            return hash;
        }

        private static ulong CombineSeed(ulong parentSeed, uint high, uint low)
        {
            return parentSeed ^ ((ulong)high << 32) ^ low;
        }

        private static double Lerp(double minimum, double maximum, double amount)
        {
            return minimum + ((maximum - minimum) * amount);
        }

        private static double Square(double value)
        {
            return value * value;
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
