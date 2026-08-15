#nullable enable

using System;
using System.Collections.Generic;
using BallisticPenetration.Core;

namespace BallisticPenetration.Core.Physics
{
    public enum PhysicalDeformationFailureReason
    {
        None = 0,
        InputMissing = 1,
        ParentMissing = 2,
        ProjectileProfileMissing = 3,
        TargetProfileMissing = 4,
        ProjectileConstructionMismatch = 5,
        CollisionIdMissing = 6,
        OutputProjectileIdMissing = 7,
        OutputProjectileIdentityInvalid = 8,
        ImpactPositionInvalid = 9,
        SurfaceNormalInvalid = 10,
        ImpactDirectionInvalid = 11,
        PhysicalThicknessInvalid = 12,
        EffectivePathLengthInvalid = 13,
        OutcomeInvalid = 14,
        OutgoingDirectionInvalid = 15,
        DuplicateCollisionId = 16,
        WorkCalculationInvalid = 17,
        EnergyDemandInvalid = 18,
        MovingOutcomeHasNoResidualEnergy = 19,
        FragmentationUnsupportedByProfile = 20,
        GeometryInvalid = 21,
        LossBudgetInvalid = 22,
        CollisionRecordInvalid = 23,
        PrimaryStateInvalid = 24,
        ConservationValidationFailed = 25
    }

    public sealed class PhysicalDeformationInput
    {
        public PhysicalProjectileState? Parent { get; set; }

        public PhysicalProjectileMaterialProfile? ProjectileProfile { get; set; }

        public PhysicalTargetMaterialProfile? TargetProfile { get; set; }

        public string? CollisionId { get; set; }

        public string? OutputProjectileId { get; set; }

        public PhysicalVector3 ImpactPositionMetres { get; set; }

        /// <summary>
        /// Target surface normal. The solver normalizes it and requires it to oppose incoming travel.
        /// </summary>
        public PhysicalVector3 SurfaceNormal { get; set; }

        /// <summary>
        /// Measured physical thickness normal to the struck surface, in metres.
        /// </summary>
        public double PhysicalThicknessMetres { get; set; }

        /// <summary>
        /// Actual path through material, in metres. It must be at least the physical thickness.
        /// </summary>
        public double EffectivePathLengthMetres { get; set; }

        /// <summary>
        /// Outcome already selected by the host ballistics system. The solver does not reroll it.
        /// </summary>
        public PhysicalCollisionOutcome ObservedOutcome { get; set; }

        /// <summary>
        /// Host-selected outgoing direction for a continuing outcome. It is normalized by the solver.
        /// </summary>
        public PhysicalVector3 ObservedOutgoingDirection { get; set; }
    }

    /// <summary>
    /// Deterministic work-energy response for one confirmed collision. A fragmented response reserves
    /// mass and energy for the next fragmentation stage; it does not manufacture fragment children.
    /// </summary>
    public sealed class PhysicalDeformationResponse
    {
        internal PhysicalDeformationResponse(
            PhysicalProjectileState? primaryState,
            PhysicalCollisionRecord collisionRecord,
            PhysicalLossBudget lossBudget,
            PhysicalVector3 incomingDirection,
            PhysicalVector3 outgoingDirection,
            PhysicalVector3 outputPositionMetres,
            PhysicalVector3 surfaceNormal,
            double physicalThicknessMetres,
            double effectivePathLengthMetres,
            double impactAngleRadians,
            double normalImpactEnergyJoules,
            double sweptVolumeCubicMetres,
            double rawTargetResistanceWorkJoules,
            double allocatedTargetWorkJoules,
            double deformationCapacityJoules,
            double deformationSeverity,
            double fractureProbability,
            double diameterExpansionRatio,
            double retainedPrimaryMassKilograms,
            double availableFragmentMassKilograms,
            double primaryEnergyJoules,
            double availableFragmentEnergyJoules,
            double residualSystemEnergyJoules,
            double residualSpeedMetresPerSecond)
        {
            PrimaryState = primaryState;
            CollisionRecord = collisionRecord;
            LossBudget = lossBudget;
            IncomingDirection = incomingDirection;
            OutgoingDirection = outgoingDirection;
            OutputPositionMetres = outputPositionMetres;
            SurfaceNormal = surfaceNormal;
            PhysicalThicknessMetres = physicalThicknessMetres;
            EffectivePathLengthMetres = effectivePathLengthMetres;
            ImpactAngleRadians = impactAngleRadians;
            NormalImpactEnergyJoules = normalImpactEnergyJoules;
            SweptVolumeCubicMetres = sweptVolumeCubicMetres;
            RawTargetResistanceWorkJoules = rawTargetResistanceWorkJoules;
            AllocatedTargetWorkJoules = allocatedTargetWorkJoules;
            DeformationCapacityJoules = deformationCapacityJoules;
            DeformationSeverity = deformationSeverity;
            FractureProbability = fractureProbability;
            DiameterExpansionRatio = diameterExpansionRatio;
            RetainedPrimaryMassKilograms = retainedPrimaryMassKilograms;
            AvailableFragmentMassKilograms = availableFragmentMassKilograms;
            PrimaryEnergyJoules = primaryEnergyJoules;
            AvailableFragmentEnergyJoules = availableFragmentEnergyJoules;
            ResidualSystemEnergyJoules = residualSystemEnergyJoules;
            ResidualSpeedMetresPerSecond = residualSpeedMetresPerSecond;
        }

