#nullable enable

using System;
using BallisticPenetration.Core;

namespace BallisticPenetration.Core.Physics
{
    public enum PhysicalProjectileKind
    {
        Unknown = 0,
        IntactProjectile = 1,
        DeformedProjectile = 2,
        ProjectileFragment = 3,
        TargetSpall = 4,
        TargetSpallFragment = 5
    }

    public enum PhysicalProjectileShapeClass
    {
        Unknown = 0,
        Spitzer = 1,
        RoundNose = 2,
        FlatNose = 3,
        ExpandedMushroom = 4,
        FlattenedDisc = 5,
        IrregularProjectileFragment = 6,
        TargetSpallFlake = 7,
        TargetSpallChunk = 8,
        SphericalShot = 9,
        Flechette = 10
    }

    public enum PhysicalProjectileConstruction
    {
        Unknown = 0,
        LeadCoreJacketed = 1,
        SteelCoreJacketed = 2,
        TungstenCoreJacketed = 3,
        MonolithicCopper = 4,
        MonolithicSteel = 5,
        FrangibleComposite = 6,
        TargetMaterial = 7,
        AluminumCoreJacketed = 8,
        CopperAlloyCoreJacketed = 9,
        SteelPenetratorLeadCoreJacketed = 10,
        SteelPenetratorCopperCoreJacketed = 11,
        SteelPenetratorAluminumCoreJacketed = 12,
        MonolithicBrass = 13,
        MonolithicZinc = 14,
        NonMetallicComposite = 15,
        MonolithicLead = 16
    }

    /// <summary>
    /// Projectile terminal-design family. Construction identifies the material system; this value
    /// identifies how the nose, jacket, penetrator, or payload is intended to behave on impact.
    /// </summary>
    public enum PhysicalProjectileDesignClass
    {
        Unknown = 0,
        FullMetalJacket = 1,
        SemiJacketed = 2,
        HollowPoint = 3,
        SoftPoint = 4,
        Expanding = 5,
        PolymerTipped = 6,
        OpenTip = 7,
        SabotedPenetrator = 8,
        ExposedPenetrator = 9,
        Frangible = 10,
        Solid = 11,
        Fragment = 12,
        Payload = 13,
        Shot = 14,
        Flechette = 15
    }

    public enum PhysicalMaterialClass
    {
        Unknown = 0,
        Air = 1,
        SoftTissue = 2,
        Bone = 3,
        Fabric = 4,
        Polymer = 5,
        Wood = 6,
        Glass = 7,
        Aluminum = 8,
        MildSteel = 9,
        ArmoredSteel = 10,
        Ceramic = 11,
        CompositeArmor = 12,
        Concrete = 13,
        Soil = 14,
        Other = 15,
        Titanium = 16
    }

    public enum PhysicalProjectileTerminalState
    {
        Unknown = 0,
        Continuing = 1,
        Exited = 2,
        Embedded = 3,
        Stopped = 4
    }

    public enum PhysicalProjectileTumbleState
    {
        Stable = 0,
        Yawing = 1,
        Tumbling = 2
    }

    public enum PhysicalProjectileRenderState
    {
        NotRendered = 0,
        Visible = 1,
        Embedded = 2,
        Culled = 3,
        Expired = 4
    }

    public enum PhysicalCollisionOutcome
    {
        Unknown = 0,
        Penetrated = 1,
        Stopped = 2,
        Deviated = 3,
        Ricocheted = 4,
        Fragmented = 5
    }

