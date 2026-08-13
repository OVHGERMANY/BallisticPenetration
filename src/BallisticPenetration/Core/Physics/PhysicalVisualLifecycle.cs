#nullable enable

using System;
using System.Collections.Generic;

namespace BallisticPenetration.Core.Physics
{
    public enum PhysicalVisualPolicyFailureReason
    {
        None = 0,
        VisibleCapacityInvalid = 1,
        TrackedCapacityInvalid = 2,
        CullingDistanceInvalid = 3,
        DimensionScaleInvalid = 4,
        MinimumDiameterInvalid = 5,
        EmbeddedLifetimeInvalid = 6
    }

    public sealed class PhysicalVisualPolicy
    {
        public const int MinimumVisibleCapacity = 8;
        public const int MaximumVisibleCapacity = 512;
        public const int MinimumTrackedCapacity = 16;
        public const int MaximumTrackedCapacity = 4096;
        public const double MinimumCullingDistanceMetres = 10d;
        public const double MaximumCullingDistanceMetres = 2000d;
        public const double MinimumDimensionScale = 0.25d;
        public const double MaximumDimensionScale = 25d;
        public const double MaximumMinimumDiameterMetres = 0.05d;
        public const double MinimumEmbeddedLifetimeSeconds = 0.25d;
        public const double MaximumEmbeddedLifetimeSeconds = 600d;

        private PhysicalVisualPolicy(
            int maximumVisibleComponents,
            int maximumTrackedComponents,
            double cullingDistanceMetres,
            double dimensionScale,
            double minimumRenderedDiameterMetres,
            double embeddedLifetimeSeconds)
        {
            MaximumVisibleComponents = maximumVisibleComponents;
            MaximumTrackedComponents = maximumTrackedComponents;
            CullingDistanceMetres = cullingDistanceMetres;
            CullingDistanceSquaredMetres = cullingDistanceMetres * cullingDistanceMetres;
            DimensionScale = dimensionScale;
            MinimumRenderedDiameterMetres = minimumRenderedDiameterMetres;
            EmbeddedLifetimeSeconds = embeddedLifetimeSeconds;
        }

        public int MaximumVisibleComponents { get; }

        public int MaximumTrackedComponents { get; }

        public double CullingDistanceMetres { get; }

        public double CullingDistanceSquaredMetres { get; }

        public double DimensionScale { get; }

        public double MinimumRenderedDiameterMetres { get; }

        public double EmbeddedLifetimeSeconds { get; }

        public bool IsWithinCullingDistance(double distanceSquaredMetres)
        {
            return FiniteDouble.IsFinite(distanceSquaredMetres)
                && distanceSquaredMetres >= 0d
                && distanceSquaredMetres <= CullingDistanceSquaredMetres;
        }

        public static bool TryCreate(
            int maximumVisibleComponents,
            int maximumTrackedComponents,
            double cullingDistanceMetres,
            double dimensionScale,
            double minimumRenderedDiameterMetres,
            double embeddedLifetimeSeconds,
            out PhysicalVisualPolicy? policy,
            out PhysicalVisualPolicyFailureReason failureReason)
        {
            policy = null;
            if (maximumVisibleComponents < MinimumVisibleCapacity
                || maximumVisibleComponents > MaximumVisibleCapacity)
            {
                failureReason = PhysicalVisualPolicyFailureReason.VisibleCapacityInvalid;
                return false;
            }

            if (maximumTrackedComponents < MinimumTrackedCapacity
                || maximumTrackedComponents > MaximumTrackedCapacity
                || maximumTrackedComponents < maximumVisibleComponents)
            {
                failureReason = PhysicalVisualPolicyFailureReason.TrackedCapacityInvalid;
                return false;
            }

            if (!FiniteDouble.IsFinite(cullingDistanceMetres)
                || cullingDistanceMetres < MinimumCullingDistanceMetres
                || cullingDistanceMetres > MaximumCullingDistanceMetres)
            {
                failureReason = PhysicalVisualPolicyFailureReason.CullingDistanceInvalid;
                return false;
            }

            if (!FiniteDouble.IsFinite(dimensionScale)
                || dimensionScale < MinimumDimensionScale
                || dimensionScale > MaximumDimensionScale)
            {
                failureReason = PhysicalVisualPolicyFailureReason.DimensionScaleInvalid;
                return false;
            }

            if (!FiniteDouble.IsFinite(minimumRenderedDiameterMetres)
                || minimumRenderedDiameterMetres < 0d
                || minimumRenderedDiameterMetres > MaximumMinimumDiameterMetres)
            {
                failureReason = PhysicalVisualPolicyFailureReason.MinimumDiameterInvalid;
                return false;
            }

            if (!FiniteDouble.IsFinite(embeddedLifetimeSeconds)
                || embeddedLifetimeSeconds < MinimumEmbeddedLifetimeSeconds
                || embeddedLifetimeSeconds > MaximumEmbeddedLifetimeSeconds)
            {
                failureReason = PhysicalVisualPolicyFailureReason.EmbeddedLifetimeInvalid;
                return false;
            }

            policy = new PhysicalVisualPolicy(
                maximumVisibleComponents,
                maximumTrackedComponents,
                cullingDistanceMetres,
                dimensionScale,
                minimumRenderedDiameterMetres,
                embeddedLifetimeSeconds);
            failureReason = PhysicalVisualPolicyFailureReason.None;
            return true;
        }
    }

