#nullable enable

using System;
using System.Collections.Generic;
using BallisticPenetration.Core.Physics;

namespace BallisticPenetration.Core.Diagnostics
{
    internal enum PhysicalLifecycleTerminalReason
    {
        Stopped = 0,
        Replaced = 1,
        Aborted = 2,
        Shutdown = 3
    }

    internal enum PhysicalLifecycleTerminalDisposition
    {
        Untracked = 0,
        Canonical = 1,
        Duplicate = 2
    }

    internal sealed class PhysicalLifecycleSnapshot
    {
        internal PhysicalLifecycleSnapshot(
            string projectileIdentity,
            string rootIdentity,
            string projectileKind,
            int fragmentIndex,
            int fragmentGeneration,
            double creationTimestamp,
            PhysicalVector3 lastKnownPosition,
            PhysicalVector3 lastKnownVelocity,
            string lastCollisionIdentity,
            int lastCollisionOrdinal)
        {
            if (string.IsNullOrWhiteSpace(projectileIdentity))
            {
                throw new ArgumentException("A physical projectile identity is required.", nameof(projectileIdentity));
            }

            ProjectileIdentity = projectileIdentity;
            RootIdentity = rootIdentity ?? string.Empty;
            ProjectileKind = projectileKind ?? string.Empty;
            FragmentIndex = fragmentIndex;
            FragmentGeneration = fragmentGeneration;
            CreationTimestamp = creationTimestamp;
            LastKnownPosition = lastKnownPosition;
            LastKnownVelocity = lastKnownVelocity;
            LastCollisionIdentity = lastCollisionIdentity ?? string.Empty;
            LastCollisionOrdinal = lastCollisionOrdinal;
        }

        internal string ProjectileIdentity { get; }

        internal string RootIdentity { get; }

        internal string ProjectileKind { get; }

        internal int FragmentIndex { get; }

        internal int FragmentGeneration { get; }

        internal double CreationTimestamp { get; }

        internal PhysicalVector3 LastKnownPosition { get; }

        internal PhysicalVector3 LastKnownVelocity { get; }

        internal double LastKnownSpeed => LastKnownVelocity.Magnitude;

        internal string LastCollisionIdentity { get; }

        internal int LastCollisionOrdinal { get; }

        internal PhysicalLifecycleSnapshot WithObservation(
            PhysicalVector3 position,
            PhysicalVector3 velocity,
            string? collisionIdentity,
            int? collisionOrdinal)
        {
            return new PhysicalLifecycleSnapshot(
                ProjectileIdentity,
                RootIdentity,
                ProjectileKind,
                FragmentIndex,
                FragmentGeneration,
                CreationTimestamp,
                position,
                velocity,
                string.IsNullOrWhiteSpace(collisionIdentity)
                    ? LastCollisionIdentity
                    : collisionIdentity,
                collisionOrdinal ?? LastCollisionOrdinal);
        }
    }

    internal sealed class PhysicalLifecycleTombstone
    {
        internal PhysicalLifecycleTombstone(
            PhysicalLifecycleSnapshot snapshot,
            PhysicalLifecycleTerminalReason? terminalReason,
            string violationReason,
            double terminalTimestamp)
        {
            Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
            TerminalReason = terminalReason;
            ViolationReason = violationReason ?? string.Empty;
            TerminalTimestamp = terminalTimestamp;
        }

        internal PhysicalLifecycleSnapshot Snapshot { get; }

        internal PhysicalLifecycleTerminalReason? TerminalReason { get; }

        internal string ViolationReason { get; }

        internal double TerminalTimestamp { get; }

        internal bool MarksMissingTerminal => !TerminalReason.HasValue;
    }

    internal sealed class PhysicalLifecycleTerminalAttempt
    {
        internal PhysicalLifecycleTerminalAttempt(
            PhysicalLifecycleTerminalDisposition disposition,
            PhysicalLifecycleTombstone? tombstone)
        {
            Disposition = disposition;
            Tombstone = tombstone;
        }

        internal PhysicalLifecycleTerminalDisposition Disposition { get; }

        internal PhysicalLifecycleTombstone? Tombstone { get; }
    }

