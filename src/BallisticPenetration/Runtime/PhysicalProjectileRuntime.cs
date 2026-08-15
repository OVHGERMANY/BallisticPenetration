#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using BallisticPenetration.Core;
using BallisticPenetration.Core.Physics;
using BallisticPenetration.Runtime.State;
using BallisticPenetration.Runtime.Rendering;
using EFT.Ballistics;
using EFT.InventoryLogic;
using UnityEngine;

namespace BallisticPenetration.Runtime
{
    internal enum PhysicalBoundFlightResult
    {
        NotBound = 0,
        Applied = 1,
        Rejected = 2
    }

    internal sealed class PhysicalRuntimeCollisionState
    {
        internal PhysicalRuntimeCollisionState(
            string transitionId,
            PhysicalProjectileState parentState,
            PhysicalProjectileMaterialProfile projectileProfile,
            PhysicalTargetMaterialProfile targetProfile,
            PhysicalFragmentationProfile fragmentationProfile,
            string targetSurfaceIdentity,
            PhysicalImpactGeometry geometry,
            float parentEftDamage,
            float parentEftPenetrationPower,
            float parentEftBallisticCoefficient,
            PhysicalShotBinding? sourceBinding)
        {
            TransitionId = transitionId;
            ParentState = parentState;
            ProjectileProfile = projectileProfile;
            TargetProfile = targetProfile;
            FragmentationProfile = fragmentationProfile;
            TargetSurfaceIdentity = targetSurfaceIdentity ?? string.Empty;
            Geometry = geometry;
            ParentEftDamage = parentEftDamage;
            ParentEftPenetrationPower = parentEftPenetrationPower;
            ParentEftBallisticCoefficient = parentEftBallisticCoefficient;
            SourceBinding = sourceBinding;
        }

        internal string TransitionId { get; }

        internal PhysicalProjectileState ParentState { get; }

        internal PhysicalProjectileMaterialProfile ProjectileProfile { get; }

        internal PhysicalTargetMaterialProfile TargetProfile { get; }

        internal PhysicalFragmentationProfile FragmentationProfile { get; }

        internal string TargetSurfaceIdentity { get; }

        internal PhysicalImpactGeometry Geometry { get; }

        internal float ParentEftDamage { get; }

        internal float ParentEftPenetrationPower { get; }

        internal float ParentEftBallisticCoefficient { get; }

        internal PhysicalShotBinding? SourceBinding { get; }
    }

    /// <summary>
    /// Transactional bridge between the pure physical model and EFT's pooled Shot objects. It never
    /// invokes host decision methods. The original child list remains untouched until every physical
    /// state, projection, replacement Shot, trajectory, armor-CF application, and binding is valid.
    /// </summary>
    internal static class PhysicalProjectileRuntime
    {
        private const float ChildSurfaceOffsetMetres = 0.002f;

        internal static PhysicalBoundFlightResult TryApplyBoundFlight(
            Shot shot,
            out PhysicalRuntimeCollisionState? collisionState,
            out BallisticFalloffFactors factors)
        {
            collisionState = null;
            factors = BallisticFalloffFactors.NeutralFallback;
            if (!PhysicalShotBindingStore.TryGet(shot, out PhysicalShotBinding? binding)
                || binding == null)
            {
                return PhysicalBoundFlightResult.NotBound;
            }

            var flightInput = new PhysicalFlightStateInput
            {
                State = binding.State,
                PositionMetres = PhysicalImpactGeometryResolver.ToPhysical(shot.HitPoint),
                VelocityMetresPerSecond = PhysicalImpactGeometryResolver.ToPhysical(
                    shot.CurrentVelocity)
            };
            if (!PhysicalProjectileFlightState.TryAdvance(
                    flightInput,
                    out PhysicalProjectileState? impactState,
                    out _)
                || impactState == null
                || !TryCalculateRatio(
                    impactState.DamageCapabilityJoules,
                    binding.State.DamageCapabilityJoules,
                    out double damageRatio)
                || !TryCalculateRatio(
                    impactState.PenetrationCapabilityJoulesPerSquareMetre,
                    binding.State.PenetrationCapabilityJoulesPerSquareMetre,
                    out double penetrationRatio)
                || !TryConvertFiniteNonNegative(
                    binding.EftDamage * damageRatio,
                    out float damage)
                || !TryConvertFiniteNonNegative(
                    binding.EftPenetrationPower * penetrationRatio,
                    out float penetrationPower))
            {
                return PhysicalBoundFlightResult.Rejected;
            }

            if (!TryCreateCollisionState(
                    shot,
                    impactState,
                    damage,
                    penetrationPower,
                    binding.EftBallisticCoefficient,
                    binding,
                    out collisionState)
                || collisionState == null)
            {
                return PhysicalBoundFlightResult.Rejected;
            }

            // HandleCollision has already applied vanilla degradation. Replace it only after the
            // complete physical collision state is valid, so a failed bridge remains a true no-op.
            shot.Damage = damage;
            shot.PenetrationPower = penetrationPower;
            factors = new BallisticFalloffFactors(
                impactState.SpeedMetresPerSecond / binding.State.SpeedMetresPerSecond,
                penetrationRatio,
                damageRatio);
            Plugin.LogPhysicalTransitionPrepared(shot, collisionState);
            PhysicalProjectileTelemetryRuntime.PublishPrepared(shot, collisionState);
            return PhysicalBoundFlightResult.Applied;
        }