        public PhysicalProjectileState? PrimaryState { get; }

        public PhysicalCollisionRecord CollisionRecord { get; }

        public PhysicalLossBudget LossBudget { get; }

        public PhysicalVector3 IncomingDirection { get; }

        public PhysicalVector3 OutgoingDirection { get; }

        /// <summary>
        /// Position at which a surviving component begins its next free-flight leg. A penetrated,
        /// deviated, or fragmented component starts at the measured far face. Ricochet and stop
        /// responses remain at the impact face; a stopped component's stored center is embedded
        /// separately so its physical model does not protrude halfway out of the target.
        /// </summary>
        public PhysicalVector3 OutputPositionMetres { get; }

        public PhysicalVector3 SurfaceNormal { get; }

        public double PhysicalThicknessMetres { get; }

        public double EffectivePathLengthMetres { get; }

        public double ImpactAngleRadians { get; }

        public double NormalImpactEnergyJoules { get; }

        public double SweptVolumeCubicMetres { get; }

        public double RawTargetResistanceWorkJoules { get; }

        public double AllocatedTargetWorkJoules { get; }

        public double DeformationCapacityJoules { get; }

        public double DeformationSeverity { get; }

        public double FractureProbability { get; }

        public double DiameterExpansionRatio { get; }

        public double RetainedPrimaryMassKilograms { get; }

        public double AvailableFragmentMassKilograms { get; }

        public double PrimaryEnergyJoules { get; }

        public double AvailableFragmentEnergyJoules { get; }

        public double ResidualSystemEnergyJoules { get; }

        public double ResidualSpeedMetresPerSecond { get; }

        public bool RequiresFragmentation
        {
            get { return CollisionRecord.Outcome == PhysicalCollisionOutcome.Fragmented; }
        }
    }

    public static class PhysicalDeformationSolver
    {
        private const double RelativeTolerance = 0.000000001d;

