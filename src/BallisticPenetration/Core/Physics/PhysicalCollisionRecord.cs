#nullable enable

using System;
using BallisticPenetration.Core;

namespace BallisticPenetration.Core.Physics
{
    public enum PhysicalCollisionRecordFailureReason
    {
        None = 0,
        CollisionIdMissing = 1,
        MaterialIdMissing = 2,
        MaterialClassInvalid = 3,
        SequenceNegative = 4,
        PositionInvalid = 5,
        IncomingVelocityInvalid = 6,
        OutgoingVelocityInvalid = 7,
        IncomingEnergyInvalid = 8,
        OutgoingEnergyInvalid = 9,
        OutgoingEnergyExceedsIncoming = 10,
        ImpactAngleInvalid = 11,
        EffectivePathLengthInvalid = 12,
        OutcomeInvalid = 13
    }

    public sealed class PhysicalCollisionRecordInput
    {
        public string? CollisionId { get; set; }

        public string? MaterialId { get; set; }

        public PhysicalMaterialClass MaterialClass { get; set; }

        public int Sequence { get; set; }

        public PhysicalVector3 PositionMetres { get; set; }

        public PhysicalVector3 IncomingVelocityMetresPerSecond { get; set; }

        public PhysicalVector3 OutgoingVelocityMetresPerSecond { get; set; }

        public double IncomingTranslationalEnergyJoules { get; set; }

        public double OutgoingTranslationalEnergyJoules { get; set; }

        /// <summary>
        /// Impact obliquity in radians: zero is normal impact and pi/2 is grazing.
        /// </summary>
        public double ImpactAngleRadians { get; set; }

        public double EffectivePathLengthMetres { get; set; }

        public PhysicalCollisionOutcome Outcome { get; set; }
    }

    public sealed class PhysicalCollisionRecord : IEquatable<PhysicalCollisionRecord>
    {
        private const double RelativeEnergyTolerance = 0.000000001d;

        private PhysicalCollisionRecord(
            PhysicalCollisionRecordInput input,
            string collisionId,
            string materialId)
        {
            CollisionId = collisionId;
            MaterialId = materialId;
            MaterialClass = input.MaterialClass;
            Sequence = input.Sequence;
            PositionMetres = input.PositionMetres;
            IncomingVelocityMetresPerSecond = input.IncomingVelocityMetresPerSecond;
            OutgoingVelocityMetresPerSecond = input.OutgoingVelocityMetresPerSecond;
            IncomingTranslationalEnergyJoules = input.IncomingTranslationalEnergyJoules;
            OutgoingTranslationalEnergyJoules = input.OutgoingTranslationalEnergyJoules;
            ImpactAngleRadians = input.ImpactAngleRadians;
            EffectivePathLengthMetres = input.EffectivePathLengthMetres;
            Outcome = input.Outcome;
        }

        public string CollisionId { get; }

        public string MaterialId { get; }

        public PhysicalMaterialClass MaterialClass { get; }

        public int Sequence { get; }

        public PhysicalVector3 PositionMetres { get; }

        public PhysicalVector3 IncomingVelocityMetresPerSecond { get; }

        public PhysicalVector3 OutgoingVelocityMetresPerSecond { get; }

        public double IncomingTranslationalEnergyJoules { get; }

        public double OutgoingTranslationalEnergyJoules { get; }

        public double ImpactAngleRadians { get; }

        public double EffectivePathLengthMetres { get; }

        public PhysicalCollisionOutcome Outcome { get; }

