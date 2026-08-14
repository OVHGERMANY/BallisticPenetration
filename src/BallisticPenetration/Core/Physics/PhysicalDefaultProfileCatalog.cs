#nullable enable

using System;
using System.Collections.Generic;

namespace BallisticPenetration.Core.Physics
{
    /// <summary>
    /// Conservative development profiles expressed in SI units. They are deterministic engineering
    /// estimates for runtime modeling, not manufacturer metallurgy or certification data.
    /// </summary>
    public static class PhysicalDefaultProfileCatalog
    {
        private static readonly Dictionary<PhysicalProjectileConstruction, PhysicalProjectileMaterialProfile>
            ProjectileProfiles = CreateProjectileProfiles();

        private static readonly Dictionary<PhysicalMaterialClass, PhysicalTargetMaterialProfile>
            TargetProfiles = CreateTargetProfiles();

        private static readonly Dictionary<PhysicalMaterialClass, PhysicalProjectileMaterialProfile>
            SpallProjectileProfiles = CreateSpallProjectileProfiles();

        private static readonly Dictionary<PhysicalMaterialClass, PhysicalFragmentationProfile>
            FragmentationProfiles = CreateFragmentationProfiles();

        public static bool TryGetProjectileProfile(
            PhysicalProjectileConstruction construction,
            out PhysicalProjectileMaterialProfile? profile)
        {
            return ProjectileProfiles.TryGetValue(construction, out profile);
        }

        public static bool TryGetTargetProfile(
            PhysicalMaterialClass materialClass,
            out PhysicalTargetMaterialProfile? profile)
        {
            return TargetProfiles.TryGetValue(materialClass, out profile);
        }

        public static bool TryGetSpallProjectileProfile(
            PhysicalMaterialClass materialClass,
            out PhysicalProjectileMaterialProfile? profile)
        {
            return SpallProjectileProfiles.TryGetValue(materialClass, out profile);
        }

        public static bool TryGetFragmentationProfile(
            PhysicalMaterialClass materialClass,
            out PhysicalFragmentationProfile? profile)
        {
            return FragmentationProfiles.TryGetValue(materialClass, out profile);
        }

        public static double GetNominalDragCoefficient(
            PhysicalProjectileConstruction construction)
        {
            switch (construction)
            {
                case PhysicalProjectileConstruction.LeadCoreJacketed:
                    return 0.31d;
                case PhysicalProjectileConstruction.SteelCoreJacketed:
                    return 0.29d;
                case PhysicalProjectileConstruction.TungstenCoreJacketed:
                    return 0.27d;
                case PhysicalProjectileConstruction.MonolithicCopper:
                    return 0.30d;
                case PhysicalProjectileConstruction.MonolithicSteel:
                    return 0.28d;
                case PhysicalProjectileConstruction.FrangibleComposite:
                    return 0.36d;
                default:
                    return double.NaN;
            }
        }