    internal sealed class PhysicalLifecycleMissingTerminal
    {
        internal PhysicalLifecycleMissingTerminal(
            PhysicalLifecycleSnapshot snapshot,
            string removalReason,
            PhysicalLifecycleTombstone tombstone)
        {
            Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
            RemovalReason = removalReason ?? string.Empty;
            Tombstone = tombstone ?? throw new ArgumentNullException(nameof(tombstone));
        }

        internal PhysicalLifecycleSnapshot Snapshot { get; }

        internal string RemovalReason { get; }

        internal PhysicalLifecycleTombstone Tombstone { get; }
    }

    internal sealed class PhysicalProjectileLifecycleTracker
    {
        internal const int DefaultTerminalTombstoneCapacity = 1024;

        private readonly int _terminalTombstoneCapacity;
        private readonly Dictionary<string, ActiveEntry> _activeByIdentity =
            new Dictionary<string, ActiveEntry>(StringComparer.Ordinal);
        private readonly Dictionary<string, PhysicalLifecycleTombstone> _tombstonesByIdentity =
            new Dictionary<string, PhysicalLifecycleTombstone>(StringComparer.Ordinal);
        private readonly Queue<string> _tombstoneOrder = new Queue<string>();
        private long _nextCreationSequence;

        internal PhysicalProjectileLifecycleTracker(
            int terminalTombstoneCapacity = DefaultTerminalTombstoneCapacity)
        {
            if (terminalTombstoneCapacity <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(terminalTombstoneCapacity),
                    "Terminal tombstone capacity must be positive.");
            }

            _terminalTombstoneCapacity = terminalTombstoneCapacity;
        }

        internal int ActiveCount => _activeByIdentity.Count;

        internal int TombstoneCount => _tombstonesByIdentity.Count;

        internal int DuplicateTerminalViolationCount { get; private set; }

        internal int MissingTerminalViolationCount { get; private set; }

        internal bool IsActive(string projectileIdentity)
        {
            return !string.IsNullOrWhiteSpace(projectileIdentity)
                && _activeByIdentity.ContainsKey(projectileIdentity);
        }

        internal bool TryGetActiveSnapshot(
            string projectileIdentity,
            out PhysicalLifecycleSnapshot? snapshot)
        {
            snapshot = null;
            if (string.IsNullOrWhiteSpace(projectileIdentity)
                || !_activeByIdentity.TryGetValue(projectileIdentity, out ActiveEntry? active))
            {
                return false;
            }

            snapshot = active.Snapshot;
            return true;
        }

        internal bool TryRegister(PhysicalLifecycleSnapshot snapshot)
        {
            if (snapshot == null
                || _activeByIdentity.ContainsKey(snapshot.ProjectileIdentity)
                || _tombstonesByIdentity.ContainsKey(snapshot.ProjectileIdentity))
            {
                return false;
            }

            _activeByIdentity.Add(
                snapshot.ProjectileIdentity,
                new ActiveEntry(_nextCreationSequence++, snapshot));
            return true;
        }

        internal bool TryObserve(
            string projectileIdentity,
            PhysicalVector3 position,
            PhysicalVector3 velocity,
            string? collisionIdentity,
            int? collisionOrdinal)
        {
            if (string.IsNullOrWhiteSpace(projectileIdentity)
                || !_activeByIdentity.TryGetValue(projectileIdentity, out ActiveEntry? active))
            {
                return false;
            }

            active.Snapshot = active.Snapshot.WithObservation(
                position,
                velocity,
                collisionIdentity,
                collisionOrdinal);
            return true;
        }

