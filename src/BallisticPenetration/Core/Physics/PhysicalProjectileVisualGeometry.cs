#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace BallisticPenetration.Core.Physics
{
    public enum PhysicalVisualGeometryFailureReason
    {
        None = 0,
        ShapeUnsupported = 1,
        SegmentCountInvalid = 2,
        StateMissing = 3,
        ScaleInvalid = 4,
        MinimumDiameterInvalid = 5,
        DimensionsInvalid = 6,
        OrientationInvalid = 7
    }

    public enum PhysicalVisualMaterialKey
    {
        Unknown = 0,
        LeadAndCopper = 1,
        SteelCore = 2,
        TungstenCore = 3,
        Copper = 4,
        Steel = 5,
        Frangible = 6,
        TargetMetal = 7,
        TargetCeramic = 8,
        TargetMineral = 9,
        TargetOrganic = 10,
        TargetOther = 11,
        Aluminum = 12,
        Brass = 13,
        Zinc = 14,
        NonMetallic = 15,
        Lead = 16
    }

    /// <summary>
    /// Immutable unit mesh. Local positive Z is the component's longitudinal axis. X and Y span
    /// one unit of diameter and Z spans one unit of length, so physical dimensions are supplied by
    /// the renderer transform rather than duplicated meshes.
    /// </summary>
    public sealed class PhysicalVisualMeshDescriptor
    {
        private readonly ReadOnlyCollection<PhysicalVector3> _vertices;
        private readonly ReadOnlyCollection<int> _triangles;

        internal PhysicalVisualMeshDescriptor(
            PhysicalProjectileShapeClass shapeClass,
            IReadOnlyList<PhysicalVector3> vertices,
            IReadOnlyList<int> triangles)
        {
            ShapeClass = shapeClass;
            var vertexCopy = new PhysicalVector3[vertices.Count];
            var triangleCopy = new int[triangles.Count];
            for (int index = 0; index < vertexCopy.Length; index++)
            {
                vertexCopy[index] = vertices[index];
            }

            for (int index = 0; index < triangleCopy.Length; index++)
            {
                triangleCopy[index] = triangles[index];
            }

            _vertices = Array.AsReadOnly(vertexCopy);
            _triangles = Array.AsReadOnly(triangleCopy);
        }

        public PhysicalProjectileShapeClass ShapeClass { get; }

        public IReadOnlyList<PhysicalVector3> Vertices
        {
            get { return _vertices; }
        }

        public IReadOnlyList<int> Triangles
        {
            get { return _triangles; }
        }
    }

    public readonly struct PhysicalVisualPose : IEquatable<PhysicalVisualPose>
    {
        internal PhysicalVisualPose(
            PhysicalProjectileShapeClass shapeClass,
            PhysicalVisualMaterialKey materialKey,
            PhysicalVector3 positionMetres,
            PhysicalOrientation orientation,
            PhysicalVector3 scaleMetres)
        {
            ShapeClass = shapeClass;
            MaterialKey = materialKey;
            PositionMetres = positionMetres;
            Orientation = orientation;
            ScaleMetres = scaleMetres;
        }

        public PhysicalProjectileShapeClass ShapeClass { get; }

        public PhysicalVisualMaterialKey MaterialKey { get; }

        public PhysicalVector3 PositionMetres { get; }

        public PhysicalOrientation Orientation { get; }

        public PhysicalVector3 ScaleMetres { get; }

        public bool Equals(PhysicalVisualPose other)
        {
            return ShapeClass == other.ShapeClass
                && MaterialKey == other.MaterialKey
                && PositionMetres.Equals(other.PositionMetres)
                && Orientation.Equals(other.Orientation)
                && ScaleMetres.Equals(other.ScaleMetres);
        }

        public override bool Equals(object? obj)
        {
            return obj is PhysicalVisualPose other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)ShapeClass;
                hash = (hash * 397) ^ (int)MaterialKey;
                hash = (hash * 397) ^ PositionMetres.GetHashCode();
                hash = (hash * 397) ^ Orientation.GetHashCode();
                hash = (hash * 397) ^ ScaleMetres.GetHashCode();
                return hash;
            }
        }

        public static bool operator ==(PhysicalVisualPose left, PhysicalVisualPose right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(PhysicalVisualPose left, PhysicalVisualPose right)
        {
            return !left.Equals(right);
        }
    }

    public static class PhysicalProjectileVisualGeometry
    {
        public const int DefaultRadialSegments = 10;

        private const double MaximumRenderedDimensionMetres = 10d;

        public static bool TryCreateUnitMesh(
            PhysicalProjectileShapeClass shapeClass,
            out PhysicalVisualMeshDescriptor? descriptor,
            out PhysicalVisualGeometryFailureReason failureReason)
        {
            return TryCreateUnitMesh(
                shapeClass,
                DefaultRadialSegments,
                out descriptor,
                out failureReason);
        }

        public static bool TryCreateUnitMesh(
            PhysicalProjectileShapeClass shapeClass,
            int radialSegments,
            out PhysicalVisualMeshDescriptor? descriptor,
            out PhysicalVisualGeometryFailureReason failureReason)
        {
            descriptor = null;
            if (radialSegments < 6 || radialSegments > 32)
            {
                failureReason = PhysicalVisualGeometryFailureReason.SegmentCountInvalid;
                return false;
            }

            switch (shapeClass)
            {
                case PhysicalProjectileShapeClass.Spitzer:
                    descriptor = BuildRevolved(
                        shapeClass,
                        radialSegments,
                        new[]
                        {
                            new Ring(-0.5d, 0.42d),
                            new Ring(-0.35d, 0.50d),
                            new Ring(0.12d, 0.50d),
                            new Ring(0.36d, 0.27d),
                            new Ring(0.5d, 0.025d)
                        });
                    break;
                case PhysicalProjectileShapeClass.RoundNose:
                    descriptor = BuildRevolved(
                        shapeClass,
                        radialSegments,
                        new[]
                        {
                            new Ring(-0.5d, 0.45d),
                            new Ring(-0.35d, 0.50d),
                            new Ring(0.15d, 0.50d),
                            new Ring(0.36d, 0.38d),
                            new Ring(0.5d, 0.05d)
                        });
                    break;
                case PhysicalProjectileShapeClass.FlatNose:
                    descriptor = BuildRevolved(
                        shapeClass,
                        radialSegments,
                        new[]
                        {
                            new Ring(-0.5d, 0.45d),
                            new Ring(-0.35d, 0.50d),
                            new Ring(0.5d, 0.50d)
                        });
                    break;
                case PhysicalProjectileShapeClass.ExpandedMushroom:
                    descriptor = BuildRevolved(
                        shapeClass,
                        radialSegments,
                        new[]
                        {
                            new Ring(-0.5d, 0.24d),
                            new Ring(0.03d, 0.28d),
                            new Ring(0.20d, 0.50d),
                            new Ring(0.38d, 0.47d),
                            new Ring(0.5d, 0.28d)
                        });
                    break;
                case PhysicalProjectileShapeClass.FlattenedDisc:
                    descriptor = BuildRevolved(
                        shapeClass,
                        radialSegments,
                        new[]
                        {
                            new Ring(-0.5d, 0.48d),
                            new Ring(-0.38d, 0.50d),
                            new Ring(0.38d, 0.50d),
                            new Ring(0.5d, 0.48d)
                        });
                    break;
                case PhysicalProjectileShapeClass.IrregularProjectileFragment:
                    descriptor = BuildIrregularFragment(shapeClass);
                    break;
                case PhysicalProjectileShapeClass.TargetSpallFlake:
                    descriptor = BuildSpallFlake(shapeClass);
                    break;
                case PhysicalProjectileShapeClass.TargetSpallChunk:
                    descriptor = BuildSpallChunk(shapeClass);
                    break;
                case PhysicalProjectileShapeClass.SphericalShot:
                    descriptor = BuildRevolved(
                        shapeClass,
                        radialSegments,
                        new[]
                        {
                            new Ring(-0.5d, 0.025d),
                            new Ring(-0.35d, 0.36d),
                            new Ring(0d, 0.50d),
                            new Ring(0.35d, 0.36d),
                            new Ring(0.5d, 0.025d)
                        });
                    break;
                case PhysicalProjectileShapeClass.Flechette:
                    descriptor = BuildRevolved(
                        shapeClass,
                        radialSegments,
                        new[]
                        {
                            new Ring(-0.5d, 0.50d),
                            new Ring(-0.38d, 0.18d),
                            new Ring(0.28d, 0.18d),
                            new Ring(0.45d, 0.08d),
                            new Ring(0.5d, 0.025d)
                        });
                    break;
                default:
                    failureReason = PhysicalVisualGeometryFailureReason.ShapeUnsupported;
                    return false;
            }

            failureReason = PhysicalVisualGeometryFailureReason.None;
            return true;
        }

        public static bool TryCreatePose(
            PhysicalProjectileState? state,
            double dimensionScale,
            double minimumRenderedDiameterMetres,
            out PhysicalVisualPose pose,
            out PhysicalVisualGeometryFailureReason failureReason)
        {
            pose = default;
            if (state == null)
            {
                failureReason = PhysicalVisualGeometryFailureReason.StateMissing;
                return false;
            }

            if (!FiniteDouble.IsFinite(dimensionScale) || dimensionScale <= 0d)
            {
                failureReason = PhysicalVisualGeometryFailureReason.ScaleInvalid;
                return false;
            }

            if (!FiniteDouble.IsFinite(minimumRenderedDiameterMetres)
                || minimumRenderedDiameterMetres < 0d)
            {
                failureReason = PhysicalVisualGeometryFailureReason.MinimumDiameterInvalid;
                return false;
            }

            double physicalDiameter = state.DeformedDiameterMetres;
            double effectiveScale = dimensionScale;
            if (minimumRenderedDiameterMetres > 0d
                && physicalDiameter * effectiveScale < minimumRenderedDiameterMetres)
            {
                effectiveScale = minimumRenderedDiameterMetres / physicalDiameter;
            }

            double diameter = physicalDiameter * effectiveScale;
            double length = state.LengthMetres * effectiveScale;
            if (!FiniteDouble.IsFinite(diameter)
                || !FiniteDouble.IsFinite(length)
                || diameter <= 0d
                || length <= 0d
                || diameter > MaximumRenderedDimensionMetres
                || length > MaximumRenderedDimensionMetres)
            {
                failureReason = PhysicalVisualGeometryFailureReason.DimensionsInvalid;
                return false;
            }

            if (!state.Orientation.IsUnit)
            {
                failureReason = PhysicalVisualGeometryFailureReason.OrientationInvalid;
                return false;
            }

            pose = new PhysicalVisualPose(
                state.ShapeClass,
                SelectMaterial(state),
                state.PositionMetres,
                state.Orientation,
                new PhysicalVector3(diameter, diameter, length));
            failureReason = PhysicalVisualGeometryFailureReason.None;
            return true;
        }

        private static PhysicalVisualMaterialKey SelectMaterial(PhysicalProjectileState state)
        {
            if (state.IsTargetMaterialOrigin)
            {
                switch (state.SourceMaterialClass)
                {
                    case PhysicalMaterialClass.Aluminum:
                    case PhysicalMaterialClass.MildSteel:
                    case PhysicalMaterialClass.ArmoredSteel:
                    case PhysicalMaterialClass.Titanium:
                        return PhysicalVisualMaterialKey.TargetMetal;
                    case PhysicalMaterialClass.Ceramic:
                    case PhysicalMaterialClass.CompositeArmor:
                    case PhysicalMaterialClass.Glass:
                        return PhysicalVisualMaterialKey.TargetCeramic;
                    case PhysicalMaterialClass.Concrete:
                    case PhysicalMaterialClass.Soil:
                        return PhysicalVisualMaterialKey.TargetMineral;
                    case PhysicalMaterialClass.SoftTissue:
                    case PhysicalMaterialClass.Bone:
                    case PhysicalMaterialClass.Fabric:
                    case PhysicalMaterialClass.Wood:
                        return PhysicalVisualMaterialKey.TargetOrganic;
                    default:
                        return PhysicalVisualMaterialKey.TargetOther;
                }
            }

            switch (state.Construction)
            {
                case PhysicalProjectileConstruction.LeadCoreJacketed:
                    return PhysicalVisualMaterialKey.LeadAndCopper;
                case PhysicalProjectileConstruction.SteelCoreJacketed:
                case PhysicalProjectileConstruction.SteelPenetratorLeadCoreJacketed:
                case PhysicalProjectileConstruction.SteelPenetratorCopperCoreJacketed:
                case PhysicalProjectileConstruction.SteelPenetratorAluminumCoreJacketed:
                    return PhysicalVisualMaterialKey.SteelCore;
                case PhysicalProjectileConstruction.TungstenCoreJacketed:
                    return PhysicalVisualMaterialKey.TungstenCore;
                case PhysicalProjectileConstruction.MonolithicCopper:
                case PhysicalProjectileConstruction.CopperAlloyCoreJacketed:
                    return PhysicalVisualMaterialKey.Copper;
                case PhysicalProjectileConstruction.MonolithicSteel:
                    return PhysicalVisualMaterialKey.Steel;
                case PhysicalProjectileConstruction.FrangibleComposite:
                    return PhysicalVisualMaterialKey.Frangible;
                case PhysicalProjectileConstruction.AluminumCoreJacketed:
                    return PhysicalVisualMaterialKey.Aluminum;
                case PhysicalProjectileConstruction.MonolithicBrass:
                    return PhysicalVisualMaterialKey.Brass;
                case PhysicalProjectileConstruction.MonolithicZinc:
                    return PhysicalVisualMaterialKey.Zinc;
                case PhysicalProjectileConstruction.NonMetallicComposite:
                    return PhysicalVisualMaterialKey.NonMetallic;
                case PhysicalProjectileConstruction.MonolithicLead:
                    return PhysicalVisualMaterialKey.Lead;
                default:
                    return PhysicalVisualMaterialKey.Unknown;
            }
        }

        private static PhysicalVisualMeshDescriptor BuildRevolved(
            PhysicalProjectileShapeClass shapeClass,
            int radialSegments,
            IReadOnlyList<Ring> rings)
        {
            var vertices = new List<PhysicalVector3>((rings.Count * radialSegments) + 2);
            var triangles = new List<int>(((rings.Count - 1) * radialSegments * 6) + (radialSegments * 6));
            for (int ringIndex = 0; ringIndex < rings.Count; ringIndex++)
            {
                Ring ring = rings[ringIndex];
                for (int segment = 0; segment < radialSegments; segment++)
                {
                    double angle = (2d * Math.PI * segment) / radialSegments;
                    vertices.Add(new PhysicalVector3(
                        Math.Cos(angle) * ring.Radius,
                        Math.Sin(angle) * ring.Radius,
                        ring.Z));
                }
            }

            for (int ringIndex = 0; ringIndex < rings.Count - 1; ringIndex++)
            {
                int currentStart = ringIndex * radialSegments;
                int nextStart = currentStart + radialSegments;
                for (int segment = 0; segment < radialSegments; segment++)
                {
                    int nextSegment = (segment + 1) % radialSegments;
                    int a = currentStart + segment;
                    int b = currentStart + nextSegment;
                    int c = nextStart + segment;
                    int d = nextStart + nextSegment;
                    AddTriangle(triangles, a, b, c);
                    AddTriangle(triangles, b, d, c);
                }
            }

            int backCenter = vertices.Count;
            vertices.Add(new PhysicalVector3(0d, 0d, rings[0].Z));
            int frontCenter = vertices.Count;
            vertices.Add(new PhysicalVector3(0d, 0d, rings[rings.Count - 1].Z));
            int frontStart = (rings.Count - 1) * radialSegments;
            for (int segment = 0; segment < radialSegments; segment++)
            {
                int nextSegment = (segment + 1) % radialSegments;
                AddTriangle(triangles, backCenter, nextSegment, segment);
                AddTriangle(
                    triangles,
                    frontCenter,
                    frontStart + segment,
                    frontStart + nextSegment);
            }

            return new PhysicalVisualMeshDescriptor(shapeClass, vertices, triangles);
        }

        private static PhysicalVisualMeshDescriptor BuildIrregularFragment(
            PhysicalProjectileShapeClass shapeClass)
        {
            var vertices = new[]
            {
                new PhysicalVector3(0.48d, 0.02d, -0.18d),
                new PhysicalVector3(-0.35d, 0.31d, -0.34d),
                new PhysicalVector3(-0.22d, -0.46d, -0.12d),
                new PhysicalVector3(0.16d, 0.27d, 0.50d),
                new PhysicalVector3(0.07d, -0.24d, 0.31d)
            };
            var triangles = new[]
            {
                0, 2, 1,
                0, 1, 3,
                1, 4, 3,
                1, 2, 4,
                2, 0, 4,
                0, 3, 4
            };
            return BuildNormalizedPolyhedron(shapeClass, vertices, triangles);
        }

        private static PhysicalVisualMeshDescriptor BuildSpallFlake(
            PhysicalProjectileShapeClass shapeClass)
        {
            var vertices = new[]
            {
                new PhysicalVector3(-0.50d, -0.28d, -0.12d),
                new PhysicalVector3(0.44d, -0.34d, -0.07d),
                new PhysicalVector3(0.35d, 0.42d, -0.16d),
                new PhysicalVector3(-0.38d, 0.31d, -0.09d),
                new PhysicalVector3(-0.42d, -0.22d, 0.10d),
                new PhysicalVector3(0.39d, -0.27d, 0.15d),
                new PhysicalVector3(0.30d, 0.36d, 0.08d),
                new PhysicalVector3(-0.33d, 0.27d, 0.13d)
            };
            return BuildNormalizedPolyhedron(shapeClass, vertices, BoxTriangles);
        }

        private static PhysicalVisualMeshDescriptor BuildSpallChunk(
            PhysicalProjectileShapeClass shapeClass)
        {
            var vertices = new[]
            {
                new PhysicalVector3(-0.47d, -0.34d, -0.42d),
                new PhysicalVector3(0.42d, -0.29d, -0.31d),
                new PhysicalVector3(0.36d, 0.43d, -0.38d),
                new PhysicalVector3(-0.31d, 0.37d, -0.27d),
                new PhysicalVector3(-0.35d, -0.25d, 0.36d),
                new PhysicalVector3(0.48d, -0.32d, 0.28d),
                new PhysicalVector3(0.27d, 0.35d, 0.49d),
                new PhysicalVector3(-0.43d, 0.29d, 0.32d)
            };
            return BuildNormalizedPolyhedron(shapeClass, vertices, BoxTriangles);
        }

        private static PhysicalVisualMeshDescriptor BuildNormalizedPolyhedron(
            PhysicalProjectileShapeClass shapeClass,
            PhysicalVector3[] vertices,
            int[] triangles)
        {
            double minimumX = double.PositiveInfinity;
            double maximumX = double.NegativeInfinity;
            double minimumY = double.PositiveInfinity;
            double maximumY = double.NegativeInfinity;
            double minimumZ = double.PositiveInfinity;
            double maximumZ = double.NegativeInfinity;
            double maximumTransverseDistanceSquared = 0d;
            for (int index = 0; index < vertices.Length; index++)
            {
                PhysicalVector3 vertex = vertices[index];
                minimumX = Math.Min(minimumX, vertex.X);
                maximumX = Math.Max(maximumX, vertex.X);
                minimumY = Math.Min(minimumY, vertex.Y);
                maximumY = Math.Max(maximumY, vertex.Y);
                minimumZ = Math.Min(minimumZ, vertex.Z);
                maximumZ = Math.Max(maximumZ, vertex.Z);
                for (int otherIndex = index + 1; otherIndex < vertices.Length; otherIndex++)
                {
                    PhysicalVector3 other = vertices[otherIndex];
                    double deltaX = other.X - vertex.X;
                    double deltaY = other.Y - vertex.Y;
                    maximumTransverseDistanceSquared = Math.Max(
                        maximumTransverseDistanceSquared,
                        (deltaX * deltaX) + (deltaY * deltaY));
                }
            }

            double transverseDiameter = Math.Sqrt(maximumTransverseDistanceSquared);
            double longitudinalLength = maximumZ - minimumZ;
            if (!FiniteDouble.IsFinite(transverseDiameter)
                || !FiniteDouble.IsFinite(longitudinalLength)
                || transverseDiameter <= 0d
                || longitudinalLength <= 0d)
            {
                throw new InvalidOperationException("Physical visual polyhedron dimensions are invalid.");
            }

            double centerX = (minimumX + maximumX) * 0.5d;
            double centerY = (minimumY + maximumY) * 0.5d;
            double centerZ = (minimumZ + maximumZ) * 0.5d;
            var normalized = new PhysicalVector3[vertices.Length];
            for (int index = 0; index < vertices.Length; index++)
            {
                PhysicalVector3 vertex = vertices[index];
                normalized[index] = new PhysicalVector3(
                    (vertex.X - centerX) / transverseDiameter,
                    (vertex.Y - centerY) / transverseDiameter,
                    (vertex.Z - centerZ) / longitudinalLength);
            }

            return new PhysicalVisualMeshDescriptor(shapeClass, normalized, triangles);
        }

        private static void AddTriangle(List<int> triangles, int a, int b, int c)
        {
            triangles.Add(a);
            triangles.Add(b);
            triangles.Add(c);
        }

        private static readonly int[] BoxTriangles =
        {
            0, 2, 1, 0, 3, 2,
            4, 5, 6, 4, 6, 7,
            0, 1, 5, 0, 5, 4,
            1, 2, 6, 1, 6, 5,
            2, 3, 7, 2, 7, 6,
            3, 0, 4, 3, 4, 7
        };

        private readonly struct Ring
        {
            internal Ring(double z, double radius)
            {
                Z = z;
                Radius = radius;
            }

            internal double Z { get; }

            internal double Radius { get; }
        }
    }
}
