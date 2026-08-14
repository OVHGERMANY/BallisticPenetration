#nullable enable

using System;

namespace BallisticPenetration.Core.Physics
{
    /// <summary>
    /// Host-free ammunition identity and eligibility rules shared by runtime code and validation.
    /// EFT also uses explosive-ammo metadata for large-caliber impact effects, so those labels alone
    /// do not classify a single projectile as an explosive payload.
    /// </summary>
    internal static class PhysicalAmmunitionPolicy
    {
        internal static bool IsEligibleSingleKineticProjectile(
            int projectileCount,
            int buckshotBullets,
            double explosionStrength,
            int fragmentsCount,
            double fuzeArmTimeSeconds,
            double minimumExplosionDistanceMetres,
            double maximumExplosionDistanceMetres)
        {
            return projectileCount == 1
                && buckshotBullets >= 0
                && buckshotBullets <= 1
                && fragmentsCount == 0
                && IsFiniteZero(explosionStrength)
                && IsFiniteZero(fuzeArmTimeSeconds)
                && IsFiniteZero(minimumExplosionDistanceMetres)
                && IsFiniteZero(maximumExplosionDistanceMetres);
        }

        internal static string SelectAuthoritativeTemplateName(
            string? internalName,
            string? displayName)
        {
            if (!string.IsNullOrWhiteSpace(internalName))
            {
                return internalName;
            }

            return string.IsNullOrWhiteSpace(displayName)
                ? string.Empty
                : displayName;
        }

        private static bool IsFiniteZero(double value)
        {
            return !double.IsNaN(value)
                && !double.IsInfinity(value)
                && value == 0d;
        }
    }
}
