#nullable enable

using System;
using BallisticPenetration.Core;

namespace BallisticPenetration.Core.Physics
{
    public enum PhysicalMaterialProfileFailureReason
    {
        None = 0,
        InputMissing = 1,
        ProfileIdMissing = 2,
        ConstructionInvalid = 3,
        MaterialClassInvalid = 4,
        DensityInvalid = 5,
        PlasticWorkDensityInvalid = 6,
        FractureEnergyInvalid = 7,
        DuctilityInvalid = 8,
        BrittlenessInvalid = 9,
        CouplingInvalid = 10,
        ExpansionRatioInvalid = 11,
        FragmentMassFractionInvalid = 12,
        PenetrationPenaltyInvalid = 13,
        DragMultiplierInvalid = 14,
        YawParameterInvalid = 15,
        ResistancePressureInvalid = 16,
        HeatFractionInvalid = 17
    }

    public sealed class PhysicalProjectileMaterialProfileInput
    {
        public string? ProfileId { get; set; }

        public PhysicalProjectileConstruction Construction { get; set; }

        public double DensityKilogramsPerCubicMetre { get; set; }

        public double PlasticDeformationWorkJoulesPerCubicMetre { get; set; }

        public double FractureEnergyJoulesPerKilogram { get; set; }

        public double Ductility { get; set; }

        public double Brittleness { get; set; }

        public double DeformationEnergyCoupling { get; set; }

        public double MaximumDiameterExpansionRatio { get; set; }

        public double MinimumFragmentMassFraction { get; set; }

        public double MaximumFragmentMassFraction { get; set; }

        public double MaximumPenetrationShapePenalty { get; set; }

        public double MaximumDragCoefficientMultiplier { get; set; }

        public double MaximumDeformationYawRadians { get; set; }

        public double YawingThresholdRadians { get; set; }

        public double TumblingThresholdRadians { get; set; }
    }

    /// <summary>
    /// Calibrated projectile-material properties. The solver does not infer these values from an EFT
    /// penetration stat; callers must provide a construction-specific physical profile.
    /// </summary>
    public sealed class PhysicalProjectileMaterialProfile
    {
        private PhysicalProjectileMaterialProfile(
            PhysicalProjectileMaterialProfileInput input,
            string profileId)
        {
            ProfileId = profileId;
            Construction = input.Construction;
            DensityKilogramsPerCubicMetre = input.DensityKilogramsPerCubicMetre;
            PlasticDeformationWorkJoulesPerCubicMetre = input.PlasticDeformationWorkJoulesPerCubicMetre;
            FractureEnergyJoulesPerKilogram = input.FractureEnergyJoulesPerKilogram;
            Ductility = input.Ductility;
            Brittleness = input.Brittleness;
            DeformationEnergyCoupling = input.DeformationEnergyCoupling;
            MaximumDiameterExpansionRatio = input.MaximumDiameterExpansionRatio;
            MinimumFragmentMassFraction = input.MinimumFragmentMassFraction;
            MaximumFragmentMassFraction = input.MaximumFragmentMassFraction;
            MaximumPenetrationShapePenalty = input.MaximumPenetrationShapePenalty;
            MaximumDragCoefficientMultiplier = input.MaximumDragCoefficientMultiplier;
            MaximumDeformationYawRadians = input.MaximumDeformationYawRadians;
            YawingThresholdRadians = input.YawingThresholdRadians;
            TumblingThresholdRadians = input.TumblingThresholdRadians;
        }

        public string ProfileId { get; }

        public PhysicalProjectileConstruction Construction { get; }

        public double DensityKilogramsPerCubicMetre { get; }

        public double PlasticDeformationWorkJoulesPerCubicMetre { get; }

        public double FractureEnergyJoulesPerKilogram { get; }

        public double Ductility { get; }

        public double Brittleness { get; }

        public double DeformationEnergyCoupling { get; }

        public double MaximumDiameterExpansionRatio { get; }

        public double MinimumFragmentMassFraction { get; }

        public double MaximumFragmentMassFraction { get; }

        public double MaximumPenetrationShapePenalty { get; }

