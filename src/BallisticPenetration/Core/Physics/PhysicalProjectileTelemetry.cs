#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Threading;

namespace BallisticPenetration.Core.Physics
{
    public enum PhysicalTelemetryStage
    {
        CollisionPrepared = 0,
        CollisionResolved = 1
    }

    /// <summary>
    /// Host identifiers copied at the observation point. No pooled host object is retained.
    /// </summary>
    public sealed class PhysicalTelemetryHostIdentity
    {
        internal PhysicalTelemetryHostIdentity(
            int rootFireIndex,
            int rootRandomSeed,
            int currentFireIndex,
            int currentRandomSeed,
            int currentFragmentIndex,
            int parentDepth,
            string rootShooterProfileId,
            string ammunitionTemplateId,
            string ammunitionTemplateName)
        {
            RootFireIndex = rootFireIndex;
            RootRandomSeed = rootRandomSeed;
            CurrentFireIndex = currentFireIndex;
            CurrentRandomSeed = currentRandomSeed;
            CurrentFragmentIndex = currentFragmentIndex;
            ParentDepth = parentDepth;
            RootShooterProfileId = rootShooterProfileId ?? string.Empty;
            AmmunitionTemplateId = ammunitionTemplateId ?? string.Empty;
            AmmunitionTemplateName = ammunitionTemplateName ?? string.Empty;
        }

        public int RootFireIndex { get; }

        public int RootRandomSeed { get; }

        public int CurrentFireIndex { get; }

        public int CurrentRandomSeed { get; }

        public int CurrentFragmentIndex { get; }

        public int ParentDepth { get; }

        public string RootShooterProfileId { get; }

        public string AmmunitionTemplateId { get; }

        public string AmmunitionTemplateName { get; }
    }

    /// <summary>
    /// Exact measured collision geometry plus the conservative target profile selected for it.
    /// </summary>
    public sealed class PhysicalTelemetryImpact
    {
        internal PhysicalTelemetryImpact(
            PhysicalVector3 positionMetres,
            PhysicalVector3 surfaceNormal,
            double physicalThicknessMetres,
            double effectivePathLengthMetres,
            string targetProfileId,
            PhysicalMaterialClass targetMaterialClass,
            string targetSurfaceIdentity,
            double targetDensityKilogramsPerCubicMetre,
            double targetResistancePressurePascals,
            double projectileDeformationCoupling,
            double projectileFractureCoupling,
            double heatLossFraction)
        {
            PositionMetres = positionMetres;
            SurfaceNormal = surfaceNormal;
            PhysicalThicknessMetres = physicalThicknessMetres;
            EffectivePathLengthMetres = effectivePathLengthMetres;
            TargetProfileId = targetProfileId ?? string.Empty;
            TargetMaterialClass = targetMaterialClass;
            TargetSurfaceIdentity = targetSurfaceIdentity ?? string.Empty;
            TargetDensityKilogramsPerCubicMetre = targetDensityKilogramsPerCubicMetre;
            TargetResistancePressurePascals = targetResistancePressurePascals;
            ProjectileDeformationCoupling = projectileDeformationCoupling;
            ProjectileFractureCoupling = projectileFractureCoupling;
            HeatLossFraction = heatLossFraction;
        }

        public PhysicalVector3 PositionMetres { get; }

        public PhysicalVector3 SurfaceNormal { get; }

        public double PhysicalThicknessMetres { get; }

        public double EffectivePathLengthMetres { get; }

        public string TargetProfileId { get; }

        public PhysicalMaterialClass TargetMaterialClass { get; }

        public string TargetSurfaceIdentity { get; }

        public double TargetDensityKilogramsPerCubicMetre { get; }

        public double TargetResistancePressurePascals { get; }

        public double ProjectileDeformationCoupling { get; }

        public double ProjectileFractureCoupling { get; }

        public double HeatLossFraction { get; }
    }

    /// <summary>
    /// Closed accounting for one physical collision. Fresh target material is separated from mass
    /// derived from the incoming projectile, while all output kinetic energy shares one ledger.
    /// </summary>
    public sealed class PhysicalTelemetryConservation
    {
        internal PhysicalTelemetryConservation(
            double parentMassKilograms,
            double allocatedParentMassKilograms,
            double targetSpallMassKilograms,
            double parentEnergyJoules,
            PhysicalLossBudget lossBudget,
            double residualEnergyJoules,
            double outputEnergyJoules,
            int parentDerivedOutputCount,
            int targetSpallOutputCount)
        {
            ParentMassKilograms = parentMassKilograms;
            AllocatedParentMassKilograms = allocatedParentMassKilograms;
            TargetSpallMassKilograms = targetSpallMassKilograms;
            ParentEnergyJoules = parentEnergyJoules;
            LossBudget = lossBudget;
            ResidualEnergyJoules = residualEnergyJoules;
            OutputEnergyJoules = outputEnergyJoules;
            ParentDerivedOutputCount = parentDerivedOutputCount;
            TargetSpallOutputCount = targetSpallOutputCount;
        }

