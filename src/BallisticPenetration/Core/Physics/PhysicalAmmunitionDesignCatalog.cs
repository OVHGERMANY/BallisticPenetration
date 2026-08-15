#nullable enable

using System;
using System.Collections.Generic;

namespace BallisticPenetration.Core.Physics
{
    /// <summary>
    /// Exact SPT 4.1.2 ammunition identity mapped to physical construction and terminal design.
    /// The catalog stores no localized game text. Entries whose source data omits metallurgy use a
    /// conservative conventional-projectile profile; payload entries are cataloged but are not
    /// admitted to the kinetic projectile replacement.
    /// </summary>
    public readonly struct PhysicalAmmunitionDesignDefinition
        : IEquatable<PhysicalAmmunitionDesignDefinition>
    {
        internal PhysicalAmmunitionDesignDefinition(
            string templateId,
            PhysicalProjectileConstruction construction,
            PhysicalProjectileDesignClass designClass,
            PhysicalProjectileShapeClass initialShapeClass,
            double fallbackMassKilograms,
            double fallbackDiameterMetres)
        {
            TemplateId = templateId;
            Construction = construction;
            DesignClass = designClass;
            InitialShapeClass = initialShapeClass;
            FallbackMassKilograms = fallbackMassKilograms;
            FallbackDiameterMetres = fallbackDiameterMetres;
        }

        public string TemplateId { get; }

        public PhysicalProjectileConstruction Construction { get; }

        public PhysicalProjectileDesignClass DesignClass { get; }

        public PhysicalProjectileShapeClass InitialShapeClass { get; }

        public double FallbackMassKilograms { get; }

        public double FallbackDiameterMetres { get; }

        public bool HasFallbackPhysicalDimensions
        {
            get
            {
                return FiniteDouble.IsFinite(FallbackMassKilograms)
                    && FallbackMassKilograms > 0d
                    && FiniteDouble.IsFinite(FallbackDiameterMetres)
                    && FallbackDiameterMetres > 0d;
            }
        }

        public bool IsKineticProjectile
        {
            get { return DesignClass != PhysicalProjectileDesignClass.Payload; }
        }

        public bool Equals(PhysicalAmmunitionDesignDefinition other)
        {
            return string.Equals(TemplateId, other.TemplateId, StringComparison.Ordinal)
                && Construction == other.Construction
                && DesignClass == other.DesignClass
                && InitialShapeClass == other.InitialShapeClass
                && FallbackMassKilograms.Equals(other.FallbackMassKilograms)
                && FallbackDiameterMetres.Equals(other.FallbackDiameterMetres);
        }

        public override bool Equals(object? obj)
        {
            return obj is PhysicalAmmunitionDesignDefinition other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = StringComparer.Ordinal.GetHashCode(TemplateId ?? string.Empty);
                hash = (hash * 397) ^ Construction.GetHashCode();
                hash = (hash * 397) ^ DesignClass.GetHashCode();
                hash = (hash * 397) ^ InitialShapeClass.GetHashCode();
                hash = (hash * 397) ^ FallbackMassKilograms.GetHashCode();
                return (hash * 397) ^ FallbackDiameterMetres.GetHashCode();
            }
        }

        public static bool operator ==(
            PhysicalAmmunitionDesignDefinition left,
            PhysicalAmmunitionDesignDefinition right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(
            PhysicalAmmunitionDesignDefinition left,
            PhysicalAmmunitionDesignDefinition right)
        {
            return !left.Equals(right);
        }
    }

    internal static class PhysicalAmmunitionDesignCatalog
    {
        private static readonly Dictionary<string, PhysicalAmmunitionDesignDefinition>
            Definitions = CreateDefinitions();

        internal static int Count
        {
            get { return Definitions.Count; }
        }

        internal static bool TryGetDefinition(
            string? templateId,
            out PhysicalAmmunitionDesignDefinition definition)
        {
            definition = default;
            return !string.IsNullOrWhiteSpace(templateId)
                && Definitions.TryGetValue(templateId, out definition);
        }

