#nullable enable

namespace BallisticPenetration.Core.Physics
{
    /// <summary>
    /// Host-free ammunition identity rules shared by runtime code and validation. Projectile
    /// construction and kinetic-versus-payload behavior are owned by the exact installed-template
    /// catalog; EFT metadata alone is not reliable enough to infer either property.
    /// </summary>
    internal static class PhysicalAmmunitionPolicy
    {
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
    }
}
