#nullable enable

using System;

namespace BallisticPenetration.Core
{
    /// <summary>
    /// Identifies which system owns the statistics represented by one projectile component.
    /// </summary>
    public enum BallisticNormalizationOwnership
    {
        SpeedCurve = 0,
        PhysicalCapability = 1
    }

    public enum BallisticNormalizationDisposition
    {
        Applied = 0,
        Duplicate = 1,
        PhysicalCapabilityBypass = 2
    }

    public enum BallisticNormalizationFailureReason
    {
        None = 0,
        StateMissing = 1,
        CollisionIdentityMissing = 2,
        CurrentStatisticsInvalid = 3,
        CurrentFactorsInvalid = 4,
        PreviousFactorsInvalid = 5,
        PreviousFactorIsZero = 6,
        AppliedRatioNotFinite = 7,
        OutputNotFinite = 8,
        ComponentIdentityMissing = 9,
        RootIdentityMissing = 10
    }

    /// <summary>
    /// Immutable record of the speed factors already represented by one exact logical component.
    /// </summary>
    public sealed class BallisticNormalizationState
    {
        private BallisticNormalizationState(
            string componentId,
            string rootShotId,
            BallisticNormalizationOwnership ownership,
            double representedPenetrationFactor,
            double representedDamageFactor,
            int collisionOrdinal,
            string lastCollisionIdentity)
        {
            ComponentId = componentId;
            RootShotId = rootShotId;
            Ownership = ownership;
            RepresentedPenetrationFactor = representedPenetrationFactor;
            RepresentedDamageFactor = representedDamageFactor;
            CollisionOrdinal = collisionOrdinal;
            LastCollisionIdentity = lastCollisionIdentity;
        }

        public string ComponentId { get; }

        public string RootShotId { get; }

        public BallisticNormalizationOwnership Ownership { get; }

        public double RepresentedPenetrationFactor { get; }

        public double RepresentedDamageFactor { get; }

        public int CollisionOrdinal { get; }

        public string LastCollisionIdentity { get; }

        public static bool TryCreateRoot(
            string componentId,
            string rootShotId,
            out BallisticNormalizationState? state,
            out BallisticNormalizationFailureReason failureReason)
        {
            return TryCreate(
                componentId,
                rootShotId,
                BallisticNormalizationOwnership.SpeedCurve,
                1d,
                1d,
                out state,
                out failureReason);
        }

        public static bool TryCreateDerivedChild(
            string componentId,
            BallisticNormalizationState parent,
            out BallisticNormalizationState? state,
            out BallisticNormalizationFailureReason failureReason)
        {
            state = null;
            if (parent == null)
            {
                failureReason = BallisticNormalizationFailureReason.StateMissing;
                return false;
            }

            return TryCreate(
                componentId,
                parent.RootShotId,
                parent.Ownership,
                parent.RepresentedPenetrationFactor,
                parent.RepresentedDamageFactor,
                out state,
                out failureReason);
        }

        public static bool TryCreatePhysicalComponent(
            string componentId,
            string rootShotId,
            BallisticFalloffFactors baselineFactors,
            out BallisticNormalizationState? state,
            out BallisticNormalizationFailureReason failureReason)
        {
            return TryCreate(
                componentId,
                rootShotId,
                BallisticNormalizationOwnership.PhysicalCapability,
                baselineFactors.PenetrationFactor,
                baselineFactors.DamageFactor,
                out state,
                out failureReason);
        }

        internal BallisticNormalizationState Advance(
            double representedPenetrationFactor,
            double representedDamageFactor,
            string collisionIdentity)
        {
            return new BallisticNormalizationState(
                ComponentId,
                RootShotId,
                Ownership,
                representedPenetrationFactor,
                representedDamageFactor,
                checked(CollisionOrdinal + 1),
                collisionIdentity);
        }

