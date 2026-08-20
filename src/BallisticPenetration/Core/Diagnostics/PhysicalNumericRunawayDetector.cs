#nullable enable

using BallisticPenetration.Core.Physics;

namespace BallisticPenetration.Core.Diagnostics
{
    internal static class PhysicalNumericRunawayDetector
    {
        private const double EnergyRatioThreshold = 4d;

        internal static bool IsRunaway(
            double assignedEnergyJoules,
            double massKilograms,
            PhysicalVector3 hostMeasuredVelocity,
            PhysicalVector3 position)
        {
            if (!hostMeasuredVelocity.IsFinite || !position.IsFinite)
            {
                return true;
            }

            if (!FiniteDouble.IsFinite(assignedEnergyJoules)
                || assignedEnergyJoules <= 0d
                || !FiniteDouble.IsFinite(massKilograms)
                || massKilograms <= 0d)
            {
                return true;
            }

            double speed = hostMeasuredVelocity.Magnitude;
            double measuredEnergy = 0.5d * massKilograms * speed * speed;
            return !FiniteDouble.IsFinite(speed)
                || !FiniteDouble.IsFinite(measuredEnergy)
                || measuredEnergy > assignedEnergyJoules * EnergyRatioThreshold;
        }
    }
}
