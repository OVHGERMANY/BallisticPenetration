#nullable enable

using System;

namespace BallisticPenetration.Core
{
    /// <summary>
    /// Explains why <see cref="BallisticFalloffCalculator.TryCalculate"/> returned false.
    /// </summary>
    public enum BallisticFalloffFailureReason
    {
        None = 0,
        ImpactSpeedNotFinite = 1,
        ImpactSpeedNegative = 2,
        TemplateSpeedNotFinite = 3,
        TemplateSpeedNotPositive = 4,
        PenetrationExponentNotFinite = 5,
        PenetrationExponentNotPositive = 6,
        DamageExponentNotFinite = 7,
        DamageExponentNotPositive = 8,
        SpeedFractionNotFinite = 9,
        PenetrationFactorNotFinite = 10,
        DamageFactorNotFinite = 11
    }

    /// <summary>
    /// Immutable exponents used to derive the penetration and damage falloff factors.
    /// Both exponents must be finite and greater than zero.
    /// </summary>
    public readonly struct FalloffExponentConfiguration : IEquatable<FalloffExponentConfiguration>
    {
        public const double DefaultPenetrationExponent = 1.4d;
        public const double DefaultDamageExponent = 0.4d;

        public FalloffExponentConfiguration(double penetrationExponent, double damageExponent)
        {
            PenetrationExponent = penetrationExponent;
            DamageExponent = damageExponent;
        }

        public double PenetrationExponent { get; }

        public double DamageExponent { get; }

        public static FalloffExponentConfiguration Default
        {
            get
            {
                return new FalloffExponentConfiguration(
                    DefaultPenetrationExponent,
                    DefaultDamageExponent);
            }
        }

        /// <summary>
        /// Validates this configuration without throwing so callers can retain a neutral fallback.
        /// </summary>
        public bool IsValid(out BallisticFalloffFailureReason failureReason)
        {
            if (!FiniteDouble.IsFinite(PenetrationExponent))
            {
                failureReason = BallisticFalloffFailureReason.PenetrationExponentNotFinite;
                return false;
            }

            if (PenetrationExponent <= 0d)
            {
                failureReason = BallisticFalloffFailureReason.PenetrationExponentNotPositive;
                return false;
            }

            if (!FiniteDouble.IsFinite(DamageExponent))
            {
                failureReason = BallisticFalloffFailureReason.DamageExponentNotFinite;
                return false;
            }

            if (DamageExponent <= 0d)
            {
                failureReason = BallisticFalloffFailureReason.DamageExponentNotPositive;
                return false;
            }

            failureReason = BallisticFalloffFailureReason.None;
            return true;
        }

        public bool Equals(FalloffExponentConfiguration other)
        {
            return PenetrationExponent.Equals(other.PenetrationExponent)
                && DamageExponent.Equals(other.DamageExponent);
        }

        public override bool Equals(object? obj)
        {
            return obj is FalloffExponentConfiguration other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (PenetrationExponent.GetHashCode() * 397)
                    ^ DamageExponent.GetHashCode();
            }
        }

        public static bool operator ==(
            FalloffExponentConfiguration left,
            FalloffExponentConfiguration right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(
            FalloffExponentConfiguration left,
            FalloffExponentConfiguration right)
        {
            return !left.Equals(right);
        }
    }

    /// <summary>
    /// The raw impact/template speed ratio and its independently calculated stat factors.
    /// </summary>
    public readonly struct BallisticFalloffFactors : IEquatable<BallisticFalloffFactors>
    {
        internal BallisticFalloffFactors(
            double speedFraction,
            double penetrationFactor,
            double damageFactor)
        {
            SpeedFraction = speedFraction;
            PenetrationFactor = penetrationFactor;
            DamageFactor = damageFactor;
        }

        /// <summary>
        /// The unbounded ratio of impact speed to ammo-template initial speed.
        /// </summary>
        public double SpeedFraction { get; }

        public double PenetrationFactor { get; }

        public double DamageFactor { get; }

        /// <summary>
        /// Safe fallback for invalid input: preserve the original template statistics.
        /// Callers must still inspect the false return value and failure reason.
        /// </summary>
        public static BallisticFalloffFactors NeutralFallback
        {
            get
            {
                return new BallisticFalloffFactors(0d, 1d, 1d);
            }
        }

        public bool Equals(BallisticFalloffFactors other)
        {
            return SpeedFraction.Equals(other.SpeedFraction)
                && PenetrationFactor.Equals(other.PenetrationFactor)
                && DamageFactor.Equals(other.DamageFactor);
        }

        public override bool Equals(object? obj)
        {
            return obj is BallisticFalloffFactors other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = SpeedFraction.GetHashCode();
                hash = (hash * 397) ^ PenetrationFactor.GetHashCode();
                hash = (hash * 397) ^ DamageFactor.GetHashCode();
                return hash;
            }
        }

