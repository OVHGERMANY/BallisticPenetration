using System;
using System.Collections.Generic;
using System.Threading;
using EFT;
using EFT.Ballistics;
using EFT.Interactive;
using EFT.InventoryLogic;
using BallisticPenetration.Core;

namespace BallisticPenetration.Runtime
{
    /// <summary>
    /// Sends forward corpse hits through Tarkov's existing armor durability
    /// method without re-running shot-status, penetration, or ricochet rolls.
    /// The local DamageInfo copy prevents armor processing from altering the
    /// original body-damage path.
    /// </summary>
    internal static class PostmortemArmorDamageProcessor
    {
        private static readonly SkillManager.FloatBuff NeutralLightVestReduction =
            new SkillManager.FloatBuff();

        private static readonly SkillManager.FloatBuff NeutralHeavyVestReduction =
            new SkillManager.FloatBuff();

        private static int _failureLogged;

        internal static void TryApply(
            BodyPartCollider collider,
            EArmorPlateCollider armorPlateCollider,
            DamageInfo damageInfo)
        {
            try
            {
                PluginConfiguration configuration = Plugin.Configuration;
                if (collider == null || configuration == null)
                {
                    return;
                }

                Player retainedPlayer = collider.Player as Player;
                if (retainedPlayer != null
                    && retainedPlayer.HealthController != null
                    && retainedPlayer.HealthController.IsAlive)
                {
                    return;
                }

                Corpse corpse = collider.GetComponentInParent<Corpse>();
                bool isDeadRetainedPlayer = retainedPlayer != null
                    && retainedPlayer.HealthController != null
                    && !retainedPlayer.HealthController.IsAlive;
                bool colliderMatches = object.ReferenceEquals(
                    damageInfo.HittedBallisticCollider,
                    collider);

                if (!PostmortemArmorPolicy.ShouldProcessHit(
                        configuration.Enabled.Value,
                        configuration.DamageArmorOnCorpses.Value,
                        damageInfo.IsForwardHit,
                        colliderMatches,
                        corpse != null || isDeadRetainedPlayer,
                        damageInfo.Damage,
                        damageInfo.PenetrationPower,
                        damageInfo.ArmorDamage))
                {
                    return;
                }

                List<ArmorComponent> armorComponents = new List<ArmorComponent>();
                if (!TryCollectArmorComponents(
                        retainedPlayer,
                        isDeadRetainedPlayer,
                        corpse,
                        armorComponents)
                    || armorComponents.Count == 0)
                {
                    return;
                }

                SkillManager skills = retainedPlayer != null ? retainedPlayer.Skills : null;
                SkillManager.FloatBuff lightVestReduction = skills != null
                    ? skills.LightVestMeleeWeaponDamageReduction
                    : NeutralLightVestReduction;
                SkillManager.FloatBuff heavyVestReduction = skills != null
                    ? skills.HeavyVestBluntThroughputDamageReduction
                    : NeutralHeavyVestReduction;

                DamageInfo localDamageInfo = damageInfo;
                for (int index = 0; index < armorComponents.Count; index++)
                {
                    ArmorComponent armor = armorComponents[index];
                    if (armor == null || armor.Item == null)
                    {
                        continue;
                    }

                    bool armorMatches = armor.ShotMatches(
                        collider.BodyPartColliderType,
                        armorPlateCollider);
                    bool blockedByThisArmor =
                        (MongoID?)armor.Item.Id == localDamageInfo.BlockedBy;
                    bool deflectedByThisArmor =
                        (MongoID?)armor.Item.Id == localDamageInfo.DeflectedBy;
                    PostmortemArmorTraversalStep step =
                        PostmortemArmorPolicy.GetTraversalStep(
                            armorMatches,
                            blockedByThisArmor,
                            deflectedByThisArmor);

                    if (step == PostmortemArmorTraversalStep.Skip)
                    {
                        continue;
                    }

                    // ApplyDamage consumes the decisions already stored in
                    // BlockedBy and DeflectedBy. Never re-query decision methods:
                    // those methods draw random values and mutate the shot.
                    float durabilityBefore = armor.Repairable.Durability;
                    float appliedDamage = armor.ApplyDamage(
                        ref localDamageInfo,
                        collider.BodyPartColliderType,
                        armorPlateCollider,
                        true,
                        armorComponents,
                        lightVestReduction,
                        heavyVestReduction);

                    if (configuration.LogAdjustments.Value)
                    {
                        LogAppliedArmorDamage(
                            armor,
                            collider,
                            armorPlateCollider,
                            appliedDamage,
                            durabilityBefore,
                            armor.Repairable.Durability,
                            blockedByThisArmor,
                            deflectedByThisArmor);
                    }

                    if (step == PostmortemArmorTraversalStep.ApplyAndStop)
                    {
                        break;
                    }
                }
            }
            catch (Exception exception)
            {
                // The original ApplyHit method must always continue.
                if (Interlocked.CompareExchange(ref _failureLogged, 1, 0) == 0)
                {
                    Plugin.LogHookFailure("Postmortem armor durability prefix", exception);
                }
            }
        }

        private static void LogAppliedArmorDamage(
            ArmorComponent armor,
            BodyPartCollider collider,
            EArmorPlateCollider armorPlateCollider,
            float appliedDamage,
            float durabilityBefore,
            float durabilityAfter,
            bool blocked,
            bool deflected)
        {
            try
            {
                Plugin.Log?.LogInfo(
                    "Applied postmortem armor durability: item=" + armor.Item.Id
                    + ", template=" + armor.Item.TemplateId
                    + ", bodyCollider=" + collider.BodyPartColliderType
                    + ", plateCollider=" + armorPlateCollider
                    + ", durability=" + durabilityBefore + " -> " + durabilityAfter
                    + ", applied=" + appliedDamage
                    + ", blocked=" + blocked
                    + ", deflected=" + deflected + ".");
            }
            catch
            {
                // Optional evidence logging must never affect the hit path.
            }
        }

        private static bool TryCollectArmorComponents(
            Player retainedPlayer,
            bool isDeadRetainedPlayer,
            Corpse corpse,
            List<ArmorComponent> armorComponents)
        {
            if (isDeadRetainedPlayer
                && retainedPlayer != null
                && retainedPlayer.Inventory != null)
            {
                retainedPlayer.Inventory.GetPutOnArmorsNonAlloc(armorComponents);
                return true;
            }

            InventoryEquipment equipment = corpse != null
                ? corpse.Item as InventoryEquipment
                : null;
            if (equipment == null)
            {
                return false;
            }

            List<Slot> slots = new List<Slot>(Inventory.ArmorSlots.Length);
            equipment.GetSlotsByNameNonAlloc(Inventory.ArmorSlots, slots);
            for (int index = 0; index < slots.Count; index++)
            {
                Slot slot = slots[index];
                if (slot != null && slot.ContainedItem != null)
                {
                    slot.ContainedItem.GetItemComponentsInChildrenNonAlloc(
                        armorComponents);
                }
            }

            armorComponents.Sort(Inventory.OrderLockedLast);
            return true;
        }
    }
}
