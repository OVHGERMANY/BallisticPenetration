#nullable enable

using System;
using BallisticPenetration.Core;

namespace BallisticPenetration.Core.Physics
{
    public enum PhysicalEftProjectionFailureReason
    {
        None = 0,
        InputMissing = 1,
        ParentMissing = 2,
        ComponentMissing = 3,
        LineageMismatch = 4,
        ParentEftValuesInvalid = 5,
        TransferMultiplierInvalid = 6,
        PhysicalCapabilityInvalid = 7,
        ProjectedValueInvalid = 8
    }

    public sealed class PhysicalEftProjectionInput
    {
        public PhysicalProjectileState? Parent { get; set; }

        public PhysicalProjectileState? Component { get; set; }

        public double ParentEftBallisticCoefficient { get; set; }

        public double ParentEftDamage { get; set; }

        public double ParentEftPenetrationPower { get; set; }

        /// <summary>
        /// Multiplier already selected by EFT for target transfer and armor CF after removing the
        /// host's placeholder child-share constant. It is measured from host-created children and
        /// is never recalculated by this layer.
        /// </summary>
        public double DamageTransferMultiplier { get; set; } = 1d;

        public double PenetrationTransferMultiplier { get; set; } = 1d;
    }

    /// <summary>
    /// Component-specific values that can be written to one EFT child shot. All values remain in
    /// double precision until the game-facing layer performs a checked float conversion.
    /// </summary>
    public sealed class PhysicalEftProjectileProjection
    {
        internal PhysicalEftProjectileProjection(
            double massGrams,
            double equivalentDiameterMillimetres,
            double speedMetresPerSecond,
            PhysicalVector3 direction,
            double ballisticCoefficient,
            double damage,
            double penetrationPower,
            double damageCapabilityRatio,
            double penetrationCapabilityRatio)
        {
            MassGrams = massGrams;
            EquivalentDiameterMillimetres = equivalentDiameterMillimetres;
            SpeedMetresPerSecond = speedMetresPerSecond;
            Direction = direction;
            BallisticCoefficient = ballisticCoefficient;
            Damage = damage;
            PenetrationPower = penetrationPower;
            DamageCapabilityRatio = damageCapabilityRatio;
            PenetrationCapabilityRatio = penetrationCapabilityRatio;
        }

        public double MassGrams { get; }

        public double EquivalentDiameterMillimetres { get; }

        public double SpeedMetresPerSecond { get; }

        public PhysicalVector3 Direction { get; }

        public double BallisticCoefficient { get; }

        public double Damage { get; }

        public double PenetrationPower { get; }

        public double DamageCapabilityRatio { get; }

        public double PenetrationCapabilityRatio { get; }
    }

    /// <summary>
    /// Maps an independently conserved physical component onto EFT's public shot fields without
    /// copying whole-projectile mass, diameter, or drag values into a fragment.
    /// </summary>
    public static class PhysicalEftProjectileProjector
    {
        private const double RelativeTolerance = 0.000000001d;