    /// <summary>
    /// Dependency-free three-dimensional vector. Position uses metres, velocity uses metres per
    /// second, and momentum uses kilogram-metres per second according to the property that owns it.
    /// </summary>
    public readonly struct PhysicalVector3 : IEquatable<PhysicalVector3>
    {
        public PhysicalVector3(double x, double y, double z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public double X { get; }

        public double Y { get; }

        public double Z { get; }

        public double MagnitudeSquared
        {
            get { return (X * X) + (Y * Y) + (Z * Z); }
        }

        public double Magnitude
        {
            get { return Math.Sqrt(MagnitudeSquared); }
        }

        public bool IsFinite
        {
            get
            {
                return FiniteDouble.IsFinite(X)
                    && FiniteDouble.IsFinite(Y)
                    && FiniteDouble.IsFinite(Z)
                    && FiniteDouble.IsFinite(MagnitudeSquared);
            }
        }

        public PhysicalVector3 Scale(double factor)
        {
            return new PhysicalVector3(X * factor, Y * factor, Z * factor);
        }

        public PhysicalVector3 Add(PhysicalVector3 other)
        {
            return new PhysicalVector3(X + other.X, Y + other.Y, Z + other.Z);
        }

        public PhysicalVector3 Subtract(PhysicalVector3 other)
        {
            return new PhysicalVector3(X - other.X, Y - other.Y, Z - other.Z);
        }

        public PhysicalVector3 Negate()
        {
            return new PhysicalVector3(-X, -Y, -Z);
        }

        public double Dot(PhysicalVector3 other)
        {
            return (X * other.X) + (Y * other.Y) + (Z * other.Z);
        }

        public PhysicalVector3 Cross(PhysicalVector3 other)
        {
            return new PhysicalVector3(
                (Y * other.Z) - (Z * other.Y),
                (Z * other.X) - (X * other.Z),
                (X * other.Y) - (Y * other.X));
        }

        public bool TryNormalize(out PhysicalVector3 unitVector)
        {
            unitVector = Zero;
            if (!IsFinite)
            {
                return false;
            }

            double magnitude = Magnitude;
            if (!FiniteDouble.IsFinite(magnitude) || magnitude <= 0d)
            {
                return false;
            }

            PhysicalVector3 candidate = Scale(1d / magnitude);
            if (!candidate.IsFinite)
            {
                return false;
            }

            unitVector = candidate;
            return true;
        }

        public bool Equals(PhysicalVector3 other)
        {
            return X.Equals(other.X) && Y.Equals(other.Y) && Z.Equals(other.Z);
        }

        public override bool Equals(object? obj)
        {
            return obj is PhysicalVector3 other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = (hash * 31) + X.GetHashCode();
                hash = (hash * 31) + Y.GetHashCode();
                hash = (hash * 31) + Z.GetHashCode();
                return hash;
            }
        }

        public static bool operator ==(PhysicalVector3 left, PhysicalVector3 right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(PhysicalVector3 left, PhysicalVector3 right)
        {
            return !left.Equals(right);
        }

        public static PhysicalVector3 Zero
        {
            get { return new PhysicalVector3(0d, 0d, 0d); }
        }
    }

    /// <summary>
    /// Unit quaternion describing projectile orientation in world space.
    /// </summary>
    public readonly struct PhysicalOrientation : IEquatable<PhysicalOrientation>
    {
        private const double UnitTolerance = 0.000001d;

        public PhysicalOrientation(double x, double y, double z, double w)
        {
            X = x;
            Y = y;
            Z = z;
            W = w;
        }

        public double X { get; }

        public double Y { get; }

        public double Z { get; }

        public double W { get; }

        public double MagnitudeSquared
        {
            get { return (X * X) + (Y * Y) + (Z * Z) + (W * W); }
        }

        public bool IsFinite
        {
            get
            {
                return FiniteDouble.IsFinite(X)
                    && FiniteDouble.IsFinite(Y)
                    && FiniteDouble.IsFinite(Z)
                    && FiniteDouble.IsFinite(W)
                    && FiniteDouble.IsFinite(MagnitudeSquared);
            }
        }

        public bool IsUnit
        {
            get { return IsFinite && Math.Abs(MagnitudeSquared - 1d) <= UnitTolerance; }
        }

        public PhysicalVector3 LongitudinalAxis
        {
            get
            {
                return new PhysicalVector3(
                    2d * ((X * Z) + (W * Y)),
                    2d * ((Y * Z) - (W * X)),
                    1d - (2d * ((X * X) + (Y * Y))));
            }
        }

        public bool Equals(PhysicalOrientation other)
        {
            return X.Equals(other.X)
                && Y.Equals(other.Y)
                && Z.Equals(other.Z)
                && W.Equals(other.W);
        }

        public override bool Equals(object? obj)
        {
            return obj is PhysicalOrientation other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = (hash * 31) + X.GetHashCode();
                hash = (hash * 31) + Y.GetHashCode();
                hash = (hash * 31) + Z.GetHashCode();
                hash = (hash * 31) + W.GetHashCode();
                return hash;
            }
        }

        public static bool operator ==(PhysicalOrientation left, PhysicalOrientation right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(PhysicalOrientation left, PhysicalOrientation right)
        {
            return !left.Equals(right);
        }

        public static PhysicalOrientation Identity
        {
            get { return new PhysicalOrientation(0d, 0d, 0d, 1d); }
        }

        /// <summary>
        /// Builds the shortest unit rotation from local positive Z to a world-space forward vector.
        /// </summary>
        public static bool TryFromForward(
            PhysicalVector3 forward,
            out PhysicalOrientation orientation)
        {
            orientation = Identity;
            PhysicalVector3 unitForward;
            if (!forward.TryNormalize(out unitForward))
            {
                return false;
            }

            double dot = Math.Max(-1d, Math.Min(1d, unitForward.Z));
            if (dot <= -0.999999999d)
            {
                orientation = new PhysicalOrientation(1d, 0d, 0d, 0d);
                return true;
            }

            double x = -unitForward.Y;
            double y = unitForward.X;
            double z = 0d;
            double w = 1d + dot;
            double magnitudeSquared = (x * x) + (y * y) + (z * z) + (w * w);
            if (!FiniteDouble.IsFinite(magnitudeSquared) || magnitudeSquared <= 0d)
            {
                return false;
            }

            double inverseMagnitude = 1d / Math.Sqrt(magnitudeSquared);
            var candidate = new PhysicalOrientation(
                x * inverseMagnitude,
                y * inverseMagnitude,
                z * inverseMagnitude,
                w * inverseMagnitude);
            if (!candidate.IsUnit)
            {
                return false;
            }

            orientation = candidate;
            return true;
        }

        /// <summary>
        /// Applies a deterministic yaw in the local frame of a unit base orientation. This produces
        /// the component's actual longitudinal attitude; the supplied base local Z normally follows
        /// its velocity.
        /// </summary>
        public static bool TryApplyYaw(
            PhysicalOrientation baseOrientation,
            double yawAngleRadians,
            ulong deterministicSeed,
            out PhysicalOrientation orientation)
        {
            orientation = Identity;
            if (!baseOrientation.IsUnit
                || !FiniteDouble.IsFinite(yawAngleRadians)
                || yawAngleRadians < 0d
                || yawAngleRadians > Math.PI)
            {
                return false;
            }

            if (yawAngleRadians <= 0d)
            {
                orientation = baseOrientation;
                return true;
            }

            const double TwoToThePowerOf53 = 9007199254740992d;
            ulong azimuthBits = MixSeed(deterministicSeed) >> 11;
            double azimuth = (azimuthBits / TwoToThePowerOf53) * (2d * Math.PI);
            double sine = Math.Sin(yawAngleRadians);
            var localLongitudinalAxis = new PhysicalVector3(
                sine * Math.Cos(azimuth),
                sine * Math.Sin(azimuth),
                Math.Cos(yawAngleRadians));
            if (!TryFromForward(localLongitudinalAxis, out PhysicalOrientation localYaw))
            {
                return false;
            }

            return TryMultiply(baseOrientation, localYaw, out orientation);
        }

        /// <summary>
        /// Carries an actual component attitude through the shortest rotation between two measured
        /// flight directions. The relative yaw and roll are retained while EFT advances the shot.
        /// </summary>
        public static bool TryTransport(
            PhysicalOrientation currentOrientation,
            PhysicalVector3 previousForward,
            PhysicalVector3 measuredForward,
            out PhysicalOrientation orientation)
        {
            orientation = Identity;
            if (!currentOrientation.IsUnit
                || !previousForward.TryNormalize(out PhysicalVector3 previousUnit)
                || !measuredForward.TryNormalize(out PhysicalVector3 measuredUnit))
            {
                return false;
            }

            double dot = Math.Max(-1d, Math.Min(1d, previousUnit.Dot(measuredUnit)));
            PhysicalOrientation delta;
            if (dot >= 0.999999999d)
            {
                delta = Identity;
            }
            else if (dot <= -0.999999999d)
            {
                PhysicalVector3 axis = previousUnit.Cross(new PhysicalVector3(1d, 0d, 0d));
                if (!axis.TryNormalize(out PhysicalVector3 normalizedAxis))
                {
                    axis = previousUnit.Cross(new PhysicalVector3(0d, 1d, 0d));
                    if (!axis.TryNormalize(out normalizedAxis))
                    {
                        return false;
                    }
                }

                delta = new PhysicalOrientation(
                    normalizedAxis.X,
                    normalizedAxis.Y,
                    normalizedAxis.Z,
                    0d);
            }
            else
            {
                PhysicalVector3 axis = previousUnit.Cross(measuredUnit);
                var candidate = new PhysicalOrientation(axis.X, axis.Y, axis.Z, 1d + dot);
                if (!TryNormalize(candidate, out delta))
                {
                    return false;
                }
            }

            return TryMultiply(delta, currentOrientation, out orientation);
        }

        private static bool TryMultiply(
            PhysicalOrientation left,
            PhysicalOrientation right,
            out PhysicalOrientation result)
        {
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
            return TryNormalize(candidate, out result);
        }

        private static bool TryNormalize(
            PhysicalOrientation candidate,
            out PhysicalOrientation orientation)
        {
            orientation = Identity;
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

            orientation = normalized;
            return true;
        }

        private static ulong MixSeed(ulong value)
        {
            ulong mixed = unchecked(value + 0x9E3779B97F4A7C15UL);
            mixed = unchecked((mixed ^ (mixed >> 30)) * 0xBF58476D1CE4E5B9UL);
            mixed = unchecked((mixed ^ (mixed >> 27)) * 0x94D049BB133111EBUL);
            return mixed ^ (mixed >> 31);
        }
    }

    public static class PhysicalProjectileGeometry
    {
        public static bool TryCalculateCircularAreaSquareMetres(
            double diameterMetres,
            out double areaSquareMetres)
        {
            areaSquareMetres = 0d;
            if (!FiniteDouble.IsFinite(diameterMetres) || diameterMetres <= 0d)
            {
                return false;
            }

            double radiusMetres = diameterMetres * 0.5d;
            double area = Math.PI * radiusMetres * radiusMetres;
            if (!FiniteDouble.IsFinite(area) || area <= 0d)
            {
                return false;
            }

            areaSquareMetres = area;
            return true;
        }

        public static bool TryCalculateEquivalentDiameterMetres(
            double projectedAreaSquareMetres,
            out double equivalentDiameterMetres)
        {
            equivalentDiameterMetres = 0d;
            if (!FiniteDouble.IsFinite(projectedAreaSquareMetres)
                || projectedAreaSquareMetres <= 0d)
            {
                return false;
            }

            double diameter = Math.Sqrt((4d * projectedAreaSquareMetres) / Math.PI);
            if (!FiniteDouble.IsFinite(diameter) || diameter <= 0d)
            {
                return false;
            }

            equivalentDiameterMetres = diameter;
            return true;
        }
    }
}