        public static bool operator ==(
            BallisticFalloffFactors left,
            BallisticFalloffFactors right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(
            BallisticFalloffFactors left,
            BallisticFalloffFactors right)
        {
            return !left.Equals(right);
        }
    }

    /// <summary>
    /// Pure, dependency-free projectile-speed falloff calculation. It intentionally has no weapon
    /// parameters: the ratio is determined only by the observed impact speed and ammo template speed.
    /// </summary>
    public static class BallisticFalloffCalculator
    {
        /// <summary>
        /// Calculates factors with the default penetration exponent (1.4) and damage exponent (0.4).
        /// </summary>
        public static bool TryCalculate(
            double impactSpeed,
            double templateSpeed,
            out BallisticFalloffFactors factors,
            out BallisticFalloffFailureReason failureReason)
        {
            return TryCalculate(
                impactSpeed,
                templateSpeed,
                FalloffExponentConfiguration.Default,
                out factors,
                out failureReason);
        }

        /// <summary>
        /// Calculates uncapped penetration and damage factors from impact speed divided by template speed.
        /// Invalid values return false, a neutral (1.0/1.0) fallback, and a specific failure reason.
        /// </summary>
        public static bool TryCalculate(
            double impactSpeed,
            double templateSpeed,
            FalloffExponentConfiguration exponentConfiguration,
            out BallisticFalloffFactors factors,
            out BallisticFalloffFailureReason failureReason)
        {
            factors = BallisticFalloffFactors.NeutralFallback;

            if (!FiniteDouble.IsFinite(impactSpeed))
            {
                failureReason = BallisticFalloffFailureReason.ImpactSpeedNotFinite;
                return false;
            }

            if (impactSpeed < 0d)
            {
                failureReason = BallisticFalloffFailureReason.ImpactSpeedNegative;
                return false;
            }

            if (!FiniteDouble.IsFinite(templateSpeed))
            {
                failureReason = BallisticFalloffFailureReason.TemplateSpeedNotFinite;
                return false;
            }

            if (templateSpeed <= 0d)
            {
                failureReason = BallisticFalloffFailureReason.TemplateSpeedNotPositive;
                return false;
            }

            if (!exponentConfiguration.IsValid(out failureReason))
            {
                return false;
            }

            var speedFraction = impactSpeed / templateSpeed;
            if (!FiniteDouble.IsFinite(speedFraction))
            {
                failureReason = BallisticFalloffFailureReason.SpeedFractionNotFinite;
                return false;
            }

            // Handle zero before Math.Pow so a stopped round stays at zero.
            if (speedFraction == 0d)
            {
                factors = new BallisticFalloffFactors(0d, 0d, 0d);
                failureReason = BallisticFalloffFailureReason.None;
                return true;
            }

            var penetrationFactor = Math.Pow(speedFraction, exponentConfiguration.PenetrationExponent);
            if (!FiniteDouble.IsFinite(penetrationFactor))
            {
                failureReason = BallisticFalloffFailureReason.PenetrationFactorNotFinite;
                return false;
            }

            var damageFactor = Math.Pow(speedFraction, exponentConfiguration.DamageExponent);
            if (!FiniteDouble.IsFinite(damageFactor))
            {
                failureReason = BallisticFalloffFailureReason.DamageFactorNotFinite;
                return false;
            }

            factors = new BallisticFalloffFactors(speedFraction, penetrationFactor, damageFactor);
            failureReason = BallisticFalloffFailureReason.None;
            return true;
        }
    }

    internal static class FiniteDouble
    {
        internal static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }
    }
}