        private static Dictionary<PhysicalProjectileConstruction, PhysicalProjectileMaterialProfile>
            CreateProjectileProfiles()
        {
            var profiles = new Dictionary<PhysicalProjectileConstruction, PhysicalProjectileMaterialProfile>();
            AddProjectileProfile(
                profiles,
                PhysicalProjectileConstruction.LeadCoreJacketed,
                "default-projectile-lead-core-jacketed",
                10500d,
                30000000d,
                15000d,
                0.75d,
                0.15d,
                0.55d,
                2.0d,
                0.05d,
                0.40d,
                0.65d,
                2.5d,
                1.20d,
                0.20d,
                0.70d);
            AddProjectileProfile(
                profiles,
                PhysicalProjectileConstruction.SteelCoreJacketed,
                "default-projectile-steel-core-jacketed",
                7850d,
                180000000d,
                35000d,
                0.30d,
                0.50d,
                0.40d,
                1.30d,
                0.08d,
                0.45d,
                0.35d,
                2.0d,
                1.00d,
                0.18d,
                0.60d);
            AddProjectileProfile(
                profiles,
                PhysicalProjectileConstruction.TungstenCoreJacketed,
                "default-projectile-tungsten-core-jacketed",
                17600d,
                250000000d,
                25000d,
                0.15d,
                0.75d,
                0.25d,
                1.10d,
                0.15d,
                0.65d,
                0.20d,
                1.8d,
                0.90d,
                0.15d,
                0.50d);
            AddProjectileProfile(
                profiles,
                PhysicalProjectileConstruction.MonolithicCopper,
                "default-projectile-monolithic-copper",
                8960d,
                120000000d,
                45000d,
                0.85d,
                0.10d,
                0.60d,
                1.80d,
                0.02d,
                0.15d,
                0.55d,
                2.2d,
                1.10d,
                0.22d,
                0.75d);
            AddProjectileProfile(
                profiles,
                PhysicalProjectileConstruction.MonolithicSteel,
                "default-projectile-monolithic-steel",
                7850d,
                220000000d,
                55000d,
                0.25d,
                0.45d,
                0.35d,
                1.20d,
                0.05d,
                0.30d,
                0.25d,
                1.9d,
                0.90d,
                0.16d,
                0.55d);
            AddProjectileProfile(
                profiles,
                PhysicalProjectileConstruction.FrangibleComposite,
                "default-projectile-frangible-composite",
                5500d,
                12000000d,
                8000d,
                0.08d,
                0.92d,
                0.70d,
                1.15d,
                0.35d,
                0.90d,
                0.80d,
                4.0d,
                1.40d,
                0.12d,
                0.40d);
            return profiles;
        }

        private static void AddProjectileProfile(
            Dictionary<PhysicalProjectileConstruction, PhysicalProjectileMaterialProfile> profiles,
            PhysicalProjectileConstruction construction,
            string profileId,
            double density,
            double plasticWorkDensity,
            double fractureEnergy,
            double ductility,
            double brittleness,
            double deformationCoupling,
            double expansion,
            double minimumFragmentMassFraction,
            double maximumFragmentMassFraction,
            double penetrationPenalty,
            double dragMultiplier,
            double maximumYaw,
            double yawThreshold,
            double tumbleThreshold)
        {
            var input = new PhysicalProjectileMaterialProfileInput
            {
                ProfileId = profileId,
                Construction = construction,
                DensityKilogramsPerCubicMetre = density,
                PlasticDeformationWorkJoulesPerCubicMetre = plasticWorkDensity,
                FractureEnergyJoulesPerKilogram = fractureEnergy,
                Ductility = ductility,
                Brittleness = brittleness,
                DeformationEnergyCoupling = deformationCoupling,
                MaximumDiameterExpansionRatio = expansion,
                MinimumFragmentMassFraction = minimumFragmentMassFraction,
                MaximumFragmentMassFraction = maximumFragmentMassFraction,
                MaximumPenetrationShapePenalty = penetrationPenalty,
                MaximumDragCoefficientMultiplier = dragMultiplier,
                MaximumDeformationYawRadians = maximumYaw,
                YawingThresholdRadians = yawThreshold,
                TumblingThresholdRadians = tumbleThreshold
            };
            if (!PhysicalProjectileMaterialProfile.TryCreate(input, out PhysicalProjectileMaterialProfile? profile, out _)
                || profile == null)
            {
                throw new InvalidOperationException("The built-in projectile profile is invalid: " + profileId);
            }

            profiles.Add(construction, profile);
        }