        public static bool TryProject(
            PhysicalEftProjectionInput? input,
            out PhysicalEftProjectileProjection? projection,
            out PhysicalEftProjectionFailureReason failureReason)
        {
            projection = null;
            if (input == null)
            {
                failureReason = PhysicalEftProjectionFailureReason.InputMissing;
                return false;
            }

            PhysicalProjectileState? parent = input.Parent;
            if (parent == null)
            {
                failureReason = PhysicalEftProjectionFailureReason.ParentMissing;
                return false;
            }

            PhysicalProjectileState? component = input.Component;
            if (component == null)
            {
                failureReason = PhysicalEftProjectionFailureReason.ComponentMissing;
                return false;
            }

            if (!HasValidLineage(parent, component))
            {
                failureReason = PhysicalEftProjectionFailureReason.LineageMismatch;
                return false;
            }

            if (!IsFinitePositive(input.ParentEftBallisticCoefficient)
                || !IsFiniteNonNegative(input.ParentEftDamage)
                || !IsFiniteNonNegative(input.ParentEftPenetrationPower))
            {
                failureReason = PhysicalEftProjectionFailureReason.ParentEftValuesInvalid;
                return false;
            }

            if (!IsFiniteNonNegative(input.DamageTransferMultiplier)
                || !IsFiniteNonNegative(input.PenetrationTransferMultiplier))
            {
                failureReason = PhysicalEftProjectionFailureReason.TransferMultiplierInvalid;
                return false;
            }

            if (!TryCalculateCapabilityRatio(
                    component.DamageCapabilityJoules,
                    parent.DamageCapabilityJoules,
                    out double damageRatio)
                || !TryCalculateCapabilityRatio(
                    component.PenetrationCapabilityJoulesPerSquareMetre,
                    parent.PenetrationCapabilityJoulesPerSquareMetre,
                    out double penetrationRatio))
            {
                failureReason = PhysicalEftProjectionFailureReason.PhysicalCapabilityInvalid;
                return false;
            }

            if (!component.VelocityMetresPerSecond.TryNormalize(out PhysicalVector3 direction))
            {
                failureReason = PhysicalEftProjectionFailureReason.ProjectedValueInvalid;
                return false;
            }

            double massGrams = component.RetainedMassKilograms * 1000d;
            double diameterMillimetres = component.EquivalentDiameterMetres * 1000d;
            double ballisticCoefficient = input.ParentEftBallisticCoefficient
                * component.BallisticCoefficientKilogramsPerSquareMetre
                / parent.BallisticCoefficientKilogramsPerSquareMetre;
            double damage = input.ParentEftDamage
                * damageRatio
                * input.DamageTransferMultiplier;
            double penetrationPower = input.ParentEftPenetrationPower
                * penetrationRatio
                * input.PenetrationTransferMultiplier;
            if (!IsFinitePositive(massGrams)
                || !IsFinitePositive(diameterMillimetres)
                || !IsFinitePositive(component.SpeedMetresPerSecond)
                || !IsFinitePositive(ballisticCoefficient)
                || !IsFiniteNonNegative(damage)
                || !IsFiniteNonNegative(penetrationPower))
            {
                failureReason = PhysicalEftProjectionFailureReason.ProjectedValueInvalid;
                return false;
            }

            projection = new PhysicalEftProjectileProjection(
                massGrams,
                diameterMillimetres,
                component.SpeedMetresPerSecond,
                direction,
                ballisticCoefficient,
                damage,
                penetrationPower,
                damageRatio,
                penetrationRatio);
            failureReason = PhysicalEftProjectionFailureReason.None;
            return true;
        }

        private static bool HasValidLineage(
            PhysicalProjectileState parent,
            PhysicalProjectileState component)
        {
            if (!string.Equals(parent.RootShotId, component.RootShotId, StringComparison.Ordinal)
                || component.CollisionHistory.Count != parent.CollisionHistory.Count + 1)
            {
                return false;
            }

            bool isPrimaryRevision = string.Equals(
                parent.ProjectileId,
                component.ProjectileId,
                StringComparison.Ordinal);
            if (isPrimaryRevision)
            {
                return component.FragmentGeneration == parent.FragmentGeneration
                    && string.Equals(
                        component.ParentProjectileId,
                        parent.ParentProjectileId,
                        StringComparison.Ordinal);
            }

            return component.FragmentGeneration == parent.FragmentGeneration + 1
                && string.Equals(
                    component.ParentProjectileId,
                    parent.ProjectileId,
                    StringComparison.Ordinal);
        }

        private static bool TryCalculateCapabilityRatio(
            double componentCapability,
            double parentCapability,
            out double ratio)
        {
            ratio = 0d;
            if (!IsFiniteNonNegative(componentCapability)
                || !IsFiniteNonNegative(parentCapability))
            {
                return false;
            }

            if (parentCapability <= RelativeTolerance)
            {
                return componentCapability <= RelativeTolerance;
            }

            double candidate = componentCapability / parentCapability;
            if (!IsFiniteNonNegative(candidate))
            {
                return false;
            }

            ratio = candidate;
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
