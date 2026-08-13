#nullable enable

namespace BallisticPenetration.Core
{
    internal enum PostmortemArmorTraversalStep
    {
        Skip,
        ApplyAndContinue,
        ApplyAndStop
    }

    /// <summary>
    /// Pure guards and traversal decisions for postmortem armor durability.
    /// Game-facing code supplies target identity and armor matches without
    /// duplicating any of Tarkov's armor calculations or random decisions.
    /// </summary>
    internal static class PostmortemArmorPolicy
    {
        internal static bool ShouldProcessHit(
            bool pluginEnabled,
            bool featureEnabled,
            bool isForwardHit,
            bool colliderMatches,
            bool isCorpseOrDeadPlayer,
            float damage,
            float penetrationPower,
            float armorDamage)
        {
            return pluginEnabled
                && featureEnabled
                && isForwardHit
                && colliderMatches
                && isCorpseOrDeadPlayer
                && IsFiniteNonNegative(damage)
                && IsFiniteNonNegative(penetrationPower)
                && IsFiniteNonNegative(armorDamage);
        }

        internal static PostmortemArmorTraversalStep GetTraversalStep(
            bool armorMatches,
            bool blockedByThisArmor,
            bool deflectedByThisArmor)
        {
            if (!armorMatches)
            {
                return PostmortemArmorTraversalStep.Skip;
            }

            return blockedByThisArmor || deflectedByThisArmor
                ? PostmortemArmorTraversalStep.ApplyAndStop
                : PostmortemArmorTraversalStep.ApplyAndContinue;
        }

        private static bool IsFiniteNonNegative(float value)
        {
            return !float.IsNaN(value)
                && !float.IsInfinity(value)
                && value >= 0f;
        }
    }
}