        public double ParentMassKilograms { get; }

        public double AllocatedParentMassKilograms { get; }

        public double UnallocatedParentMassKilograms
        {
            get { return ParentMassKilograms - AllocatedParentMassKilograms; }
        }

        public double TargetSpallMassKilograms { get; }

        public double ParentEnergyJoules { get; }

        public PhysicalLossBudget LossBudget { get; }

        public double ModeledLossEnergyJoules
        {
            get { return LossBudget.TotalLossJoules; }
        }

        public double ResidualEnergyJoules { get; }

        public double OutputEnergyJoules { get; }

        public double EnergyClosureErrorJoules
        {
            get { return ResidualEnergyJoules - OutputEnergyJoules; }
        }

        public int ParentDerivedOutputCount { get; }

        public int TargetSpallOutputCount { get; }
    }

    /// <summary>
    /// Immutable transition record. PhysicalProjectileState is itself immutable and contains the
    /// complete component geometry, mass, velocity, energy, attitude, lineage, and collision history.
    /// </summary>
    public sealed class PhysicalProjectileTelemetryEvent
    {
        private readonly ReadOnlyCollection<PhysicalProjectileState> _outputs;

        internal PhysicalProjectileTelemetryEvent(
            PhysicalTelemetryStage stage,
            string transitionId,
            PhysicalCollisionOutcome outcome,
            PhysicalTelemetryHostIdentity host,
            PhysicalTelemetryImpact impact,
            PhysicalProjectileState parent,
            IReadOnlyList<PhysicalProjectileState> outputs,
            PhysicalTelemetryConservation? conservation)
        {
            Stage = stage;
            TransitionId = transitionId ?? string.Empty;
            Outcome = outcome;
            Host = host ?? throw new ArgumentNullException(nameof(host));
            Impact = impact ?? throw new ArgumentNullException(nameof(impact));
            Parent = parent ?? throw new ArgumentNullException(nameof(parent));
            Conservation = conservation;

            IReadOnlyList<PhysicalProjectileState> safeOutputs = outputs
                ?? throw new ArgumentNullException(nameof(outputs));

            var copy = new PhysicalProjectileState[safeOutputs.Count];
            for (int index = 0; index < copy.Length; index++)
            {
                copy[index] = safeOutputs[index]
                    ?? throw new ArgumentException("Telemetry outputs cannot contain null states.", nameof(outputs));
            }

            _outputs = Array.AsReadOnly(copy);
        }

        public PhysicalTelemetryStage Stage { get; }

        public string TransitionId { get; }

        public PhysicalCollisionOutcome Outcome { get; }

        public PhysicalTelemetryHostIdentity Host { get; }

        public PhysicalTelemetryImpact Impact { get; }

        public PhysicalProjectileState Parent { get; }

        public IReadOnlyList<PhysicalProjectileState> Outputs
        {
            get { return _outputs; }
        }

        public PhysicalTelemetryConservation? Conservation { get; }
    }

    /// <summary>
    /// Optional observation seam for development tools. The simulation performs no snapshot work
    /// while this event has no subscribers, and a failing observer cannot interrupt another observer
    /// or the physical collision transaction.
    /// </summary>
    public static class PhysicalProjectileTelemetry
    {
        public const int SchemaVersion = 1;

        private static Action<object>? _transitionPublished;

        public static void Subscribe(Action<object> observer)
        {
            Action<object> safeObserver = observer
                ?? throw new ArgumentNullException(nameof(observer));
            Action<object>? current;
            Action<object>? updated;
            do
            {
                current = _transitionPublished;
                updated = (Action<object>?)Delegate.Combine(current, safeObserver);
            }
            while (!ReferenceEquals(
                Interlocked.CompareExchange(ref _transitionPublished, updated, current),
                current));
        }

        public static void Unsubscribe(Action<object> observer)
        {
            Action<object> safeObserver = observer
                ?? throw new ArgumentNullException(nameof(observer));
            Action<object>? current;
            Action<object>? updated;
            do
            {
                current = _transitionPublished;
                updated = (Action<object>?)Delegate.Remove(current, safeObserver);
            }
            while (!ReferenceEquals(
                Interlocked.CompareExchange(ref _transitionPublished, updated, current),
                current));
        }

        public static bool HasSubscribers
        {
            get { return Volatile.Read(ref _transitionPublished) != null; }
        }