        internal static bool TryPrepareRootCollision(
            Shot shot,
            out PhysicalRuntimeCollisionState? collisionState)
        {
            collisionState = null;
            if (shot == null
                || shot.Parent != null
                || !PhysicalRuntimeProfileResolver.TryResolveProjectile(
                    shot,
                    out PhysicalProjectileMaterialProfile? projectileProfile,
                    out PhysicalProjectileDesignClass designClass,
                    out PhysicalProjectileShapeClass shapeClass,
                    out double dragCoefficient,
                    out double massKilograms,
                    out double diameterMetres)
                || projectileProfile == null)
            {
                return false;
            }

            string rootId = CreateRootId(shot);
            var rootInput = new PhysicalRootProjectileInput
            {
                ProjectileId = rootId,
                RootShotId = rootId,
                DeterministicSeed = CreateDeterministicSeed(shot),
                Construction = projectileProfile.Construction,
                DesignClass = designClass,
                ShapeClass = shapeClass,
                MassKilograms = massKilograms,
                NominalDiameterMetres = diameterMetres,
                MaterialDensityKilogramsPerCubicMetre = projectileProfile.DensityKilogramsPerCubicMetre,
                DragCoefficient = dragCoefficient,
                PositionMetres = PhysicalImpactGeometryResolver.ToPhysical(shot.HitPoint),
                VelocityMetresPerSecond = PhysicalImpactGeometryResolver.ToPhysical(
                    shot.CurrentVelocity)
            };
            if (!PhysicalRootProjectileFactory.TryCreate(
                    rootInput,
                    out PhysicalProjectileState? rootState,
                    out _)
                || rootState == null)
            {
                return false;
            }

            bool prepared = TryCreateCollisionState(
                shot,
                rootState,
                shot.Damage,
                shot.PenetrationPower,
                shot.BallisticCoefficient,
                null,
                out collisionState);
            if (prepared && collisionState != null)
            {
                Plugin.LogPhysicalTransitionPrepared(shot, collisionState);
                PhysicalProjectileTelemetryRuntime.PublishPrepared(shot, collisionState);
            }

            return prepared;
        }