        public double MaximumDragCoefficientMultiplier { get; }

        public double MaximumDeformationYawRadians { get; }

        public double YawingThresholdRadians { get; }

        public double TumblingThresholdRadians { get; }

        public static bool TryCreate(
            PhysicalProjectileMaterialProfileInput? input,
            out PhysicalProjectileMaterialProfile? profile,
            out PhysicalMaterialProfileFailureReason failureReason)
        {
            profile = null;
            if (input == null)
            {
                failureReason = PhysicalMaterialProfileFailureReason.InputMissing;
                return false;
            }

            string? profileId = input.ProfileId;
            if (string.IsNullOrWhiteSpace(profileId))
            {
                failureReason = PhysicalMaterialProfileFailureReason.ProfileIdMissing;
                return false;
            }

            if (input.Construction <= PhysicalProjectileConstruction.Unknown
                || input.Construction > PhysicalProjectileConstruction.TargetMaterial)
            {
                failureReason = PhysicalMaterialProfileFailureReason.ConstructionInvalid;
                return false;
            }

            if (!IsFinitePositive(input.DensityKilogramsPerCubicMetre))
            {
                failureReason = PhysicalMaterialProfileFailureReason.DensityInvalid;
                return false;
            }

            if (!IsFinitePositive(input.PlasticDeformationWorkJoulesPerCubicMetre))
            {
                failureReason = PhysicalMaterialProfileFailureReason.PlasticWorkDensityInvalid;
                return false;
            }

            if (!IsFinitePositive(input.FractureEnergyJoulesPerKilogram))
            {
                failureReason = PhysicalMaterialProfileFailureReason.FractureEnergyInvalid;
                return false;
            }

            if (!IsUnitInterval(input.Ductility))
            {
                failureReason = PhysicalMaterialProfileFailureReason.DuctilityInvalid;
                return false;
            }

            if (!IsUnitInterval(input.Brittleness))
            {
                failureReason = PhysicalMaterialProfileFailureReason.BrittlenessInvalid;
                return false;
            }

            if (!IsUnitInterval(input.DeformationEnergyCoupling))
            {
                failureReason = PhysicalMaterialProfileFailureReason.CouplingInvalid;
                return false;
            }

            if (!FiniteDouble.IsFinite(input.MaximumDiameterExpansionRatio)
                || input.MaximumDiameterExpansionRatio < 1d)
            {
                failureReason = PhysicalMaterialProfileFailureReason.ExpansionRatioInvalid;
                return false;
            }

            if (!IsUnitInterval(input.MinimumFragmentMassFraction)
                || !IsUnitInterval(input.MaximumFragmentMassFraction)
                || input.MinimumFragmentMassFraction <= 0d
                || input.MaximumFragmentMassFraction < input.MinimumFragmentMassFraction)
            {
                failureReason = PhysicalMaterialProfileFailureReason.FragmentMassFractionInvalid;
                return false;
            }

            if (!IsUnitInterval(input.MaximumPenetrationShapePenalty))
            {
                failureReason = PhysicalMaterialProfileFailureReason.PenetrationPenaltyInvalid;
                return false;
            }

            if (!FiniteDouble.IsFinite(input.MaximumDragCoefficientMultiplier)
                || input.MaximumDragCoefficientMultiplier < 1d)
            {
                failureReason = PhysicalMaterialProfileFailureReason.DragMultiplierInvalid;
                return false;
            }

            if (!FiniteDouble.IsFinite(input.MaximumDeformationYawRadians)
                || input.MaximumDeformationYawRadians < 0d
                || input.MaximumDeformationYawRadians > Math.PI
                || !FiniteDouble.IsFinite(input.YawingThresholdRadians)
                || input.YawingThresholdRadians < 0d
                || !FiniteDouble.IsFinite(input.TumblingThresholdRadians)
                || input.TumblingThresholdRadians < input.YawingThresholdRadians
                || input.TumblingThresholdRadians > Math.PI)
            {
                failureReason = PhysicalMaterialProfileFailureReason.YawParameterInvalid;
                return false;
            }

            profile = new PhysicalProjectileMaterialProfile(input, profileId);
            failureReason = PhysicalMaterialProfileFailureReason.None;
            return true;
        }