        public static bool TrySolve(
            PhysicalDeformationInput? input,
            out PhysicalDeformationResponse? response,
            out PhysicalDeformationFailureReason failureReason)
        {
            response = null;
            if (input == null)
            {
                failureReason = PhysicalDeformationFailureReason.InputMissing;
                return false;
            }

            PhysicalProjectileState? parent = input.Parent;
            if (parent == null)
            {
                failureReason = PhysicalDeformationFailureReason.ParentMissing;
                return false;
            }

            PhysicalProjectileMaterialProfile? projectileProfile = input.ProjectileProfile;
            if (projectileProfile == null)
            {
                failureReason = PhysicalDeformationFailureReason.ProjectileProfileMissing;
                return false;
            }

            PhysicalTargetMaterialProfile? targetProfile = input.TargetProfile;
            if (targetProfile == null)
            {
                failureReason = PhysicalDeformationFailureReason.TargetProfileMissing;
                return false;
            }

            if (projectileProfile.Construction != parent.Construction)
            {
                failureReason = PhysicalDeformationFailureReason.ProjectileConstructionMismatch;
                return false;
            }

            string? collisionId = input.CollisionId;
            if (string.IsNullOrWhiteSpace(collisionId))
            {
                failureReason = PhysicalDeformationFailureReason.CollisionIdMissing;
                return false;
            }

            string? outputProjectileId = input.OutputProjectileId;
            if (string.IsNullOrWhiteSpace(outputProjectileId))
            {
                failureReason = PhysicalDeformationFailureReason.OutputProjectileIdMissing;
                return false;
            }

            if (!string.Equals(outputProjectileId, parent.ProjectileId, StringComparison.Ordinal))
            {
                failureReason = PhysicalDeformationFailureReason.OutputProjectileIdentityInvalid;
                return false;
            }

            if (!input.ImpactPositionMetres.IsFinite)
            {
                failureReason = PhysicalDeformationFailureReason.ImpactPositionInvalid;
                return false;
            }

            if (!IsFinitePositive(input.PhysicalThicknessMetres))
            {
                failureReason = PhysicalDeformationFailureReason.PhysicalThicknessInvalid;
                return false;
            }

            double pathTolerance = Math.Max(0.000000000001d, input.PhysicalThicknessMetres * RelativeTolerance);
            if (!IsFinitePositive(input.EffectivePathLengthMetres)
                || input.EffectivePathLengthMetres + pathTolerance < input.PhysicalThicknessMetres)
            {
                failureReason = PhysicalDeformationFailureReason.EffectivePathLengthInvalid;
                return false;
            }

            if (input.ObservedOutcome < PhysicalCollisionOutcome.Penetrated
                || input.ObservedOutcome > PhysicalCollisionOutcome.Fragmented)
            {
                failureReason = PhysicalDeformationFailureReason.OutcomeInvalid;
                return false;
            }

            for (int index = 0; index < parent.CollisionHistory.Count; index++)
            {
                if (string.Equals(
                    parent.CollisionHistory[index].CollisionId,
                    collisionId,
                    StringComparison.Ordinal))
                {
                    failureReason = PhysicalDeformationFailureReason.DuplicateCollisionId;
                    return false;
                }
            }

            PhysicalVector3 incomingDirection;
            if (!parent.VelocityMetresPerSecond.TryNormalize(out incomingDirection))
            {
                failureReason = PhysicalDeformationFailureReason.ImpactDirectionInvalid;
                return false;
            }

            PhysicalVector3 surfaceNormal;
            if (!input.SurfaceNormal.TryNormalize(out surfaceNormal))
            {
                failureReason = PhysicalDeformationFailureReason.SurfaceNormalInvalid;
                return false;
            }

            double normalAlignment = -incomingDirection.Dot(surfaceNormal);
            if (!FiniteDouble.IsFinite(normalAlignment)
                || normalAlignment <= 0d
                || normalAlignment > 1d + RelativeTolerance)
            {
                failureReason = PhysicalDeformationFailureReason.ImpactDirectionInvalid;
                return false;
            }

            normalAlignment = Clamp(normalAlignment, 0d, 1d);
            double impactAngleRadians = Math.Acos(normalAlignment);
            if (!FiniteDouble.IsFinite(impactAngleRadians))
            {
                failureReason = PhysicalDeformationFailureReason.ImpactDirectionInvalid;
                return false;
            }

            bool isMovingOutcome = input.ObservedOutcome != PhysicalCollisionOutcome.Stopped;
            PhysicalVector3 outgoingDirection = PhysicalVector3.Zero;
            if (isMovingOutcome
                && !input.ObservedOutgoingDirection.TryNormalize(out outgoingDirection))
            {
                failureReason = PhysicalDeformationFailureReason.OutgoingDirectionInvalid;
                return false;
            }

            PhysicalVector3 outputPositionMetres = input.ImpactPositionMetres;
            if (isMovingOutcome
                && input.ObservedOutcome != PhysicalCollisionOutcome.Ricocheted)
            {
                outputPositionMetres = input.ImpactPositionMetres.Add(
                    incomingDirection.Scale(input.EffectivePathLengthMetres));
                if (!outputPositionMetres.IsFinite)
                {
                    failureReason = PhysicalDeformationFailureReason.GeometryInvalid;
                    return false;
                }
            }

            double parentEnergyJoules = parent.TranslationalKineticEnergyJoules;
            double projectileVolumeCubicMetres = parent.RetainedMassKilograms
                / projectileProfile.DensityKilogramsPerCubicMetre;
            double sweptVolumeCubicMetres = parent.ProjectedAreaSquareMetres
                * input.EffectivePathLengthMetres;
            double rawTargetWorkJoules = targetProfile.EffectiveResistancePressurePascals
                * sweptVolumeCubicMetres;
            double deformationCapacityJoules = projectileProfile
                .PlasticDeformationWorkJoulesPerCubicMetre
                * projectileVolumeCubicMetres;
            double normalImpactEnergyJoules = parentEnergyJoules
                * normalAlignment
                * normalAlignment;
            double deformationDriveJoules = normalImpactEnergyJoules
                * projectileProfile.DeformationEnergyCoupling
                * targetProfile.ProjectileDeformationCoupling;
            double expansionResponse = PhysicalProjectileDesignResponse.GetExpansionResponse(
                parent.DesignClass);
            double fractureResponse = PhysicalProjectileDesignResponse.GetFractureResponse(
                parent.DesignClass);
            if (!IsFinitePositive(expansionResponse) || !IsFinitePositive(fractureResponse))
            {
                failureReason = PhysicalDeformationFailureReason.WorkCalculationInvalid;
                return false;
            }

            double rawDeformationWorkJoules = Math.Min(
                deformationCapacityJoules,
                deformationDriveJoules);
            double fractureThresholdJoules = parent.RetainedMassKilograms
                * projectileProfile.FractureEnergyJoulesPerKilogram;
            double brittleResponse = 0.05d + (0.95d * projectileProfile.Brittleness);
            double fractureDriveJoules = normalImpactEnergyJoules
                * targetProfile.ProjectileFractureCoupling
                * brittleResponse
                * fractureResponse;
            double fractureProbability = CalculateFractureProbability(
                fractureDriveJoules,
                fractureThresholdJoules);
            double rawFractureWorkJoules = input.ObservedOutcome
                == PhysicalCollisionOutcome.Fragmented
                    ? Math.Min(fractureThresholdJoules, fractureDriveJoules)
                    : 0d;

            if (!AreFiniteNonNegative(
                parentEnergyJoules,
                projectileVolumeCubicMetres,
                sweptVolumeCubicMetres,
                rawTargetWorkJoules,
                deformationCapacityJoules,
                normalImpactEnergyJoules,
                deformationDriveJoules,
                rawDeformationWorkJoules,
                fractureThresholdJoules,
                fractureDriveJoules,
                fractureProbability,
                rawFractureWorkJoules)
                || parentEnergyJoules <= 0d
                || projectileVolumeCubicMetres <= 0d
                || sweptVolumeCubicMetres <= 0d
                || deformationCapacityJoules <= 0d
                || fractureThresholdJoules <= 0d)
            {
                failureReason = PhysicalDeformationFailureReason.WorkCalculationInvalid;
                return false;
            }

            if (input.ObservedOutcome == PhysicalCollisionOutcome.Fragmented
                && (fractureDriveJoules <= 0d || fractureProbability <= 0d))
            {
                failureReason = PhysicalDeformationFailureReason.FragmentationUnsupportedByProfile;
                return false;
            }

            double rawDemandJoules = rawTargetWorkJoules
                + rawDeformationWorkJoules
                + rawFractureWorkJoules;
            if (!FiniteDouble.IsFinite(rawDemandJoules) || rawDemandJoules < 0d)
            {
                failureReason = PhysicalDeformationFailureReason.EnergyDemandInvalid;
                return false;
            }

            double targetWorkJoules = rawTargetWorkJoules;
            double deformationWorkJoules = rawDeformationWorkJoules;
            double fractureWorkJoules = rawFractureWorkJoules;
            double otherLossJoules = 0d;
            double energyTolerance = Math.Max(1d, parentEnergyJoules) * RelativeTolerance;
            if (input.ObservedOutcome == PhysicalCollisionOutcome.Stopped)
            {
                if (rawDemandJoules > parentEnergyJoules && rawDemandJoules > 0d)
                {
                    double demandScale = parentEnergyJoules / rawDemandJoules;
                    targetWorkJoules *= demandScale;
                    deformationWorkJoules *= demandScale;
                    fractureWorkJoules *= demandScale;
                }

                double modeledDemandJoules = targetWorkJoules
                    + deformationWorkJoules
                    + fractureWorkJoules;
                otherLossJoules = Math.Max(0d, parentEnergyJoules - modeledDemandJoules);
            }
            else if (rawDemandJoules >= parentEnergyJoules - energyTolerance)
            {
                failureReason = PhysicalDeformationFailureReason.MovingOutcomeHasNoResidualEnergy;
                return false;
            }

            double heatLossJoules = targetWorkJoules * targetProfile.HeatLossFraction;
            double penetrationLossJoules = targetWorkJoules - heatLossJoules;
            var lossBudget = new PhysicalLossBudget(
                penetrationLossJoules,
                deformationWorkJoules,
                fractureWorkJoules,
                heatLossJoules,
                otherLossJoules);
            PhysicalLossBudgetFailureReason lossFailureReason;
            if (!lossBudget.IsValid(out lossFailureReason)
                || lossBudget.TotalLossJoules > parentEnergyJoules + energyTolerance)
            {
                failureReason = PhysicalDeformationFailureReason.LossBudgetInvalid;
                return false;
            }

            double residualSystemEnergyJoules = Math.Max(
                0d,
                parentEnergyJoules - lossBudget.TotalLossJoules);
            if (isMovingOutcome && residualSystemEnergyJoules <= energyTolerance)
            {
                failureReason = PhysicalDeformationFailureReason.MovingOutcomeHasNoResidualEnergy;
                return false;
            }

            double deformationSeverity = Clamp(
                deformationWorkJoules / deformationCapacityJoules,
                0d,
                1d);
            double diameterExpansionRatio = Math.Min(
                projectileProfile.MaximumDiameterExpansionRatio,
                1d + ((projectileProfile.MaximumDiameterExpansionRatio - 1d)
                    * deformationSeverity
                    * projectileProfile.Ductility
                    * expansionResponse));
            double fragmentMassFraction = 0d;
            if (input.ObservedOutcome == PhysicalCollisionOutcome.Fragmented)
            {
                fragmentMassFraction = projectileProfile.MinimumFragmentMassFraction
                    + ((projectileProfile.MaximumFragmentMassFraction
                        - projectileProfile.MinimumFragmentMassFraction)
                        * fractureProbability);
                fragmentMassFraction = Clamp(fragmentMassFraction, 0d, 1d);
            }

            double availableFragmentMassKilograms = parent.RetainedMassKilograms
                * fragmentMassFraction;
            double retainedPrimaryMassKilograms = parent.RetainedMassKilograms
                - availableFragmentMassKilograms;
            double availableFragmentEnergyJoules = residualSystemEnergyJoules
                * fragmentMassFraction;
            double primaryEnergyJoules = residualSystemEnergyJoules
                - availableFragmentEnergyJoules;
            double residualSpeedMetresPerSecond = isMovingOutcome
                ? Math.Sqrt(
                    (2d * residualSystemEnergyJoules)
                    / parent.RetainedMassKilograms)
                : 0d;

            if (!AreFiniteNonNegative(
                deformationSeverity,
                diameterExpansionRatio,
                fragmentMassFraction,
                availableFragmentMassKilograms,
                retainedPrimaryMassKilograms,
                availableFragmentEnergyJoules,
                primaryEnergyJoules,
                residualSystemEnergyJoules,
                residualSpeedMetresPerSecond)
                || diameterExpansionRatio < 1d)
            {
                failureReason = PhysicalDeformationFailureReason.GeometryInvalid;
                return false;
            }

            PhysicalVector3 collisionOutgoingVelocity = isMovingOutcome
                ? outgoingDirection.Scale(residualSpeedMetresPerSecond)
                : PhysicalVector3.Zero;
            var collisionInput = new PhysicalCollisionRecordInput
            {
                CollisionId = collisionId,
                MaterialId = targetProfile.ProfileId,
                MaterialClass = targetProfile.MaterialClass,
                Sequence = parent.CollisionHistory.Count,
                PositionMetres = input.ImpactPositionMetres,
                IncomingVelocityMetresPerSecond = parent.VelocityMetresPerSecond,
                OutgoingVelocityMetresPerSecond = collisionOutgoingVelocity,
                IncomingTranslationalEnergyJoules = parentEnergyJoules,
                OutgoingTranslationalEnergyJoules = residualSystemEnergyJoules,
                ImpactAngleRadians = impactAngleRadians,
                EffectivePathLengthMetres = input.EffectivePathLengthMetres,
                Outcome = input.ObservedOutcome
            };
            PhysicalCollisionRecord? collisionRecord;
            PhysicalCollisionRecordFailureReason collisionFailureReason;
            if (!PhysicalCollisionRecord.TryCreate(
                collisionInput,
                out collisionRecord,
                out collisionFailureReason)
                || collisionRecord == null)
            {
                failureReason = PhysicalDeformationFailureReason.CollisionRecordInvalid;
                return false;
            }

            PhysicalProjectileState? primaryState = null;
            double massTolerance = Math.Max(
                0.000000000001d,
                parent.RetainedMassKilograms * RelativeTolerance);
            if (retainedPrimaryMassKilograms > massTolerance)
            {
                if (!TryCreatePrimaryState(
                    input,
                    projectileProfile,
                    outputProjectileId,
                    parent,
                    collisionRecord,
                    outgoingDirection,
                    outputPositionMetres,
                    isMovingOutcome,
                    impactAngleRadians,
                    deformationSeverity,
                    diameterExpansionRatio,
                    retainedPrimaryMassKilograms,
                    primaryEnergyJoules,
                    residualSpeedMetresPerSecond,
                    out primaryState))
                {
                    failureReason = PhysicalDeformationFailureReason.PrimaryStateInvalid;
                    return false;
                }
            }

            PhysicalConservationResult conservationResult;
            PhysicalConservationFailureReason conservationFailureReason;
            if (!PhysicalProjectileConservation.TryValidateDeformationResponse(
                parent,
                primaryState,
                collisionRecord,
                availableFragmentMassKilograms,
                availableFragmentEnergyJoules,
                lossBudget,
                out conservationResult,
                out conservationFailureReason))
            {
                failureReason = PhysicalDeformationFailureReason.ConservationValidationFailed;
                return false;
            }

            response = new PhysicalDeformationResponse(
                primaryState,
                collisionRecord,
                lossBudget,
                incomingDirection,
                outgoingDirection,
                outputPositionMetres,
                surfaceNormal,
                input.PhysicalThicknessMetres,
                input.EffectivePathLengthMetres,
                impactAngleRadians,
                normalImpactEnergyJoules,
                sweptVolumeCubicMetres,
                rawTargetWorkJoules,
                targetWorkJoules,
                deformationCapacityJoules,
                deformationSeverity,
                fractureProbability,
                diameterExpansionRatio,
                retainedPrimaryMassKilograms,
                availableFragmentMassKilograms,
                primaryEnergyJoules,
                availableFragmentEnergyJoules,
                residualSystemEnergyJoules,
                residualSpeedMetresPerSecond);
            failureReason = PhysicalDeformationFailureReason.None;
            return true;
        }

