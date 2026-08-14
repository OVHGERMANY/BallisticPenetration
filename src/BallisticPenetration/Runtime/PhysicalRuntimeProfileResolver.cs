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
            out PhysicalProjectileShapeClass shapeClass,
            out double dragCoefficient)
        {
            projectileProfile = null;
            shapeClass = PhysicalProjectileShapeClass.Unknown;
            dragCoefficient = double.NaN;
            if (shot == null
                || !(shot.Ammo?.Template is AmmoTemplate template)
                || template.ProjectileCount != 1
                || template.buckshotBullets > 1
                || template.HasGrenaderComponent
                || template.ExplosionStrength > 0f
                || template.FragmentsCount > 0
                || !string.IsNullOrEmpty(template.ExplosionType))
            {
                return false;
            }

            PhysicalProjectileConstruction construction = ClassifyConstruction(template);
            if (!PhysicalDefaultProfileCatalog.TryGetProjectileProfile(
                    construction,
                    out projectileProfile)
                || projectileProfile == null)
            {
                return false;
            }

            shapeClass = ClassifyShape(template);
            dragCoefficient = PhysicalDefaultProfileCatalog.GetNominalDragCoefficient(construction);
            return shapeClass != PhysicalProjectileShapeClass.Unknown
                && !double.IsNaN(dragCoefficient)
                && !double.IsInfinity(dragCoefficient)
                && dragCoefficient > 0d;
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

        private static PhysicalProjectileConstruction ClassifyConstruction(AmmoTemplate template)
        {
            if (template.FragmentationChance >= 0.50f && template.PenetrationPower <= 20)
            {
                return PhysicalProjectileConstruction.FrangibleComposite;
            }

            if (template.PenetrationPower >= 60)
            {
                return PhysicalProjectileConstruction.TungstenCoreJacketed;
            }

            if (template.PenetrationPower >= 25 || template.ArmorDamage >= 35)
            {
                return PhysicalProjectileConstruction.SteelCoreJacketed;
            }

            return PhysicalProjectileConstruction.LeadCoreJacketed;
        }

        private static PhysicalProjectileShapeClass ClassifyShape(AmmoTemplate template)
        {
            if (template.PenetrationPower < 20 && template.Damage >= 80)
            {
                return PhysicalProjectileShapeClass.FlatNose;
            }

            if (template.PenetrationPower < 25)
            {
                return PhysicalProjectileShapeClass.RoundNose;
            }

            return PhysicalProjectileShapeClass.Spitzer;
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
