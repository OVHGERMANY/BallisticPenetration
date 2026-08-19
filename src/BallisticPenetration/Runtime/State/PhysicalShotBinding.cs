#nullable enable

using System;
using System.Runtime.CompilerServices;
using BallisticPenetration.Core.Physics;
using BallisticPenetration.Runtime.Diagnostics;
using EFT.Ballistics;
using EFT.InventoryLogic;
using UnityEngine;

namespace BallisticPenetration.Runtime.State
{
    /// <summary>
    /// Immutable physical state and the EFT values represented by it at the beginning of one flight
    /// leg. A replacement binding is installed after every validated state transition.
    /// </summary>
    internal sealed class PhysicalShotBinding
    {
        internal PhysicalShotBinding(
            Shot shot,
            PhysicalProjectileState state,
            float eftDamage,
            float eftPenetrationPower,
            float eftBallisticCoefficient,
            bool targetWasAlreadyDead)
        {
            if (shot == null)
            {
                throw new ArgumentNullException(nameof(shot));
            }

            State = state ?? throw new ArgumentNullException(nameof(state));
            if (!IsFiniteNonNegative(eftDamage)
                || !IsFiniteNonNegative(eftPenetrationPower)
                || !IsFinitePositive(eftBallisticCoefficient))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(eftDamage),
                    "EFT reference values must be finite and the ballistic coefficient must be positive.");
            }

            EftDamage = eftDamage;
            EftPenetrationPower = eftPenetrationPower;
            EftBallisticCoefficient = eftBallisticCoefficient;
            CreationTimeSeconds = Time.realtimeSinceStartupAsDouble;
            CreationPosition = shot.CurrentPosition;
            CreationVelocity = shot.CurrentVelocity;
            TargetWasAlreadyDead = targetWasAlreadyDead;
            Incarnation = new PhysicalShotIncarnation(shot);
        }

        internal PhysicalProjectileState State { get; }

        internal float EftDamage { get; }

        internal float EftPenetrationPower { get; }

        internal float EftBallisticCoefficient { get; }

        internal double CreationTimeSeconds { get; }

        internal Vector3 CreationPosition { get; }

        internal Vector3 CreationVelocity { get; }

        internal bool TargetWasAlreadyDead { get; }

        internal PhysicalShotIncarnation Incarnation { get; }

        internal bool Matches(Shot shot)
        {
            return Incarnation.Matches(shot);
        }

        private static bool IsFinitePositive(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;
        }

        private static bool IsFiniteNonNegative(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f;
        }
    }

    /// <summary>
    /// Snapshot used to reject a ConditionalWeakTable entry after EFT recycles the same Shot object.
    /// No single mutable field is treated as a generation counter; all stable creation fields must
    /// still describe the same flight leg.
    /// </summary>
    internal readonly struct PhysicalShotIncarnation
    {
        private readonly Item? _ammo;
        private readonly Item? _weapon;
        private readonly Shot? _parent;
        private readonly object? _trajectoryInfo;
        private readonly int _fragmentIndex;
        private readonly int _randomSeed;
        private readonly int _fireIndex;
        private readonly Vector3 _startPosition;
        private readonly Vector3 _startVelocity;
        private readonly Vector3 _masterOrigin;
        private readonly string? _playerProfileId;

        internal PhysicalShotIncarnation(Shot shot)
        {
            _ammo = shot.Ammo;
            _weapon = shot.Weapon;
            _parent = shot.Parent;
            _trajectoryInfo = shot.TrajectoryInfo;
            _fragmentIndex = shot.FragmentIndex;
            _randomSeed = shot.RandomSeed;
            _fireIndex = shot.FireIndex;
            _startPosition = shot.StartPosition;
            _startVelocity = shot.StartVelocity;
            _masterOrigin = shot.MasterOrigin;
            _playerProfileId = shot.PlayerProfileID;
        }

        internal bool Matches(Shot shot)
        {
            return shot != null
                && ReferenceEquals(shot.Ammo, _ammo)
                && ReferenceEquals(shot.Weapon, _weapon)
                && ReferenceEquals(shot.Parent, _parent)
                && ReferenceEquals(shot.TrajectoryInfo, _trajectoryInfo)
                && shot.FragmentIndex == _fragmentIndex
                && shot.RandomSeed == _randomSeed
                && shot.FireIndex == _fireIndex
                && shot.StartPosition.Equals(_startPosition)
                && shot.StartVelocity.Equals(_startVelocity)
                && shot.MasterOrigin.Equals(_masterOrigin)
                && string.Equals(shot.PlayerProfileID, _playerProfileId, StringComparison.Ordinal);
        }
    }

    internal static class PhysicalShotBindingStore
    {
        private static readonly object Gate = new object();
        private static readonly ConditionalWeakTable<Shot, PhysicalShotBinding> Bindings =
            new ConditionalWeakTable<Shot, PhysicalShotBinding>();

        internal static PhysicalShotBinding Set(
            Shot shot,
            PhysicalProjectileState state,
            float eftDamage,
            float eftPenetrationPower,
            float eftBallisticCoefficient,
            bool targetWasAlreadyDead)
        {
            if (shot == null)
            {
                throw new ArgumentNullException(nameof(shot));
            }

            var binding = new PhysicalShotBinding(
                shot,
                state,
                eftDamage,
                eftPenetrationPower,
                eftBallisticCoefficient,
                targetWasAlreadyDead);

            if (!binding.Matches(shot))
            {
                throw new InvalidOperationException("Physical shot binding does not match the current shot incarnation.");
            }

            PhysicalShotBinding? displaced = null;
            lock (Gate)
            {
                Bindings.TryGetValue(shot, out displaced);
                Bindings.Remove(shot);
                Bindings.Add(shot, binding);
            }

            if (displaced != null)
            {
                PhysicalProjectileLifecycleDiagnostics.RecordRemoval(
                    shot,
                    displaced,
                    "binding-overwritten");
            }

            return binding;
        }

        internal static bool TryGet(Shot shot, out PhysicalShotBinding? binding)
        {
            binding = null;
            if (shot == null)
            {
                return false;
            }

            PhysicalShotBinding? removed = null;
            lock (Gate)
            {
                if (!Bindings.TryGetValue(shot, out PhysicalShotBinding stored))
                {
                    return false;
                }

                if (!stored.Matches(shot))
                {
                    Bindings.Remove(shot);
                    removed = stored;
                }
                else
                {
                    binding = stored;
                }
            }

            if (removed != null)
            {
                PhysicalProjectileLifecycleDiagnostics.RecordRemoval(
                    shot,
                    removed,
                    "binding-incarnation-mismatch");
                return false;
            }

            return binding != null;
        }

        internal static void RemoveIfSame(Shot shot, PhysicalShotBinding expected)
        {
            if (shot == null || expected == null)
            {
                return;
            }

            bool removed = false;
            lock (Gate)
            {
                if (Bindings.TryGetValue(shot, out PhysicalShotBinding current)
                    && ReferenceEquals(current, expected))
                {
                    Bindings.Remove(shot);
                    removed = true;
                }
            }

            if (removed)
            {
                PhysicalProjectileLifecycleDiagnostics.RecordRemoval(
                    shot,
                    expected,
                    "binding-remove-if-same");
            }
        }
    }
}
