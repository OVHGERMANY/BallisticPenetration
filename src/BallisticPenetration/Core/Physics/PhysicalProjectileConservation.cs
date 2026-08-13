#nullable enable

using System;
using System.Collections.Generic;
using BallisticPenetration.Core;

namespace BallisticPenetration.Core.Physics
{
    public enum PhysicalLossBudgetFailureReason
    {
        None = 0,
        PenetrationLossInvalid = 1,
        DeformationLossInvalid = 2,
        FractureLossInvalid = 3,
        HeatLossInvalid = 4,
        OtherLossInvalid = 5,
        TotalLossInvalid = 6
    }

    public readonly struct PhysicalLossBudget : IEquatable<PhysicalLossBudget>
    {
        public PhysicalLossBudget(
            double penetrationLossJoules,
            double deformationLossJoules,
            double fractureLossJoules,
            double heatLossJoules,
            double otherLossJoules)
        {
            PenetrationLossJoules = penetrationLossJoules;
            DeformationLossJoules = deformationLossJoules;
            FractureLossJoules = fractureLossJoules;
            HeatLossJoules = heatLossJoules;
            OtherLossJoules = otherLossJoules;
        }

        public double PenetrationLossJoules { get; }

        public double DeformationLossJoules { get; }

        public double FractureLossJoules { get; }

        public double HeatLossJoules { get; }

        public double OtherLossJoules { get; }

        public double TotalLossJoules
        {
            get
            {
                return PenetrationLossJoules
                    + DeformationLossJoules
                    + FractureLossJoules
                    + HeatLossJoules
                    + OtherLossJoules;
            }
        }

        public bool IsValid(out PhysicalLossBudgetFailureReason failureReason)
        {
            if (!IsFiniteNonNegative(PenetrationLossJoules))
            {
                failureReason = PhysicalLossBudgetFailureReason.PenetrationLossInvalid;
                return false;
            }

            if (!IsFiniteNonNegative(DeformationLossJoules))
            {
                failureReason = PhysicalLossBudgetFailureReason.DeformationLossInvalid;
                return false;
            }

            if (!IsFiniteNonNegative(FractureLossJoules))
            {
                failureReason = PhysicalLossBudgetFailureReason.FractureLossInvalid;
                return false;
            }

            if (!IsFiniteNonNegative(HeatLossJoules))
            {
                failureReason = PhysicalLossBudgetFailureReason.HeatLossInvalid;
                return false;
            }

            if (!IsFiniteNonNegative(OtherLossJoules))
            {
                failureReason = PhysicalLossBudgetFailureReason.OtherLossInvalid;
                return false;
            }

            if (!IsFiniteNonNegative(TotalLossJoules))
            {
                failureReason = PhysicalLossBudgetFailureReason.TotalLossInvalid;
                return false;
            }

            failureReason = PhysicalLossBudgetFailureReason.None;
            return true;
        }

        public bool Equals(PhysicalLossBudget other)
        {
            return PenetrationLossJoules.Equals(other.PenetrationLossJoules)
                && DeformationLossJoules.Equals(other.DeformationLossJoules)
                && FractureLossJoules.Equals(other.FractureLossJoules)
                && HeatLossJoules.Equals(other.HeatLossJoules)
                && OtherLossJoules.Equals(other.OtherLossJoules);
        }

        public override bool Equals(object? obj)
        {
            return obj is PhysicalLossBudget other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = PenetrationLossJoules.GetHashCode();
                hash = (hash * 397) ^ DeformationLossJoules.GetHashCode();
                hash = (hash * 397) ^ FractureLossJoules.GetHashCode();
                hash = (hash * 397) ^ HeatLossJoules.GetHashCode();
                hash = (hash * 397) ^ OtherLossJoules.GetHashCode();
                return hash;
            }
        }

        public static bool operator ==(PhysicalLossBudget left, PhysicalLossBudget right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(PhysicalLossBudget left, PhysicalLossBudget right)
        {
            return !left.Equals(right);
        }

        private static bool IsFiniteNonNegative(double value)
        {
            return FiniteDouble.IsFinite(value) && value >= 0d;
        }
    }