        public static bool TryCreate(
            PhysicalCollisionRecordInput? input,
            out PhysicalCollisionRecord? record,
            out PhysicalCollisionRecordFailureReason failureReason)
        {
            record = null;
            if (input == null)
            {
                failureReason = PhysicalCollisionRecordFailureReason.CollisionIdMissing;
                return false;
            }

            string? collisionId = input.CollisionId;
            if (string.IsNullOrWhiteSpace(collisionId))
            {
                failureReason = PhysicalCollisionRecordFailureReason.CollisionIdMissing;
                return false;
            }

            string? materialId = input.MaterialId;
            if (string.IsNullOrWhiteSpace(materialId))
            {
                failureReason = PhysicalCollisionRecordFailureReason.MaterialIdMissing;
                return false;
            }

            if (!IsValidMaterialClass(input.MaterialClass))
            {
                failureReason = PhysicalCollisionRecordFailureReason.MaterialClassInvalid;
                return false;
            }

            if (input.Sequence < 0)
            {
                failureReason = PhysicalCollisionRecordFailureReason.SequenceNegative;
                return false;
            }

            if (!input.PositionMetres.IsFinite)
            {
                failureReason = PhysicalCollisionRecordFailureReason.PositionInvalid;
                return false;
            }

            if (!input.IncomingVelocityMetresPerSecond.IsFinite)
            {
                failureReason = PhysicalCollisionRecordFailureReason.IncomingVelocityInvalid;
                return false;
            }

            if (!input.OutgoingVelocityMetresPerSecond.IsFinite)
            {
                failureReason = PhysicalCollisionRecordFailureReason.OutgoingVelocityInvalid;
                return false;
            }

            if (!IsFiniteNonNegative(input.IncomingTranslationalEnergyJoules))
            {
                failureReason = PhysicalCollisionRecordFailureReason.IncomingEnergyInvalid;
                return false;
            }

            if (!IsFiniteNonNegative(input.OutgoingTranslationalEnergyJoules))
            {
                failureReason = PhysicalCollisionRecordFailureReason.OutgoingEnergyInvalid;
                return false;
            }

            double allowedEnergy = input.IncomingTranslationalEnergyJoules
                + (Math.Max(1d, input.IncomingTranslationalEnergyJoules) * RelativeEnergyTolerance);
            if (input.OutgoingTranslationalEnergyJoules > allowedEnergy)
            {
                failureReason = PhysicalCollisionRecordFailureReason.OutgoingEnergyExceedsIncoming;
                return false;
            }

            if (!FiniteDouble.IsFinite(input.ImpactAngleRadians)
                || input.ImpactAngleRadians < 0d
                || input.ImpactAngleRadians > (Math.PI * 0.5d))
            {
                failureReason = PhysicalCollisionRecordFailureReason.ImpactAngleInvalid;
                return false;
            }

            if (!IsFiniteNonNegative(input.EffectivePathLengthMetres))
            {
                failureReason = PhysicalCollisionRecordFailureReason.EffectivePathLengthInvalid;
                return false;
            }

            if (input.Outcome < PhysicalCollisionOutcome.Penetrated
                || input.Outcome > PhysicalCollisionOutcome.Fragmented)
            {
                failureReason = PhysicalCollisionRecordFailureReason.OutcomeInvalid;
                return false;
            }

            record = new PhysicalCollisionRecord(input, collisionId, materialId);
            failureReason = PhysicalCollisionRecordFailureReason.None;
            return true;
        }

        public bool Equals(PhysicalCollisionRecord? other)
        {
            return !ReferenceEquals(other, null)
                && string.Equals(CollisionId, other.CollisionId, StringComparison.Ordinal)
                && string.Equals(MaterialId, other.MaterialId, StringComparison.Ordinal)
                && MaterialClass == other.MaterialClass
                && Sequence == other.Sequence
                && PositionMetres == other.PositionMetres
                && IncomingVelocityMetresPerSecond == other.IncomingVelocityMetresPerSecond
                && OutgoingVelocityMetresPerSecond == other.OutgoingVelocityMetresPerSecond
                && IncomingTranslationalEnergyJoules.Equals(
                    other.IncomingTranslationalEnergyJoules)
                && OutgoingTranslationalEnergyJoules.Equals(
                    other.OutgoingTranslationalEnergyJoules)
                && ImpactAngleRadians.Equals(other.ImpactAngleRadians)
                && EffectivePathLengthMetres.Equals(other.EffectivePathLengthMetres)
                && Outcome == other.Outcome;
        }

        public override bool Equals(object? obj)
        {
            return Equals(obj as PhysicalCollisionRecord);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = StringComparer.Ordinal.GetHashCode(CollisionId);
                hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(MaterialId);
                hash = (hash * 397) ^ (int)MaterialClass;
                hash = (hash * 397) ^ Sequence;
                hash = (hash * 397) ^ PositionMetres.GetHashCode();
                hash = (hash * 397) ^ IncomingVelocityMetresPerSecond.GetHashCode();
                hash = (hash * 397) ^ OutgoingVelocityMetresPerSecond.GetHashCode();
                hash = (hash * 397) ^ IncomingTranslationalEnergyJoules.GetHashCode();
                hash = (hash * 397) ^ OutgoingTranslationalEnergyJoules.GetHashCode();
                hash = (hash * 397) ^ ImpactAngleRadians.GetHashCode();
                hash = (hash * 397) ^ EffectivePathLengthMetres.GetHashCode();
                hash = (hash * 397) ^ (int)Outcome;
                return hash;
            }
        }

        public static bool operator ==(
            PhysicalCollisionRecord? left,
            PhysicalCollisionRecord? right)
        {
            if (ReferenceEquals(left, right))
            {
                return true;
            }

            return !ReferenceEquals(left, null) && left.Equals(right);
        }

        public static bool operator !=(
            PhysicalCollisionRecord? left,
            PhysicalCollisionRecord? right)
        {
            return !(left == right);
        }

        private static bool IsFiniteNonNegative(double value)
        {
            return FiniteDouble.IsFinite(value) && value >= 0d;
        }

        private static bool IsValidMaterialClass(PhysicalMaterialClass materialClass)
        {
            return materialClass >= PhysicalMaterialClass.Unknown
                && materialClass <= PhysicalMaterialClass.Titanium;
        }
    }
}