        internal PhysicalLifecycleTerminalAttempt TryTerminate(
            string projectileIdentity,
            PhysicalLifecycleTerminalReason attemptedReason,
            double terminalTimestamp)
        {
            if (string.IsNullOrWhiteSpace(projectileIdentity))
            {
                return new PhysicalLifecycleTerminalAttempt(
                    PhysicalLifecycleTerminalDisposition.Untracked,
                    null);
            }

            if (_tombstonesByIdentity.TryGetValue(
                    projectileIdentity,
                    out PhysicalLifecycleTombstone? firstTerminal))
            {
                DuplicateTerminalViolationCount++;
                return new PhysicalLifecycleTerminalAttempt(
                    PhysicalLifecycleTerminalDisposition.Duplicate,
                    firstTerminal);
            }

            if (!_activeByIdentity.TryGetValue(projectileIdentity, out ActiveEntry? active))
            {
                return new PhysicalLifecycleTerminalAttempt(
                    PhysicalLifecycleTerminalDisposition.Untracked,
                    null);
            }

            _activeByIdentity.Remove(projectileIdentity);
            var tombstone = new PhysicalLifecycleTombstone(
                active.Snapshot,
                attemptedReason,
                string.Empty,
                terminalTimestamp);
            AddTombstone(tombstone);
            return new PhysicalLifecycleTerminalAttempt(
                PhysicalLifecycleTerminalDisposition.Canonical,
                tombstone);
        }

        internal PhysicalLifecycleMissingTerminal? RemoveWithoutTerminal(
            string projectileIdentity,
            string removalReason,
            double removalTimestamp)
        {
            if (string.IsNullOrWhiteSpace(projectileIdentity)
                || !_activeByIdentity.TryGetValue(projectileIdentity, out ActiveEntry? active))
            {
                return null;
            }

            _activeByIdentity.Remove(projectileIdentity);
            MissingTerminalViolationCount++;
            var tombstone = new PhysicalLifecycleTombstone(
                active.Snapshot,
                null,
                removalReason,
                removalTimestamp);
            AddTombstone(tombstone);
            return new PhysicalLifecycleMissingTerminal(active.Snapshot, removalReason, tombstone);
        }

        internal IReadOnlyList<PhysicalLifecycleSnapshot> CloseActiveForShutdown(double terminalTimestamp)
        {
            var entries = new List<ActiveEntry>(_activeByIdentity.Values);
            entries.Sort(CompareActiveEntries);
            var closed = new List<PhysicalLifecycleSnapshot>(entries.Count);
            for (int index = 0; index < entries.Count; index++)
            {
                ActiveEntry active = entries[index];
                closed.Add(active.Snapshot);
                AddTombstone(
                    new PhysicalLifecycleTombstone(
                        active.Snapshot,
                        PhysicalLifecycleTerminalReason.Shutdown,
                        string.Empty,
                        terminalTimestamp));
            }

            _activeByIdentity.Clear();
            return closed;
        }

        internal bool ContainsTombstone(string projectileIdentity)
        {
            return !string.IsNullOrWhiteSpace(projectileIdentity)
                && _tombstonesByIdentity.ContainsKey(projectileIdentity);
        }

        internal void Clear()
        {
            _activeByIdentity.Clear();
            _tombstonesByIdentity.Clear();
            _tombstoneOrder.Clear();
            DuplicateTerminalViolationCount = 0;
            MissingTerminalViolationCount = 0;
            _nextCreationSequence = 0;
        }

        private void AddTombstone(PhysicalLifecycleTombstone tombstone)
        {
            string projectileIdentity = tombstone.Snapshot.ProjectileIdentity;
            while (_tombstonesByIdentity.Count >= _terminalTombstoneCapacity)
            {
                string evictedIdentity = _tombstoneOrder.Dequeue();
                _tombstonesByIdentity.Remove(evictedIdentity);
            }

            _tombstonesByIdentity.Add(projectileIdentity, tombstone);
            _tombstoneOrder.Enqueue(projectileIdentity);
        }

        private static int CompareActiveEntries(ActiveEntry left, ActiveEntry right)
        {
            int sequenceComparison = left.CreationSequence.CompareTo(right.CreationSequence);
            return sequenceComparison != 0
                ? sequenceComparison
                : string.Compare(
                    left.Snapshot.ProjectileIdentity,
                    right.Snapshot.ProjectileIdentity,
                    StringComparison.Ordinal);
        }

        private sealed class ActiveEntry
        {
            internal ActiveEntry(long creationSequence, PhysicalLifecycleSnapshot snapshot)
            {
                CreationSequence = creationSequence;
                Snapshot = snapshot;
            }

            internal long CreationSequence { get; }

            internal PhysicalLifecycleSnapshot Snapshot { get; set; }
        }
    }
}