    public enum PhysicalConservationFailureReason
    {
        None = 0,
        ParentMissing = 1,
        OutputsMissing = 2,
        LossBudgetInvalid = 3,
        LossesExceedParentEnergy = 4,
        ChildMissing = 5,
        DuplicateChildIdentity = 6,
        DuplicateFragmentIndex = 7,
        ParentIdentityMismatch = 8,
        SourceIdentityMismatch = 9,
        RootIdentityMismatch = 10,
        GenerationMismatch = 11,
        ParentDerivedMassExceedsParent = 12,
        RetainedParentDerivedMassExceedsParent = 13,
        ChildEnergyExceedsResidual = 14,
        ParentFragmentMissing = 15,
        DerivedTotalInvalid = 16,
        SourceCollisionMismatch = 17,
        FragmentMassReservationInvalid = 18,
        FragmentEnergyReservationInvalid = 19,
        StateRevisionIdentityMismatch = 20,
        StateRevisionLineageMismatch = 21,
        StateRevisionHistoryMismatch = 22,
        StateRevisionOriginalMassMismatch = 23,
        ResponseMassNotClosed = 24,
        ResponseEnergyNotClosed = 25,
        StateRevisionKindMismatch = 26,
        StateRevisionNominalGeometryMismatch = 27,
        StateRevisionCollisionMismatch = 28,
        ResponseCollisionMissing = 29,
        FragmentReservationOutcomeMismatch = 30,
        StateRevisionTerminalStateMismatch = 31,
        FragmentationOutputKindInvalid = 32,
        ParentFragmentMassNotClosed = 33,
        ParentFragmentEnergyNotClosed = 34,
        TargetSpallEnergyNotClosed = 35,
        FragmentationEnergyReclassificationInvalid = 36,
        FragmentationResponseMassNotClosed = 37,
        FragmentationResponseEnergyNotClosed = 38,
        FragmentationHistoryMismatch = 39,
        TargetSpallMassNotClosed = 40
    }

    public readonly struct PhysicalConservationResult : IEquatable<PhysicalConservationResult>
    {
        internal PhysicalConservationResult(
            double availableParentMassKilograms,
            double allocatedParentMassKilograms,
            double retainedParentMassKilograms,
            double targetSpallMassKilograms,
            double parentEnergyJoules,
            double modeledLossEnergyJoules,
            double residualEnergyJoules,
            double childEnergyJoules,
            int parentDerivedOutputCount,
            int targetSpallOutputCount)
        {
            AvailableParentMassKilograms = availableParentMassKilograms;
            AllocatedParentMassKilograms = allocatedParentMassKilograms;
            RetainedParentMassKilograms = retainedParentMassKilograms;
            TargetSpallMassKilograms = targetSpallMassKilograms;
            ParentEnergyJoules = parentEnergyJoules;
            ModeledLossEnergyJoules = modeledLossEnergyJoules;
            ResidualEnergyJoules = residualEnergyJoules;
            ChildEnergyJoules = childEnergyJoules;
            ParentDerivedOutputCount = parentDerivedOutputCount;
            TargetSpallOutputCount = targetSpallOutputCount;
        }

        public double AvailableParentMassKilograms { get; }

        public double AllocatedParentMassKilograms { get; }

        public double RetainedParentMassKilograms { get; }

        /// <summary>
        /// Fresh target material mass created at this collision. It is reported separately and
        /// never consumes mass retained by the immediate parent component.
        /// </summary>
        public double TargetSpallMassKilograms { get; }

        public double ParentEnergyJoules { get; }

        public double ModeledLossEnergyJoules { get; }

        public double ResidualEnergyJoules { get; }

        public double ChildEnergyJoules { get; }

        public int ParentDerivedOutputCount { get; }

        public int TargetSpallOutputCount { get; }

        public double UnallocatedParentMassKilograms
        {
            get { return AvailableParentMassKilograms - AllocatedParentMassKilograms; }
        }

        public double UnallocatedResidualEnergyJoules
        {
            get { return ResidualEnergyJoules - ChildEnergyJoules; }
        }

        public bool Equals(PhysicalConservationResult other)
        {
            return AvailableParentMassKilograms.Equals(other.AvailableParentMassKilograms)
                && AllocatedParentMassKilograms.Equals(other.AllocatedParentMassKilograms)
                && RetainedParentMassKilograms.Equals(other.RetainedParentMassKilograms)
                && TargetSpallMassKilograms.Equals(other.TargetSpallMassKilograms)
                && ParentEnergyJoules.Equals(other.ParentEnergyJoules)
                && ModeledLossEnergyJoules.Equals(other.ModeledLossEnergyJoules)
                && ResidualEnergyJoules.Equals(other.ResidualEnergyJoules)
                && ChildEnergyJoules.Equals(other.ChildEnergyJoules)
                && ParentDerivedOutputCount == other.ParentDerivedOutputCount
                && TargetSpallOutputCount == other.TargetSpallOutputCount;
        }

        public override bool Equals(object? obj)
        {
            return obj is PhysicalConservationResult other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = AvailableParentMassKilograms.GetHashCode();
                hash = (hash * 397) ^ AllocatedParentMassKilograms.GetHashCode();
                hash = (hash * 397) ^ RetainedParentMassKilograms.GetHashCode();
                hash = (hash * 397) ^ TargetSpallMassKilograms.GetHashCode();
                hash = (hash * 397) ^ ParentEnergyJoules.GetHashCode();
                hash = (hash * 397) ^ ModeledLossEnergyJoules.GetHashCode();
                hash = (hash * 397) ^ ResidualEnergyJoules.GetHashCode();
                hash = (hash * 397) ^ ChildEnergyJoules.GetHashCode();
                hash = (hash * 397) ^ ParentDerivedOutputCount;
                hash = (hash * 397) ^ TargetSpallOutputCount;
                return hash;
            }
        }