        [SuppressMessage(
            "Design",
            "CA1031:Do not catch general exception types",
            Justification = "An optional observer must never interrupt the physical simulation or another observer.")]
        internal static void Publish(PhysicalProjectileTelemetryEvent telemetryEvent)
        {
            Action<object>? handlers = Volatile.Read(ref _transitionPublished);
            if (handlers == null)
            {
                return;
            }

            Delegate[] subscribers = handlers.GetInvocationList();
            for (int index = 0; index < subscribers.Length; index++)
            {
                try
                {
                    ((Action<object>)subscribers[index])(telemetryEvent);
                }
                catch (Exception)
                {
                    // Observation is deliberately isolated from the physical transaction.
                }
            }
        }
    }

    internal static class PhysicalProjectileTelemetryFactory
    {
        private const double RelativeTolerance = 0.000000001d;

        private static readonly IReadOnlyList<PhysicalProjectileState> NoOutputs =
            Array.AsReadOnly(Array.Empty<PhysicalProjectileState>());

        internal static PhysicalProjectileTelemetryEvent CreatePrepared(
            string transitionId,
            PhysicalTelemetryHostIdentity host,
            PhysicalTelemetryImpact impact,
            PhysicalProjectileState parent)
        {
            return new PhysicalProjectileTelemetryEvent(
                PhysicalTelemetryStage.CollisionPrepared,
                transitionId,
                PhysicalCollisionOutcome.Unknown,
                host,
                impact,
                parent,
                NoOutputs,
                null);
        }

        internal static bool TryCreateResolved(
            string transitionId,
            PhysicalCollisionOutcome outcome,
            PhysicalTelemetryHostIdentity host,
            PhysicalTelemetryImpact impact,
            PhysicalProjectileState parent,
            IReadOnlyList<PhysicalProjectileState> outputs,
            PhysicalLossBudget lossBudget,
            out PhysicalProjectileTelemetryEvent? telemetryEvent)
        {
            telemetryEvent = null;
            if (string.IsNullOrWhiteSpace(transitionId)
                || outcome == PhysicalCollisionOutcome.Unknown
                || host == null
                || impact == null
                || parent == null
                || outputs == null
                || outputs.Count == 0
                || !lossBudget.IsValid(out _)
                || !IsFiniteNonNegative(parent.RetainedMassKilograms)
                || !IsFiniteNonNegative(parent.TranslationalKineticEnergyJoules))
            {
                return false;
            }

            double energyTolerance = Math.Max(
                1d,
                parent.TranslationalKineticEnergyJoules) * RelativeTolerance;
            if (lossBudget.TotalLossJoules
                > parent.TranslationalKineticEnergyJoules + energyTolerance)
            {
                return false;
            }

            double allocatedParentMass = 0d;
            double targetSpallMass = 0d;
            double outputEnergy = 0d;
            int parentDerivedCount = 0;
            int targetSpallCount = 0;
            for (int index = 0; index < outputs.Count; index++)
            {
                PhysicalProjectileState? output = outputs[index];
                if (output == null
                    || !IsFiniteNonNegative(output.RetainedMassKilograms)
                    || !IsFiniteNonNegative(output.TranslationalKineticEnergyJoules))
                {
                    return false;
                }

                if (output.Kind == PhysicalProjectileKind.TargetSpall)
                {
                    targetSpallMass += output.RetainedMassKilograms;
                    targetSpallCount++;
                }
                else
                {
                    allocatedParentMass += output.RetainedMassKilograms;
                    parentDerivedCount++;
                }

                outputEnergy += output.TranslationalKineticEnergyJoules;
            }

            double residualEnergy = Math.Max(
                0d,
                parent.TranslationalKineticEnergyJoules - lossBudget.TotalLossJoules);
            double massTolerance = Math.Max(
                0.000000000001d,
                parent.RetainedMassKilograms * RelativeTolerance);
            if (!AreFiniteNonNegative(
                    allocatedParentMass,
                    targetSpallMass,
                    outputEnergy,
                    residualEnergy)
                || allocatedParentMass > parent.RetainedMassKilograms + massTolerance
                || outputEnergy > residualEnergy + energyTolerance)
            {
                return false;
            }

            var conservation = new PhysicalTelemetryConservation(
                parent.RetainedMassKilograms,
                allocatedParentMass,
                targetSpallMass,
                parent.TranslationalKineticEnergyJoules,
                lossBudget,
                residualEnergy,
                outputEnergy,
                parentDerivedCount,
                targetSpallCount);
            telemetryEvent = new PhysicalProjectileTelemetryEvent(
                PhysicalTelemetryStage.CollisionResolved,
                transitionId,
                outcome,
                host,
                impact,
                parent,
                outputs,
                conservation);
            return true;
        }

        private static bool AreFiniteNonNegative(params double[] values)
        {
            for (int index = 0; index < values.Length; index++)
            {
                if (!IsFiniteNonNegative(values[index]))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsFiniteNonNegative(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value) && value >= 0d;
        }
    }
}