        private static bool TryCreatePrimaryState(
            PhysicalDeformationInput input,
            PhysicalProjectileMaterialProfile projectileProfile,
            string outputProjectileId,
            PhysicalProjectileState parent,
            PhysicalCollisionRecord collisionRecord,
            PhysicalVector3 outgoingDirection,
            PhysicalVector3 outputPositionMetres,
            bool isMovingOutcome,
            double impactAngleRadians,
            double deformationSeverity,
            double diameterExpansionRatio,
            double retainedPrimaryMassKilograms,
            double primaryEnergyJoules,
            double residualSpeedMetresPerSecond,
            out PhysicalProjectileState? primaryState)
        {
            primaryState = null;
            double primaryMassRatio = retainedPrimaryMassKilograms
                / parent.RetainedMassKilograms;
            double deformedDiameterMetres = parent.DeformedDiameterMetres
                * diameterExpansionRatio;
            double crossSectionScale = diameterExpansionRatio * diameterExpansionRatio;
            double lengthMetres = parent.LengthMetres
                * primaryMassRatio
                / crossSectionScale;
            double addedYawRadians = projectileProfile.MaximumDeformationYawRadians
                * deformationSeverity
                * (0.25d + (0.75d * Math.Sin(impactAngleRadians)));
            double yawAngleRadians = Clamp(
                parent.YawAngleRadians + addedYawRadians,
                0d,
                Math.PI);
            PhysicalVector3 attitudeDirection = input.ObservedOutcome
                == PhysicalCollisionOutcome.Stopped
                ? parent.VelocityMetresPerSecond
                : outgoingDirection;
            if (!PhysicalOrientation.TryFromForward(
                    attitudeDirection,
                    out PhysicalOrientation attitudeBase))
            {
                return false;
            }

            if (!PhysicalOrientation.TryApplyYaw(
                    attitudeBase,
                    yawAngleRadians,
                    parent.DeterministicSeed,
                    out PhysicalOrientation outputOrientation))
            {
                return false;
            }

            double frontalAreaSquareMetres;
            if (!PhysicalProjectileGeometry.TryCalculateCircularAreaSquareMetres(
                deformedDiameterMetres,
                out frontalAreaSquareMetres))
            {
                return false;
            }

            double sideAreaSquareMetres = deformedDiameterMetres * lengthMetres;
            double projectedAreaSquareMetres = (frontalAreaSquareMetres
                * Math.Abs(Math.Cos(yawAngleRadians)))
                + (sideAreaSquareMetres * Math.Abs(Math.Sin(yawAngleRadians)));
            double yawDragMultiplier = 1d
                + (Math.Sin(yawAngleRadians) * Math.Sin(yawAngleRadians));
            double deformationDragMultiplier = 1d
                + ((projectileProfile.MaximumDragCoefficientMultiplier - 1d)
                    * deformationSeverity);
            double dragCoefficient = parent.DragCoefficient
                * deformationDragMultiplier
                * yawDragMultiplier;
            if (!IsFinitePositive(deformedDiameterMetres)
                || !IsFinitePositive(lengthMetres)
                || !IsFinitePositive(projectedAreaSquareMetres)
                || !IsFinitePositive(dragCoefficient))
            {
                return false;
            }

            PhysicalProjectileShapeClass shapeClass = SelectShapeClass(
                parent,
                deformedDiameterMetres,
                lengthMetres,
                diameterExpansionRatio,
                deformationSeverity);
            PhysicalProjectileTumbleState tumbleState = SelectTumbleState(
                yawAngleRadians,
                projectileProfile);
            double energyRatio = primaryEnergyJoules
                / parent.TranslationalKineticEnergyJoules;
            double shapeRetention = 1d
                - (projectileProfile.MaximumPenetrationShapePenalty
                    * deformationSeverity);
            double yawRetention = Math.Max(0d, Math.Cos(yawAngleRadians));
            double penetrationCapability = parent.PenetrationCapabilityJoulesPerSquareMetre
                * energyRatio
                * (parent.ProjectedAreaSquareMetres / projectedAreaSquareMetres)
                * shapeRetention
                * yawRetention;
            double damageCapability = Math.Min(
                primaryEnergyJoules,
                parent.DamageCapabilityJoules * energyRatio);
            if (!AreFiniteNonNegative(
                primaryMassRatio,
                energyRatio,
                shapeRetention,
                yawRetention,
                penetrationCapability,
                damageCapability))
            {
                return false;
            }

            var history = new List<PhysicalCollisionRecord>(parent.CollisionHistory.Count + 1);
            for (int index = 0; index < parent.CollisionHistory.Count; index++)
            {
                history.Add(parent.CollisionHistory[index]);
            }

            history.Add(collisionRecord);
            PhysicalVector3 velocity = isMovingOutcome
                ? outgoingDirection.Scale(residualSpeedMetresPerSecond)
                : PhysicalVector3.Zero;
            PhysicalVector3 statePositionMetres = outputPositionMetres;
            if (!isMovingOutcome
                && parent.VelocityMetresPerSecond.TryNormalize(
                    out PhysicalVector3 incomingDirection))
            {
                double embeddedCenterDepthMetres = Math.Min(
                    lengthMetres * 0.5d,
                    input.EffectivePathLengthMetres);
                statePositionMetres = input.ImpactPositionMetres.Add(
                    incomingDirection.Scale(embeddedCenterDepthMetres));
            }

            var stateInput = new PhysicalProjectileStateInput
            {
                Kind = SelectProjectileKind(parent, deformationSeverity),
                ProjectileId = outputProjectileId,
                RootShotId = parent.RootShotId,
                ParentProjectileId = parent.ParentProjectileId,
                SourceProjectileId = parent.SourceProjectileId,
                SourceMaterialId = parent.SourceMaterialId,
                SourceMaterialClass = parent.SourceMaterialClass,
                SourceCollisionId = parent.SourceCollisionId,
                FragmentIndex = parent.FragmentIndex,
                FragmentGeneration = parent.FragmentGeneration,
                DeterministicSeed = parent.DeterministicSeed,
                Construction = parent.Construction,
                DesignClass = parent.DesignClass,
                ShapeClass = shapeClass,
                OriginalMassKilograms = parent.OriginalMassKilograms,
                RetainedMassKilograms = retainedPrimaryMassKilograms,
                NominalDiameterMetres = parent.NominalDiameterMetres,
                DeformedDiameterMetres = deformedDiameterMetres,
                ProjectedAreaSquareMetres = projectedAreaSquareMetres,
                LengthMetres = lengthMetres,
                DragCoefficient = dragCoefficient,
                PositionMetres = statePositionMetres,
                VelocityMetresPerSecond = velocity,
                Orientation = outputOrientation,
                YawAngleRadians = yawAngleRadians,
                TumbleState = tumbleState,
                PenetrationCapabilityJoulesPerSquareMetre = penetrationCapability,
                DamageCapabilityJoules = damageCapability,
                TerminalState = SelectTerminalState(input.ObservedOutcome),
                RenderState = isMovingOutcome
                    ? PhysicalProjectileRenderState.NotRendered
                    : PhysicalProjectileRenderState.Embedded,
                CollisionHistory = history
            };
            PhysicalProjectileStateFailureReason stateFailureReason;
            return PhysicalProjectileState.TryCreate(
                stateInput,
                out primaryState,
                out stateFailureReason);
        }