        [SuppressMessage(
            "Design",
            "CA1031:Do not catch general exception types",
            Justification = "The experimental bridge must leave the already-created EFT child list untouched for every compatibility failure.")]
        internal static bool TryApplyObservedOutcome(
            Shot shot,
            PhysicalRuntimeCollisionState collisionState)
        {
            try
            {
                if (shot == null || collisionState == null)
                {
                    return false;
                }

                if (!TryClassifyOutcome(
                        shot,
                        out PhysicalCollisionOutcome outcome,
                        out PhysicalVector3 outgoingDirection))
                {
                    return false;
                }

                string collisionId = collisionState.TransitionId;
                var deformationInput = new PhysicalDeformationInput
                {
                    Parent = collisionState.ParentState,
                    ProjectileProfile = collisionState.ProjectileProfile,
                    TargetProfile = collisionState.TargetProfile,
                    CollisionId = collisionId,
                    OutputProjectileId = collisionState.ParentState.ProjectileId,
                    ImpactPositionMetres = collisionState.Geometry.Position,
                    SurfaceNormal = collisionState.Geometry.SurfaceNormal,
                    PhysicalThicknessMetres = collisionState.Geometry.PhysicalThicknessMetres,
                    EffectivePathLengthMetres = collisionState.Geometry.EffectivePathLengthMetres,
                    ObservedOutcome = outcome,
                    ObservedOutgoingDirection = outgoingDirection
                };
                if (!PhysicalDeformationSolver.TrySolve(
                        deformationInput,
                        out PhysicalDeformationResponse? deformation,
                        out _)
                    || deformation == null)
                {
                    return false;
                }

                PhysicalLossBudget effectiveLossBudget = deformation.LossBudget;

                if (outcome == PhysicalCollisionOutcome.Stopped)
                {
                    PhysicalProjectileState? stoppedState = deformation.PrimaryState;
                    if (stoppedState == null)
                    {
                        return false;
                    }

                    if (collisionState.SourceBinding != null)
                    {
                        PhysicalShotBindingStore.RemoveIfSame(
                            shot,
                            collisionState.SourceBinding);
                        PhysicalProjectileVisualRuntime.Retire(
                            collisionState.SourceBinding);
                    }

                    PhysicalProjectileVisualRuntime.RegisterEmbedded(stoppedState);
                    PhysicalProjectileTelemetryRuntime.PublishResolvedStopped(
                        shot,
                        collisionState,
                        stoppedState,
                        effectiveLossBudget);
                    return true;
                }

                var components = new List<PhysicalProjectileState>();
                if (outcome == PhysicalCollisionOutcome.Fragmented)
                {
                    var fragmentationInput = new PhysicalFragmentationInput
                    {
                        Parent = collisionState.ParentState,
                        DeformationResponse = deformation,
                        ProjectileProfile = collisionState.ProjectileProfile,
                        TargetProfile = collisionState.TargetProfile,
                        FragmentationProfile = collisionState.FragmentationProfile,
                        ObservedProjectileFragmentCount = shot.Fragments.Count,
                        ProjectileIdPrefix = collisionId + "-projectile",
                        TargetSpallIdPrefix = collisionId + "-spall"
                    };
                    if (!PhysicalFragmentationSolver.TrySolve(
                            fragmentationInput,
                            out PhysicalFragmentationResponse? fragmentation,
                            out _)
                        || fragmentation == null)
                    {
                        return false;
                    }

                    if (fragmentation.PrimaryState != null)
                    {
                        components.Add(fragmentation.PrimaryState);
                    }

                    for (int index = 0; index < fragmentation.AllSecondaryComponents.Count; index++)
                    {
                        components.Add(fragmentation.AllSecondaryComponents[index]);
                    }

                    effectiveLossBudget = fragmentation.EffectiveLossBudget;
                }
                else
                {
                    if (deformation.PrimaryState == null)
                    {
                        return false;
                    }

                    components.Add(deformation.PrimaryState);
                    if ((outcome == PhysicalCollisionOutcome.Penetrated
                            || outcome == PhysicalCollisionOutcome.Deviated)
                        && collisionState.FragmentationProfile.ProducesTargetSpall)
                    {
                        var targetSpallInput = new PhysicalTargetSpallInput
                        {
                            Parent = collisionState.ParentState,
                            DeformationResponse = deformation,
                            TargetProfile = collisionState.TargetProfile,
                            FragmentationProfile = collisionState.FragmentationProfile,
                            TargetSpallIdPrefix = collisionId + "-spall"
                        };
                        if (!PhysicalFragmentationSolver.TrySolveTargetSpall(
                                targetSpallInput,
                                out PhysicalTargetSpallResponse? targetSpall,
                                out _)
                            || targetSpall == null)
                        {
                            return false;
                        }

                        for (int index = 0; index < targetSpall.Components.Count; index++)
                        {
                            components.Add(targetSpall.Components[index]);
                        }

                        effectiveLossBudget = targetSpall.EffectiveLossBudget;
                    }
                }

                if (components.Count == 0
                    || !TryCreateReplacementChildren(
                        shot,
                        collisionState,
                        outcome,
                        components,
                        out List<Shot>? replacements,
                        out List<PhysicalShotBinding>? replacementBindings)
                    || replacements == null
                    || replacementBindings == null)
                {
                    return false;
                }

                var originalChildren = new List<Shot>(shot.Fragments);
                shot.Fragments.Clear();
                for (int index = 0; index < replacements.Count; index++)
                {
                    shot.Fragments.Add(replacements[index]);
                }

                if (collisionState.SourceBinding != null)
                {
                    PhysicalShotBindingStore.RemoveIfSame(
                        shot,
                        collisionState.SourceBinding);
                    PhysicalProjectileVisualRuntime.Retire(
                        collisionState.SourceBinding);
                }

                for (int index = 0; index < replacements.Count; index++)
                {
                    PhysicalProjectileVisualRuntime.RegisterLive(
                        replacements[index],
                        replacementBindings[index]);
                }

                ReleaseShotsBestEffort(originalChildren);
                PhysicalProjectileTelemetryRuntime.PublishResolved(
                    shot,
                    collisionState,
                    outcome,
                    components,
                    effectiveLossBudget);
                return true;
            }
            catch (Exception exception)
            {
                Plugin.LogHookFailure("Physical projectile outcome bridge", exception);
                return false;
            }
        }