        private static bool TryCreate(
            string componentId,
            string rootShotId,
            BallisticNormalizationOwnership ownership,
            double representedPenetrationFactor,
            double representedDamageFactor,
            out BallisticNormalizationState? state,
            out BallisticNormalizationFailureReason failureReason)
        {
            state = null;
            if (string.IsNullOrWhiteSpace(componentId))
            {
                failureReason = BallisticNormalizationFailureReason.ComponentIdentityMissing;
                return false;
            }

            if (string.IsNullOrWhiteSpace(rootShotId))
            {
                failureReason = BallisticNormalizationFailureReason.RootIdentityMissing;
                return false;
            }

            if (!IsFiniteNonNegative(representedPenetrationFactor)
                || !IsFiniteNonNegative(representedDamageFactor))
            {
                failureReason = BallisticNormalizationFailureReason.PreviousFactorsInvalid;
                return false;
            }

            state = new BallisticNormalizationState(
                componentId,
                rootShotId,
                ownership,
                representedPenetrationFactor,
                representedDamageFactor,
                0,
                string.Empty);
            failureReason = BallisticNormalizationFailureReason.None;
            return true;
        }

        private static bool IsFiniteNonNegative(double value)
        {
            return FiniteDouble.IsFinite(value) && value >= 0d;
        }
    }

    /// <summary>
    /// All values calculated for one collision. Callers commit NextState only after both host fields
    /// have been written successfully.
    /// </summary>
    public sealed class BallisticNormalizationTransition
    {
        internal BallisticNormalizationTransition(
            BallisticNormalizationDisposition disposition,
            BallisticNormalizationState nextState,
            double previousPenetrationFactor,
            double previousDamageFactor,
            double currentPenetrationFactor,
            double currentDamageFactor,
            double appliedPenetrationRatio,
            double appliedDamageRatio,
            double outputDamage,
            double outputPenetrationPower)
        {
            Disposition = disposition;
            NextState = nextState;
            PreviousPenetrationFactor = previousPenetrationFactor;
            PreviousDamageFactor = previousDamageFactor;
            CurrentPenetrationFactor = currentPenetrationFactor;
            CurrentDamageFactor = currentDamageFactor;
            AppliedPenetrationRatio = appliedPenetrationRatio;
            AppliedDamageRatio = appliedDamageRatio;
            OutputDamage = outputDamage;
            OutputPenetrationPower = outputPenetrationPower;
        }

        public BallisticNormalizationDisposition Disposition { get; }

        public BallisticNormalizationState NextState { get; }

        public double PreviousPenetrationFactor { get; }

        public double PreviousDamageFactor { get; }

        public double CurrentPenetrationFactor { get; }

        public double CurrentDamageFactor { get; }

        public double AppliedPenetrationRatio { get; }

        public double AppliedDamageRatio { get; }

        public double OutputDamage { get; }

        public double OutputPenetrationPower { get; }

        public int CollisionOrdinal
        {
            get { return NextState.CollisionOrdinal; }
        }
    }

