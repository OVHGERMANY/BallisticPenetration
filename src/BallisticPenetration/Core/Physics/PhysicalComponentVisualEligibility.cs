#nullable enable

namespace BallisticPenetration.Core.Physics
{
    /// <summary>
    /// Presentation context for a physically simulated projectile component.
    /// This classification never changes the component's simulation state.
    /// </summary>
    public enum PhysicalComponentVisualContext
    {
        Unknown = 0,
        InFlight = 1,
        EmbeddedWorldSurface = 2,
        EmbeddedCharacterSurface = 3
    }

    /// <summary>
    /// Keeps standalone component geometry out of presentation contexts where it clips through
    /// character renderers or reads as crude target-material debris.
    /// </summary>
    public static class PhysicalComponentVisualEligibility
    {
        public static bool ShouldRender(
            PhysicalProjectileState? state,
            PhysicalComponentVisualContext context)
        {
            if (state == null || state.IsTargetMaterialOrigin)
            {
                return false;
            }

            return context == PhysicalComponentVisualContext.InFlight
                || context == PhysicalComponentVisualContext.EmbeddedWorldSurface;
        }
    }
}