        private static bool TryCreateCollisionState(
            Shot shot,
            PhysicalProjectileState state,
            float parentEftDamage,
            float parentEftPenetrationPower,
            float parentEftBallisticCoefficient,
            PhysicalShotBinding? sourceBinding,
            out PhysicalRuntimeCollisionState? collisionState)
        {
            collisionState = null;
            if (!PhysicalRuntimeProfileResolver.TryResolveProjectileProfile(
                    state,
                    out PhysicalProjectileMaterialProfile? projectileProfile)
                || projectileProfile == null
                || !PhysicalRuntimeProfileResolver.TryResolveTarget(
                    shot,
                    out PhysicalTargetMaterialProfile? targetProfile,
                    out PhysicalFragmentationProfile? fragmentationProfile,
                    out string targetSurfaceIdentity)
                || targetProfile == null
                || fragmentationProfile == null
                || !PhysicalImpactGeometryResolver.TryResolve(
                    shot,
                    out PhysicalImpactGeometry geometry)
                || !IsFiniteNonNegative(parentEftDamage)
                || !IsFiniteNonNegative(parentEftPenetrationPower)
                || !IsFinitePositive(parentEftBallisticCoefficient))
            {
                return false;
            }

            collisionState = new PhysicalRuntimeCollisionState(
                PhysicalProjectileTransitionIdentity.CreateCollisionId(state),
                state,
                projectileProfile,
                targetProfile,
                fragmentationProfile,
                targetSurfaceIdentity,
                geometry,
                parentEftDamage,
                parentEftPenetrationPower,
                parentEftBallisticCoefficient,
                sourceBinding);
            return true;
        }

        private static bool TryClassifyOutcome(
            Shot shot,
            out PhysicalCollisionOutcome outcome,
            out PhysicalVector3 outgoingDirection)
        {
            outgoingDirection = PhysicalVector3.Zero;
            switch (shot.BulletState)
            {
                case Shot.EBulletState.Flying:
                    if (shot.Fragments.Count != 0)
                    {
                        outcome = PhysicalCollisionOutcome.Unknown;
                        return false;
                    }

                    outcome = PhysicalCollisionOutcome.Penetrated;
                    return TryGetDirection(shot.CurrentVelocity, out outgoingDirection);
                case Shot.EBulletState.DeviationHit:
                    if (shot.Fragments.Count != 1)
                    {
                        outcome = PhysicalCollisionOutcome.Unknown;
                        return false;
                    }

                    outcome = PhysicalCollisionOutcome.Deviated;
                    return TryGetDirection(
                        shot.Fragments[0].CurrentVelocity,
                        out outgoingDirection);
                case Shot.EBulletState.RicochetHit:
                    if (shot.Fragments.Count != 1)
                    {
                        outcome = PhysicalCollisionOutcome.Unknown;
                        return false;
                    }

                    outcome = PhysicalCollisionOutcome.Ricocheted;
                    return TryGetDirection(
                        shot.Fragments[0].CurrentVelocity,
                        out outgoingDirection);
                case Shot.EBulletState.FragmentationHit:
                    outcome = PhysicalCollisionOutcome.Fragmented;
                    Vector3 fragmentationAxis = shot.Fragments.Count > 0
                        ? shot.Fragments[0].CurrentVelocity
                        : shot.CurrentVelocity;
                    return TryGetDirection(fragmentationAxis, out outgoingDirection);
                case Shot.EBulletState.StopHit:
                    outcome = PhysicalCollisionOutcome.Stopped;
                    return shot.Fragments.Count == 0;
                default:
                    outcome = PhysicalCollisionOutcome.Unknown;
                    return false;
            }
        }

