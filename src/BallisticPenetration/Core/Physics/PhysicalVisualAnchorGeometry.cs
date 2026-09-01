#nullable enable

using System;

namespace BallisticPenetration.Core.Physics
{
    /// <summary>
    /// Dependency-free quaternion composition for embedded visual anchors. Position conversion
    /// remains with the host transform so Unity can preserve the exact collider hierarchy scale.
    /// </summary>
    public static class PhysicalVisualAnchorGeometry
    {
        public static bool TryCreateLocalOrientation(
            PhysicalOrientation anchorWorldOrientation,
            PhysicalOrientation visualWorldOrientation,
            out PhysicalOrientation localOrientation)
        {
            localOrientation = PhysicalOrientation.Identity;
            if (!anchorWorldOrientation.IsUnit || !visualWorldOrientation.IsUnit)
            {
                return false;
            }

            var inverseAnchor = new PhysicalOrientation(
                -anchorWorldOrientation.X,
                -anchorWorldOrientation.Y,
                -anchorWorldOrientation.Z,
                anchorWorldOrientation.W);
            return TryMultiply(inverseAnchor, visualWorldOrientation, out localOrientation);
        }

        public static bool TryResolveWorldOrientation(
            PhysicalOrientation anchorWorldOrientation,
            PhysicalOrientation localOrientation,
            out PhysicalOrientation visualWorldOrientation)
        {
            visualWorldOrientation = PhysicalOrientation.Identity;
            return anchorWorldOrientation.IsUnit
                && localOrientation.IsUnit
                && TryMultiply(
                    anchorWorldOrientation,
                    localOrientation,
                    out visualWorldOrientation);
        }

        private static bool TryMultiply(
            PhysicalOrientation left,
            PhysicalOrientation right,
            out PhysicalOrientation result)
        {
            result = PhysicalOrientation.Identity;
            var candidate = new PhysicalOrientation(
                (left.W * right.X)
                    + (left.X * right.W)
                    + (left.Y * right.Z)
                    - (left.Z * right.Y),
                (left.W * right.Y)
                    - (left.X * right.Z)
                    + (left.Y * right.W)
                    + (left.Z * right.X),
                (left.W * right.Z)
                    + (left.X * right.Y)
                    - (left.Y * right.X)
                    + (left.Z * right.W),
                (left.W * right.W)
                    - (left.X * right.X)
                    - (left.Y * right.Y)
                    - (left.Z * right.Z));
            double magnitudeSquared = candidate.MagnitudeSquared;
            if (!FiniteDouble.IsFinite(magnitudeSquared) || magnitudeSquared <= 0d)
            {
                return false;
            }

            double inverseMagnitude = 1d / Math.Sqrt(magnitudeSquared);
            var normalized = new PhysicalOrientation(
                candidate.X * inverseMagnitude,
                candidate.Y * inverseMagnitude,
                candidate.Z * inverseMagnitude,
                candidate.W * inverseMagnitude);
            if (!normalized.IsUnit)
            {
                return false;
            }

            result = normalized;
            return true;
        }
    }
}
