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

    public readonly struct PhysicalLossBudget
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
        ProjectileMassExceedsParent = 12,
        RetainedProjectileMassExceedsParent = 13,
        ChildEnergyExceedsResidual = 14,
        ProjectileFragmentMissing = 15,
        DerivedTotalInvalid = 16,
        SourceCollisionMismatch = 17
    }

    public readonly struct PhysicalConservationResult
    {
        internal PhysicalConservationResult(
            double availableProjectileMassKilograms,
            double allocatedProjectileMassKilograms,
            double retainedProjectileMassKilograms,
            double targetSpallMassKilograms,
            double parentEnergyJoules,
            double modeledLossEnergyJoules,
            double residualEnergyJoules,
            double childEnergyJoules,
            int projectileOutputCount,
            int targetSpallOutputCount)
        {
            AvailableProjectileMassKilograms = availableProjectileMassKilograms;
            AllocatedProjectileMassKilograms = allocatedProjectileMassKilograms;
            RetainedProjectileMassKilograms = retainedProjectileMassKilograms;
            TargetSpallMassKilograms = targetSpallMassKilograms;
            ParentEnergyJoules = parentEnergyJoules;
            ModeledLossEnergyJoules = modeledLossEnergyJoules;
            ResidualEnergyJoules = residualEnergyJoules;
            ChildEnergyJoules = childEnergyJoules;
            ProjectileOutputCount = projectileOutputCount;
            TargetSpallOutputCount = targetSpallOutputCount;
        }

        public double AvailableProjectileMassKilograms { get; }

        public double AllocatedProjectileMassKilograms { get; }

        public double RetainedProjectileMassKilograms { get; }

        /// <summary>
        /// Target material mass. This is reported separately and never consumes projectile mass.
        /// </summary>
        public double TargetSpallMassKilograms { get; }

        public double ParentEnergyJoules { get; }

        public double ModeledLossEnergyJoules { get; }

        public double ResidualEnergyJoules { get; }

        public double ChildEnergyJoules { get; }

        public int ProjectileOutputCount { get; }

        public int TargetSpallOutputCount { get; }

        public double UnallocatedProjectileMassKilograms
        {
            get { return AvailableProjectileMassKilograms - AllocatedProjectileMassKilograms; }
        }

        public double UnallocatedResidualEnergyJoules
        {
            get { return ResidualEnergyJoules - ChildEnergyJoules; }
        }
    }

    public static class PhysicalProjectileConservation
    {
        private const double RelativeTolerance = 0.000000001d;

        public static bool TryValidateTransition(
            PhysicalProjectileState parent,
            IReadOnlyList<PhysicalProjectileState> outputs,
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

        public static bool TryValidateFragmentationTransition(
            PhysicalProjectileState parent,
            IReadOnlyList<PhysicalProjectileState> outputs,
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
            PhysicalProjectileState parent,
            IReadOnlyList<PhysicalProjectileState> outputs,
            PhysicalLossBudget lossBudget,
            bool requireProjectileFragment,
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
            double allocatedProjectileMassKilograms = 0d;
            double retainedProjectileMassKilograms = 0d;
            double targetSpallMassKilograms = 0d;
            double childEnergyJoules = 0d;
            int projectileOutputCount = 0;
            int targetSpallOutputCount = 0;
            bool hasProjectileFragment = false;
            string sourceCollisionId = null;
            var childIds = new HashSet<string>(StringComparer.Ordinal);
            var fragmentIndices = new HashSet<int>();

            for (int index = 0; index < outputs.Count; index++)
            {
                PhysicalProjectileState child = outputs[index];
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

                if (child.IsProjectileDerivedMass)
                {
                    projectileOutputCount++;
                    allocatedProjectileMassKilograms += child.OriginalMassKilograms;
                    retainedProjectileMassKilograms += child.RetainedMassKilograms;
                    hasProjectileFragment |= child.Kind == PhysicalProjectileKind.ProjectileFragment;
                }
                else
                {
                    targetSpallOutputCount++;
                    targetSpallMassKilograms += child.RetainedMassKilograms;
                }

                childEnergyJoules += child.TranslationalKineticEnergyJoules;
            }

            if (!AreFiniteNonNegative(
                allocatedProjectileMassKilograms,
                retainedProjectileMassKilograms,
                targetSpallMassKilograms,
                childEnergyJoules,
                residualEnergyJoules))
            {
                failureReason = PhysicalConservationFailureReason.DerivedTotalInvalid;
                return false;
            }

            double massTolerance = Math.Max(0.000000001d, parent.RetainedMassKilograms * RelativeTolerance);
            if (allocatedProjectileMassKilograms > parent.RetainedMassKilograms + massTolerance)
            {
                failureReason = PhysicalConservationFailureReason.ProjectileMassExceedsParent;
                return false;
            }

            if (retainedProjectileMassKilograms > parent.RetainedMassKilograms + massTolerance)
            {
                failureReason = PhysicalConservationFailureReason.RetainedProjectileMassExceedsParent;
                return false;
            }

            if (childEnergyJoules > residualEnergyJoules + energyTolerance)
            {
                failureReason = PhysicalConservationFailureReason.ChildEnergyExceedsResidual;
                return false;
            }

            if (requireProjectileFragment && !hasProjectileFragment)
            {
                failureReason = PhysicalConservationFailureReason.ProjectileFragmentMissing;
                return false;
            }

            result = new PhysicalConservationResult(
                parent.RetainedMassKilograms,
                allocatedProjectileMassKilograms,
                retainedProjectileMassKilograms,
                targetSpallMassKilograms,
                parentEnergyJoules,
                lossBudget.TotalLossJoules,
                residualEnergyJoules,
                childEnergyJoules,
                projectileOutputCount,
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
    }
}