    public readonly struct PhysicalVisualLease : IEquatable<PhysicalVisualLease>
    {
        internal PhysicalVisualLease(int slot, ulong generation, long ownerToken)
        {
            Slot = slot;
            Generation = generation;
            OwnerToken = ownerToken;
        }

        public int Slot { get; }

        public ulong Generation { get; }

        public long OwnerToken { get; }

        public bool Equals(PhysicalVisualLease other)
        {
            return Slot == other.Slot
                && Generation == other.Generation
                && OwnerToken == other.OwnerToken;
        }

        public override bool Equals(object? obj)
        {
            return obj is PhysicalVisualLease other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Slot;
                hash = (hash * 397) ^ Generation.GetHashCode();
                hash = (hash * 397) ^ OwnerToken.GetHashCode();
                return hash;
            }
        }

        public static bool operator ==(PhysicalVisualLease left, PhysicalVisualLease right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(PhysicalVisualLease left, PhysicalVisualLease right)
        {
            return !left.Equals(right);
        }
    }

    /// <summary>
    /// Dependency-free slot ledger. A slot can be changed only by the exact owner and generation
    /// that acquired it, so a late cleanup cannot disable a component that reused the same slot.
    /// </summary>
    public sealed class PhysicalVisualOwnershipLedger
    {
        private readonly long[] _owners;
        private readonly ulong[] _generations;
        private readonly SortedSet<int> _freeSlots;

        public PhysicalVisualOwnershipLedger(int capacity)
        {
            if (capacity <= 0 || capacity > PhysicalVisualPolicy.MaximumVisibleCapacity)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity));
            }

            _owners = new long[capacity];
            _generations = new ulong[capacity];
            _freeSlots = new SortedSet<int>();
            for (int slot = 0; slot < capacity; slot++)
            {
                _freeSlots.Add(slot);
            }
        }

        public int Capacity
        {
            get { return _owners.Length; }
        }

        public int ActiveCount
        {
            get { return Capacity - _freeSlots.Count; }
        }

        public bool TryAcquire(long ownerToken, out PhysicalVisualLease lease)
        {
            return TryAcquire(ownerToken, Capacity, out lease);
        }

        public bool TryAcquire(
            long ownerToken,
            int allowedCapacity,
            out PhysicalVisualLease lease)
        {
            lease = default;
            if (ownerToken <= 0
                || allowedCapacity <= 0
                || allowedCapacity > Capacity
                || _freeSlots.Count == 0)
            {
                return false;
            }

            int slot = -1;
            foreach (int candidate in _freeSlots)
            {
                if (candidate >= allowedCapacity)
                {
                    break;
                }

                slot = candidate;
                break;
            }

            if (slot < 0)
            {
                return false;
            }

            _freeSlots.Remove(slot);
            ulong generation = _generations[slot] == ulong.MaxValue
                ? 1UL
                : _generations[slot] + 1UL;
            _generations[slot] = generation;
            _owners[slot] = ownerToken;
            lease = new PhysicalVisualLease(slot, generation, ownerToken);
            return true;
        }

        public bool IsCurrent(PhysicalVisualLease lease)
        {
            return lease.Slot >= 0
                && lease.Slot < Capacity
                && lease.OwnerToken > 0
                && _owners[lease.Slot] == lease.OwnerToken
                && _generations[lease.Slot] == lease.Generation;
        }

        public bool Release(PhysicalVisualLease lease)
        {
            if (!IsCurrent(lease))
            {
                return false;
            }

            _owners[lease.Slot] = 0L;
            _freeSlots.Add(lease.Slot);
            return true;
        }

        public void Reset()
        {
            _freeSlots.Clear();
            for (int slot = 0; slot < Capacity; slot++)
            {
                _owners[slot] = 0L;
                _freeSlots.Add(slot);
            }
        }
    }
}