        private static bool TryCreateReplacementChildren(
            Shot parent,
            PhysicalRuntimeCollisionState collisionState,
            PhysicalCollisionOutcome outcome,
            List<PhysicalProjectileState> components,
            out List<Shot>? replacements,
            out List<PhysicalShotBinding>? bindings)
        {
            replacements = new List<Shot>(components.Count);
            bindings = new List<PhysicalShotBinding>(components.Count);
            if (!TryGetTargetTransferMultiplier(
                    parent,
                    outcome,
                    out double targetTransferMultiplier))
            {
                return false;
            }

            bool complete = false;
            try
            {
                for (int index = 0; index < components.Count; index++)
                {
                    PhysicalProjectileState component = components[index];
                    double componentTransfer = component.Kind == PhysicalProjectileKind.TargetSpall
                        ? 1d
                        : targetTransferMultiplier;
                    var projectionInput = new PhysicalEftProjectionInput
                    {
                        Parent = collisionState.ParentState,
                        Component = component,
                        ParentEftBallisticCoefficient = collisionState.ParentEftBallisticCoefficient,
                        ParentEftDamage = collisionState.ParentEftDamage,
                        ParentEftPenetrationPower = collisionState.ParentEftPenetrationPower,
                        DamageTransferMultiplier = componentTransfer,
                        PenetrationTransferMultiplier = componentTransfer
                    };
                    if (!PhysicalEftProjectileProjector.TryProject(
                            projectionInput,
                            out PhysicalEftProjectileProjection? projection,
                            out _)
                        || projection == null
                        || !TryCreateChildShot(
                            parent,
                            component,
                            projection,
                            outcome,
                            out Shot? child)
                        || child == null)
                    {
                        return false;
                    }

                    replacements.Add(child);
                    if (component.Kind != PhysicalProjectileKind.TargetSpall
                        && outcome != PhysicalCollisionOutcome.Ricocheted)
                    {
                        // This is EFT's degradation-only CF path. It performs no penetration or
                        // ricochet roll and is applied to the new component exactly once.
                        Shot.ApplyPenetratedDegradation(
                            parent.HittedBallisticCollider,
                            parent.IsForwardHit,
                            child);
                    }

                    PhysicalShotBinding binding = PhysicalShotBindingStore.Set(
                        child,
                        component,
                        child.Damage,
                        child.PenetrationPower,
                        child.BallisticCoefficient);
                    bindings.Add(binding);
                    Plugin.LogPhysicalComponentProjected(
                        collisionState,
                        component,
                        projection,
                        child);
                }

                complete = replacements.Count == components.Count;
                return complete;
            }
            finally
            {
                if (!complete)
                {
                    CleanupReplacementShots(replacements, bindings);
                    replacements = null;
                    bindings = null;
                }
            }
        }