        private static Dictionary<string, PhysicalAmmunitionDesignDefinition> CreateDefinitions()
        {
            var definitions = new Dictionary<string, PhysicalAmmunitionDesignDefinition>(
                208,
                StringComparer.Ordinal);
            Add(
                definitions,
                "54527a984bdc2d4e668b4567",
                PhysicalProjectileConstruction.SteelPenetratorLeadCoreJacketed,
                PhysicalProjectileDesignClass.FullMetalJacket,
                PhysicalProjectileShapeClass.Spitzer); // patron_556x45_M855
            Add(
                definitions,
                "54527ac44bdc2d36668b4567",
                PhysicalProjectileConstruction.SteelPenetratorCopperCoreJacketed,
                PhysicalProjectileDesignClass.SemiJacketed,
                PhysicalProjectileShapeClass.Spitzer); // patron_556x45_M855A1
            Add(
                definitions,
                "560d61e84bdc2da74d8b4571",
                PhysicalProjectileConstruction.SteelCoreJacketed,
                PhysicalProjectileDesignClass.FullMetalJacket,
                PhysicalProjectileShapeClass.Spitzer); // patron_762x54R_SNB
            Add(
                definitions,
                "5656d7c34bdc2d9d198b4587",
                PhysicalProjectileConstruction.SteelCoreJacketed,
                PhysicalProjectileDesignClass.FullMetalJacket,
                PhysicalProjectileShapeClass.Spitzer); // patron_762x39_PS
            Add(
                definitions,
                "56d59d3ad2720bdb418b4577",
                PhysicalProjectileConstruction.SteelCoreJacketed,
                PhysicalProjectileDesignClass.SemiJacketed,
                PhysicalProjectileShapeClass.RoundNose); // patron_9x19_PST_gzh
            Add(
                definitions,
                "56dfef82d2720bbd668b4567",
                PhysicalProjectileConstruction.SteelCoreJacketed,
                PhysicalProjectileDesignClass.FullMetalJacket,
                PhysicalProjectileShapeClass.Spitzer); // patron_545x39_BP
            Add(
                definitions,
                "56dff026d2720bb8668b4567",
                PhysicalProjectileConstruction.TungstenCoreJacketed,
                PhysicalProjectileDesignClass.FullMetalJacket,
                PhysicalProjectileShapeClass.Spitzer); // patron_545x39_BS
            Add(
                definitions,
                "56dff061d2720bb5668b4567",
                PhysicalProjectileConstruction.SteelCoreJacketed,
                PhysicalProjectileDesignClass.FullMetalJacket,
                PhysicalProjectileShapeClass.Spitzer); // patron_545x39_BT
            Add(
                definitions,
                "56dff0bed2720bb0668b4567",
                PhysicalProjectileConstruction.LeadCoreJacketed,
                PhysicalProjectileDesignClass.FullMetalJacket,
                PhysicalProjectileShapeClass.Spitzer); // patron_545x39_FMJ
            Add(
                definitions,
                "56dff216d2720bbd668b4568",
                PhysicalProjectileConstruction.LeadCoreJacketed,
                PhysicalProjectileDesignClass.HollowPoint,
                PhysicalProjectileShapeClass.Spitzer); // patron_545x39_HP
            Add(
                definitions,
                "56dff2ced2720bb4668b4567",
                PhysicalProjectileConstruction.SteelCoreJacketed,
                PhysicalProjectileDesignClass.FullMetalJacket,
                PhysicalProjectileShapeClass.Spitzer); // patron_545x39_PP
            Add(
                definitions,
                "56dff338d2720bbd668b4569",
                PhysicalProjectileConstruction.LeadCoreJacketed,
                PhysicalProjectileDesignClass.FullMetalJacket,
                PhysicalProjectileShapeClass.Spitzer); // patron_545x39_PRS
            Add(
                definitions,
                "56dff3afd2720bba668b4567",
                PhysicalProjectileConstruction.SteelCoreJacketed,
                PhysicalProjectileDesignClass.FullMetalJacket,
                PhysicalProjectileShapeClass.Spitzer); // patron_545x39_PS
            Add(
                definitions,
                "56dff421d2720b5f5a8b4567",
                PhysicalProjectileConstruction.LeadCoreJacketed,
                PhysicalProjectileDesignClass.SoftPoint,
                PhysicalProjectileShapeClass.Spitzer); // patron_545x39_SP
            Add(
                definitions,
                "56dff4a2d2720bbd668b456a",
                PhysicalProjectileConstruction.LeadCoreJacketed,
                PhysicalProjectileDesignClass.FullMetalJacket,
                PhysicalProjectileShapeClass.Spitzer); // patron_545x39_T
            Add(
                definitions,
                "56dff4ecd2720b5f5a8b4568",
                PhysicalProjectileConstruction.TungstenCoreJacketed,
                PhysicalProjectileDesignClass.FullMetalJacket,
                PhysicalProjectileShapeClass.Spitzer); // patron_545x39_US
            Add(
                definitions,
                "5735fdcd2459776445391d61",
                PhysicalProjectileConstruction.LeadCoreJacketed,
                PhysicalProjectileDesignClass.FullMetalJacket,
                PhysicalProjectileShapeClass.Spitzer); // patron_762x25tt_akbs
            Add(
                definitions,
                "5735ff5c245977640e39ba7e",
                PhysicalProjectileConstruction.LeadCoreJacketed,
                PhysicalProjectileDesignClass.FullMetalJacket,
                PhysicalProjectileShapeClass.Spitzer); // patron_762x25tt_FMJ43
            Add(
                definitions,
                "573601b42459776410737435",
                PhysicalProjectileConstruction.LeadCoreJacketed,
                PhysicalProjectileDesignClass.FullMetalJacket,
                PhysicalProjectileShapeClass.Spitzer); // patron_762x25tt_LRN
            Add(
                definitions,
                "573602322459776445391df1",
                PhysicalProjectileConstruction.LeadCoreJacketed,
                PhysicalProjectileDesignClass.FullMetalJacket,
                PhysicalProjectileShapeClass.Spitzer); // patron_762x25tt_LRNPC
            Add(
                definitions,
                "5736026a245977644601dc61",
                PhysicalProjectileConstruction.LeadCoreJacketed,
                PhysicalProjectileDesignClass.FullMetalJacket,
                PhysicalProjectileShapeClass.Spitzer); // patron_762x25tt_P_Gl
            Add(
                definitions,
                "573603562459776430731618",
                PhysicalProjectileConstruction.SteelCoreJacketed,
                PhysicalProjectileDesignClass.FullMetalJacket,
                PhysicalProjectileShapeClass.Spitzer); // patron_762x25tt_Pst_gzh
            Add(
                definitions,
                "573603c924597764442bd9cb",
                PhysicalProjectileConstruction.LeadCoreJacketed,
                PhysicalProjectileDesignClass.FullMetalJacket,
                PhysicalProjectileShapeClass.Spitzer); // patron_762x25tt_T_Gzh
            Add(
                definitions,
                "573718ba2459775a75491131",
                PhysicalProjectileConstruction.SteelCoreJacketed,
                PhysicalProjectileDesignClass.FullMetalJacket,
                PhysicalProjectileShapeClass.RoundNose); // patron_9x18pm_BZT_gzh
            Add(
                definitions,
                "573719762459775a626ccbc1",
                PhysicalProjectileConstruction.LeadCoreJacketed,
                PhysicalProjectileDesignClass.FullMetalJacket,
                PhysicalProjectileShapeClass.RoundNose); // patron_9x18pm_P_gzh
            Add(
                definitions,
                "573719df2459775a626ccbc2",
                PhysicalProjectileConstruction.SteelCoreJacketed,
                PhysicalProjectileDesignClass.SemiJacketed,
                PhysicalProjectileShapeClass.RoundNose); // patron_9x18pm_PBM
            Add(
                definitions,
                "57371aab2459775a77142f22",
                PhysicalProjectileConstruction.SteelCoreJacketed,
                PhysicalProjectileDesignClass.FullMetalJacket,
                PhysicalProjectileShapeClass.RoundNose); // patron_9x18pm_PMM
            Add(
                definitions,
                "57371b192459775a9f58a5e0",
                PhysicalProjectileConstruction.LeadCoreJacketed,
                PhysicalProjectileDesignClass.HollowPoint,
                PhysicalProjectileShapeClass.RoundNose); // patron_9x18pm_PPE_gzh
            Add(
                definitions,
                "57371e4124597760ff7b25f1",
                PhysicalProjectileConstruction.LeadCoreJacketed,
                PhysicalProjectileDesignClass.FullMetalJacket,
                PhysicalProjectileShapeClass.RoundNose); // patron_9x18pm_PPT_gzh
            Add(
                definitions,
                "57371eb62459776125652ac1",
                PhysicalProjectileConstruction.LeadCoreJacketed,
                PhysicalProjectileDesignClass.FullMetalJacket,
                PhysicalProjectileShapeClass.RoundNose); // patron_9x18pm_PRS_gs
            Add(
                definitions,
                "57371f2b24597761224311f1",
                PhysicalProjectileConstruction.LeadCoreJacketed,
                PhysicalProjectileDesignClass.FullMetalJacket,
                PhysicalProjectileShapeClass.RoundNose); // patron_9x18pm_PS_gs_PPO
            Add(
                definitions,
                "57371f8d24597761006c6a81",
                PhysicalProjectileConstruction.LeadCoreJacketed,
                PhysicalProjectileDesignClass.FullMetalJacket,
                PhysicalProjectileShapeClass.RoundNose); // patron_9x18pm_PSO_gzh
            Add(
                definitions,
                "5737201124597760fc4431f1",
                PhysicalProjectileConstruction.SteelCoreJacketed,
                PhysicalProjectileDesignClass.FullMetalJacket,
                PhysicalProjectileShapeClass.RoundNose); // patron_9x18pm_PST_gzh
            Add(
                definitions,
                "5737207f24597760ff7b25f2",
                PhysicalProjectileConstruction.LeadCoreJacketed,
                PhysicalProjectileDesignClass.HollowPoint,
                PhysicalProjectileShapeClass.RoundNose); // patron_9x18pm_PSV
            Add(
                definitions,
                "573720e02459776143012541",
                PhysicalProjectileConstruction.SteelCoreJacketed,
                PhysicalProjectileDesignClass.HollowPoint,
                PhysicalProjectileShapeClass.RoundNose); // patron_9x18pm_RG028_gzh
            Add(
                definitions,
                "57372140245977611f70ee91",
                PhysicalProjectileConstruction.LeadCoreJacketed,
                PhysicalProjectileDesignClass.Expanding,
                PhysicalProjectileShapeClass.RoundNose); // patron_9x18pm_SP7_gzh
            Add(
                definitions,
                "5737218f245977612125ba51",
                PhysicalProjectileConstruction.FrangibleComposite,
                PhysicalProjectileDesignClass.Frangible,
                PhysicalProjectileShapeClass.RoundNose); // patron_9x18pm_SP8_gzh
            Add(
                definitions,
                "57a0dfb82459774d3078b56c",
                PhysicalProjectileConstruction.SteelCoreJacketed,
                PhysicalProjectileDesignClass.FullMetalJacket,
                PhysicalProjectileShapeClass.Spitzer); // patron_9x39_sp5
            Add(
                definitions,
                "57a0e5022459774d1673f889",
                PhysicalProjectileConstruction.SteelCoreJacketed,
                PhysicalProjectileDesignClass.SemiJacketed,
                PhysicalProjectileShapeClass.Spitzer); // patron_9x39_sp6
            Add(
                definitions,
                "58864a4f2459770fcc257101",
                PhysicalProjectileConstruction.LeadCoreJacketed,
                PhysicalProjectileDesignClass.Expanding,
                PhysicalProjectileShapeClass.RoundNose); // patron_9x19_PSO_gzh
            Add(
                definitions,
                "5887431f2459777e1612938f",
                PhysicalProjectileConstruction.SteelCoreJacketed,
                PhysicalProjectileDesignClass.FullMetalJacket,
                PhysicalProjectileShapeClass.Spitzer); // patron_762x54R_LPS_Gzh
            Add(
                definitions,
                "58dd3ad986f77403051cba8f",
                PhysicalProjectileConstruction.LeadCoreJacketed,
                PhysicalProjectileDesignClass.FullMetalJacket,
                PhysicalProjectileShapeClass.Spitzer); // patron_762x51_M80
            Add(
                definitions,
                "5943d9c186f7745a13413ac9",
                PhysicalProjectileConstruction.MonolithicSteel,
                PhysicalProjectileDesignClass.Fragment,
                PhysicalProjectileShapeClass.IrregularProjectileFragment); // shrapnel
            Add(
                definitions,
                "5996f6cb86f774678763a6ca",
                PhysicalProjectileConstruction.MonolithicSteel,
                PhysicalProjectileDesignClass.Fragment,
                PhysicalProjectileShapeClass.IrregularProjectileFragment); // shrapnel_RGD5
            Add(
                definitions,
                "5996f6d686f77467977ba6cc",
                PhysicalProjectileConstruction.MonolithicSteel,
                PhysicalProjectileDesignClass.Fragment,
                PhysicalProjectileShapeClass.IrregularProjectileFragment); // shrapnel_F1
            Add(
                definitions,
                "5996f6fc86f7745e585b4de3",
                PhysicalProjectileConstruction.MonolithicSteel,
                PhysicalProjectileDesignClass.Fragment,
                PhysicalProjectileShapeClass.IrregularProjectileFragment); // shrapnel_m67
            Add(
                definitions,
                "59e0d99486f7744a32234762",
                PhysicalProjectileConstruction.SteelCoreJacketed,
                PhysicalProjectileDesignClass.FullMetalJacket,
                PhysicalProjectileShapeClass.Spitzer); // patron_762x39_BP
            Add(
                definitions,
                "59e4cf5286f7741778269d8a",
                PhysicalProjectileConstruction.LeadCoreJacketed,
                PhysicalProjectileDesignClass.FullMetalJacket,
                PhysicalProjectileShapeClass.Spitzer); // patron_762x39_T45M
            Add(
                definitions,
                "59e4d24686f7741776641ac7",
                PhysicalProjectileConstruction.SteelCoreJacketed,
                PhysicalProjectileDesignClass.FullMetalJacket,
                PhysicalProjectileShapeClass.Spitzer); // patron_762x39_US
            Add(
                definitions,
                "59e4d3d286f774176a36250a",
                PhysicalProjectileConstruction.LeadCoreJacketed,
                PhysicalProjectileDesignClass.HollowPoint,
                PhysicalProjectileShapeClass.Spitzer); // patron_762x39_HP
            Add(
                definitions,
                "59e6542b86f77411dc52a77a",
                PhysicalProjectileConstruction.LeadCoreJacketed,
                PhysicalProjectileDesignClass.FullMetalJacket,
                PhysicalProjectileShapeClass.Spitzer); // patron_366_TKM_FMJ
            Add(
                definitions,
                "59e655cb86f77411dc52a77b",
                PhysicalProjectileConstruction.MonolithicZinc,
                PhysicalProjectileDesignClass.Solid,
                PhysicalProjectileShapeClass.Spitzer); // patron_366_TKM_EKO
            Add(
                definitions,
                "59e6658b86f77411d949b250",
                PhysicalProjectileConstruction.LeadCoreJacketed,
                PhysicalProjectileDesignClass.SoftPoint,
                PhysicalProjectileShapeClass.Spitzer); // patron_366_TKM_Geksa
            Add(
                definitions,
                "59e68f6f86f7746c9f75e846",
                PhysicalProjectileConstruction.LeadCoreJacketed,
                PhysicalProjectileDesignClass.FullMetalJacket,
                PhysicalProjectileShapeClass.Spitzer); // patron_556x45_M856
            Add(
                definitions,
                "59e6906286f7746c9f75e847",
                PhysicalProjectileConstruction.CopperAlloyCoreJacketed,
                PhysicalProjectileDesignClass.FullMetalJacket,
                PhysicalProjectileShapeClass.Spitzer); // patron_556x45_M856A1
            Add(
                definitions,
                "59e690b686f7746c9f75e848",
                PhysicalProjectileConstruction.TungstenCoreJacketed,
                PhysicalProjectileDesignClass.FullMetalJacket,
                PhysicalProjectileShapeClass.Spitzer); // patron_556x45_M995
            Add(
                definitions,
                "59e6918f86f7746c9f75e849",
                PhysicalProjectileConstruction.FrangibleComposite,
                PhysicalProjectileDesignClass.Frangible,
                PhysicalProjectileShapeClass.Spitzer); // patron_556x45_MK_255_Mod_0
            Add(
                definitions,
                "59e6920f86f77411d82aa167",
                PhysicalProjectileConstruction.LeadCoreJacketed,
                PhysicalProjectileDesignClass.FullMetalJacket,
                PhysicalProjectileShapeClass.Spitzer); // patron_556x45_55_FMJ
            Add(
                definitions,
                "59e6927d86f77411da468256",
                PhysicalProjectileConstruction.LeadCoreJacketed,
                PhysicalProjectileDesignClass.HollowPoint,
                PhysicalProjectileShapeClass.Spitzer); // patron_556x45_55_HP
            Add(
                definitions,
                "59e77a2386f7742ee578960a",
                PhysicalProjectileConstruction.SteelCoreJacketed,
                PhysicalProjectileDesignClass.FullMetalJacket,
                PhysicalProjectileShapeClass.Spitzer); // patron_762x54R_7N1
            Add(
                definitions,
                "5a269f97c4a282000b151807",
                PhysicalProjectileConstruction.SteelCoreJacketed,
                PhysicalProjectileDesignClass.SemiJacketed,
                PhysicalProjectileShapeClass.RoundNose); // patron_9x21_sp10
            Add(
                definitions,
                "5a26abfac4a28232980eabff",
                PhysicalProjectileConstruction.LeadCoreJacketed,
                PhysicalProjectileDesignClass.FullMetalJacket,
                PhysicalProjectileShapeClass.RoundNose); // patron_9x21_sp11
            Add(
                definitions,
                "5a26ac06c4a282000c5a90a8",
                PhysicalProjectileConstruction.LeadCoreJacketed,
                PhysicalProjectileDesignClass.PolymerTipped,
                PhysicalProjectileShapeClass.Spitzer); // patron_9x21_sp12
            Add(
                definitions,
                "5a26ac0ec4a28200741e1e18",
                PhysicalProjectileConstruction.SteelCoreJacketed,
                PhysicalProjectileDesignClass.SemiJacketed,
                PhysicalProjectileShapeClass.RoundNose); // patron_9x21_sp13
            Add(
                definitions,
                "5a3c16fe86f77452b62de32a",
                PhysicalProjectileConstruction.LeadCoreJacketed,
                PhysicalProjectileDesignClass.FullMetalJacket,
                PhysicalProjectileShapeClass.RoundNose); // patron_9x19_luger_cci
            Add(
                definitions,
                "5a6086ea4f39f99cd479502f",
                PhysicalProjectileConstruction.SteelCoreJacketed,
                PhysicalProjectileDesignClass.FullMetalJacket,
                PhysicalProjectileShapeClass.Spitzer); // patron_762x51_M61
            Add(
                definitions,
                "5a608bf24f39f98ffc77720e",
                PhysicalProjectileConstruction.LeadCoreJacketed,
                PhysicalProjectileDesignClass.FullMetalJacket,
                PhysicalProjectileShapeClass.Spitzer); // patron_762x51_M62
            Add(
                definitions,
                "5ba2678ad4351e44f824b344",
                PhysicalProjectileConstruction.LeadCoreJacketed,
                PhysicalProjectileDesignClass.FullMetalJacket,
                PhysicalProjectileShapeClass.RoundNose); // patron_46x30_fmj_sx
            Add(
                definitions,
                "5ba26812d4351e003201fef1",
                PhysicalProjectileConstruction.MonolithicBrass,
                PhysicalProjectileDesignClass.HollowPoint,
                PhysicalProjectileShapeClass.RoundNose); // patron_46x30_action_sx
            Add(
                definitions,
                "5ba26835d4351e0035628ff5",
                PhysicalProjectileConstruction.SteelCoreJacketed,
                PhysicalProjectileDesignClass.FullMetalJacket,
                PhysicalProjectileShapeClass.RoundNose); // patron_46x30_ap_sx
            Add(
                definitions,
                "5ba26844d4351e00334c9475",
                PhysicalProjectileConstruction.TungstenCoreJacketed,
                PhysicalProjectileDesignClass.FullMetalJacket,
                PhysicalProjectileShapeClass.RoundNose); // patron_46x30_subsonic_sx
            Add(
                definitions,
                "5c0d56a986f774449d5de529",
                PhysicalProjectileConstruction.MonolithicCopper,
                PhysicalProjectileDesignClass.Frangible,
                PhysicalProjectileShapeClass.RoundNose); // patron_9x19_rip
            Add(
                definitions,
                "5c0d5ae286f7741e46554302",
                PhysicalProjectileConstruction.LeadCoreJacketed,
                PhysicalProjectileDesignClass.PolymerTipped,
                PhysicalProjectileShapeClass.Spitzer); // patron_556x45_varmageddon
            Add(
                definitions,
                "5c0d5e4486f77478390952fe",
                PhysicalProjectileConstruction.TungstenCoreJacketed,
                PhysicalProjectileDesignClass.FullMetalJacket,
                PhysicalProjectileShapeClass.Spitzer); // patron_545x39_7n39
            Add(
                definitions,
                "5c0d668f86f7747ccb7f13b2",
                PhysicalProjectileConstruction.SteelCoreJacketed,
                PhysicalProjectileDesignClass.SemiJacketed,
                PhysicalProjectileShapeClass.Spitzer); // patron_9x39_spp
            Add(
                definitions,
                "5c0d688c86f77413ae3407b2",
                PhysicalProjectileConstruction.SteelCoreJacketed,
                PhysicalProjectileDesignClass.SemiJacketed,
                PhysicalProjectileShapeClass.Spitzer); // patron_9x39_bp
            Add(
                definitions,
                "5c3df7d588a4501f290594e5",
                PhysicalProjectileConstruction.SteelCoreJacketed,
                PhysicalProjectileDesignClass.FullMetalJacket,
                PhysicalProjectileShapeClass.RoundNose); // patron_9x19_GT
            Add(
                definitions,
                "5c925fa22e221601da359b7b",
                PhysicalProjectileConstruction.SteelCoreJacketed,
                PhysicalProjectileDesignClass.SabotedPenetrator,
                PhysicalProjectileShapeClass.Spitzer); // patron_9x19_ap_63
            Add(
                definitions,
                "5cadf6ddae9215051e1c23b2",
                PhysicalProjectileConstruction.LeadCoreJacketed,
                PhysicalProjectileDesignClass.FullMetalJacket,
                PhysicalProjectileShapeClass.Spitzer); // patron_127x55_ps12
            Add(
                definitions,
                "5cadf6e5ae921500113bb973",
                PhysicalProjectileConstruction.AluminumCoreJacketed,
                PhysicalProjectileDesignClass.SemiJacketed,
                PhysicalProjectileShapeClass.Spitzer); // patron_127x55_ps12a
            Add(
                definitions,
                "5cadf6eeae921500134b2799",
                PhysicalProjectileConstruction.SteelCoreJacketed,
                PhysicalProjectileDesignClass.SemiJacketed,
                PhysicalProjectileShapeClass.Spitzer); // patron_127x55_ps12b
            Add(
                definitions,
                "5cc80f38e4a949001152b560",
                PhysicalProjectileConstruction.SteelPenetratorAluminumCoreJacketed,
                PhysicalProjectileDesignClass.FullMetalJacket,
                PhysicalProjectileShapeClass.RoundNose); // patron_57x28_ss190
            Add(
                definitions,
                "5cc80f53e4a949000e1ea4f8",
                PhysicalProjectileConstruction.SteelPenetratorAluminumCoreJacketed,
                PhysicalProjectileDesignClass.FullMetalJacket,
                PhysicalProjectileShapeClass.RoundNose); // patron_57x28_l191
            Add(
                definitions,
                "5cc80f67e4a949035e43bbba",
                PhysicalProjectileConstruction.LeadCoreJacketed,
                PhysicalProjectileDesignClass.FullMetalJacket,
                PhysicalProjectileShapeClass.RoundNose); // patron_57x28_sb193
            Add(
                definitions,
                "5cc80f79e4a949033c7343b2",
                PhysicalProjectileConstruction.AluminumCoreJacketed,
                PhysicalProjectileDesignClass.OpenTip,
                PhysicalProjectileShapeClass.RoundNose); // patron_57x28_ss198lf
            Add(
                definitions,
                "5cc80f8fe4a949033b0224a2",
                PhysicalProjectileConstruction.LeadCoreJacketed,
                PhysicalProjectileDesignClass.PolymerTipped,
                PhysicalProjectileShapeClass.Spitzer); // patron_57x28_ss197sr
            Add(
                definitions,
                "5cc86832d7f00c000d3a6e6c",
                PhysicalProjectileConstruction.MonolithicCopper,
                PhysicalProjectileDesignClass.Frangible,
                PhysicalProjectileShapeClass.RoundNose); // patron_57x28_r37f
            Add(
                definitions,
                "5cc86840d7f00c002412c56c",
                PhysicalProjectileConstruction.MonolithicCopper,
                PhysicalProjectileDesignClass.Expanding,
                PhysicalProjectileShapeClass.RoundNose); // patron_57x28_r37x
            Add(
                definitions,
                "5cde8864d7f00c0010373be1",
                PhysicalProjectileConstruction.SteelCoreJacketed,
                PhysicalProjectileDesignClass.FullMetalJacket,
                PhysicalProjectileShapeClass.Spitzer); // patron_127x108
            Add(
                definitions,
                "5d2f2ab648f03550091993ca",
                PhysicalProjectileConstruction.SteelCoreJacketed,
                PhysicalProjectileDesignClass.FullMetalJacket,
                PhysicalProjectileShapeClass.Spitzer); // patron_127x108_bzt
            Add(
                definitions,
                "5e023cf8186a883be655e54f",
                PhysicalProjectileConstruction.LeadCoreJacketed,
                PhysicalProjectileDesignClass.FullMetalJacket,
                PhysicalProjectileShapeClass.Spitzer); // patron_762x54r_t46m
            Add(
                definitions,
                "5e023d34e8a400319a28ed44",
                PhysicalProjectileConstruction.SteelCoreJacketed,
                PhysicalProjectileDesignClass.FullMetalJacket,
                PhysicalProjectileShapeClass.Spitzer); // patron_762x54r_7bt1
            Add(
                definitions,
                "5e023d48186a883be655e551",
                PhysicalProjectileConstruction.TungstenCoreJacketed,
                PhysicalProjectileDesignClass.FullMetalJacket,
                PhysicalProjectileShapeClass.Spitzer); // patron_762x54r_7n37
            Add(
                definitions,
                "5e023e53d4353e3302577c4c",
                PhysicalProjectileConstruction.LeadCoreJacketed,
                PhysicalProjectileDesignClass.FullMetalJacket,
                PhysicalProjectileShapeClass.Spitzer); // patron_762x51_bpz_fmj
            Add(
                definitions,
                "5e023e6e34d52a55c3304f71",
                PhysicalProjectileConstruction.LeadCoreJacketed,
                PhysicalProjectileDesignClass.SoftPoint,
                PhysicalProjectileShapeClass.Spitzer); // patron_762x51_tpz_sp
            Add(
                definitions,
                "5e023e88277cce2b522ff2b1",
                PhysicalProjectileConstruction.LeadCoreJacketed,
                PhysicalProjectileDesignClass.SoftPoint,
                PhysicalProjectileShapeClass.Spitzer); // patron_762x51_ultra_nosler
            Add(
                definitions,
                "5e81f423763d9f754677bf2e",
                PhysicalProjectileConstruction.LeadCoreJacketed,
                PhysicalProjectileDesignClass.FullMetalJacket,
                PhysicalProjectileShapeClass.RoundNose); // patron_1143x23_acp
            Add(
                definitions,
                "5e85a9f4add9fe03027d9bf1",
                PhysicalProjectileConstruction.NonMetallicComposite,
                PhysicalProjectileDesignClass.Payload,
                PhysicalProjectileShapeClass.RoundNose); // patron_23x75_star
            Add(
                definitions,
                "5e85aa1a988a8701445df1f5",
                PhysicalProjectileConstruction.MonolithicSteel,
                PhysicalProjectileDesignClass.Solid,
                PhysicalProjectileShapeClass.FlatNose); // patron_23x75_barricade
            Add(
                definitions,
                "5e85aac65505fa48730d8af2",
                PhysicalProjectileConstruction.NonMetallicComposite,
                PhysicalProjectileDesignClass.Payload,
                PhysicalProjectileShapeClass.RoundNose); // patron_23x75_cheremukha_7m
            Add(
                definitions,
                "5ea2a8e200685063ec28c05a",
                PhysicalProjectileConstruction.MonolithicCopper,
                PhysicalProjectileDesignClass.Frangible,
                PhysicalProjectileShapeClass.RoundNose); // patron_1143x23_rip
            Add(
                definitions,
                "5efb0c1bd79ff02a1f5e68d9",
                PhysicalProjectileConstruction.TungstenCoreJacketed,
                PhysicalProjectileDesignClass.FullMetalJacket,
                PhysicalProjectileShapeClass.Spitzer); // patron_762x51_m993
            Add(
                definitions,
                "5efb0cabfb3e451d70735af5",
                PhysicalProjectileConstruction.SteelCoreJacketed,
                PhysicalProjectileDesignClass.SabotedPenetrator,
                PhysicalProjectileShapeClass.Spitzer); // patron_1143x23_acp_ap
            Add(
                definitions,
                "5efb0d4f4bc50b58e81710f3",
                PhysicalProjectileConstruction.LeadCoreJacketed,
                PhysicalProjectileDesignClass.FullMetalJacket,
                PhysicalProjectileShapeClass.RoundNose); // patron_1143x23_acp_lasermatch_fmj
            Add(
                definitions,
                "5efb0da7a29a85116f6ea05f",
                PhysicalProjectileConstruction.SteelCoreJacketed,
                PhysicalProjectileDesignClass.SemiJacketed,
                PhysicalProjectileShapeClass.RoundNose); // patron_9x19_7n31
            Add(
                definitions,
                "5efb0e16aeb21837e749c7ff",
                PhysicalProjectileConstruction.LeadCoreJacketed,
                PhysicalProjectileDesignClass.HollowPoint,
                PhysicalProjectileShapeClass.RoundNose); // patron_9x19_quakemaker
            Add(
                definitions,
                "5efb0fc6aeb21837e749c801",
                PhysicalProjectileConstruction.LeadCoreJacketed,
                PhysicalProjectileDesignClass.HollowPoint,
                PhysicalProjectileShapeClass.RoundNose); // patron_1143x23_acp_hydra_shok
            Add(
                definitions,
                "5f0596629e22f464da6bbdd9",
                PhysicalProjectileConstruction.SteelCoreJacketed,
                PhysicalProjectileDesignClass.SemiJacketed,
                PhysicalProjectileShapeClass.Spitzer); // patron_366_custom_ap
            Add(
                definitions,
                "5fbe3ffdf8b6a877a729ea82",
                PhysicalProjectileConstruction.LeadCoreJacketed,
                PhysicalProjectileDesignClass.FullMetalJacket,
                PhysicalProjectileShapeClass.Spitzer); // patron_762x35_blackout
            Add(
                definitions,
                "5fc275cf85fd526b824a571a",
                PhysicalProjectileConstruction.LeadCoreJacketed,
                PhysicalProjectileDesignClass.FullMetalJacket,
                PhysicalProjectileShapeClass.Spitzer); // patron_86x70_lapua_magnum
            Add(
                definitions,
                "5fc382a9d724d907e2077dab",
                PhysicalProjectileConstruction.TungstenCoreJacketed,
                PhysicalProjectileDesignClass.SabotedPenetrator,
                PhysicalProjectileShapeClass.Spitzer); // patron_86x70_lapua_ap
            Add(
                definitions,
                "5fc382b6d6fa9c00c571bbc3",
                PhysicalProjectileConstruction.MonolithicCopper,
                PhysicalProjectileDesignClass.Expanding,
                PhysicalProjectileShapeClass.Spitzer); // patron_86x70_lapua_tac_x
            Add(
                definitions,
                "5fc382c1016cce60e8341b20",
                PhysicalProjectileConstruction.LeadCoreJacketed,
                PhysicalProjectileDesignClass.FullMetalJacket,
                PhysicalProjectileShapeClass.Spitzer); // patron_86x70_lapua_magnum_upz
            Add(
                definitions,
                "5fd20ff893a8961fc660a954",
                PhysicalProjectileConstruction.SteelPenetratorCopperCoreJacketed,
                PhysicalProjectileDesignClass.SemiJacketed,
                PhysicalProjectileShapeClass.Spitzer); // patron_762x35_blackout_ap
            Add(
                definitions,
                "60194943740c5d77f6705eea",
                PhysicalProjectileConstruction.LeadCoreJacketed,
                PhysicalProjectileDesignClass.FullMetalJacket,
                PhysicalProjectileShapeClass.Spitzer); // patron_556x45_mk_318_mod_0
            Add(
                definitions,
                "601949593ae8f707c4608daa",
                PhysicalProjectileConstruction.TungstenCoreJacketed,
                PhysicalProjectileDesignClass.SabotedPenetrator,
                PhysicalProjectileShapeClass.Spitzer); // patron_556x45_ssa_ap
            Add(
                definitions,
                "601aa3d2b2bcb34913271e6d",
                PhysicalProjectileConstruction.TungstenCoreJacketed,
                PhysicalProjectileDesignClass.SabotedPenetrator,
                PhysicalProjectileShapeClass.Spitzer); // patron_762x39_mai_ap
            Add(
                definitions,
                "61962b617c6c7b169525f168",
                PhysicalProjectileConstruction.SteelCoreJacketed,
                PhysicalProjectileDesignClass.FullMetalJacket,
                PhysicalProjectileShapeClass.Spitzer); // patron_545x39_7n40
            Add(
                definitions,
                "61962d879bb3d20b0946d385",
                PhysicalProjectileConstruction.SteelCoreJacketed,
                PhysicalProjectileDesignClass.SemiJacketed,
                PhysicalProjectileShapeClass.Spitzer); // patron_9x39_pab9
            Add(
                definitions,
                "6196364158ef8c428c287d9f",
                PhysicalProjectileConstruction.LeadCoreJacketed,
                PhysicalProjectileDesignClass.PolymerTipped,
                PhysicalProjectileShapeClass.Spitzer); // patron_762x35_vmax
            Add(
                definitions,
                "6196365d58ef8c428c287da1",
                PhysicalProjectileConstruction.AluminumCoreJacketed,
                PhysicalProjectileDesignClass.FullMetalJacket,
                PhysicalProjectileShapeClass.Spitzer); // patron_762x35_whisper
            Add(
                definitions,
                "619636be6db0f2477964e710",
                PhysicalProjectileConstruction.LeadCoreJacketed,
                PhysicalProjectileDesignClass.FullMetalJacket,
                PhysicalProjectileShapeClass.Spitzer); // patron_762x35_m62
            Add(
                definitions,
                "62330b3ed4dc74626d570b95",
                PhysicalProjectileConstruction.LeadCoreJacketed,
                PhysicalProjectileDesignClass.FullMetalJacket,
                PhysicalProjectileShapeClass.RoundNose); // patron_9x33r_fmj
            Add(
                definitions,
                "62330bfadc5883093563729b",
                PhysicalProjectileConstruction.MonolithicCopper,
                PhysicalProjectileDesignClass.HollowPoint,
                PhysicalProjectileShapeClass.RoundNose); // patron_9x33r_hp
            Add(
                definitions,
                "62330c18744e5e31df12f516",
                PhysicalProjectileConstruction.LeadCoreJacketed,
                PhysicalProjectileDesignClass.HollowPoint,
                PhysicalProjectileShapeClass.RoundNose); // patron_9x33r_jhp
            Add(
                definitions,
                "62330c40bdd19b369e1e53d1",
                PhysicalProjectileConstruction.LeadCoreJacketed,
                PhysicalProjectileDesignClass.SoftPoint,
                PhysicalProjectileShapeClass.RoundNose); // patron_9x33r_sp
            Add(
                definitions,
                "62389aaba63f32501b1b444f",
                PhysicalProjectileConstruction.NonMetallicComposite,
                PhysicalProjectileDesignClass.Payload,
                PhysicalProjectileShapeClass.RoundNose); // patron_26x75_green
            Add(
                definitions,
                "62389ba9a63f32501b1b4451",
                PhysicalProjectileConstruction.NonMetallicComposite,
                PhysicalProjectileDesignClass.Payload,
                PhysicalProjectileShapeClass.RoundNose); // patron_26x75_red
            Add(
                definitions,
                "62389bc9423ed1685422dc57",
                PhysicalProjectileConstruction.NonMetallicComposite,
                PhysicalProjectileDesignClass.Payload,
                PhysicalProjectileShapeClass.RoundNose); // patron_26x75_white
            Add(
                definitions,
                "62389be94d5d474bf712e709",
                PhysicalProjectileConstruction.NonMetallicComposite,
                PhysicalProjectileDesignClass.Payload,
                PhysicalProjectileShapeClass.RoundNose); // patron_26x75_yellow
            Add(
                definitions,
                "6241c316234b593b5676b637",
                PhysicalProjectileConstruction.NonMetallicComposite,
                PhysicalProjectileDesignClass.Solid,
                PhysicalProjectileShapeClass.Spitzer); // patron_6mm_airsoft
            Add(
                definitions,
                "624c0570c9b794431568f5d5",
                PhysicalProjectileConstruction.NonMetallicComposite,
                PhysicalProjectileDesignClass.Payload,
                PhysicalProjectileShapeClass.RoundNose); // patron_rsp_green
            Add(
                definitions,
                "624c09cfbc2e27219346d955",
                PhysicalProjectileConstruction.NonMetallicComposite,
                PhysicalProjectileDesignClass.Payload,
                PhysicalProjectileShapeClass.RoundNose); // patron_rsp_red
            Add(
                definitions,
                "624c09da2cec124eb67c1046",
                PhysicalProjectileConstruction.NonMetallicComposite,
                PhysicalProjectileDesignClass.Payload,
                PhysicalProjectileShapeClass.RoundNose); // patron_rsp_white
            Add(
                definitions,
                "624c09e49b98e019a3315b66",
                PhysicalProjectileConstruction.NonMetallicComposite,
                PhysicalProjectileDesignClass.Payload,
                PhysicalProjectileShapeClass.RoundNose); // patron_rsp_yellow
            Add(
                definitions,
                "635267f063651329f75a4ee8",
                PhysicalProjectileConstruction.NonMetallicComposite,
                PhysicalProjectileDesignClass.Payload,
                PhysicalProjectileShapeClass.RoundNose); // patron_26x75_acidgreen
            Add(
                definitions,
                "63b35f281745dd52341e5da7",
                PhysicalProjectileConstruction.MonolithicSteel,
                PhysicalProjectileDesignClass.Fragment,
                PhysicalProjectileShapeClass.IrregularProjectileFragment); // shrapnel_F1_new
            Add(
                definitions,
                "64b6979341772715af0f9c39",
                PhysicalProjectileConstruction.LeadCoreJacketed,
                PhysicalProjectileDesignClass.SoftPoint,
                PhysicalProjectileShapeClass.RoundNose); // patron_46x30_jsp
            Add(
                definitions,
                "64b7af434b75259c590fa893",
                PhysicalProjectileConstruction.SteelCoreJacketed,
                PhysicalProjectileDesignClass.FullMetalJacket,
                PhysicalProjectileShapeClass.Spitzer); // patron_762x39_pp
            Add(
                definitions,
                "64b7af5a8532cf95ee0a0dbd",
                PhysicalProjectileConstruction.LeadCoreJacketed,
                PhysicalProjectileDesignClass.FullMetalJacket,
                PhysicalProjectileShapeClass.Spitzer); // patron_762x39_fmj
            Add(
                definitions,
                "64b7af734b75259c590fa895",
                PhysicalProjectileConstruction.LeadCoreJacketed,
                PhysicalProjectileDesignClass.SoftPoint,
                PhysicalProjectileShapeClass.Spitzer); // patron_762x39_sp
            Add(
                definitions,
                "64b7bbb74b75259c590fa897",
                PhysicalProjectileConstruction.LeadCoreJacketed,
                PhysicalProjectileDesignClass.FullMetalJacket,
                PhysicalProjectileShapeClass.RoundNose); // patron_9x19_m882
            Add(
                definitions,
                "64b8725c4b75259c590fa899",
                PhysicalProjectileConstruction.TungstenCoreJacketed,
                PhysicalProjectileDesignClass.SabotedPenetrator,
                PhysicalProjectileShapeClass.Spitzer); // patron_762x35_cbj
            Add(
                definitions,
                "64b8f7968532cf95ee0a0dbf",
                PhysicalProjectileConstruction.LeadCoreJacketed,
                PhysicalProjectileDesignClass.FullMetalJacket,
                PhysicalProjectileShapeClass.Spitzer); // patron_762x54r_fmj
            Add(
                definitions,
                "64b8f7b5389d7ffd620ccba2",
                PhysicalProjectileConstruction.LeadCoreJacketed,
                PhysicalProjectileDesignClass.SoftPoint,
                PhysicalProjectileShapeClass.Spitzer); // patron_762x54r_spbt
            Add(
                definitions,
                "64b8f7c241772715af0f9c3d",
                PhysicalProjectileConstruction.LeadCoreJacketed,
                PhysicalProjectileDesignClass.HollowPoint,
                PhysicalProjectileShapeClass.Spitzer); // patron_762x54r_bthp
            Add(
                definitions,
                "6529243824cbe3c74a05e5c1",
                PhysicalProjectileConstruction.LeadCoreJacketed,
                PhysicalProjectileDesignClass.FullMetalJacket,
                PhysicalProjectileShapeClass.Spitzer); // patron_68x51
            Add(
                definitions,
                "6529302b8c26af6326029fb7",
                PhysicalProjectileConstruction.LeadCoreJacketed,
                PhysicalProjectileDesignClass.FullMetalJacket,
                PhysicalProjectileShapeClass.Spitzer); // patron_68x51_fmj
            Add(
                definitions,
                "6576f4708ca9c4381d16cd9d",
                PhysicalProjectileConstruction.SteelCoreJacketed,
                PhysicalProjectileDesignClass.ExposedPenetrator,
                PhysicalProjectileShapeClass.Spitzer); // patron_9x21_7n42
            Add(
                definitions,
                "6576f93989f0062e741ba952",
                PhysicalProjectileConstruction.SteelCoreJacketed,
                PhysicalProjectileDesignClass.FullMetalJacket,
                PhysicalProjectileShapeClass.RoundNose); // patron_9x21_7u4
            Add(
                definitions,
                "6576f96220d53a5b8f3e395e",
                PhysicalProjectileConstruction.LeadCoreJacketed,
                PhysicalProjectileDesignClass.FullMetalJacket,
                PhysicalProjectileShapeClass.Spitzer); // patron_9x39_fmj
            Add(
                definitions,
                "6601546f86889319850bd566",
                PhysicalProjectileConstruction.NonMetallicComposite,
                PhysicalProjectileDesignClass.Solid,
                PhysicalProjectileShapeClass.FlatNose); // patron_20x1mm
            Add(
                definitions,
                "668fe62ac62660a5d8071446",
                PhysicalProjectileConstruction.LeadCoreJacketed,
                PhysicalProjectileDesignClass.FullMetalJacket,
                PhysicalProjectileShapeClass.FlatNose); // patron_127x33_fmj
            Add(
                definitions,
                "66a0d1c87d0d369e270bb9de",
                PhysicalProjectileConstruction.LeadCoreJacketed,
                PhysicalProjectileDesignClass.HollowPoint,
                PhysicalProjectileShapeClass.RoundNose); // patron_127x33_jhp
            Add(
                definitions,
                "66a0d1e0ed648d72fe064d06",
                PhysicalProjectileConstruction.MonolithicCopper,
                PhysicalProjectileDesignClass.Solid,
                PhysicalProjectileShapeClass.RoundNose); // patron_127x33_cooper
            Add(
                definitions,
                "66a0d1f88486c69fce00fdf6",
                PhysicalProjectileConstruction.LeadCoreJacketed,
                PhysicalProjectileDesignClass.SoftPoint,
                PhysicalProjectileShapeClass.RoundNose); // patron_127x33_jsp
            Add(
                definitions,
                "66d97834d2985e11480d5c1e",
                PhysicalProjectileConstruction.NonMetallicComposite,
                PhysicalProjectileDesignClass.Payload,
                PhysicalProjectileShapeClass.RoundNose); // patron_rsp_blue
            Add(
                definitions,
                "66d9f3047b82b9a9aa055d81",
                PhysicalProjectileConstruction.NonMetallicComposite,
                PhysicalProjectileDesignClass.Payload,
                PhysicalProjectileShapeClass.RoundNose); // patron_rsp_yellow_event
            Add(
                definitions,
                "66ec2aa6daf127599c0c31f1",
                PhysicalProjectileConstruction.MonolithicSteel,
                PhysicalProjectileDesignClass.Fragment,
                PhysicalProjectileShapeClass.IrregularProjectileFragment); // shrapnel_mine_om_82
            Add(
                definitions,
                "675ea4891b2579e8fe0250aa",
                PhysicalProjectileConstruction.NonMetallicComposite,
                PhysicalProjectileDesignClass.Payload,
                PhysicalProjectileShapeClass.RoundNose); // patron_rsp_newyear
            Add(
                definitions,
                "67654a6759116d347b0bfb86",
                PhysicalProjectileConstruction.MonolithicSteel,
                PhysicalProjectileDesignClass.Fragment,
                PhysicalProjectileShapeClass.IrregularProjectileFragment); // shrapnel_v40
            Add(
                definitions,
                "6768c25aa7b238f14a08d3f6",
                PhysicalProjectileConstruction.SteelPenetratorCopperCoreJacketed,
                PhysicalProjectileDesignClass.ExposedPenetrator,
                PhysicalProjectileShapeClass.Spitzer); // patron_762x51_m80a1
            Add(
                definitions,
                "67ade494d748873e5f0161df",
                PhysicalProjectileConstruction.MonolithicSteel,
                PhysicalProjectileDesignClass.Fragment,
                PhysicalProjectileShapeClass.IrregularProjectileFragment); // shrapnel_vog30
            Add(
                definitions,
                "67d41936f378a36c4706eeb9",
                PhysicalProjectileConstruction.LeadCoreJacketed,
                PhysicalProjectileDesignClass.HollowPoint,
                PhysicalProjectileShapeClass.Spitzer); // patron_127x99_hp
            Add(
                definitions,
                "67dc212493ce32834b0fa446",
                PhysicalProjectileConstruction.LeadCoreJacketed,
                PhysicalProjectileDesignClass.FullMetalJacket,
                PhysicalProjectileShapeClass.Spitzer); // patron_127x99_m21
            Add(
                definitions,
                "67dc255ee3028a8b120efc48",
                PhysicalProjectileConstruction.LeadCoreJacketed,
                PhysicalProjectileDesignClass.FullMetalJacket,
                PhysicalProjectileShapeClass.Spitzer); // patron_127x99_m33
            Add(
                definitions,
                "67dc2648ba5b79876906a166",
                PhysicalProjectileConstruction.TungstenCoreJacketed,
                PhysicalProjectileDesignClass.SabotedPenetrator,
                PhysicalProjectileShapeClass.Spitzer); // patron_127x99_m903
            Add(
                definitions,
                "560d5e524bdc2d25448b4571",
                PhysicalProjectileConstruction.MonolithicLead,
                PhysicalProjectileDesignClass.Shot,
                PhysicalProjectileShapeClass.SphericalShot); // patron_12x70_buckshot
            Add(
                definitions,
                "5656eb674bdc2d35148b457c",
                PhysicalProjectileConstruction.MonolithicSteel,
                PhysicalProjectileDesignClass.Payload,
                PhysicalProjectileShapeClass.RoundNose); // 40 VOG-25
            Add(
                definitions,
                "58820d1224597753c90aeb13",
                PhysicalProjectileConstruction.MonolithicLead,
                PhysicalProjectileDesignClass.Solid,
                PhysicalProjectileShapeClass.RoundNose); // patron_12x70_slug
            Add(
                definitions,
                "5a38ebd9c4a282000d722a5b",
                PhysicalProjectileConstruction.MonolithicLead,
                PhysicalProjectileDesignClass.Shot,
                PhysicalProjectileShapeClass.SphericalShot); // patron_20x70_buckshot
            Add(
                definitions,
                "5c0d591486f7744c505b416f",
                PhysicalProjectileConstruction.MonolithicCopper,
                PhysicalProjectileDesignClass.Frangible,
                PhysicalProjectileShapeClass.RoundNose); // patron_12x70_rip
            Add(
                definitions,
                "5d6e6772a4b936088465b17c",
                PhysicalProjectileConstruction.MonolithicLead,
                PhysicalProjectileDesignClass.Shot,
                PhysicalProjectileShapeClass.SphericalShot); // patron_12x70_buckshot_525
            Add(
                definitions,
                "5d6e67fba4b9361bc73bc779",
                PhysicalProjectileConstruction.MonolithicLead,
                PhysicalProjectileDesignClass.Shot,
                PhysicalProjectileShapeClass.SphericalShot); // patron_12x70_buckshot_65
            Add(
                definitions,
                "5d6e6806a4b936088465b17e",
                PhysicalProjectileConstruction.MonolithicLead,
                PhysicalProjectileDesignClass.Shot,
                PhysicalProjectileShapeClass.SphericalShot); // patron_12x70_buckshot_85
            Add(
                definitions,
                "5d6e6869a4b9361c140bcfde",
                PhysicalProjectileConstruction.MonolithicLead,
                PhysicalProjectileDesignClass.Expanding,
                PhysicalProjectileShapeClass.RoundNose); // patron_12x70_slug_grizzly_40
            Add(
                definitions,
                "5d6e6891a4b9361bd473feea",
                PhysicalProjectileConstruction.MonolithicLead,
                PhysicalProjectileDesignClass.Expanding,
                PhysicalProjectileShapeClass.RoundNose); // patron_12x70_slug_poleva_3
            Add(
                definitions,
                "5d6e689ca4b9361bc8618956",
                PhysicalProjectileConstruction.LeadCoreJacketed,
                PhysicalProjectileDesignClass.FullMetalJacket,
                PhysicalProjectileShapeClass.RoundNose); // patron_12x70_slug_poleva_6u
            Add(
                definitions,
                "5d6e68a8a4b9360b6c0d54e2",
                PhysicalProjectileConstruction.MonolithicSteel,
                PhysicalProjectileDesignClass.SabotedPenetrator,
                PhysicalProjectileShapeClass.Spitzer); // patron_12x70_slug_ap_20
            Add(
                definitions,
                "5d6e68b3a4b9361bca7e50b5",
                PhysicalProjectileConstruction.MonolithicCopper,
                PhysicalProjectileDesignClass.HollowPoint,
                PhysicalProjectileShapeClass.RoundNose); // patron_12x70_slug_hp_copper
            Add(
                definitions,
                "5d6e68c4a4b9361b93413f79",
                PhysicalProjectileConstruction.LeadCoreJacketed,
                PhysicalProjectileDesignClass.FullMetalJacket,
                PhysicalProjectileShapeClass.Spitzer); // patron_12x70_slug_50_bmg_m17_traccer
            Add(
                definitions,
                "5d6e68d1a4b93622fe60e845",
                PhysicalProjectileConstruction.LeadCoreJacketed,
                PhysicalProjectileDesignClass.HollowPoint,
                PhysicalProjectileShapeClass.Spitzer); // patron_12x70_slug_superformance
            Add(
                definitions,
                "5d6e68dea4b9361bcc29e659",
                PhysicalProjectileConstruction.MonolithicLead,
                PhysicalProjectileDesignClass.SabotedPenetrator,
                PhysicalProjectileShapeClass.RoundNose); // patron_12x70_dual_sabot_slug
            Add(
                definitions,
                "5d6e68e6a4b9361c140bcfe0",
                PhysicalProjectileConstruction.LeadCoreJacketed,
                PhysicalProjectileDesignClass.PolymerTipped,
                PhysicalProjectileShapeClass.Spitzer); // patron_12x70_slug_ftx_custom_lite
            Add(
                definitions,
                "5d6e6911a4b9361bd5780d52",
                PhysicalProjectileConstruction.MonolithicSteel,
                PhysicalProjectileDesignClass.Flechette,
                PhysicalProjectileShapeClass.Flechette); // patron_12x70_flechette
            Add(
                definitions,
                "5d6e695fa4b936359b35d852",
                PhysicalProjectileConstruction.MonolithicLead,
                PhysicalProjectileDesignClass.Shot,
                PhysicalProjectileShapeClass.SphericalShot); // patron_20x70_buckshot_56
            Add(
                definitions,
                "5d6e69b9a4b9361bc8618958",
                PhysicalProjectileConstruction.MonolithicLead,
                PhysicalProjectileDesignClass.Shot,
                PhysicalProjectileShapeClass.SphericalShot); // patron_20x70_buckshot_62
            Add(
                definitions,
                "5d6e69c7a4b9360b6c0d54e4",
                PhysicalProjectileConstruction.MonolithicLead,
                PhysicalProjectileDesignClass.Shot,
                PhysicalProjectileShapeClass.SphericalShot); // patron_20x70_buckshot_73
            Add(
                definitions,
                "5d6e6a05a4b93618084f58d0",
                PhysicalProjectileConstruction.MonolithicLead,
                PhysicalProjectileDesignClass.Solid,
                PhysicalProjectileShapeClass.RoundNose); // patron_20x70_slug_star
            Add(
                definitions,
                "5d6e6a42a4b9364f07165f52",
                PhysicalProjectileConstruction.LeadCoreJacketed,
                PhysicalProjectileDesignClass.FullMetalJacket,
                PhysicalProjectileShapeClass.RoundNose); // patron_20x70_slug_poleva_6u
            Add(
                definitions,
                "5d6e6a53a4b9361bd473feec",
                PhysicalProjectileConstruction.MonolithicLead,
                PhysicalProjectileDesignClass.Expanding,
                PhysicalProjectileShapeClass.RoundNose); // patron_20x70_slug_poleva_3
            Add(
                definitions,
                "5d6e6a5fa4b93614ec501745",
                PhysicalProjectileConstruction.MonolithicLead,
                PhysicalProjectileDesignClass.HollowPoint,
                PhysicalProjectileShapeClass.RoundNose); // patron_20x70_slug_broadhead
            Add(
                definitions,
                "5d70e500a4b9364de70d38ce",
                PhysicalProjectileConstruction.MonolithicSteel,
                PhysicalProjectileDesignClass.Payload,
                PhysicalProjectileShapeClass.RoundNose); // patron_30x29_vog_30
            Add(
                definitions,
                "5e85a9a6eacf8c039e4e2ac1",
                PhysicalProjectileConstruction.MonolithicLead,
                PhysicalProjectileDesignClass.Shot,
                PhysicalProjectileShapeClass.SphericalShot); // patron_23x75_shrapnel_10
            Add(
                definitions,
                "5ede4739e0350d05467f73e8",
                PhysicalProjectileConstruction.MonolithicSteel,
                PhysicalProjectileDesignClass.Payload,
                PhysicalProjectileShapeClass.RoundNose); // patron_40x46_m406
            Add(
                definitions,
                "5ede47405b097655935d7d16",
                PhysicalProjectileConstruction.MonolithicSteel,
                PhysicalProjectileDesignClass.Payload,
                PhysicalProjectileShapeClass.RoundNose); // patron_40x46_m441
            Add(
                definitions,
                "5ede474b0c226a66f5402622",
                PhysicalProjectileConstruction.MonolithicSteel,
                PhysicalProjectileDesignClass.Payload,
                PhysicalProjectileShapeClass.RoundNose); // patron_40x46_m381
            Add(
                definitions,
                "5ede475339ee016e8c534742",
                PhysicalProjectileConstruction.MonolithicLead,
                PhysicalProjectileDesignClass.Shot,
                PhysicalProjectileShapeClass.SphericalShot); // patron_40x46_m576
            Add(
                definitions,
                "5ede475b549eed7c6d5c18fb",
                PhysicalProjectileConstruction.MonolithicSteel,
                PhysicalProjectileDesignClass.Payload,
                PhysicalProjectileShapeClass.RoundNose); // patron_40x46_m386
            Add(
                definitions,
                "5ede47641cf3836a88318df1",
                PhysicalProjectileConstruction.NonMetallicComposite,
                PhysicalProjectileDesignClass.Payload,
                PhysicalProjectileShapeClass.RoundNose); // patron_40x46_m716
            Add(
                definitions,
                "5f0c892565703e5c461894e9",
                PhysicalProjectileConstruction.MonolithicSteel,
                PhysicalProjectileDesignClass.Payload,
                PhysicalProjectileShapeClass.RoundNose); // patron_40x46_m433
            Add(
                definitions,
                "5f647f31b6238e5dd066e196",
                PhysicalProjectileConstruction.MonolithicLead,
                PhysicalProjectileDesignClass.Shot,
                PhysicalProjectileShapeClass.SphericalShot); // patron_23x75_shrapnel_25
            Add(
                definitions,
                "5f647fd3f6e4ab66c82faed6",
                PhysicalProjectileConstruction.NonMetallicComposite,
                PhysicalProjectileDesignClass.Solid,
                PhysicalProjectileShapeClass.SphericalShot,
                0.010d,
                0.023d); // patron_23x75_wave_r; installed template omits both values
            Add(
                definitions,
                "64b8ee384b75259c590fa89b",
                PhysicalProjectileConstruction.MonolithicSteel,
                PhysicalProjectileDesignClass.Flechette,
                PhysicalProjectileShapeClass.Flechette); // patron_12x70_piranha
            Add(
                definitions,
                "660137d8481cc6907a0c5cda",
                PhysicalProjectileConstruction.MonolithicSteel,
                PhysicalProjectileDesignClass.SabotedPenetrator,
                PhysicalProjectileShapeClass.Spitzer); // patron_20x70_slug_ap
            Add(
                definitions,
                "660137ef76c1b56143052be8",
                PhysicalProjectileConstruction.MonolithicLead,
                PhysicalProjectileDesignClass.Solid,
                PhysicalProjectileShapeClass.Spitzer); // patron_20x70_slug_dg
            Add(
                definitions,
                "6601380580e77cfd080e3418",
                PhysicalProjectileConstruction.MonolithicSteel,
                PhysicalProjectileDesignClass.Flechette,
                PhysicalProjectileShapeClass.Flechette); // patron_20x70_flechette
            Add(
                definitions,
                "67446fdd752be02c220f27b3",
                PhysicalProjectileConstruction.MonolithicSteel,
                PhysicalProjectileDesignClass.Payload,
                PhysicalProjectileShapeClass.Spitzer); // patron_725
            return definitions;
        }

        private static void Add(
            Dictionary<string, PhysicalAmmunitionDesignDefinition> definitions,
            string templateId,
            PhysicalProjectileConstruction construction,
            PhysicalProjectileDesignClass designClass,
            PhysicalProjectileShapeClass initialShapeClass,
            double fallbackMassKilograms = double.NaN,
            double fallbackDiameterMetres = double.NaN)
        {
            definitions.Add(
                templateId,
                new PhysicalAmmunitionDesignDefinition(
                    templateId,
                    construction,
                    designClass,
                    initialShapeClass,
                    fallbackMassKilograms,
                    fallbackDiameterMetres));
        }
    }
}