        public static bool operator ==(
            PhysicalConservationResult left,
            PhysicalConservationResult right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(
            PhysicalConservationResult left,
            PhysicalConservationResult right)
        {
            return !left.Equals(right);
        }
    }

    public static class PhysicalProjectileConservation
    {
        private const double RelativeTolerance = 0.000000001d;

        /// <summary>
        /// Validates a new immutable state of the same physical component plus mass and energy
        /// reserved for fragment children that have not been constructed yet.
        /// </summary>
        public static bool TryValidateDeformationResponse(
            PhysicalProjectileState? parent,
            PhysicalProjectileState? primaryRevision,
            PhysicalCollisionRecord? responseCollision,
            double reservedFragmentMassKilograms,
            double reservedFragmentEnergyJoules,
            PhysicalLossBudget lossBudget,
            out PhysicalConservationResult result,
            out PhysicalConservationFailureReason failureReason)
        {
            result = default;
            if (parent == null)
            {
                failureReason = PhysicalConservationFailureReason.ParentMissing;
                return false;
            }

            if (responseCollision == null)
            {
                failureReason = PhysicalConservationFailureReason.ResponseCollisionMissing;
                return false;
            }

            PhysicalLossBudgetFailureReason lossFailureReason;
            if (!lossBudget.IsValid(out lossFailureReason))
            {
                failureReason = PhysicalConservationFailureReason.LossBudgetInvalid;
                return false;
            }

            if (!IsFiniteNonNegative(reservedFragmentMassKilograms))
            {
                failureReason = PhysicalConservationFailureReason.FragmentMassReservationInvalid;
                return false;
            }

            if (!IsFiniteNonNegative(reservedFragmentEnergyJoules))
            {
                failureReason = PhysicalConservationFailureReason.FragmentEnergyReservationInvalid;
                return false;
            }

            double parentEnergyJoules = parent.TranslationalKineticEnergyJoules;
            double energyTolerance = Math.Max(1d, parentEnergyJoules) * RelativeTolerance;
            double massTolerance = Math.Max(
                0.000000001d,
                parent.RetainedMassKilograms * RelativeTolerance);
            if (lossBudget.TotalLossJoules > parentEnergyJoules + energyTolerance)
            {
                failureReason = PhysicalConservationFailureReason.LossesExceedParentEnergy;
                return false;
            }

            double residualEnergyJoules = Math.Max(
                0d,
                parentEnergyJoules - lossBudget.TotalLossJoules);
            double recordedOutgoingSpeedMetresPerSecond =
                responseCollision.OutgoingVelocityMetresPerSecond.Magnitude;
            double recordedOutgoingEnergyFromVelocityJoules = 0.5d
                * parent.RetainedMassKilograms
                * recordedOutgoingSpeedMetresPerSecond
                * recordedOutgoingSpeedMetresPerSecond;
            if (responseCollision.Sequence != parent.CollisionHistory.Count
                || responseCollision.IncomingVelocityMetresPerSecond
                    != parent.VelocityMetresPerSecond
                || !responseCollision.IncomingTranslationalEnergyJoules.Equals(
                    parentEnergyJoules)
                || Math.Abs(
                    responseCollision.OutgoingTranslationalEnergyJoules
                        - residualEnergyJoules) > energyTolerance
                || !FiniteDouble.IsFinite(recordedOutgoingEnergyFromVelocityJoules)
                || Math.Abs(recordedOutgoingEnergyFromVelocityJoules - residualEnergyJoules)
                    > energyTolerance)
            {
                failureReason = PhysicalConservationFailureReason.StateRevisionCollisionMismatch;
                return false;
            }

            bool fragmentedOutcome = responseCollision.Outcome
                == PhysicalCollisionOutcome.Fragmented;
            bool hasFragmentMassReservation = reservedFragmentMassKilograms > massTolerance;
            bool hasFragmentEnergyReservation = reservedFragmentEnergyJoules > energyTolerance;
            if (fragmentedOutcome != hasFragmentMassReservation
                || fragmentedOutcome != hasFragmentEnergyReservation)
            {
                failureReason = PhysicalConservationFailureReason.FragmentReservationOutcomeMismatch;
                return false;
            }

            double primaryMassKilograms = 0d;
            double primaryEnergyJoules = 0d;
            int primaryOutputCount = 0;
            if (primaryRevision != null)
            {
                if (!string.Equals(
                    primaryRevision.ProjectileId,
                    parent.ProjectileId,
                    StringComparison.Ordinal))
                {
                    failureReason = PhysicalConservationFailureReason.StateRevisionIdentityMismatch;
                    return false;
                }

                if (!HasSameLineage(parent, primaryRevision))
                {
                    failureReason = PhysicalConservationFailureReason.StateRevisionLineageMismatch;
                    return false;
                }

                if (!IsValidStateRevisionKind(parent.Kind, primaryRevision.Kind))
                {
                    failureReason = PhysicalConservationFailureReason.StateRevisionKindMismatch;
                    return false;
                }

                if (!parent.OriginalMassKilograms.Equals(primaryRevision.OriginalMassKilograms))
                {
                    failureReason = PhysicalConservationFailureReason.StateRevisionOriginalMassMismatch;
                    return false;
                }

                if (!parent.NominalDiameterMetres.Equals(primaryRevision.NominalDiameterMetres))
                {
                    failureReason = PhysicalConservationFailureReason.StateRevisionNominalGeometryMismatch;
                    return false;
                }

                if (!HasAppendedCollisionHistory(parent, primaryRevision))
                {
                    failureReason = PhysicalConservationFailureReason.StateRevisionHistoryMismatch;
                    return false;
                }

                PhysicalCollisionRecord appendedCollision = primaryRevision.CollisionHistory[
                    primaryRevision.CollisionHistory.Count - 1];
                if (appendedCollision != responseCollision)
                {
                    failureReason = PhysicalConservationFailureReason.StateRevisionCollisionMismatch;
                    return false;
                }

                if (primaryRevision.TerminalState
                    != SelectTerminalState(responseCollision.Outcome))
                {
                    failureReason = PhysicalConservationFailureReason.StateRevisionTerminalStateMismatch;
                    return false;
                }

                primaryMassKilograms = primaryRevision.RetainedMassKilograms;
                primaryEnergyJoules = primaryRevision.TranslationalKineticEnergyJoules;
                primaryOutputCount = 1;
            }

            double allocatedMassKilograms = primaryMassKilograms
                + reservedFragmentMassKilograms;
            double allocatedEnergyJoules = primaryEnergyJoules
                + reservedFragmentEnergyJoules;
            if (!AreFiniteNonNegative(
                residualEnergyJoules,
                allocatedMassKilograms,
                allocatedEnergyJoules))
            {
                failureReason = PhysicalConservationFailureReason.DerivedTotalInvalid;
                return false;
            }

            if (Math.Abs(allocatedMassKilograms - parent.RetainedMassKilograms)
                > massTolerance)
            {
                failureReason = PhysicalConservationFailureReason.ResponseMassNotClosed;
                return false;
            }

            if (Math.Abs(allocatedEnergyJoules - residualEnergyJoules) > energyTolerance)
            {
                failureReason = PhysicalConservationFailureReason.ResponseEnergyNotClosed;
                return false;
            }

            result = new PhysicalConservationResult(
                parent.RetainedMassKilograms,
                allocatedMassKilograms,
                allocatedMassKilograms,
                0d,
                parentEnergyJoules,
                lossBudget.TotalLossJoules,
                residualEnergyJoules,
                allocatedEnergyJoules,
                primaryOutputCount,
                0);
            failureReason = PhysicalConservationFailureReason.None;
            return true;
        }