        private static bool TryCreateChildShot(
            Shot parent,
            PhysicalProjectileState component,
            PhysicalEftProjectileProjection projection,
            PhysicalCollisionOutcome outcome,
            out Shot? child)
        {
            child = null;
            if (!TryConvertFinitePositive(projection.MassGrams, out float massGrams)
                || !TryConvertFinitePositive(
                    projection.EquivalentDiameterMillimetres,
                    out float diameterMillimetres)
                || !TryConvertFinitePositive(
                    projection.SpeedMetresPerSecond,
                    out float speed)
                || !TryConvertFinitePositive(
                    projection.BallisticCoefficient,
                    out float ballisticCoefficient)
                || !TryConvertFiniteNonNegative(projection.Damage, out float damage)
                || !TryConvertFiniteNonNegative(
                    projection.PenetrationPower,
                    out float penetrationPower))
            {
                return false;
            }

            Vector3 direction = PhysicalImpactGeometryResolver.ToUnity(projection.Direction);
            Vector3 position = PhysicalImpactGeometryResolver.ToUnity(component.PositionMetres)
                + (direction * ChildSurfaceOffsetMetres);
            if (!IsFiniteVector(position)
                || !IsFiniteVector(direction)
                || direction.sqrMagnitude <= 0f)
            {
                return false;
            }

            direction.Normalize();
            if (!TryGetChildHostSettings(
                    parent,
                    outcome,
                    out int fragmentIndex,
                    out float penetrationChance,
                    out float ricochetChance,
                    out float fragmentationChance,
                    out float deviationChance,
                    out int minimumFragments,
                    out int maximumFragments))
            {
                return false;
            }

            child = Shot.Create(
                parent.Ammo,
                fragmentIndex,
                PhysicalHostRandomSeed.Map(
                    component.DeterministicSeed,
                    BallisticsCalculator.RND_COUNT),
                position,
                direction,
                speed,
                speed,
                massGrams,
                diameterMillimetres,
                damage,
                penetrationPower,
                penetrationChance,
                ricochetChance,
                fragmentationChance,
                deviationChance,
                minimumFragments,
                maximumFragments,
                parent._defaultHitBody,
                parent.Randoms,
                ballisticCoefficient,
                parent.Player != null ? parent.Player.iPlayer.ProfileId : parent.PlayerProfileID,
                parent.Weapon,
                parent.FireIndex,
                parent,
                parent.DelayedDamage);
            return true;
        }

        private static bool TryGetChildHostSettings(
            Shot parent,
            PhysicalCollisionOutcome outcome,
            out int fragmentIndex,
            out float penetrationChance,
            out float ricochetChance,
            out float fragmentationChance,
            out float deviationChance,
            out int minimumFragments,
            out int maximumFragments)
        {
            fragmentIndex = parent.FragmentIndex;
            penetrationChance = parent.PenetrationChance;
            ricochetChance = parent.RicochetChance;
            fragmentationChance = parent.FragmentationChance;
            deviationChance = parent.DeviationChance;
            minimumFragments = parent.MinFragmentsCount;
            maximumFragments = parent.MaxFragmentsCount;

            if (outcome == PhysicalCollisionOutcome.Fragmented)
            {
                fragmentIndex++;
                penetrationChance *= 0.2f;
                ricochetChance *= 0.2f;
                fragmentationChance = 0f;
                deviationChance = 1f;
                minimumFragments = 0;
                maximumFragments = 0;
            }
            else if (outcome == PhysicalCollisionOutcome.Ricocheted)
            {
                fragmentIndex++;
                penetrationChance *= 0.2f;
                ricochetChance *= 0.2f;
                fragmentationChance *= 0.2f;
                deviationChance = 1f;
            }
            else if (outcome == PhysicalCollisionOutcome.Deviated)
            {
                float chanceMultiplier = 1f;
                if (parent.IsForwardHit
                    && parent.HittedBallisticCollider is BodyPartCollider)
                {
                    chanceMultiplier = Mathf.Lerp(
                        0.2f,
                        1f,
                        parent.HittedBallisticCollider.PenetrationChance);
                }

                penetrationChance *= chanceMultiplier;
                ricochetChance *= chanceMultiplier;
                fragmentationChance *= chanceMultiplier;
                deviationChance -= 0.08f;
            }
            else if (outcome != PhysicalCollisionOutcome.Penetrated)
            {
                return false;
            }

            return IsFiniteNonNegative(penetrationChance)
                && IsFiniteNonNegative(ricochetChance)
                && IsFiniteNonNegative(fragmentationChance)
                && IsFinite(deviationChance)
                && minimumFragments >= 0
                && maximumFragments >= minimumFragments;
        }