        private static bool IsFinitePositive(double value)
        {
            return FiniteDouble.IsFinite(value) && value > 0d;
        }

        private static bool IsUnitInterval(double value)
        {
            return FiniteDouble.IsFinite(value) && value >= 0d && value <= 1d;
        }
    }

    public sealed class PhysicalTargetMaterialProfileInput
    {
        public string? ProfileId { get; set; }

        public PhysicalMaterialClass MaterialClass { get; set; }

        public double DensityKilogramsPerCubicMetre { get; set; }

        /// <summary>
        /// Calibrated average resisting pressure used by work = pressure * swept volume.
        /// </summary>
        public double EffectiveResistancePressurePascals { get; set; }

        public double ProjectileDeformationCoupling { get; set; }

        public double ProjectileFractureCoupling { get; set; }

        public double HeatLossFraction { get; set; }
    }

    public sealed class PhysicalTargetMaterialProfile
    {
        private PhysicalTargetMaterialProfile(
            PhysicalTargetMaterialProfileInput input,
            string profileId)
        {
            ProfileId = profileId;
            MaterialClass = input.MaterialClass;
            DensityKilogramsPerCubicMetre = input.DensityKilogramsPerCubicMetre;
            EffectiveResistancePressurePascals = input.EffectiveResistancePressurePascals;
            ProjectileDeformationCoupling = input.ProjectileDeformationCoupling;
            ProjectileFractureCoupling = input.ProjectileFractureCoupling;
            HeatLossFraction = input.HeatLossFraction;
        }

        public string ProfileId { get; }

        public PhysicalMaterialClass MaterialClass { get; }

        public double DensityKilogramsPerCubicMetre { get; }

        public double EffectiveResistancePressurePascals { get; }

        public double ProjectileDeformationCoupling { get; }

        public double ProjectileFractureCoupling { get; }

        public double HeatLossFraction { get; }

        public static bool TryCreate(
            PhysicalTargetMaterialProfileInput? input,
            out PhysicalTargetMaterialProfile? profile,
            out PhysicalMaterialProfileFailureReason failureReason)
        {
            profile = null;
            if (input == null)
            {
                failureReason = PhysicalMaterialProfileFailureReason.InputMissing;
                return false;
            }

            string? profileId = input.ProfileId;
            if (string.IsNullOrWhiteSpace(profileId))
            {
                failureReason = PhysicalMaterialProfileFailureReason.ProfileIdMissing;
                return false;
            }

            if (input.MaterialClass <= PhysicalMaterialClass.Unknown
                || input.MaterialClass > PhysicalMaterialClass.Titanium)
            {
                failureReason = PhysicalMaterialProfileFailureReason.MaterialClassInvalid;
                return false;
            }

            if (!FiniteDouble.IsFinite(input.DensityKilogramsPerCubicMetre)
                || input.DensityKilogramsPerCubicMetre <= 0d)
            {
                failureReason = PhysicalMaterialProfileFailureReason.DensityInvalid;
                return false;
            }

            if (!FiniteDouble.IsFinite(input.EffectiveResistancePressurePascals)
                || input.EffectiveResistancePressurePascals <= 0d)
            {
                failureReason = PhysicalMaterialProfileFailureReason.ResistancePressureInvalid;
                return false;
            }

            if (!IsUnitInterval(input.ProjectileDeformationCoupling)
                || !IsUnitInterval(input.ProjectileFractureCoupling))
            {
                failureReason = PhysicalMaterialProfileFailureReason.CouplingInvalid;
                return false;
            }

            if (!IsUnitInterval(input.HeatLossFraction))
            {
                failureReason = PhysicalMaterialProfileFailureReason.HeatFractionInvalid;
                return false;
            }

            profile = new PhysicalTargetMaterialProfile(input, profileId);
            failureReason = PhysicalMaterialProfileFailureReason.None;
            return true;
        }

        private static bool IsUnitInterval(double value)
        {
            return FiniteDouble.IsFinite(value) && value >= 0d && value <= 1d;
        }
    }
}
