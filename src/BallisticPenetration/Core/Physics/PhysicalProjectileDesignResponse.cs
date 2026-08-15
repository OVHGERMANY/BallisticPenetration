#nullable enable

namespace BallisticPenetration.Core.Physics
{
    /// <summary>
    /// Bounded terminal-response modifiers for the projectile's actual nose and jacket design.
    /// Material capacity remains owned by the construction profile; these factors determine how
    /// readily that material capacity is expressed by this particular projectile design.
    /// </summary>
    public static class PhysicalProjectileDesignResponse
    {
        public static double GetExpansionResponse(PhysicalProjectileDesignClass designClass)
        {
            switch (designClass)
            {
                case PhysicalProjectileDesignClass.FullMetalJacket:
                    return 0.55d;
                case PhysicalProjectileDesignClass.SemiJacketed:
                    return 0.90d;
                case PhysicalProjectileDesignClass.HollowPoint:
                    return 1.25d;
                case PhysicalProjectileDesignClass.SoftPoint:
                    return 1.10d;
                case PhysicalProjectileDesignClass.Expanding:
                    return 1.15d;
                case PhysicalProjectileDesignClass.PolymerTipped:
                    return 1.20d;
                case PhysicalProjectileDesignClass.OpenTip:
                    return 0.80d;
                case PhysicalProjectileDesignClass.SabotedPenetrator:
                    return 0.25d;
                case PhysicalProjectileDesignClass.ExposedPenetrator:
                    return 0.35d;
                case PhysicalProjectileDesignClass.Frangible:
                    return 0.40d;
                case PhysicalProjectileDesignClass.Solid:
                    return 0.65d;
                case PhysicalProjectileDesignClass.Fragment:
                    return 0.30d;
                case PhysicalProjectileDesignClass.Shot:
                    return 0.85d;
                case PhysicalProjectileDesignClass.Flechette:
                    return 0.20d;
                default:
                    return double.NaN;
            }
        }

        public static double GetFractureResponse(PhysicalProjectileDesignClass designClass)
        {
            switch (designClass)
            {
                case PhysicalProjectileDesignClass.FullMetalJacket:
                    return 0.90d;
                case PhysicalProjectileDesignClass.SemiJacketed:
                    return 1.05d;
                case PhysicalProjectileDesignClass.HollowPoint:
                    return 1.20d;
                case PhysicalProjectileDesignClass.SoftPoint:
                    return 1.05d;
                case PhysicalProjectileDesignClass.Expanding:
                    return 1.10d;
                case PhysicalProjectileDesignClass.PolymerTipped:
                    return 1.15d;
                case PhysicalProjectileDesignClass.OpenTip:
                    return 1.15d;
                case PhysicalProjectileDesignClass.SabotedPenetrator:
                    return 0.85d;
                case PhysicalProjectileDesignClass.ExposedPenetrator:
                    return 0.85d;
                case PhysicalProjectileDesignClass.Frangible:
                    return 1.75d;
                case PhysicalProjectileDesignClass.Solid:
                    return 0.75d;
                case PhysicalProjectileDesignClass.Fragment:
                    return 1.25d;
                case PhysicalProjectileDesignClass.Shot:
                    return 0.80d;
                case PhysicalProjectileDesignClass.Flechette:
                    return 0.70d;
                default:
                    return double.NaN;
            }
        }

        public static double GetInitialDragMultiplier(PhysicalProjectileDesignClass designClass)
        {
            switch (designClass)
            {
                case PhysicalProjectileDesignClass.FullMetalJacket:
                    return 1d;
                case PhysicalProjectileDesignClass.SemiJacketed:
                    return 1.02d;
                case PhysicalProjectileDesignClass.HollowPoint:
                    return 1.08d;
                case PhysicalProjectileDesignClass.SoftPoint:
                    return 1.05d;
                case PhysicalProjectileDesignClass.Expanding:
                    return 1.06d;
                case PhysicalProjectileDesignClass.PolymerTipped:
                    return 0.95d;
                case PhysicalProjectileDesignClass.OpenTip:
                    return 0.98d;
                case PhysicalProjectileDesignClass.SabotedPenetrator:
                    return 0.88d;
                case PhysicalProjectileDesignClass.ExposedPenetrator:
                    return 0.95d;
                case PhysicalProjectileDesignClass.Frangible:
                    return 1.10d;
                case PhysicalProjectileDesignClass.Solid:
                    return 1.02d;
                case PhysicalProjectileDesignClass.Fragment:
                    return 1.45d;
                case PhysicalProjectileDesignClass.Shot:
                    return 1.30d;
                case PhysicalProjectileDesignClass.Flechette:
                    return 0.75d;
                default:
                    return double.NaN;
            }
        }
    }
}