        private static double CalculateFractureProbability(
            double fractureDriveJoules,
            double fractureThresholdJoules)
        {
            if (fractureDriveJoules <= 0d || fractureThresholdJoules <= 0d)
            {
                return 0d;
            }

            double ratio = fractureDriveJoules / fractureThresholdJoules;
            if (double.IsPositiveInfinity(ratio) || ratio >= 50d)
            {
                return 1d;
            }

            if (!FiniteDouble.IsFinite(ratio) || ratio <= 0d)
            {
                return 0d;
            }

            return Clamp(1d - Math.Exp(-ratio), 0d, 1d);
        }

        private static PhysicalProjectileKind SelectProjectileKind(
            PhysicalProjectileState parent,
            double deformationSeverity)
        {
            if (parent.Construction == PhysicalProjectileConstruction.TargetMaterial)
            {
                return PhysicalProjectileKind.TargetSpallFragment;
            }

            if (parent.Kind == PhysicalProjectileKind.ProjectileFragment)
            {
                return PhysicalProjectileKind.ProjectileFragment;
            }

            if (parent.Kind == PhysicalProjectileKind.DeformedProjectile
                || deformationSeverity > RelativeTolerance)
            {
                return PhysicalProjectileKind.DeformedProjectile;
            }

            return PhysicalProjectileKind.IntactProjectile;
        }