        private static Dictionary<PhysicalMaterialClass, PhysicalTargetMaterialProfile>
            CreateTargetProfiles()
        {
            var profiles = new Dictionary<PhysicalMaterialClass, PhysicalTargetMaterialProfile>();
            AddTargetProfile(profiles, PhysicalMaterialClass.SoftTissue, 1050d, 1200000d, 0.65d, 0.30d, 0.08d);
            AddTargetProfile(profiles, PhysicalMaterialClass.Bone, 1850d, 85000000d, 0.80d, 0.70d, 0.12d);
            AddTargetProfile(profiles, PhysicalMaterialClass.Fabric, 400d, 5000000d, 0.25d, 0.10d, 0.05d);
            AddTargetProfile(profiles, PhysicalMaterialClass.Polymer, 1150d, 40000000d, 0.50d, 0.35d, 0.10d);
            AddTargetProfile(profiles, PhysicalMaterialClass.Wood, 650d, 12000000d, 0.45d, 0.30d, 0.08d);
            AddTargetProfile(profiles, PhysicalMaterialClass.Glass, 2500d, 55000000d, 0.65d, 0.80d, 0.18d);
            AddTargetProfile(profiles, PhysicalMaterialClass.Aluminum, 2700d, 180000000d, 0.65d, 0.55d, 0.15d);
            AddTargetProfile(profiles, PhysicalMaterialClass.MildSteel, 7850d, 350000000d, 0.80d, 0.70d, 0.18d);
            AddTargetProfile(profiles, PhysicalMaterialClass.ArmoredSteel, 7850d, 900000000d, 0.90d, 0.85d, 0.20d);
            AddTargetProfile(profiles, PhysicalMaterialClass.Titanium, 4500d, 750000000d, 0.85d, 0.75d, 0.18d);
            AddTargetProfile(profiles, PhysicalMaterialClass.Ceramic, 3700d, 1100000000d, 0.90d, 0.95d, 0.22d);
            AddTargetProfile(profiles, PhysicalMaterialClass.CompositeArmor, 2200d, 650000000d, 0.85d, 0.80d, 0.18d);
            AddTargetProfile(profiles, PhysicalMaterialClass.Concrete, 2400d, 120000000d, 0.75d, 0.75d, 0.20d);
            AddTargetProfile(profiles, PhysicalMaterialClass.Soil, 1600d, 8000000d, 0.30d, 0.15d, 0.06d);
            AddTargetProfile(profiles, PhysicalMaterialClass.Other, 1800d, 80000000d, 0.55d, 0.50d, 0.12d);
            return profiles;
        }

        private static void AddTargetProfile(
            Dictionary<PhysicalMaterialClass, PhysicalTargetMaterialProfile> profiles,
            PhysicalMaterialClass materialClass,
            double density,
            double resistancePressure,
            double deformationCoupling,
            double fractureCoupling,
            double heatLossFraction)
        {
            string profileId = "default-target-" + materialClass.ToString();
            var input = new PhysicalTargetMaterialProfileInput
            {
                ProfileId = profileId,
                MaterialClass = materialClass,
                DensityKilogramsPerCubicMetre = density,
                EffectiveResistancePressurePascals = resistancePressure,
                ProjectileDeformationCoupling = deformationCoupling,
                ProjectileFractureCoupling = fractureCoupling,
                HeatLossFraction = heatLossFraction
            };
            if (!PhysicalTargetMaterialProfile.TryCreate(input, out PhysicalTargetMaterialProfile? profile, out _)
                || profile == null)
            {
                throw new InvalidOperationException("The built-in target profile is invalid: " + profileId);
            }

            profiles.Add(materialClass, profile);
        }

        private static Dictionary<PhysicalMaterialClass, PhysicalProjectileMaterialProfile>
            CreateSpallProjectileProfiles()
        {
            var profiles = new Dictionary<PhysicalMaterialClass, PhysicalProjectileMaterialProfile>();
            foreach (KeyValuePair<PhysicalMaterialClass, PhysicalTargetMaterialProfile> entry
                in TargetProfiles)
            {
                PhysicalTargetMaterialProfile target = entry.Value;
                double brittleness = target.ProjectileFractureCoupling;
                double ductility = 1d - brittleness;
                double specificResistanceJoulesPerKilogram =
                    target.EffectiveResistancePressurePascals
                    / target.DensityKilogramsPerCubicMetre;
                double fractureEnergyJoulesPerKilogram = Math.Max(
                    100d,
                    specificResistanceJoulesPerKilogram
                    * Math.Max(0.02d, 0.20d * ductility));
                double minimumFragmentMassFraction = 0.03d;
                double maximumFragmentMassFraction = Math.Max(
                    minimumFragmentMassFraction,
                    0.50d - (0.30d * brittleness));
                string profileId = "default-spall-projectile-" + entry.Key.ToString();
                var input = new PhysicalProjectileMaterialProfileInput
                {
                    ProfileId = profileId,
                    Construction = PhysicalProjectileConstruction.TargetMaterial,
                    DensityKilogramsPerCubicMetre = target.DensityKilogramsPerCubicMetre,
                    PlasticDeformationWorkJoulesPerCubicMetre =
                        target.EffectiveResistancePressurePascals
                        * Math.Max(0.05d, target.ProjectileDeformationCoupling),
                    FractureEnergyJoulesPerKilogram = fractureEnergyJoulesPerKilogram,
                    Ductility = ductility,
                    Brittleness = brittleness,
                    DeformationEnergyCoupling = target.ProjectileDeformationCoupling,
                    MaximumDiameterExpansionRatio = 1d + (0.30d * ductility),
                    MinimumFragmentMassFraction = minimumFragmentMassFraction,
                    MaximumFragmentMassFraction = maximumFragmentMassFraction,
                    MaximumPenetrationShapePenalty = 0.75d,
                    MaximumDragCoefficientMultiplier = 3.5d,
                    MaximumDeformationYawRadians = 1.2d,
                    YawingThresholdRadians = 0.12d,
                    TumblingThresholdRadians = 0.35d
                };
                if (!PhysicalProjectileMaterialProfile.TryCreate(
                        input,
                        out PhysicalProjectileMaterialProfile? profile,
                        out _)
                    || profile == null)
                {
                    throw new InvalidOperationException(
                        "The built-in target-spall projectile profile is invalid: " + profileId);
                }

                profiles.Add(entry.Key, profile);
            }

            return profiles;
        }

