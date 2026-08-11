using System;
using System.Reflection;
using EFT.Ballistics;
using EFT.InventoryLogic;
using HarmonyLib;
using SPT.Reflection.Patching;

namespace BallisticPenetration.Runtime.Patches
{
    internal sealed class BodyPartColliderPostmortemArmorPatch : ModulePatch
    {
        internal const string HarmonyOwnerId =
            "com.janky.ballisticpenetration.corpse-body-armor-damage";

        private readonly MethodInfo _target;

        internal BodyPartColliderPostmortemArmorPatch(MethodInfo target)
            : base(HarmonyOwnerId)
        {
            _target = target ?? throw new ArgumentNullException(nameof(target));
        }

        protected override MethodBase GetTargetMethod()
        {
            return _target;
        }

        [PatchPrefix]
        [HarmonyPriority(Priority.Last)]
        private static void Prefix(
            BodyPartCollider __instance,
            DamageInfo damageInfo)
        {
            PostmortemArmorDamageProcessor.TryApply(
                __instance,
                (EArmorPlateCollider)0,
                damageInfo);
        }
    }

    internal sealed class ArmorPlateColliderPostmortemArmorPatch : ModulePatch
    {
        internal const string HarmonyOwnerId =
            "com.janky.ballisticpenetration.corpse-plate-armor-damage";

        private readonly MethodInfo _target;

        internal ArmorPlateColliderPostmortemArmorPatch(MethodInfo target)
            : base(HarmonyOwnerId)
        {
            _target = target ?? throw new ArgumentNullException(nameof(target));
        }

        protected override MethodBase GetTargetMethod()
        {
            return _target;
        }

        [PatchPrefix]
        [HarmonyPriority(Priority.Last)]
        private static void Prefix(
            ArmorPlateCollider __instance,
            DamageInfo damageInfo)
        {
            PostmortemArmorDamageProcessor.TryApply(
                __instance,
                __instance.ArmorPlateColliderType,
                damageInfo);
        }
    }
}
