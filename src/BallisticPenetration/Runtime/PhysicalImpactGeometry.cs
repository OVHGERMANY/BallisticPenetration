#nullable enable

using BallisticPenetration.Core.Physics;
using EFT.Ballistics;
using UnityEngine;

namespace BallisticPenetration.Runtime
{
    internal readonly struct PhysicalImpactGeometry
    {
        internal PhysicalImpactGeometry(
            PhysicalVector3 position,
            PhysicalVector3 surfaceNormal,
            double physicalThicknessMetres,
            double effectivePathLengthMetres)
        {
            Position = position;
            SurfaceNormal = surfaceNormal;
            PhysicalThicknessMetres = physicalThicknessMetres;
            EffectivePathLengthMetres = effectivePathLengthMetres;
        }

        internal PhysicalVector3 Position { get; }

        internal PhysicalVector3 SurfaceNormal { get; }

        internal double PhysicalThicknessMetres { get; }

        internal double EffectivePathLengthMetres { get; }
    }

    /// <summary>
    /// Measures only the exact collider that EFT selected. A failed ray exit rejects physical
    /// modeling for the collision instead of inventing thickness from a template resistance value.
    /// </summary>
    internal static class PhysicalImpactGeometryResolver
    {
        private const float MinimumPathMetres = 0.0001f;
        private const float SurfaceOffsetMetres = 0.001f;
        private const float BoundsPaddingMetres = 0.05f;

        internal static bool TryResolve(Shot shot, out PhysicalImpactGeometry geometry)
        {
            geometry = default;
            if (shot == null || shot.HitCollider == null)
            {
                return false;
            }

            Vector3 impactPoint = shot.HitPoint;
            Vector3 surfaceNormal = shot.HitNormal;
            Vector3 direction = shot.CurrentVelocity.normalized;
            if (!IsFiniteVector(impactPoint)
                || !IsFiniteVector(surfaceNormal)
                || !IsFiniteVector(direction)
                || surfaceNormal.sqrMagnitude <= 0f
                || direction.sqrMagnitude <= 0f)
            {
                return false;
            }

            surfaceNormal.Normalize();
            direction.Normalize();
            float alignment = -Vector3.Dot(direction, surfaceNormal);
            if (!IsFinite(alignment) || alignment <= 0f)
            {
                return false;
            }

            Bounds bounds = shot.HitCollider.bounds;
            float maximumPath = bounds.extents.magnitude * 2f + BoundsPaddingMetres;
            if (!IsFinite(maximumPath) || maximumPath <= MinimumPathMetres)
            {
                return false;
            }

            Vector3 farOrigin = impactPoint + (direction * maximumPath);
            var reverseRay = new Ray(farOrigin, -direction);
            if (!shot.HitCollider.Raycast(reverseRay, out RaycastHit farHit, maximumPath + BoundsPaddingMetres)
                || !ReferenceEquals(farHit.collider, shot.HitCollider)
                || !IsFiniteVector(farHit.point))
            {
                return false;
            }

            float effectivePath = Vector3.Dot(farHit.point - impactPoint, direction);
            if (!IsFinite(effectivePath) || effectivePath < MinimumPathMetres)
            {
                return false;
            }

            // Reject a reverse hit that only rediscovered the entry face.
            if ((farHit.point - impactPoint).sqrMagnitude
                <= SurfaceOffsetMetres * SurfaceOffsetMetres)
            {
                return false;
            }

            float physicalThickness = effectivePath * alignment;
            if (!IsFinite(physicalThickness)
                || physicalThickness < MinimumPathMetres
                || physicalThickness > effectivePath)
            {
                return false;
            }

            geometry = new PhysicalImpactGeometry(
                ToPhysical(impactPoint),
                ToPhysical(surfaceNormal),
                physicalThickness,
                effectivePath);
            return true;
        }

        internal static PhysicalVector3 ToPhysical(Vector3 value)
        {
            return new PhysicalVector3(value.x, value.y, value.z);
        }

        internal static Vector3 ToUnity(PhysicalVector3 value)
        {
            return new Vector3((float)value.X, (float)value.Y, (float)value.Z);
        }

        private static bool IsFiniteVector(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