        private static Dictionary<PhysicalMaterialClass, PhysicalFragmentationProfile>
            CreateFragmentationProfiles()
        {
            var profiles = new Dictionary<PhysicalMaterialClass, PhysicalFragmentationProfile>();
            foreach (PhysicalMaterialClass materialClass in TargetProfiles.Keys)
            {
                bool producesSpall = IsHardSpallMaterial(materialClass);
                var input = new PhysicalFragmentationProfileInput
                {
                    MaximumProjectileFragmentCount = 32,
                    MinimumProjectileFragmentMassKilograms = 0.00001d,
                    ProjectileConeHalfAngleRadians = 0.45d,
                    MinimumProjectileAspectRatio = 0.35d,
                    MaximumProjectileAspectRatio = 3.5d,
                    MinimumProjectileDragMultiplier = 1.2d,
                    MaximumProjectileDragMultiplier = 4.0d,
                    ProjectilePenetrationEfficiency = 0.55d,
                    TargetSpallEjectedMassFraction = producesSpall ? 0.02d : 0d,
                    TargetSpallKineticEnergyFraction = producesSpall ? 0.10d : 0d,
                    NominalTargetSpallMassKilograms = producesSpall ? 0.00005d : 0d,
                    MaximumTargetSpallCount = producesSpall ? 24 : 0,
                    TargetSpallConeHalfAngleRadians = producesSpall ? 0.70d : 0d,
                    MinimumTargetSpallAspectRatio = producesSpall ? 0.20d : 0d,
                    MaximumTargetSpallAspectRatio = producesSpall ? 2.5d : 0d,
                    MinimumTargetSpallDragCoefficient = producesSpall ? 0.8d : 0d,
                    MaximumTargetSpallDragCoefficient = producesSpall ? 2.5d : 0d,
                    TargetSpallPenetrationEfficiency = producesSpall ? 0.35d : 0d
                };
                if (!PhysicalFragmentationProfile.TryCreate(input, out PhysicalFragmentationProfile? profile, out _)
                    || profile == null)
                {
                    throw new InvalidOperationException(
                        "The built-in fragmentation profile is invalid: " + materialClass);
                }

                profiles.Add(materialClass, profile);
            }

            return profiles;
        }

        private static bool IsHardSpallMaterial(PhysicalMaterialClass materialClass)
        {
            return materialClass == PhysicalMaterialClass.Glass
                || materialClass == PhysicalMaterialClass.Aluminum
                || materialClass == PhysicalMaterialClass.MildSteel
                || materialClass == PhysicalMaterialClass.ArmoredSteel
                || materialClass == PhysicalMaterialClass.Titanium
                || materialClass == PhysicalMaterialClass.Ceramic
                || materialClass == PhysicalMaterialClass.CompositeArmor
                || materialClass == PhysicalMaterialClass.Concrete;
        }
    }
}