        private static bool TryGetTargetTransferMultiplier(
            Shot parent,
            PhysicalCollisionOutcome outcome,
            out double multiplier)
        {
            multiplier = 1d;
            if (outcome == PhysicalCollisionOutcome.Ricocheted)
            {
                multiplier = Math.Min(
                    1d,
                    parent.RicochetChance + parent.HittedBallisticCollider.RicochetChance)
                    * 0.5d;
            }
            else if ((outcome == PhysicalCollisionOutcome.Deviated
                    || outcome == PhysicalCollisionOutcome.Fragmented)
                && parent.HittedBallisticCollider is BodyPartCollider bodyPartCollider)
            {
                double ammoPenetrationDamage = (parent.Ammo as Ammo)?.PenetrationDamageMod ?? 0d;
                multiplier = ((parent.PenetrationPower
                        - parent.HittedBallisticCollider.PenetrationLevel)
                    / 100d)
                    + bodyPartCollider.penetrationDamageMod
                    + ammoPenetrationDamage;
                multiplier = Math.Max(0d, Math.Min(1d, multiplier));
            }

            return IsFiniteNonNegative(multiplier);
        }

        private static void CleanupReplacementShots(
            List<Shot> replacements,
            List<PhysicalShotBinding> bindings)
        {
            int boundCount = Math.Min(replacements.Count, bindings.Count);
            for (int index = 0; index < boundCount; index++)
            {
                PhysicalShotBindingStore.RemoveIfSame(
                    replacements[index],
                    bindings[index]);
            }

            ReleaseShotsBestEffort(replacements);
        }

        private static void ReleaseShotsBestEffort(List<Shot> shots)
        {
            for (int index = 0; index < shots.Count; index++)
            {
                ReleaseShotBestEffort(shots[index]);
            }
        }

        [SuppressMessage(
            "Design",
            "CA1031:Do not catch general exception types",
            Justification = "A cleanup failure must not corrupt the active replacement list or host simulation.")]
        private static void ReleaseShotBestEffort(Shot shot)
        {
            try
            {
                if (shot != null)
                {
                    Shot.Release(shot);
                }
            }
            catch (Exception exception)
            {
                Plugin.LogHookFailure("Physical child cleanup", exception);
            }
        }

        private static bool TryGetDirection(Vector3 value, out PhysicalVector3 direction)
        {
            direction = PhysicalVector3.Zero;
            PhysicalVector3 physical = PhysicalImpactGeometryResolver.ToPhysical(value);
            return physical.TryNormalize(out direction);
        }

        private static bool TryCalculateRatio(
            double currentValue,
            double referenceValue,
            out double ratio)
        {
            ratio = 0d;
            if (!IsFiniteNonNegative(currentValue)
                || !IsFiniteNonNegative(referenceValue))
            {
                return false;
            }

            if (referenceValue <= 0d)
            {
                return currentValue <= 0d;
            }

            ratio = currentValue / referenceValue;
            return IsFiniteNonNegative(ratio);
        }

        private static bool TryConvertFinitePositive(double value, out float converted)
        {
            converted = (float)value;
            return IsFinitePositive(value) && IsFinitePositive(converted);
        }

        private static bool TryConvertFiniteNonNegative(double value, out float converted)
        {
            converted = (float)value;
            return IsFiniteNonNegative(value) && IsFiniteNonNegative(converted);
        }

        private static string CreateRootId(Shot shot)
        {
            return string.Concat(
                "shot-",
                shot.Ammo.Id.ToString(),
                "-",
                shot.FireIndex.ToString(CultureInfo.InvariantCulture),
                "-",
                shot.RandomSeed.ToString(CultureInfo.InvariantCulture));
        }

        private static ulong CreateDeterministicSeed(Shot shot)
        {
            unchecked
            {
                ulong seed = (uint)shot.RandomSeed;
                seed = (seed << 32) ^ (uint)shot.FireIndex;
                string ammoId = shot.Ammo.Id.ToString();
                for (int index = 0; index < ammoId.Length; index++)
                {
                    seed ^= ammoId[index];
                    seed *= 1099511628211UL;
                }

                return seed;
            }
        }

        private static bool IsFiniteVector(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        private static bool IsFinitePositive(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value) && value > 0d;
        }

        private static bool IsFiniteNonNegative(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value) && value >= 0d;
        }

        private static bool IsFinitePositive(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;
        }

        private static bool IsFiniteNonNegative(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