    /// <summary>
    /// Applies only the difference between the newly required speed curve and the curve already
    /// represented in cumulative EFT statistics.
    /// </summary>
    public static class BallisticNormalizationCalculator
    {
        public static bool TryAdvance(
            BallisticNormalizationState state,
            string collisionIdentity,
            double currentDamage,
            double currentPenetrationPower,
            BallisticFalloffFactors currentFactors,
            out BallisticNormalizationTransition? transition,
            out BallisticNormalizationFailureReason failureReason)
        {
            transition = null;
            if (state == null)
            {
                failureReason = BallisticNormalizationFailureReason.StateMissing;
                return false;
            }

            if (string.IsNullOrWhiteSpace(collisionIdentity))
            {
                failureReason = BallisticNormalizationFailureReason.CollisionIdentityMissing;
                return false;
            }

            if (!IsFiniteNonNegative(currentDamage)
                || !IsFiniteNonNegative(currentPenetrationPower))
            {
                failureReason = BallisticNormalizationFailureReason.CurrentStatisticsInvalid;
                return false;
            }

            if (!IsFiniteNonNegative(currentFactors.PenetrationFactor)
                || !IsFiniteNonNegative(currentFactors.DamageFactor))
            {
                failureReason = BallisticNormalizationFailureReason.CurrentFactorsInvalid;
                return false;
            }

            if (!IsFiniteNonNegative(state.RepresentedPenetrationFactor)
                || !IsFiniteNonNegative(state.RepresentedDamageFactor))
            {
                failureReason = BallisticNormalizationFailureReason.PreviousFactorsInvalid;
                return false;
            }

            if (string.Equals(
                    collisionIdentity,
                    state.LastCollisionIdentity,
                    StringComparison.Ordinal))
            {
                transition = new BallisticNormalizationTransition(
                    BallisticNormalizationDisposition.Duplicate,
                    state,
                    state.RepresentedPenetrationFactor,
                    state.RepresentedDamageFactor,
                    currentFactors.PenetrationFactor,
                    currentFactors.DamageFactor,
                    1d,
                    1d,
                    currentDamage,
                    currentPenetrationPower);
                failureReason = BallisticNormalizationFailureReason.None;
                return true;
            }

            if (state.Ownership == BallisticNormalizationOwnership.PhysicalCapability)
            {
                BallisticNormalizationState nextPhysicalState = state.Advance(
                    currentFactors.PenetrationFactor,
                    currentFactors.DamageFactor,
                    collisionIdentity);
                transition = new BallisticNormalizationTransition(
                    BallisticNormalizationDisposition.PhysicalCapabilityBypass,
                    nextPhysicalState,
                    state.RepresentedPenetrationFactor,
                    state.RepresentedDamageFactor,
                    currentFactors.PenetrationFactor,
                    currentFactors.DamageFactor,
                    1d,
                    1d,
                    currentDamage,
                    currentPenetrationPower);
                failureReason = BallisticNormalizationFailureReason.None;
                return true;
            }

            if (!TryGetAppliedRatio(
                    currentFactors.PenetrationFactor,
                    state.RepresentedPenetrationFactor,
                    out double penetrationRatio)
                || !TryGetAppliedRatio(
                    currentFactors.DamageFactor,
                    state.RepresentedDamageFactor,
                    out double damageRatio))
            {
                failureReason = BallisticNormalizationFailureReason.PreviousFactorIsZero;
                return false;
            }

            if (!FiniteDouble.IsFinite(penetrationRatio)
                || !FiniteDouble.IsFinite(damageRatio))
            {
                failureReason = BallisticNormalizationFailureReason.AppliedRatioNotFinite;
                return false;
            }

            double outputDamage = currentDamage * damageRatio;
            double outputPenetrationPower = currentPenetrationPower * penetrationRatio;
            if (!IsFiniteNonNegative(outputDamage)
                || !IsFiniteNonNegative(outputPenetrationPower))
            {
                failureReason = BallisticNormalizationFailureReason.OutputNotFinite;
                return false;
            }

            BallisticNormalizationState nextState = state.Advance(
                currentFactors.PenetrationFactor,
                currentFactors.DamageFactor,
                collisionIdentity);
            transition = new BallisticNormalizationTransition(
                BallisticNormalizationDisposition.Applied,
                nextState,
                state.RepresentedPenetrationFactor,
                state.RepresentedDamageFactor,
                currentFactors.PenetrationFactor,
                currentFactors.DamageFactor,
                penetrationRatio,
                damageRatio,
                outputDamage,
                outputPenetrationPower);
            failureReason = BallisticNormalizationFailureReason.None;
            return true;
        }

        private static bool TryGetAppliedRatio(
            double currentFactor,
            double previousFactor,
            out double ratio)
        {
            if (previousFactor == 0d)
            {
                if (currentFactor == 0d)
                {
                    ratio = 1d;
                    return true;
                }

                ratio = 0d;
                return false;
            }

            ratio = currentFactor / previousFactor;
            return true;
        }

        private static bool IsFiniteNonNegative(double value)
        {
            return FiniteDouble.IsFinite(value) && value >= 0d;
        }
    }
}