        public static bool TryValidateTransition(
            PhysicalProjectileState? parent,
            IReadOnlyList<PhysicalProjectileState?>? outputs,
            PhysicalLossBudget lossBudget,
            out PhysicalConservationResult result,
            out PhysicalConservationFailureReason failureReason)
        {
            return TryValidate(
                parent,
                outputs,
                lossBudget,
                false,
                out result,
                out failureReason);
        }

        /// <summary>
        /// Validates the completed second stage of a deformation response. The primary revision
        /// retains its identity, projectile fragments consume only the reserved projectile mass and
        /// energy, and target spall consumes separately reported target mass plus energy reclassified
        /// from penetration work.
        /// </summary>
        public static bool TryValidateFragmentationResolution(
            PhysicalProjectileState? parent,
            PhysicalProjectileState? primaryRevision,
            PhysicalCollisionRecord? responseCollision,
            IReadOnlyList<PhysicalProjectileState?>? secondaryOutputs,
            double reservedParentFragmentMassKilograms,
            double reservedParentFragmentEnergyJoules,
            double reservedTargetSpallMassKilograms,
            double reservedTargetSpallEnergyJoules,
            PhysicalLossBudget originalLossBudget,
            PhysicalLossBudget effectiveLossBudget,
            out PhysicalConservationResult result,
            out PhysicalConservationFailureReason failureReason)
        {
            result = default;
            if (!TryValidateDeformationResponse(
                parent,
                primaryRevision,
                responseCollision,
                reservedParentFragmentMassKilograms,
                reservedParentFragmentEnergyJoules,
                originalLossBudget,
                out _,
                out failureReason))
            {
                return false;
            }

            if (parent == null || responseCollision == null)
            {
                failureReason = parent == null
                    ? PhysicalConservationFailureReason.ParentMissing
                    : PhysicalConservationFailureReason.ResponseCollisionMissing;
                return false;
            }

            if (secondaryOutputs == null)
            {
                failureReason = PhysicalConservationFailureReason.OutputsMissing;
                return false;
            }

            PhysicalLossBudgetFailureReason lossFailureReason;
            if (!effectiveLossBudget.IsValid(out lossFailureReason)
                || !IsFiniteNonNegative(reservedTargetSpallMassKilograms)
                || !IsFiniteNonNegative(reservedTargetSpallEnergyJoules))
            {
                failureReason = PhysicalConservationFailureReason.LossBudgetInvalid;
                return false;
            }

            double parentEnergyJoules = parent.TranslationalKineticEnergyJoules;
            double energyTolerance = Math.Max(1d, parentEnergyJoules) * RelativeTolerance;
            double massTolerance = Math.Max(
                0.000000000001d,
                parent.RetainedMassKilograms * RelativeTolerance);
            double reclassifiedEnergyJoules = originalLossBudget.TotalLossJoules
                - effectiveLossBudget.TotalLossJoules;
            if (Math.Abs(reclassifiedEnergyJoules - reservedTargetSpallEnergyJoules)
                    > energyTolerance
                || Math.Abs(
                    originalLossBudget.PenetrationLossJoules
                        - effectiveLossBudget.PenetrationLossJoules
                        - reservedTargetSpallEnergyJoules) > energyTolerance
                || !originalLossBudget.DeformationLossJoules.Equals(
                    effectiveLossBudget.DeformationLossJoules)
                || !originalLossBudget.FractureLossJoules.Equals(
                    effectiveLossBudget.FractureLossJoules)
                || !originalLossBudget.HeatLossJoules.Equals(
                    effectiveLossBudget.HeatLossJoules)
                || !originalLossBudget.OtherLossJoules.Equals(
                    effectiveLossBudget.OtherLossJoules))
            {
                failureReason = PhysicalConservationFailureReason.FragmentationEnergyReclassificationInvalid;
                return false;
            }

            double parentFragmentMassKilograms = 0d;
            double parentFragmentRetainedMassKilograms = 0d;
            double parentFragmentEnergyJoules = 0d;
            double targetSpallOriginalMassKilograms = 0d;
            double targetSpallMassKilograms = 0d;
            double targetSpallEnergyJoules = 0d;
            int parentFragmentCount = 0;
            int targetSpallCount = 0;
            var childIds = new HashSet<string>(StringComparer.Ordinal);
            var fragmentIndices = new HashSet<int>();
            if (parent.FragmentGeneration == int.MaxValue)
            {
                failureReason = PhysicalConservationFailureReason.GenerationMismatch;
                return false;
            }

            for (int index = 0; index < secondaryOutputs.Count; index++)
            {
                PhysicalProjectileState? child = secondaryOutputs[index];
                if (child == null)
                {
                    failureReason = PhysicalConservationFailureReason.ChildMissing;
                    return false;
                }

                if (child.Kind != PhysicalProjectileKind.ProjectileFragment
                    && child.Kind != PhysicalProjectileKind.TargetSpallFragment
                    && child.Kind != PhysicalProjectileKind.TargetSpall)
                {
                    failureReason = PhysicalConservationFailureReason.FragmentationOutputKindInvalid;
                    return false;
                }

                if (!childIds.Add(child.ProjectileId))
                {
                    failureReason = PhysicalConservationFailureReason.DuplicateChildIdentity;
                    return false;
                }

                if (!fragmentIndices.Add(child.FragmentIndex))
                {
                    failureReason = PhysicalConservationFailureReason.DuplicateFragmentIndex;
                    return false;
                }

                if (!string.Equals(
                    child.ParentProjectileId,
                    parent.ProjectileId,
                    StringComparison.Ordinal))
                {
                    failureReason = PhysicalConservationFailureReason.ParentIdentityMismatch;
                    return false;
                }

                if (!string.Equals(
                    child.SourceProjectileId,
                    parent.ProjectileId,
                    StringComparison.Ordinal))
                {
                    failureReason = PhysicalConservationFailureReason.SourceIdentityMismatch;
                    return false;
                }

                if (!string.Equals(
                    child.RootShotId,
                    parent.RootShotId,
                    StringComparison.Ordinal))
                {
                    failureReason = PhysicalConservationFailureReason.RootIdentityMismatch;
                    return false;
                }

                if (child.FragmentGeneration != parent.FragmentGeneration + 1)
                {
                    failureReason = PhysicalConservationFailureReason.GenerationMismatch;
                    return false;
                }

                if (!string.Equals(
                    child.SourceCollisionId,
                    responseCollision.CollisionId,
                    StringComparison.Ordinal))
                {
                    failureReason = PhysicalConservationFailureReason.SourceCollisionMismatch;
                    return false;
                }

                if (!HasInheritedCollisionHistory(parent, child, responseCollision))
                {
                    failureReason = PhysicalConservationFailureReason.FragmentationHistoryMismatch;
                    return false;
                }

                if (child.Kind == PhysicalProjectileKind.ProjectileFragment
                    || child.Kind == PhysicalProjectileKind.TargetSpallFragment)
                {
                    parentFragmentCount++;
                    parentFragmentMassKilograms += child.OriginalMassKilograms;
                    parentFragmentRetainedMassKilograms += child.RetainedMassKilograms;
                    parentFragmentEnergyJoules += child.TranslationalKineticEnergyJoules;
                }
                else
                {
                    targetSpallCount++;
                    targetSpallOriginalMassKilograms += child.OriginalMassKilograms;
                    targetSpallMassKilograms += child.RetainedMassKilograms;
                    targetSpallEnergyJoules += child.TranslationalKineticEnergyJoules;
                }
            }

            bool parentFragmentsRequired =
                reservedParentFragmentMassKilograms > massTolerance
                || reservedParentFragmentEnergyJoules > energyTolerance;
            if (parentFragmentsRequired && parentFragmentCount == 0)
            {
                failureReason = PhysicalConservationFailureReason.ParentFragmentMissing;
                return false;
            }

            if (!AreFiniteNonNegative(
                parentFragmentMassKilograms,
                parentFragmentRetainedMassKilograms,
                parentFragmentEnergyJoules,
                targetSpallOriginalMassKilograms,
                targetSpallMassKilograms,
                targetSpallEnergyJoules))
            {
                failureReason = PhysicalConservationFailureReason.DerivedTotalInvalid;
                return false;
            }

            if (Math.Abs(
                parentFragmentMassKilograms
                    - reservedParentFragmentMassKilograms) > massTolerance
                || Math.Abs(
                    parentFragmentRetainedMassKilograms
                        - reservedParentFragmentMassKilograms) > massTolerance)
            {
                failureReason = PhysicalConservationFailureReason.ParentFragmentMassNotClosed;
                return false;
            }

            if (Math.Abs(
                parentFragmentEnergyJoules
                    - reservedParentFragmentEnergyJoules) > energyTolerance)
            {
                failureReason = PhysicalConservationFailureReason.ParentFragmentEnergyNotClosed;
                return false;
            }

            if (Math.Abs(targetSpallEnergyJoules - reservedTargetSpallEnergyJoules)
                > energyTolerance)
            {
                failureReason = PhysicalConservationFailureReason.TargetSpallEnergyNotClosed;
                return false;
            }

            if (Math.Abs(
                targetSpallOriginalMassKilograms - reservedTargetSpallMassKilograms)
                    > massTolerance
                || Math.Abs(targetSpallMassKilograms - reservedTargetSpallMassKilograms)
                    > massTolerance)
            {
                failureReason = PhysicalConservationFailureReason.TargetSpallMassNotClosed;
                return false;
            }

            double primaryMassKilograms = primaryRevision?.RetainedMassKilograms ?? 0d;
            double primaryEnergyJoules = primaryRevision?.TranslationalKineticEnergyJoules ?? 0d;
            double allocatedParentMassKilograms = primaryMassKilograms
                + parentFragmentMassKilograms;
            if (Math.Abs(
                allocatedParentMassKilograms - parent.RetainedMassKilograms)
                > massTolerance)
            {
                failureReason = PhysicalConservationFailureReason.FragmentationResponseMassNotClosed;
                return false;
            }

            double allComponentEnergyJoules = primaryEnergyJoules
                + parentFragmentEnergyJoules
                + targetSpallEnergyJoules;
            double effectiveResidualEnergyJoules = Math.Max(
                0d,
                parentEnergyJoules - effectiveLossBudget.TotalLossJoules);
            if (Math.Abs(allComponentEnergyJoules - effectiveResidualEnergyJoules)
                    > energyTolerance
                || Math.Abs(
                    parentEnergyJoules
                        - effectiveLossBudget.TotalLossJoules
                        - allComponentEnergyJoules) > energyTolerance)
            {
                failureReason = PhysicalConservationFailureReason.FragmentationResponseEnergyNotClosed;
                return false;
            }

            result = new PhysicalConservationResult(
                parent.RetainedMassKilograms,
                allocatedParentMassKilograms,
                allocatedParentMassKilograms,
                targetSpallMassKilograms,
                parentEnergyJoules,
                effectiveLossBudget.TotalLossJoules,
                effectiveResidualEnergyJoules,
                allComponentEnergyJoules,
                parentFragmentCount + (primaryRevision == null ? 0 : 1),
                targetSpallCount);
            failureReason = PhysicalConservationFailureReason.None;
            return true;
        }

