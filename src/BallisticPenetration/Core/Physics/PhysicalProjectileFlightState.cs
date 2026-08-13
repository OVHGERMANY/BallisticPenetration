#nullable enable

using BallisticPenetration.Core;

namespace BallisticPenetration.Core.Physics
{
    public enum PhysicalFlightStateFailureReason
    {
        None = 0,
        InputMissing = 1,
        StateMissing = 2,
        StateNotMoving = 3,
        PositionInvalid = 4,
        VelocityInvalid = 5,
        EnergyInvalid = 6,
        CapabilityInvalid = 7,
        StateCreationFailed = 8
    }

    public sealed class PhysicalFlightStateInput
    {
        public PhysicalProjectileState? State { get; set; }

        public PhysicalVector3 PositionMetres { get; set; }

        public PhysicalVector3 VelocityMetresPerSecond { get; set; }
    }

    /// <summary>
    /// Advances an immutable component to a measured point on its flight. Aerodynamic and gravity
    /// integration remain owned by EFT; this method reconciles the physical state with EFT's exact
    /// position and velocity before the next terminal interaction.
    /// </summary>
    public static class PhysicalProjectileFlightState
    {
        public static bool TryAdvance(
            PhysicalFlightStateInput? input,
            out PhysicalProjectileState? state,
            out PhysicalFlightStateFailureReason failureReason)
        {
            state = null;
            if (input == null)
            {
                failureReason = PhysicalFlightStateFailureReason.InputMissing;
                return false;
            }

            PhysicalProjectileState? current = input.State;
            if (current == null)
            {
                failureReason = PhysicalFlightStateFailureReason.StateMissing;
                return false;
            }

            if (current.TerminalState != PhysicalProjectileTerminalState.Continuing
                && current.TerminalState != PhysicalProjectileTerminalState.Exited)
            {
                failureReason = PhysicalFlightStateFailureReason.StateNotMoving;
                return false;
            }

            if (!input.PositionMetres.IsFinite)
            {
                failureReason = PhysicalFlightStateFailureReason.PositionInvalid;
                return false;
            }

            double speed = input.VelocityMetresPerSecond.Magnitude;
            if (!input.VelocityMetresPerSecond.IsFinite
                || !FiniteDouble.IsFinite(speed)
                || speed <= 0d)
            {
                failureReason = PhysicalFlightStateFailureReason.VelocityInvalid;
                return false;
            }

            double energy = 0.5d * current.RetainedMassKilograms * speed * speed;
            if (!FiniteDouble.IsFinite(energy) || energy <= 0d)
            {
                failureReason = PhysicalFlightStateFailureReason.EnergyInvalid;
                return false;
            }

            double priorEnergy = current.TranslationalKineticEnergyJoules;
            if (!FiniteDouble.IsFinite(priorEnergy) || priorEnergy <= 0d)
            {
                failureReason = PhysicalFlightStateFailureReason.EnergyInvalid;
                return false;
            }

            double energyRatio = energy / priorEnergy;
            double damageCapability = System.Math.Min(
                energy,
                current.DamageCapabilityJoules * energyRatio);
            double penetrationCapability = current.PenetrationCapabilityJoulesPerSquareMetre
                * energyRatio;
            if (!IsFiniteNonNegative(damageCapability)
                || !IsFiniteNonNegative(penetrationCapability))
            {
                failureReason = PhysicalFlightStateFailureReason.CapabilityInvalid;
                return false;
            }

            var stateInput = new PhysicalProjectileStateInput
            {
                Kind = current.Kind,
                ProjectileId = current.ProjectileId,
                RootShotId = current.RootShotId,
                ParentProjectileId = current.ParentProjectileId,
                SourceProjectileId = current.SourceProjectileId,
                SourceMaterialId = current.SourceMaterialId,
                SourceMaterialClass = current.SourceMaterialClass,
                SourceCollisionId = current.SourceCollisionId,
                FragmentIndex = current.FragmentIndex,
                FragmentGeneration = current.FragmentGeneration,
                DeterministicSeed = current.DeterministicSeed,
                Construction = current.Construction,
                ShapeClass = current.ShapeClass,
                OriginalMassKilograms = current.OriginalMassKilograms,
                RetainedMassKilograms = current.RetainedMassKilograms,
                NominalDiameterMetres = current.NominalDiameterMetres,
                DeformedDiameterMetres = current.DeformedDiameterMetres,
                ProjectedAreaSquareMetres = current.ProjectedAreaSquareMetres,
                LengthMetres = current.LengthMetres,
                DragCoefficient = current.DragCoefficient,
                PositionMetres = input.PositionMetres,
                VelocityMetresPerSecond = input.VelocityMetresPerSecond,
                Orientation = current.Orientation,
                YawAngleRadians = current.YawAngleRadians,
                TumbleState = current.TumbleState,
                PenetrationCapabilityJoulesPerSquareMetre = penetrationCapability,
                DamageCapabilityJoules = damageCapability,
                TerminalState = PhysicalProjectileTerminalState.Continuing,
                RenderState = current.RenderState,
                CollisionHistory = current.CollisionHistory
            };
            if (!PhysicalProjectileState.TryCreate(stateInput, out state, out _)
                || state == null)
            {
                failureReason = PhysicalFlightStateFailureReason.StateCreationFailed;
                return false;
            }

            failureReason = PhysicalFlightStateFailureReason.None;
            return true;
        }

        private static bool IsFiniteNonNegative(double value)
        {
            return FiniteDouble.IsFinite(value) && value >= 0d;
        }
    }
}
