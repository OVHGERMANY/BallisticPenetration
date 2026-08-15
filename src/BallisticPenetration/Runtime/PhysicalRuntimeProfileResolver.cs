#nullable enable

using BallisticPenetration.Core.Physics;
using EFT.Ballistics;
using EFT.InventoryLogic;

namespace BallisticPenetration.Runtime
{
    /// <summary>
    /// Maps the limited physical information exposed by EFT onto conservative development profiles.
    /// The mapping is deliberately fail-open and is not represented as manufacturer metallurgy.
    /// </summary>
    internal static class PhysicalRuntimeProfileResolver
    {
        internal static bool TryResolveProjectile(
            Shot shot,
            out PhysicalProjectileMaterialProfile? projectileProfile,
            out PhysicalProjectileDesignClass designClass,
            out PhysicalProjectileShapeClass shapeClass,
            out double dragCoefficient,
            out double massKilograms,
            out double diameterMetres)
        {
            projectileProfile = null;
            designClass = PhysicalProjectileDesignClass.Unknown;
            shapeClass = PhysicalProjectileShapeClass.Unknown;
            dragCoefficient = double.NaN;
            massKilograms = double.NaN;
            diameterMetres = double.NaN;
            if (shot == null
                || !(shot.Ammo?.Template is AmmoTemplate template))
            {
                return false;
            }

            if (!PhysicalAmmunitionDesignCatalog.TryGetDefinition(
                    template.StringId,
                    out PhysicalAmmunitionDesignDefinition definition)
                || !definition.IsKineticProjectile)
            {
                return false;
            }

            if (!PhysicalDefaultProfileCatalog.TryGetProjectileProfile(
                    definition.Construction,
                    out projectileProfile)
                || projectileProfile == null)
            {
                return false;
            }

            designClass = definition.DesignClass;
            shapeClass = definition.InitialShapeClass;
            dragCoefficient = PhysicalDefaultProfileCatalog.GetNominalDragCoefficient(
                definition.Construction,
                designClass,
                shapeClass);
            massKilograms = ConvertPositiveToSi(
                template.BulletMassGram,
                0.001d,
                definition.FallbackMassKilograms);
            diameterMetres = ConvertPositiveToSi(
                template.BulletDiameterMilimeters,
                0.001d,
                definition.FallbackDiameterMetres);
            return shapeClass != PhysicalProjectileShapeClass.Unknown
                && designClass != PhysicalProjectileDesignClass.Unknown
                && !double.IsNaN(dragCoefficient)
                && !double.IsInfinity(dragCoefficient)
                && dragCoefficient > 0d
                && !double.IsNaN(massKilograms)
                && !double.IsInfinity(massKilograms)
                && massKilograms > 0d
                && !double.IsNaN(diameterMetres)
                && !double.IsInfinity(diameterMetres)
                && diameterMetres > 0d;
        }

        private static double ConvertPositiveToSi(
            float sourceValue,
            double scale,
            double fallbackValue)
        {
            double converted = sourceValue * scale;
            if (!double.IsNaN(converted)
                && !double.IsInfinity(converted)
                && converted > 0d)
            {
                return converted;
            }

            return !double.IsNaN(fallbackValue)
                && !double.IsInfinity(fallbackValue)
                && fallbackValue > 0d
                    ? fallbackValue
                    : double.NaN;
        }

        internal static bool TryResolveProjectileProfile(
            PhysicalProjectileState state,
            out PhysicalProjectileMaterialProfile? projectileProfile)
        {
            projectileProfile = null;
            if (state == null)
            {
                return false;
            }

            if (state.Construction == PhysicalProjectileConstruction.TargetMaterial)
            {
                return PhysicalDefaultProfileCatalog.TryGetSpallProjectileProfile(
                        state.SourceMaterialClass,
                        out projectileProfile)
                    && projectileProfile != null;
            }

            return PhysicalDefaultProfileCatalog.TryGetProjectileProfile(
                    state.Construction,
                    out projectileProfile)
                && projectileProfile != null;
        }

        internal static bool TryResolveTarget(
            Shot shot,
            out PhysicalTargetMaterialProfile? targetProfile,
            out PhysicalFragmentationProfile? fragmentationProfile,
            out string targetSurfaceIdentity)
        {
            targetProfile = null;
            fragmentationProfile = null;
            targetSurfaceIdentity = string.Empty;
            if (shot?.HittedBallisticCollider == null)
            {
                return false;
            }

            PhysicalSurfaceMaterialMetadataStatus metadataStatus =
                PhysicalSurfaceMaterialContract.TryRead(
                    shot.HittedBallisticCollider,
                    out PhysicalMaterialClass materialClass,
                    out targetSurfaceIdentity);
            if (metadataStatus == PhysicalSurfaceMaterialMetadataStatus.Invalid)
            {
                return false;
            }
            if (metadataStatus == PhysicalSurfaceMaterialMetadataStatus.Absent)
            {
                materialClass = MapMaterial(shot.HittedBallisticCollider.TypeOfMaterial);
            }
            return materialClass != PhysicalMaterialClass.Unknown
                && materialClass != PhysicalMaterialClass.Air
                && PhysicalDefaultProfileCatalog.TryGetTargetProfile(
                    materialClass,
                    out targetProfile)
                && targetProfile != null
                && PhysicalDefaultProfileCatalog.TryGetFragmentationProfile(
                    materialClass,
                    out fragmentationProfile)
                && fragmentationProfile != null;
        }

        private static PhysicalMaterialClass MapMaterial(MaterialType materialType)
        {
            switch (materialType)
            {
                case MaterialType.Body:
                    return PhysicalMaterialClass.SoftTissue;
                case MaterialType.Fabric:
                case MaterialType.Cardboard:
                case MaterialType.GarbagePaper:
                    return PhysicalMaterialClass.Fabric;
                case MaterialType.Plastic:
                case MaterialType.Rubber:
                case MaterialType.Tyre:
                    return PhysicalMaterialClass.Polymer;
                case MaterialType.WoodThin:
                case MaterialType.WoodThick:
                    return PhysicalMaterialClass.Wood;
                case MaterialType.Glass:
                case MaterialType.GlassShattered:
                case MaterialType.GlassVisor:
                    return PhysicalMaterialClass.Glass;
                case MaterialType.Chainfence:
                case MaterialType.GarbageMetal:
                case MaterialType.Grate:
                case MaterialType.MetalThin:
                    return PhysicalMaterialClass.MildSteel;
                case MaterialType.MetalThick:
                case MaterialType.MetalNoDecal:
                    return PhysicalMaterialClass.ArmoredSteel;
                case MaterialType.BodyArmor:
                case MaterialType.Helmet:
                case MaterialType.HelmetRicochet:
                    return PhysicalMaterialClass.CompositeArmor;
                case MaterialType.Concrete:
                case MaterialType.Stone:
                case MaterialType.Tile:
                case MaterialType.Asphalt:
                    return PhysicalMaterialClass.Concrete;
                case MaterialType.Mud:
                case MaterialType.Gravel:
                case MaterialType.Pebbles:
                case MaterialType.Soil:
                case MaterialType.SoilForest:
                case MaterialType.Swamp:
                    return PhysicalMaterialClass.Soil;
                case MaterialType.GenericSoft:
                case MaterialType.GrassHigh:
                case MaterialType.GrassLow:
                case MaterialType.Snow:
                    return PhysicalMaterialClass.Other;
                case MaterialType.GenericHard:
                    return PhysicalMaterialClass.Other;
                default:
                    return PhysicalMaterialClass.Unknown;
            }
        }
    }
}