        public static bool TryValidateFragmentationTransition(
            PhysicalProjectileState? parent,
            IReadOnlyList<PhysicalProjectileState?>? outputs,
            PhysicalLossBudget lossBudget,
            out PhysicalConservationResult result,
            out PhysicalConservationFailureReason failureReason)
        {
            return TryValidate(
                parent,
                outputs,
                lossBudget,
                true,
                out result,
                out failureReason);
        }

        private static bool TryValidate(
            PhysicalProjectileState? parent,
            IReadOnlyList<PhysicalProjectileState?>? outputs,
            PhysicalLossBudget lossBudget,
            bool requireParentFragment,
            out PhysicalConservationResult result,
            out PhysicalConservationFailureReason failureReason)
        {
            result = default;
            if (parent == null)
            {
                failureReason = PhysicalConservationFailureReason.ParentMissing;
                return false;
            }

            if (outputs == null)
            {
                failureReason = PhysicalConservationFailureReason.OutputsMissing;
                return false;
            }

            PhysicalLossBudgetFailureReason lossFailureReason;
            if (!lossBudget.IsValid(out lossFailureReason))
            {
                failureReason = PhysicalConservationFailureReason.LossBudgetInvalid;
                return false;
            }

            double parentEnergyJoules = parent.TranslationalKineticEnergyJoules;
            double energyTolerance = Math.Max(1d, parentEnergyJoules) * RelativeTolerance;
            if (lossBudget.TotalLossJoules > parentEnergyJoules + energyTolerance)
            {
                failureReason = PhysicalConservationFailureReason.LossesExceedParentEnergy;
                return false;
            }

            double residualEnergyJoules = Math.Max(0d, parentEnergyJoules - lossBudget.TotalLossJoules);
            double allocatedParentMassKilograms = 0d;
            double retainedParentMassKilograms = 0d;
            double targetSpallMassKilograms = 0d;
            double childEnergyJoules = 0d;
            int parentDerivedOutputCount = 0;
            int targetSpallOutputCount = 0;
            bool hasParentFragment = false;
            string? sourceCollisionId = null;
            var childIds = new HashSet<string>(StringComparer.Ordinal);
            var fragmentIndices = new HashSet<int>();

            for (int index = 0; index < outputs.Count; index++)
            {
                PhysicalProjectileState? child = outputs[index];
                if (child == null)
                {
                    failureReason = PhysicalConservationFailureReason.ChildMissing;
                    return false;
                }

                if (!childIds.Add(child.ProjectileId))
                {
                    failureReason = PhysicalConservationFailureReason.DuplicateChildIdentity;
                    return false;
                }

                if (!fragmentIndices.Add(child.FragmentIndex))
                {
                    failureReason = PhysicalConservationFailureReason.DuplicateFragmentIndex;
                    return false;
                }

                if (!string.Equals(child.ParentProjectileId, parent.ProjectileId, StringComparison.Ordinal))
                {
                    failureReason = PhysicalConservationFailureReason.ParentIdentityMismatch;
                    return false;
                }

                if (!string.Equals(child.SourceProjectileId, parent.ProjectileId, StringComparison.Ordinal))
                {
                    failureReason = PhysicalConservationFailureReason.SourceIdentityMismatch;
                    return false;
                }

                if (sourceCollisionId == null)
                {
                    sourceCollisionId = child.SourceCollisionId;
                }
                else if (!string.Equals(
                    sourceCollisionId,
                    child.SourceCollisionId,
                    StringComparison.Ordinal))
                {
                    failureReason = PhysicalConservationFailureReason.SourceCollisionMismatch;
                    return false;
                }

                if (!string.Equals(child.RootShotId, parent.RootShotId, StringComparison.Ordinal))
                {
                    failureReason = PhysicalConservationFailureReason.RootIdentityMismatch;
                    return false;
                }

                if (child.FragmentGeneration != parent.FragmentGeneration + 1)
                {
                    failureReason = PhysicalConservationFailureReason.GenerationMismatch;
                    return false;
                }

                if (child.IsParentDerivedMass)
                {
                    parentDerivedOutputCount++;
                    allocatedParentMassKilograms += child.OriginalMassKilograms;
                    retainedParentMassKilograms += child.RetainedMassKilograms;
                    hasParentFragment |= child.Kind == PhysicalProjectileKind.ProjectileFragment
                        || child.Kind == PhysicalProjectileKind.TargetSpallFragment;
                }
                else
                {
                    targetSpallOutputCount++;
                    targetSpallMassKilograms += child.RetainedMassKilograms;
                }

                childEnergyJoules += child.TranslationalKineticEnergyJoules;
            }

            if (!AreFiniteNonNegative(
                allocatedParentMassKilograms,
                retainedParentMassKilograms,
                targetSpallMassKilograms,
                childEnergyJoules,
                residualEnergyJoules))
            {
                failureReason = PhysicalConservationFailureReason.DerivedTotalInvalid;
                return false;
            }

            double massTolerance = Math.Max(0.000000001d, parent.RetainedMassKilograms * RelativeTolerance);
            if (allocatedParentMassKilograms > parent.RetainedMassKilograms + massTolerance)
            {
                failureReason = PhysicalConservationFailureReason.ParentDerivedMassExceedsParent;
                return false;
            }

            if (retainedParentMassKilograms > parent.RetainedMassKilograms + massTolerance)
            {
                failureReason = PhysicalConservationFailureReason.RetainedParentDerivedMassExceedsParent;
                return false;
            }

            if (childEnergyJoules > residualEnergyJoules + energyTolerance)
            {
                failureReason = PhysicalConservationFailureReason.ChildEnergyExceedsResidual;
                return false;
            }

            if (requireParentFragment && !hasParentFragment)
            {
                failureReason = PhysicalConservationFailureReason.ParentFragmentMissing;
                return false;
            }

            result = new PhysicalConservationResult(
                parent.RetainedMassKilograms,
                allocatedParentMassKilograms,
                retainedParentMassKilograms,
                targetSpallMassKilograms,
                parentEnergyJoules,
                lossBudget.TotalLossJoules,
                residualEnergyJoules,
                childEnergyJoules,
                parentDerivedOutputCount,
                targetSpallOutputCount);
            failureReason = PhysicalConservationFailureReason.None;
            return true;
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

        private static bool IsFiniteNonNegative(double value)
        {
            return FiniteDouble.IsFinite(value) && value >= 0d;
        }

        private static bool HasSameLineage(
            PhysicalProjectileState parent,
            PhysicalProjectileState revision)
        {
            return string.Equals(revision.RootShotId, parent.RootShotId, StringComparison.Ordinal)
                && string.Equals(
                    revision.ParentProjectileId,
                    parent.ParentProjectileId,
                    StringComparison.Ordinal)
                && string.Equals(
                    revision.SourceProjectileId,
                    parent.SourceProjectileId,
                    StringComparison.Ordinal)
                && string.Equals(
                    revision.SourceMaterialId,
                    parent.SourceMaterialId,
                    StringComparison.Ordinal)
                && revision.SourceMaterialClass == parent.SourceMaterialClass
                && string.Equals(
                    revision.SourceCollisionId,
                    parent.SourceCollisionId,
                    StringComparison.Ordinal)
                && revision.FragmentIndex == parent.FragmentIndex
                && revision.FragmentGeneration == parent.FragmentGeneration
                && revision.DeterministicSeed == parent.DeterministicSeed
                && revision.Construction == parent.Construction;
        }

        private static bool HasInheritedCollisionHistory(
            PhysicalProjectileState parent,
            PhysicalProjectileState child,
            PhysicalCollisionRecord responseCollision)
        {
            if (child.CollisionHistory.Count != parent.CollisionHistory.Count + 1)
            {
                return false;
            }

            for (int index = 0; index < parent.CollisionHistory.Count; index++)
            {
                if (child.CollisionHistory[index] != parent.CollisionHistory[index])
                {
                    return false;
                }
            }

            return child.CollisionHistory[parent.CollisionHistory.Count] == responseCollision;
        }

        private static bool HasAppendedCollisionHistory(
            PhysicalProjectileState parent,
            PhysicalProjectileState revision)
        {
            if (revision.CollisionHistory.Count != parent.CollisionHistory.Count + 1)
            {
                return false;
            }

            for (int index = 0; index < parent.CollisionHistory.Count; index++)
            {
                if (revision.CollisionHistory[index] != parent.CollisionHistory[index])
                {
                    return false;
                }
            }

            return revision.CollisionHistory[parent.CollisionHistory.Count].Sequence
                == parent.CollisionHistory.Count;
        }

        private static bool IsValidStateRevisionKind(
            PhysicalProjectileKind parentKind,
            PhysicalProjectileKind revisionKind)
        {
            if (parentKind == PhysicalProjectileKind.IntactProjectile)
            {
                return revisionKind == PhysicalProjectileKind.IntactProjectile
                    || revisionKind == PhysicalProjectileKind.DeformedProjectile;
            }

            if (parentKind == PhysicalProjectileKind.TargetSpall
                || parentKind == PhysicalProjectileKind.TargetSpallFragment)
            {
                return revisionKind == PhysicalProjectileKind.TargetSpallFragment;
            }

            return revisionKind == parentKind;
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
    }
}