        private static PhysicalProjectileShapeClass SelectShapeClass(
            PhysicalProjectileState parent,
            double deformedDiameterMetres,
            double lengthMetres,
            double diameterExpansionRatio,
            double deformationSeverity)
        {
            if (parent.Construction == PhysicalProjectileConstruction.TargetMaterial)
            {
                return lengthMetres / deformedDiameterMetres < 0.75d
                    ? PhysicalProjectileShapeClass.TargetSpallFlake
                    : PhysicalProjectileShapeClass.TargetSpallChunk;
            }

            if (lengthMetres / deformedDiameterMetres < 0.75d)
            {
                return PhysicalProjectileShapeClass.FlattenedDisc;
            }

            if (parent.Kind == PhysicalProjectileKind.ProjectileFragment)
            {
                return PhysicalProjectileShapeClass.IrregularProjectileFragment;
            }

            if (diameterExpansionRatio > 1.02d)
            {
                return PhysicalProjectileShapeClass.ExpandedMushroom;
            }

            return parent.ShapeClass;
        }

        private static PhysicalProjectileTumbleState SelectTumbleState(
            double yawAngleRadians,
            PhysicalProjectileMaterialProfile profile)
        {
            if (yawAngleRadians >= profile.TumblingThresholdRadians)
            {
                return PhysicalProjectileTumbleState.Tumbling;
            }

            if (yawAngleRadians >= profile.YawingThresholdRadians)
            {
                return PhysicalProjectileTumbleState.Yawing;
            }

            return PhysicalProjectileTumbleState.Stable;
        }

        private static PhysicalProjectileTerminalState SelectTerminalState(
            PhysicalCollisionOutcome outcome)
        {
            if (outcome == PhysicalCollisionOutcome.Stopped)
            {
                return PhysicalProjectileTerminalState.Stopped;
            }

            if (outcome == PhysicalCollisionOutcome.Ricocheted)
            {
                return PhysicalProjectileTerminalState.Continuing;
            }

            return PhysicalProjectileTerminalState.Exited;
        }

        private static double Clamp(double value, double minimum, double maximum)
        {
            return Math.Max(minimum, Math.Min(maximum, value));
        }

        private static bool IsFinitePositive(double value)
        {
            return FiniteDouble.IsFinite(value) && value > 0d;
        }

        private static bool AreFiniteNonNegative(params double[] values)
        {
            for (int index = 0; index < values.Length; index++)
            {
                if (!FiniteDouble.IsFinite(values[index]) || values[index] < 0d)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
