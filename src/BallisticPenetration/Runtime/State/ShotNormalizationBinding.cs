#nullable enable

using System;
using System.Globalization;
using BallisticPenetration.Core;
using BallisticPenetration.Core.Physics;
using EFT.Ballistics;
using EFT.InventoryLogic;
using UnityEngine;

namespace BallisticPenetration.Runtime.State
{
    internal sealed class ShotNormalizationBinding
    {
        internal ShotNormalizationBinding(Shot shot, BallisticNormalizationState state)
        {
            if (shot == null)
            {
                throw new ArgumentNullException(nameof(shot));
            }

            State = state ?? throw new ArgumentNullException(nameof(state));
            Incarnation = new PhysicalShotIncarnation(shot);
        }

        internal BallisticNormalizationState State { get; }

        internal PhysicalShotIncarnation Incarnation { get; }

        internal bool Matches(Shot shot)
        {
            return Incarnation.Matches(shot);
        }
    }

    /// <summary>
    /// Exact-shot ownership for speed normalization. Immutable state is replaced only after both EFT
    /// fields have been written; an incarnation mismatch removes stale pooled-shot state.
    /// </summary>
    internal static class ShotNormalizationBindingStore
    {
        private static readonly PoolSafeReferenceBindingStore<Shot, ShotNormalizationBinding>
            Bindings = new PoolSafeReferenceBindingStore<Shot, ShotNormalizationBinding>(
                delegate (ShotNormalizationBinding binding, Shot shot)
                {
                    return binding.Matches(shot);
                });

        internal static bool TryGetOrCreateRoot(
            Shot shot,
            out ShotNormalizationBinding? binding)
        {
            binding = null;
            if (shot == null)
            {
                return false;
            }

            if (TryGet(shot, out binding) && binding != null)
            {
                return true;
            }

            string rootId = CreateRootId(shot);
            if (!BallisticNormalizationState.TryCreateRoot(
                    rootId,
                    rootId,
                    out BallisticNormalizationState? state,
                    out _)
                || state == null)
            {
                return false;
            }

            var candidate = new ShotNormalizationBinding(shot, state);
            return Bindings.TryGetOrSet(shot, candidate, out binding);
        }

        internal static bool TryGet(Shot shot, out ShotNormalizationBinding? binding)
        {
            binding = null;
            if (shot == null)
            {
                return false;
            }

            return Bindings.TryGet(shot, out binding);
        }

        internal static bool TryCommit(
            Shot shot,
            ShotNormalizationBinding expected,
            BallisticNormalizationState nextState,
            out ShotNormalizationBinding? committed)
        {
            committed = null;
            if (shot == null || expected == null || nextState == null)
            {
                return false;
            }

            var replacement = new ShotNormalizationBinding(shot, nextState);
            return Bindings.TryReplace(shot, expected, replacement, out committed);
        }

        internal static bool TrySetDerivedChild(
            Shot child,
            BallisticNormalizationState parentState)
        {
            if (child == null || parentState == null)
            {
                return false;
            }

            string componentId = CreateDerivedComponentId(parentState.ComponentId, child);
            if (!BallisticNormalizationState.TryCreateDerivedChild(
                    componentId,
                    parentState,
                    out BallisticNormalizationState? childState,
                    out _)
                || childState == null)
            {
                return false;
            }

            Set(child, childState);
            return true;
        }

        internal static bool TrySetPhysicalComponent(
            Shot child,
            PhysicalProjectileState physicalState,
            BallisticFalloffFactors baselineFactors)
        {
            if (child == null || physicalState == null)
            {
                return false;
            }

            if (!BallisticNormalizationState.TryCreatePhysicalComponent(
                    physicalState.ProjectileId,
                    physicalState.RootShotId,
                    baselineFactors,
                    out BallisticNormalizationState? childState,
                    out _)
                || childState == null)
            {
                return false;
            }

            Set(child, childState);
            return true;
        }

        internal static string CreateCollisionIdentity(
            Shot shot,
            BallisticNormalizationState state)
        {
            if (PhysicalShotBindingStore.TryGet(shot, out PhysicalShotBinding? physicalBinding)
                && physicalBinding != null)
            {
                return PhysicalProjectileTransitionIdentity.CreateCollisionId(
                    physicalBinding.State);
            }

            int colliderId = shot.HittedBallisticCollider != null
                ? shot.HittedBallisticCollider.GetInstanceID()
                : 0;
            Vector3 hitPoint = shot.HitPoint;
            return string.Concat(
                state.ComponentId,
                "-host-",
                colliderId.ToString(CultureInfo.InvariantCulture),
                "-t-",
                shot.TimeSinceShot.ToString("R", CultureInfo.InvariantCulture),
                "-p-",
                hitPoint.x.ToString("R", CultureInfo.InvariantCulture),
                "-",
                hitPoint.y.ToString("R", CultureInfo.InvariantCulture),
                "-",
                hitPoint.z.ToString("R", CultureInfo.InvariantCulture));
        }

        internal static void RemoveIfSame(Shot shot, ShotNormalizationBinding expected)
        {
            if (shot == null || expected == null)
            {
                return;
            }

            Bindings.RemoveIfSame(shot, expected);
        }

        private static void Set(Shot shot, BallisticNormalizationState state)
        {
            var binding = new ShotNormalizationBinding(shot, state);
            if (!Bindings.Set(shot, binding))
            {
                throw new InvalidOperationException(
                    "Normalization binding does not match the current shot incarnation.");
            }
        }

        private static string CreateRootId(Shot shot)
        {
            string ammoId = (shot.Ammo as Ammo)?.Id
                ?? shot.Ammo?.Id
                ?? "unknown-ammo";
            return string.Concat(
                "shot-",
                ammoId,
                "-",
                shot.FireIndex.ToString(CultureInfo.InvariantCulture),
                "-",
                shot.RandomSeed.ToString(CultureInfo.InvariantCulture));
        }

        private static string CreateDerivedComponentId(string parentComponentId, Shot child)
        {
            Vector3 start = child.StartPosition;
            return string.Concat(
                parentComponentId,
                "-host-child-",
                child.FragmentIndex.ToString(CultureInfo.InvariantCulture),
                "-",
                child.RandomSeed.ToString(CultureInfo.InvariantCulture),
                "-",
                start.x.ToString("R", CultureInfo.InvariantCulture),
                "-",
                start.y.ToString("R", CultureInfo.InvariantCulture),
                "-",
                start.z.ToString("R", CultureInfo.InvariantCulture));
        }
    }
}
