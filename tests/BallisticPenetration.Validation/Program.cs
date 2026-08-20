#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;
using BallisticPenetration.Core;
using BallisticPenetration.Core.Diagnostics;
using BallisticPenetration.Core.Physics;

namespace BallisticPenetration.Validation
{
    internal static class Program
    {
        private const string ItemsRelativePath = @"SPT_Runtime\SPT_Data\database\templates\items.json";
        private const string SnbInternalName = "patron_762x54R_SNB";
        private const string UsInternalName = "patron_545x39_US";
        private const string SevenN40TemplateId = "61962b617c6c7b169525f168";
        private const string SsaApTemplateId = "601949593ae8f707c4608daa";
        private const string M903TemplateId = "67dc2648ba5b79876906a166";
        private const string Vog25TemplateId = "5656eb674bdc2d35148b457c";
        private const double Tolerance = 0.000000000001d;

        private static readonly double[] SweepFractions =
        {
            0.25d,
            0.5d,
            0.67d,
            0.821d,
            1d,
            1.152d,
            1.2d,
            1.5d,
            2d
        };

        private static readonly bool[] NoArmorMatches = { false, false, false };

        private static readonly PhysicalProjectileConstruction[] SupportedProjectileConstructions =
        {
            PhysicalProjectileConstruction.LeadCoreJacketed,
            PhysicalProjectileConstruction.SteelCoreJacketed,
            PhysicalProjectileConstruction.TungstenCoreJacketed,
            PhysicalProjectileConstruction.MonolithicCopper,
            PhysicalProjectileConstruction.MonolithicSteel,
            PhysicalProjectileConstruction.FrangibleComposite,
            PhysicalProjectileConstruction.AluminumCoreJacketed,
            PhysicalProjectileConstruction.CopperAlloyCoreJacketed,
            PhysicalProjectileConstruction.SteelPenetratorLeadCoreJacketed,
            PhysicalProjectileConstruction.SteelPenetratorCopperCoreJacketed,
            PhysicalProjectileConstruction.SteelPenetratorAluminumCoreJacketed,
            PhysicalProjectileConstruction.MonolithicBrass,
            PhysicalProjectileConstruction.MonolithicZinc,
            PhysicalProjectileConstruction.NonMetallicComposite,
            PhysicalProjectileConstruction.MonolithicLead
        };

        private static int _passed;
        private static int _failed;

        private sealed class PooledNormalizationHost
        {
            internal PooledNormalizationHost(int incarnation)
            {
                Incarnation = incarnation;
            }

            internal int Incarnation { get; set; }
        }

        private sealed class PooledNormalizationBinding
        {
            internal PooledNormalizationBinding(
                PooledNormalizationHost host,
                BallisticNormalizationState state)
            {
                Incarnation = host.Incarnation;
                State = state;
            }

            internal int Incarnation { get; }

            internal BallisticNormalizationState State { get; }

            internal bool Matches(PooledNormalizationHost host)
            {
                return host != null && host.Incarnation == Incarnation;
            }
        }

        public static int Main(string[] args)
        {
            string itemsPath;
            try
            {
                itemsPath = ResolveItemsPath(args);
            }
            catch (InvalidOperationException exception)
            {
                return ReportValidationSetupFailure(exception);
            }
            catch (ArgumentException exception)
            {
                return ReportValidationSetupFailure(exception);
            }
            catch (NotSupportedException exception)
            {
                return ReportValidationSetupFailure(exception);
            }
            catch (SecurityException exception)
            {
                return ReportValidationSetupFailure(exception);
            }

            IReadOnlyList<BallisticTemplate>? templates = null;
            BallisticTemplate? snb = null;
            Run("Exact SPT core version gate", ValidateExactSptCoreVersionGate);
            Run("Postmortem armor hit guards", ValidatePostmortemArmorHitGuards);
            Run("Postmortem armor traversal", ValidatePostmortemArmorTraversal);
            Run("Physical collision history invariants", ValidatePhysicalCollisionHistory);
            Run("Physical projectile state and derived SI values", ValidatePhysicalProjectileState);
            Run("Physical projectile invalid-state fallback", ValidatePhysicalProjectileInvalidFallback);
            Run("Physical component render geometry", ValidatePhysicalVisualGeometry);
            Run("Physical renderer ownership and culling policy", ValidatePhysicalVisualLifecycle);
            Run("Physical renderer FIFO command budgeting", ValidatePhysicalVisualCommandBudget);
            Run("Physical renderer deterministic capacity stress", ValidatePhysicalVisualCapacityStress);
            Run("Physical renderer core remains dependency-free", ValidatePhysicalRendererIsolation);
            Run(
                "Physical ammunition identity and kinetic eligibility",
                delegate { ValidatePhysicalAmmunitionPolicy(itemsPath); });
            Run("Physical transition telemetry snapshots and observer isolation", ValidatePhysicalTelemetry);
            Run("Physical transition identities use exact component state", ValidatePhysicalTransitionIdentity);
            Run(
                "Physical collision observed/resolved correlation and deduplication",
                ValidatePhysicalCollisionObservedResolvedCorrelation);
            Run(
                "Checked collision deduplication stress uses production path",
                ValidateProductionCollisionDeduplicationStress);
            Run(
                "Bounded physical lifecycle terminal diagnostics",
                ValidatePhysicalLifecycleTerminalDiagnostics);
            Run("Field report session-start JSON", ValidateFieldReportSessionStart);
            Run("Field report lifecycle correlation serialization", ValidateFieldReportLifecycleSerialization);
            Run("Field report concurrent JSONL ordering", ValidateFieldReportConcurrency);
            Run("Field report issue-marker flush", ValidateFieldReportIssueMarkerFlush);
            Run("Field report normal shutdown finalization", ValidateFieldReportShutdownFinalization);
            Run("Field report stale partial crash recovery", ValidateFieldReportCrashRecovery);
            Run("Field report exceptions remain isolated", ValidateFieldReportExceptionIsolation);
            Run("Disabled field recorder creates no report", ValidateDisabledFieldRecorder);
            Run("Field report queue overflow accounting", ValidateFieldReportQueueOverflow);
            Run("Field report completed-file retention", ValidateFieldReportRetention);
            Run("Field report active partial retention protection", ValidateFieldReportActivePartialProtection);
            Run("Field report size truncation and critical eligibility", ValidateFieldReportTruncation);
            Run("Field report privacy path exclusions", ValidateFieldReportPrivacy);
            Run("Field report runtime-error detail and aggregation", ValidateRuntimeErrorEvidence);
            Run("Recycled shot report context rejects pooled state", ValidateRecycledShotReportContext);
            Run("Numeric runaway detection catches first corrupted transition", ValidateNumericRunawayDetection);
            Run("Projectile and target-spall conservation", ValidatePhysicalConservation);
            Run("Deterministic projectile random stream", ValidateDeterministicProjectileRandom);
            Run("Physical child seeds stay inside the host random table", ValidatePhysicalHostRandomSeed);
            Run("Physical material profile validation", ValidatePhysicalMaterialProfiles);
            Run("Reflection-only physical surface material contract", ValidatePhysicalSurfaceMaterialContract);
            Run("Built-in physical profile catalog", ValidatePhysicalDefaultProfileCatalog);
            Run("Projectile design controls deformation, fracture, and drag", ValidatePhysicalProjectileDesignResponse);
            Run("Conserved deformation and material response", ValidatePhysicalDeformationResponse);
            Run("Deformation solver fail-open behavior", ValidatePhysicalDeformationFallback);
            Run("Deformation solver deterministic property sweep", ValidatePhysicalDeformationStressSweep);
            Run("Physical fragmentation profile validation", ValidatePhysicalFragmentationProfile);
            Run("Conserved deterministic projectile fragmentation and target spall", ValidatePhysicalFragmentationResponse);
            Run("Target spall is independent of projectile fragmentation", ValidateIndependentTargetSpall);
            Run("Target spall survives later deformation and fragmentation", ValidateTargetSpallContinuation);
            Run("Measured root projectiles derive SI geometry and energy", ValidatePhysicalRootProjectileFactory);
            Run("Physical fragments project to independent EFT shot values", ValidatePhysicalEftProjection);
            Run("Target-spall host trajectory remains numerically stable", ValidateTargetSpallHostTrajectoryStability);
            Run("Physical-to-EFT projection fails open", ValidatePhysicalEftProjectionFallback);
            Run("Physical fragment flight advances from measured EFT motion", ValidatePhysicalFlightState);
            Run("Zero host fragment count closes physical reservations", ValidatePhysicalFragmentationMinimumOutput);
            Run("Fragmentation solver fail-open behavior", ValidatePhysicalFragmentationFallback);
            Run("Fragmentation solver deterministic property sweep", ValidatePhysicalFragmentationStressSweep);
            Run("Parse SPT items.json safely with System.Text.Json", delegate
            {
                templates = LoadTemplatesWithBallisticStats(itemsPath);
                snb = FindTemplate(templates, SnbInternalName);
                AssertNear("SNB InitialSpeed", 875d, snb.InitialSpeed);
                AssertNear("SNB PenetrationPower", 62d, snb.PenetrationPower);
                AssertNear("SNB Damage", 75d, snb.Damage);
            });

            if (templates != null)
            {
                Run("Full SPT template speed-factor sweep", delegate { ValidateFullTemplateSweep(templates); });
                Run(
                    "Full SPT multi-layer normalization sweep",
                    delegate { ValidateFullTemplateNormalizationSweep(templates); });
                Run("5.45x39 US template", delegate { ValidateUsTemplate(templates); });
            }

            if (snb != null)
            {
                Run("SNB factor rows and scaled-stat totals", delegate { ValidateSnbRows(snb); });
                Run("Weapon independence", delegate { ValidateWeaponIndependence(snb); });
                Run("Unbounded speed fractions", ValidateUnboundedFactors);
                Run("Zero impact", ValidateZeroImpact);
                Run("Invalid inputs retain the neutral fallback", ValidateInvalidInputs);
                Run("Configurable exponents", ValidateConfigurableExponents);
                Run(
                    "Monotonic factors and idempotent collision normalization",
                    delegate { ValidateMonotonicAndCumulativeCalculations(snb); });
            }
            else
            {
                Run("SNB-dependent calculations", delegate
                {
                    throw new InvalidOperationException("SNB data was not loaded, so data-backed checks cannot run.");
                });
            }

            Console.WriteLine();
            Console.WriteLine(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Validation complete: {0} passed, {1} failed.",
                    _passed,
                    _failed));

            return _failed == 0 ? 0 : 1;
        }

        private static string ResolveItemsPath(string[] args)
        {
            if (args != null && args.Length > 0 && !string.IsNullOrWhiteSpace(args[0]))
            {
                return Path.GetFullPath(args[0]);
            }

            string? sptRoot = Environment.GetEnvironmentVariable("SPT_ROOT");
            if (!string.IsNullOrWhiteSpace(sptRoot))
            {
                return Path.Combine(Path.GetFullPath(sptRoot), ItemsRelativePath);
            }

            throw new InvalidOperationException(
                "Pass the installed items.json path as the first argument or set the SPT_ROOT environment variable.");
        }

        private static void ValidateExactSptCoreVersionGate()
        {
            AssertEqual(
                "supported SPT core version text",
                "4.1.2",
                SptVersionCompatibility.SupportedCoreVersionText);
            AssertTrue(
                "exact 4.1.2 accepted",
                SptVersionCompatibility.IsExactSupportedCoreVersion(new Version(4, 1, 2)));
            AssertTrue(
                "missing version rejected",
                !SptVersionCompatibility.IsExactSupportedCoreVersion(null));

            Version[] unsupportedVersions =
            {
                new Version(4, 1, 1),
                new Version(4, 1, 3),
                new Version(4, 2, 0),
                new Version(5, 0, 0),
                new Version(4, 1, 2, 0)
            };

            foreach (Version unsupportedVersion in unsupportedVersions)
            {
                AssertTrue(
                    "unsupported SPT core version rejected: " + unsupportedVersion,
                    !SptVersionCompatibility.IsExactSupportedCoreVersion(unsupportedVersion));
            }
        }

        private static void ValidatePhysicalAmmunitionPolicy(string itemsPath)
        {
            AssertEqual(
                "internal taxonomy name wins over inconsistent display name",
                "patron_545x39_7n40",
                PhysicalAmmunitionPolicy.SelectAuthoritativeTemplateName(
                    "patron_545x39_7n40",
                    "patron_545x39_BS"));
            AssertEqual(
                "display name remains a fallback",
                "fallback_name",
                PhysicalAmmunitionPolicy.SelectAuthoritativeTemplateName(
                    string.Empty,
                    "fallback_name"));
            AssertEqual(
                "missing names remain empty",
                string.Empty,
                PhysicalAmmunitionPolicy.SelectAuthoritativeTemplateName(null, null));

            using var stream = File.OpenRead(itemsPath);
            using var document = JsonDocument.Parse(stream);
            JsonElement sevenN40 = document.RootElement.GetProperty(SevenN40TemplateId);
            JsonElement sevenN40Properties = sevenN40.GetProperty("_props");
            AssertEqual(
                "installed 7N40 internal name",
                "patron_545x39_7n40",
                sevenN40.GetProperty("_name").GetString() ?? string.Empty);
            AssertEqual(
                "installed 7N40 inconsistent display name regression",
                "patron_545x39_BS",
                sevenN40Properties.GetProperty("Name").GetString() ?? string.Empty);

            JsonElement m903Properties = document.RootElement
                .GetProperty(M903TemplateId)
                .GetProperty("_props");
            AssertTrue(
                "installed M903 carries the large-impact component",
                m903Properties.GetProperty("HasGrenaderComponent").GetBoolean());
            AssertEqual(
                "installed M903 large-impact effect marker",
                "big_round_impact",
                m903Properties.GetProperty("ExplosionType").GetString() ?? string.Empty);
            AssertTrue(
                "installed M903 design resolves",
                PhysicalAmmunitionDesignCatalog.TryGetDefinition(
                    M903TemplateId,
                    out PhysicalAmmunitionDesignDefinition m903Definition));
            AssertEqual(
                "installed M903 construction",
                PhysicalProjectileConstruction.TungstenCoreJacketed,
                m903Definition.Construction);
            AssertEqual(
                "installed M903 design",
                PhysicalProjectileDesignClass.SabotedPenetrator,
                m903Definition.DesignClass);
            AssertNear(
                "installed M903 penetration power",
                115d,
                m903Properties.GetProperty("PenetrationPower").GetDouble());
            AssertTrue(
                "tungsten profile available for M903",
                PhysicalDefaultProfileCatalog.TryGetProjectileProfile(
                    PhysicalProjectileConstruction.TungstenCoreJacketed,
                    out PhysicalProjectileMaterialProfile? m903Profile)
                && m903Profile != null);
            PhysicalProjectileMaterialProfile m903ProfileValue = RequireValue(
                "M903 physical material profile",
                m903Profile);
            double m903Speed = m903Properties.GetProperty("InitialSpeed").GetDouble();
            double m903MassKilograms = m903Properties.GetProperty("BulletMassGram").GetDouble()
                * 0.001d;
            double m903DiameterMetres = m903Properties
                .GetProperty("BulletDiameterMilimeters")
                .GetDouble()
                * 0.001d;
            var m903Input = new PhysicalRootProjectileInput
            {
                ProjectileId = "installed-m903-root",
                RootShotId = "installed-m903-shot",
                DeterministicSeed = 0x4D393033UL,
                Construction = m903Definition.Construction,
                DesignClass = m903Definition.DesignClass,
                ShapeClass = m903Definition.InitialShapeClass,
                MassKilograms = m903MassKilograms,
                NominalDiameterMetres = m903DiameterMetres,
                MaterialDensityKilogramsPerCubicMetre =
                    m903ProfileValue.DensityKilogramsPerCubicMetre,
                DragCoefficient = PhysicalDefaultProfileCatalog.GetNominalDragCoefficient(
                    m903Definition.Construction,
                    m903Definition.DesignClass,
                    m903Definition.InitialShapeClass),
                PositionMetres = PhysicalVector3.Zero,
                VelocityMetresPerSecond = new PhysicalVector3(0d, 0d, m903Speed)
            };
            AssertTrue(
                "installed M903 creates a finite physical root",
                PhysicalRootProjectileFactory.TryCreate(
                    m903Input,
                    out PhysicalProjectileState? m903State,
                    out PhysicalRootProjectileFailureReason m903Reason));
            AssertEqual(
                "installed M903 root reason",
                PhysicalRootProjectileFailureReason.None,
                m903Reason);
            PhysicalProjectileState m903StateValue = RequireValue(
                "installed M903 physical root",
                m903State);
            AssertNear("installed M903 root mass", 0.027d, m903StateValue.RetainedMassKilograms);
            AssertNear("installed M903 root diameter", 0.00762d, m903StateValue.NominalDiameterMetres);
            AssertNear(
                "installed M903 root energy",
                0.5d * m903MassKilograms * m903Speed * m903Speed,
                m903StateValue.TranslationalKineticEnergyJoules);

            AssertTrue(
                "SSA AP design resolves",
                PhysicalAmmunitionDesignCatalog.TryGetDefinition(
                    SsaApTemplateId,
                    out PhysicalAmmunitionDesignDefinition ssaApDefinition));
            AssertEqual(
                "SSA AP tungsten-carbide construction",
                PhysicalProjectileConstruction.TungstenCoreJacketed,
                ssaApDefinition.Construction);
            AssertEqual(
                "SSA AP sabot design",
                PhysicalProjectileDesignClass.SabotedPenetrator,
                ssaApDefinition.DesignClass);
            AssertEqual(
                "SSA AP initial shape",
                PhysicalProjectileShapeClass.Spitzer,
                ssaApDefinition.InitialShapeClass);
            AssertTrue(
                "catalog definition value equality",
                PhysicalAmmunitionDesignCatalog.TryGetDefinition(
                    SsaApTemplateId,
                    out PhysicalAmmunitionDesignDefinition repeatedSsaApDefinition)
                && ssaApDefinition == repeatedSsaApDefinition
                && ssaApDefinition.Equals((object)repeatedSsaApDefinition)
                && ssaApDefinition.GetHashCode() == repeatedSsaApDefinition.GetHashCode());
            AssertTrue(
                "catalog definition value inequality",
                PhysicalAmmunitionDesignCatalog.TryGetDefinition(
                    Vog25TemplateId,
                    out PhysicalAmmunitionDesignDefinition unequalDefinition)
                && ssaApDefinition != unequalDefinition
                && !ssaApDefinition.Equals(unequalDefinition));

            int fireableTemplateCount = 0;
            int kineticTemplateCount = 0;
            int payloadTemplateCount = 0;
            foreach (JsonProperty itemProperty in document.RootElement.EnumerateObject())
            {
                if (!itemProperty.Value.TryGetProperty("_props", out JsonElement properties)
                    || !properties.TryGetProperty("InitialSpeed", out JsonElement speedElement)
                    || speedElement.GetDouble() <= 0d)
                {
                    continue;
                }

                fireableTemplateCount++;
                AssertTrue(
                    "fireable ammunition design resolves: " + itemProperty.Name,
                    PhysicalAmmunitionDesignCatalog.TryGetDefinition(
                        itemProperty.Name,
                        out PhysicalAmmunitionDesignDefinition definition));
                AssertEqual(
                    "catalog preserves template identity: " + itemProperty.Name,
                    itemProperty.Name,
                    definition.TemplateId);
                AssertTrue(
                    "catalog construction is explicit: " + itemProperty.Name,
                    definition.Construction > PhysicalProjectileConstruction.Unknown
                    && definition.Construction != PhysicalProjectileConstruction.TargetMaterial
                    && definition.Construction <= PhysicalProjectileConstruction.MonolithicLead);
                AssertTrue(
                    "catalog design is explicit: " + itemProperty.Name,
                    definition.DesignClass > PhysicalProjectileDesignClass.Unknown
                    && definition.DesignClass <= PhysicalProjectileDesignClass.Flechette);
                AssertTrue(
                    "catalog shape is explicit: " + itemProperty.Name,
                    definition.InitialShapeClass > PhysicalProjectileShapeClass.Unknown
                    && definition.InitialShapeClass <= PhysicalProjectileShapeClass.Flechette);
                if (definition.IsKineticProjectile)
                {
                    kineticTemplateCount++;
                    AssertTrue(
                        "kinetic catalog profile resolves: " + itemProperty.Name,
                        PhysicalDefaultProfileCatalog.TryGetProjectileProfile(
                            definition.Construction,
                            out PhysicalProjectileMaterialProfile? catalogProfile)
                        && catalogProfile != null);
                    double sourceMassKilograms = properties
                        .GetProperty("BulletMassGram")
                        .GetDouble() * 0.001d;
                    double sourceDiameterMetres = properties
                        .GetProperty("BulletDiameterMilimeters")
                        .GetDouble() * 0.001d;
                    AssertTrue(
                        "kinetic physical dimensions resolve: " + itemProperty.Name,
                        (IsFinite(sourceMassKilograms)
                            && sourceMassKilograms > 0d
                            && IsFinite(sourceDiameterMetres)
                            && sourceDiameterMetres > 0d)
                        || definition.HasFallbackPhysicalDimensions);
                    double catalogDrag = PhysicalDefaultProfileCatalog.GetNominalDragCoefficient(
                        definition.Construction,
                        definition.DesignClass,
                        definition.InitialShapeClass);
                    AssertTrue(
                        "kinetic catalog drag resolves: " + itemProperty.Name,
                        IsFinite(catalogDrag) && catalogDrag > 0d);
                }
                else
                {
                    payloadTemplateCount++;
                }
            }

            AssertEqual("fireable ammunition catalog coverage", 208, fireableTemplateCount);
            AssertEqual(
                "catalog definition count",
                fireableTemplateCount,
                PhysicalAmmunitionDesignCatalog.Count);
            AssertEqual("kinetic projectile count", 185, kineticTemplateCount);
            AssertEqual("non-kinetic payload count", 23, payloadTemplateCount);
            AssertTrue(
                "unknown ammunition does not receive a guessed construction",
                !PhysicalAmmunitionDesignCatalog.TryGetDefinition(
                    "not-an-installed-ammunition-template",
                    out _));

            AssertTrue(
                "installed VOG-25 resolves as a payload",
                PhysicalAmmunitionDesignCatalog.TryGetDefinition(
                    Vog25TemplateId,
                    out PhysicalAmmunitionDesignDefinition vog25Definition)
                && !vog25Definition.IsKineticProjectile
                && vog25Definition.DesignClass == PhysicalProjectileDesignClass.Payload);
            AssertTrue(
                "M576 effect metadata does not hide its physical buckshot",
                PhysicalAmmunitionDesignCatalog.TryGetDefinition(
                    "5ede475339ee016e8c534742",
                    out PhysicalAmmunitionDesignDefinition m576Definition)
                && m576Definition.IsKineticProjectile
                && m576Definition.Construction == PhysicalProjectileConstruction.MonolithicLead
                && m576Definition.DesignClass == PhysicalProjectileDesignClass.Shot
                && m576Definition.InitialShapeClass == PhysicalProjectileShapeClass.SphericalShot);
            AssertTrue(
                "Wave-R supplies catalog physical dimensions for missing source values",
                PhysicalAmmunitionDesignCatalog.TryGetDefinition(
                    "5f647fd3f6e4ab66c82faed6",
                    out PhysicalAmmunitionDesignDefinition waveRDefinition)
                && waveRDefinition.IsKineticProjectile
                && waveRDefinition.HasFallbackPhysicalDimensions
                && Math.Abs(waveRDefinition.FallbackMassKilograms - 0.010d) <= Tolerance
                && Math.Abs(waveRDefinition.FallbackDiameterMetres - 0.023d) <= Tolerance);
            foreach (string copperFrangibleId in new[]
            {
                "5c0d56a986f774449d5de529",
                "5cc86832d7f00c000d3a6e6c",
                "5ea2a8e200685063ec28c05a"
            })
            {
                AssertTrue(
                    "machined-copper frangible construction: " + copperFrangibleId,
                    PhysicalAmmunitionDesignCatalog.TryGetDefinition(
                        copperFrangibleId,
                        out PhysicalAmmunitionDesignDefinition copperFrangible)
                    && copperFrangible.Construction
                        == PhysicalProjectileConstruction.MonolithicCopper
                    && copperFrangible.DesignClass == PhysicalProjectileDesignClass.Frangible);
            }
            AssertTrue(
                "7.62x39 SP uses the soft-point response",
                PhysicalAmmunitionDesignCatalog.TryGetDefinition(
                    "64b7af734b75259c590fa895",
                    out PhysicalAmmunitionDesignDefinition sevenSixTwoSp)
                && sevenSixTwoSp.DesignClass == PhysicalProjectileDesignClass.SoftPoint);
        }

        private static void ValidatePostmortemArmorHitGuards()
        {
            AssertTrue(
                "valid postmortem armor hit accepted",
                PostmortemArmorPolicy.ShouldProcessHit(
                    true,
                    true,
                    true,
                    true,
                    true,
                    50f,
                    30f,
                    40f));
            AssertTrue(
                "finite zero values remain valid",
                PostmortemArmorPolicy.ShouldProcessHit(
                    true,
                    true,
                    true,
                    true,
                    true,
                    0f,
                    0f,
                    0f));

            AssertTrue(
                "master-disabled hit rejected",
                !PostmortemArmorPolicy.ShouldProcessHit(
                    false, true, true, true, true, 50f, 30f, 40f));
            AssertTrue(
                "feature-disabled hit rejected",
                !PostmortemArmorPolicy.ShouldProcessHit(
                    true, false, true, true, true, 50f, 30f, 40f));
            AssertTrue(
                "non-forward hit rejected",
                !PostmortemArmorPolicy.ShouldProcessHit(
                    true, true, false, true, true, 50f, 30f, 40f));
            AssertTrue(
                "mismatched collider rejected",
                !PostmortemArmorPolicy.ShouldProcessHit(
                    true, true, true, false, true, 50f, 30f, 40f));
            AssertTrue(
                "living or unknown target rejected",
                !PostmortemArmorPolicy.ShouldProcessHit(
                    true, true, true, true, false, 50f, 30f, 40f));

            float[] invalidValues =
            {
                float.NaN,
                float.PositiveInfinity,
                float.NegativeInfinity,
                -1f
            };
            for (int index = 0; index < invalidValues.Length; index++)
            {
                float invalidValue = invalidValues[index];
                AssertTrue(
                    "invalid damage rejected at index " + index,
                    !PostmortemArmorPolicy.ShouldProcessHit(
                        true, true, true, true, true, invalidValue, 30f, 40f));
                AssertTrue(
                    "invalid penetration rejected at index " + index,
                    !PostmortemArmorPolicy.ShouldProcessHit(
                        true, true, true, true, true, 50f, invalidValue, 40f));
                AssertTrue(
                    "invalid armor damage rejected at index " + index,
                    !PostmortemArmorPolicy.ShouldProcessHit(
                        true, true, true, true, true, 50f, 30f, invalidValue));
            }
        }

        private static void ValidatePostmortemArmorTraversal()
        {
            AssertEqual(
                "nonmatching armor skipped",
                PostmortemArmorTraversalStep.Skip,
                PostmortemArmorPolicy.GetTraversalStep(false, false, false));
            AssertEqual(
                "penetrated armor applies and continues",
                PostmortemArmorTraversalStep.ApplyAndContinue,
                PostmortemArmorPolicy.GetTraversalStep(true, false, false));
            AssertEqual(
                "blocking armor applies and stops",
                PostmortemArmorTraversalStep.ApplyAndStop,
                PostmortemArmorPolicy.GetTraversalStep(true, true, false));
            AssertEqual(
                "deflecting armor applies and stops",
                PostmortemArmorTraversalStep.ApplyAndStop,
                PostmortemArmorPolicy.GetTraversalStep(true, false, true));

            bool[] matches = { true, false, true, true };
            AssertEqual(
                "penetrating hit damages every matching layer",
                3,
                CountPostmortemArmorApplications(matches, -1));
            AssertEqual(
                "blocked hit includes blocker and excludes later armor",
                2,
                CountPostmortemArmorApplications(matches, 2));
            AssertEqual(
                "deflected hit includes first armor and stops",
                1,
                CountPostmortemArmorApplications(matches, 0));
            AssertEqual(
                "uncovered hit damages no armor",
                0,
                CountPostmortemArmorApplications(
                    NoArmorMatches,
                    -1));
        }

        private static int CountPostmortemArmorApplications(
            bool[] matches,
            int stoppingArmorIndex)
        {
            int applied = 0;
            for (int index = 0; index < matches.Length; index++)
            {
                PostmortemArmorTraversalStep step =
                    PostmortemArmorPolicy.GetTraversalStep(
                        matches[index],
                        index == stoppingArmorIndex,
                        false);
                if (step == PostmortemArmorTraversalStep.Skip)
                {
                    continue;
                }

                applied++;
                if (step == PostmortemArmorTraversalStep.ApplyAndStop)
                {
                    break;
                }
            }

            return applied;
        }

        private static void ValidatePhysicalCollisionHistory()
        {
            PhysicalCollisionRecord? record;
            PhysicalCollisionRecordFailureReason reason;
            PhysicalCollisionRecordInput input = CreateValidCollisionInput();
            AssertTrue(
                "valid physical collision record accepted",
                PhysicalCollisionRecord.TryCreate(input, out record, out reason));
            PhysicalCollisionRecord validRecord = RequireValue(
                "valid physical collision record",
                record);
            AssertEqual("valid physical collision reason", PhysicalCollisionRecordFailureReason.None, reason);
            AssertEqual("collision id preserved", "collision-1", validRecord.CollisionId);
            AssertNear("collision path length preserved", 0.012d, validRecord.EffectivePathLengthMetres);
            AssertEqual("collision outcome preserved", PhysicalCollisionOutcome.Penetrated, validRecord.Outcome);

            PhysicalCollisionRecord? equivalentRecord;
            AssertTrue(
                "equivalent collision record accepted",
                PhysicalCollisionRecord.TryCreate(
                    CreateValidCollisionInput(),
                    out equivalentRecord,
                    out reason));
            PhysicalCollisionRecord validEquivalentRecord = RequireValue(
                "equivalent collision record",
                equivalentRecord);
            AssertTrue("collision value equality", validRecord == validEquivalentRecord);
            AssertEqual(
                "collision value hash",
                validRecord.GetHashCode(),
                validEquivalentRecord.GetHashCode());

            PhysicalCollisionRecordInput changedMaterialInput = CreateValidCollisionInput();
            changedMaterialInput.MaterialId = "different-material";
            PhysicalCollisionRecord? changedMaterialRecord;
            AssertTrue(
                "changed-material collision accepted",
                PhysicalCollisionRecord.TryCreate(
                    changedMaterialInput,
                    out changedMaterialRecord,
                    out reason));
            AssertTrue("collision value inequality", validRecord != changedMaterialRecord);

            input = CreateValidCollisionInput();
            input.OutgoingTranslationalEnergyJoules = 5000.01d;
            AssertTrue(
                "collision energy creation fails open",
                !PhysicalCollisionRecord.TryCreate(input, out record, out reason));
            AssertTrue("failed collision returns no record", record == null);
            AssertEqual(
                "collision energy failure reason",
                PhysicalCollisionRecordFailureReason.OutgoingEnergyExceedsIncoming,
                reason);

            input = CreateValidCollisionInput();
            input.PositionMetres = new PhysicalVector3(double.NaN, 0d, 0d);
            AssertTrue(
                "nonfinite collision position rejected",
                !PhysicalCollisionRecord.TryCreate(input, out record, out reason));
            AssertEqual(
                "nonfinite collision position reason",
                PhysicalCollisionRecordFailureReason.PositionInvalid,
                reason);
        }

        private static void ValidatePhysicalProjectileState()
        {
            PhysicalCollisionRecord collision = CreateValidCollisionRecord();
            var mutableHistory = new List<PhysicalCollisionRecord> { collision };
            PhysicalProjectileStateInput input = CreateValidRootInput(800d, 0.01d, 0.0095d);
            double area;
            AssertTrue(
                "deformed circular area calculated",
                PhysicalProjectileGeometry.TryCalculateCircularAreaSquareMetres(0.008d, out area));
            input.Kind = PhysicalProjectileKind.DeformedProjectile;
            input.ShapeClass = PhysicalProjectileShapeClass.ExpandedMushroom;
            input.DeformedDiameterMetres = 0.008d;
            input.ProjectedAreaSquareMetres = area;
            input.LengthMetres = 0.025d;
            input.DragCoefficient = 0.32d;
            input.YawAngleRadians = 0.08d;
            input.TumbleState = PhysicalProjectileTumbleState.Yawing;
            AssertTrue(
                "valid physical projectile attitude created",
                PhysicalOrientation.TryApplyYaw(
                    input.Orientation,
                    input.YawAngleRadians,
                    input.DeterministicSeed,
                    out PhysicalOrientation validAttitude));
            input.Orientation = validAttitude;
            input.DamageCapabilityJoules = 2500d;
            input.PenetrationCapabilityJoulesPerSquareMetre = 2500d / area;
            input.CollisionHistory = mutableHistory;

            PhysicalProjectileState? state;
            PhysicalProjectileStateFailureReason reason;
            AssertTrue(
                "valid physical projectile accepted",
                PhysicalProjectileState.TryCreate(input, out state, out reason));
            PhysicalProjectileState validState = RequireValue("valid physical projectile", state);
            AssertEqual("valid physical projectile reason", PhysicalProjectileStateFailureReason.None, reason);
            AssertEqual("physical state schema", 2, PhysicalProjectileState.SchemaVersion);
            AssertEqual(
                "physical projectile design retained",
                PhysicalProjectileDesignClass.FullMetalJacket,
                validState.DesignClass);
            AssertNear("physical projectile speed", 800d, validState.SpeedMetresPerSecond);
            AssertNear("physical projectile momentum x", 0d, validState.MomentumKilogramMetresPerSecond.X);
            AssertNear("physical projectile momentum z", 7.6d, validState.MomentumKilogramMetresPerSecond.Z);
            AssertNear("physical projectile kinetic energy", 3040d, validState.TranslationalKineticEnergyJoules);
            AssertNear("physical projectile equivalent diameter", 0.008d, validState.EquivalentDiameterMetres);
            AssertNear("physical projectile aspect ratio", 3.125d, validState.AspectRatio);
            AssertNear(
                "component-specific physical ballistic coefficient",
                0.0095d / (0.32d * area),
                validState.BallisticCoefficientKilogramsPerSquareMetre);
            AssertEqual("physical projectile history copied", 1, validState.CollisionHistory.Count);
            AssertTrue("root projectile has no parent-derived allocation", !validState.IsParentDerivedMass);
            AssertTrue("root projectile is not target-material origin", !validState.IsTargetMaterialOrigin);

            mutableHistory.Clear();
            AssertEqual("physical projectile history is immutable", 1, validState.CollisionHistory.Count);

            double equivalentDiameter;
            AssertTrue(
                "equivalent diameter calculation succeeds",
                PhysicalProjectileGeometry.TryCalculateEquivalentDiameterMetres(
                    validState.ProjectedAreaSquareMetres,
                    out equivalentDiameter));
            AssertNear("equivalent diameter round trip", 0.008d, equivalentDiameter);
        }

        private static void ValidatePhysicalProjectileInvalidFallback()
        {
            PhysicalProjectileStateInput input = CreateValidRootInput(800d, 0.01d, 0.0095d);
            input.DesignClass = PhysicalProjectileDesignClass.Unknown;
            AssertPhysicalStateFailure(
                "unknown projectile design",
                input,
                PhysicalProjectileStateFailureReason.DesignClassInvalid);

            input = CreateValidRootInput(800d, 0.01d, 0.0095d);
            input.OriginalMassKilograms = double.NaN;
            AssertPhysicalStateFailure(
                "nonfinite original mass",
                input,
                PhysicalProjectileStateFailureReason.OriginalMassInvalid);
            AssertTrue("failed state does not rewrite input", double.IsNaN(input.OriginalMassKilograms));

            input = CreateValidRootInput(800d, 0.01d, 0.0101d);
            AssertPhysicalStateFailure(
                "retained mass exceeds original",
                input,
                PhysicalProjectileStateFailureReason.RetainedMassExceedsOriginal);

            input = CreateValidRootInput(800d, 0.01d, 0.0095d);
            input.ProjectedAreaSquareMetres = 0d;
            AssertPhysicalStateFailure(
                "zero projected area",
                input,
                PhysicalProjectileStateFailureReason.ProjectedAreaInvalid);

            input = CreateValidRootInput(0d, 0.01d, 0.0095d);
            AssertPhysicalStateFailure(
                "continuing projectile with zero velocity",
                input,
                PhysicalProjectileStateFailureReason.MovingStateHasZeroVelocity);

            input = CreateValidRootInput(10d, 0.01d, 0.0095d);
            input.TerminalState = PhysicalProjectileTerminalState.Stopped;
            AssertPhysicalStateFailure(
                "stopped projectile with velocity",
                input,
                PhysicalProjectileStateFailureReason.RestingStateHasVelocity);

            input = CreateValidRootInput(800d, 0.01d, 0.0095d);
            input.Orientation = new PhysicalOrientation(0d, 0d, 0d, 2d);
            AssertPhysicalStateFailure(
                "nonnormalized orientation",
                input,
                PhysicalProjectileStateFailureReason.OrientationInvalid);

            input = CreateValidRootInput(800d, 0.01d, 0.0095d);
            input.YawAngleRadians = 0.5d;
            input.TumbleState = PhysicalProjectileTumbleState.Yawing;
            AssertPhysicalStateFailure(
                "attitude yaw mismatch",
                input,
                PhysicalProjectileStateFailureReason.AttitudeYawMismatch);

            input = CreateValidRootInput(800d, 0.01d, 0.0095d);
            input.DamageCapabilityJoules = 5000d;
            AssertPhysicalStateFailure(
                "damage energy exceeds available kinetic energy",
                input,
                PhysicalProjectileStateFailureReason.DamageCapabilityExceedsEnergy);

            PhysicalProjectileState parent = CreatePhysicalStateOrThrow(
                CreateValidRootInput(1000d, 0.01d, 0.01d));
            input = CreateChildInput(
                parent,
                PhysicalProjectileKind.ProjectileFragment,
                "fragment-invalid-source",
                0,
                0.001d,
                400d);
            input.SourceMaterialClass = PhysicalMaterialClass.Unknown;
            AssertPhysicalStateFailure(
                "child without a physical source material",
                input,
                PhysicalProjectileStateFailureReason.ChildLineageInvalid);

            input = CreateChildInput(
                parent,
                PhysicalProjectileKind.TargetSpall,
                "spall-invalid-origin",
                0,
                0.001d,
                400d);
            input.Construction = PhysicalProjectileConstruction.SteelCoreJacketed;
            AssertPhysicalStateFailure(
                "target spall with projectile construction",
                input,
                PhysicalProjectileStateFailureReason.MaterialOriginMismatch);

            PhysicalCollisionRecord collision = CreateValidCollisionRecord();
            input = CreateValidRootInput(800d, 0.01d, 0.0095d);
            input.CollisionHistory = new[] { collision, collision };
            AssertPhysicalStateFailure(
                "duplicate collision history identity",
                input,
                PhysicalProjectileStateFailureReason.DuplicateCollisionId);

            PhysicalCollisionRecordInput wrongSequenceInput = CreateValidCollisionInput();
            wrongSequenceInput.Sequence = 1;
            PhysicalCollisionRecord? wrongSequenceRecord;
            PhysicalCollisionRecordFailureReason collisionReason;
            AssertTrue(
                "wrong-sequence collision record itself remains valid",
                PhysicalCollisionRecord.TryCreate(
                    wrongSequenceInput,
                    out wrongSequenceRecord,
                    out collisionReason));
            input = CreateValidRootInput(800d, 0.01d, 0.0095d);
            input.CollisionHistory = new[]
            {
                RequireValue("wrong-sequence collision record", wrongSequenceRecord)
            };
            AssertPhysicalStateFailure(
                "collision history sequence mismatch",
                input,
                PhysicalProjectileStateFailureReason.CollisionSequenceMismatch);
        }

        private static void ValidatePhysicalTransitionIdentity()
        {
            PhysicalProjectileStateInput firstInput = CreateValidRootInput(800d, 0.01d, 0.0095d);
            firstInput.ProjectileId = "component-a";
            firstInput.RootShotId = "shared-root";
            firstInput.DeterministicSeed = 0x0000000000000001UL;
            firstInput.CollisionHistory = new[] { CreateValidCollisionRecord() };
            PhysicalProjectileState first = CreatePhysicalStateOrThrow(firstInput);

            PhysicalProjectileStateInput secondInput = CreateValidRootInput(800d, 0.01d, 0.0095d);
            secondInput.ProjectileId = "component-b";
            secondInput.RootShotId = "shared-root";
            secondInput.DeterministicSeed = 0x0000000100000001UL;
            secondInput.CollisionHistory = new[] { CreateValidCollisionRecord() };
            PhysicalProjectileState second = CreatePhysicalStateOrThrow(secondInput);

            string firstIdentity = PhysicalProjectileTransitionIdentity.CreateCollisionId(first);
            string secondIdentity = PhysicalProjectileTransitionIdentity.CreateCollisionId(second);
            AssertEqual(
                "first component transition identity",
                "component-a-collision-1",
                firstIdentity);
            AssertEqual(
                "second component transition identity",
                "component-b-collision-1",
                secondIdentity);
            AssertEqual(
                "component transition identity is deterministic",
                firstIdentity,
                PhysicalProjectileTransitionIdentity.CreateCollisionId(first));
            AssertTrue(
                "components remain distinct when their EFT seed truncation matches",
                !string.Equals(firstIdentity, secondIdentity, StringComparison.Ordinal));
            AssertTrue(
                "transition identity carries exact component identity",
                firstIdentity.StartsWith("component-a-collision-", StringComparison.Ordinal)
                    && secondIdentity.StartsWith("component-b-collision-", StringComparison.Ordinal));
        }

        private static void ValidatePhysicalCollisionObservedResolvedCorrelation()
        {
            var rootInput = CreateValidRootInput(800d, 0.01d, 0.0095d);
            PhysicalCollisionRecord previousCollision = CreateCollisionRecordOrThrow(
                CreateCollisionInput(0, PhysicalCollisionOutcome.Penetrated));
            rootInput.CollisionHistory = new[] { previousCollision };
            PhysicalProjectileState parent = CreatePhysicalStateOrThrow(rootInput);
            int expectedRecordSequence = parent.CollisionHistory.Count;
            string expectedCollisionId = PhysicalProjectileTransitionIdentity.CreateCollisionId(parent);
            PhysicalCollisionRecord resolvedCollision = CreateCollisionRecordOrThrow(
                CreateCollisionInput(expectedRecordSequence, PhysicalCollisionOutcome.Penetrated));

            AssertEqual(
                "collision identity sequence starts from next collision",
                parent.CollisionHistory.Count,
                resolvedCollision.Sequence);

            AssertEqual(
                "collision sequence and per-projectile ordinal align",
                expectedRecordSequence,
                ResolveCollisionOrdinalFromSequence(resolvedCollision.Sequence));

            var tracker = new PhysicalCollisionEventDeduplicator();
            AssertTrue(
                "first observed event is emitted",
                tracker.TryRecord(
                    parent.ProjectileId,
                    expectedCollisionId,
                    "observed"));
            AssertTrue(
                "first resolved event is emitted",
                tracker.TryRecord(
                    parent.ProjectileId,
                    expectedCollisionId,
                    "resolved"));

            AssertTrue(
                "duplicate observed event is suppressed",
                !tracker.TryRecord(
                    parent.ProjectileId,
                    expectedCollisionId,
                    "observed"));
            AssertTrue(
                "duplicate resolved event is suppressed",
                !tracker.TryRecord(
                    parent.ProjectileId,
                    expectedCollisionId,
                    "resolved"));

            string replacementCollisionIdentity = parent.ProjectileId + "-collision-" + (expectedRecordSequence + 1);
            AssertTrue(
                "distinct collision for same projectile still records",
                tracker.TryRecord(
                    parent.ProjectileId,
                    replacementCollisionIdentity,
                    "observed"));

            string otherProjectile = "other-" + parent.ProjectileId;
            AssertTrue(
                "same recordSequence on different projectiles does not dedupe",
                tracker.TryRecord(
                    otherProjectile,
                    expectedCollisionId,
                    "observed"));

            ResolvedLifecycleSemantics stopped = CalculateResolvedLifecycleSemantics(
                PhysicalCollisionOutcome.Stopped,
                false);
            AssertTrue(
                "stopped resolved event is ballistic and not lifecycle terminal",
                stopped.BallisticTerminal && !stopped.LifecycleTerminal && stopped.ResolutionKnown);
            AssertTrue(
                "stopped resolved event remains continuation false, replaced false",
                !stopped.Continued && !stopped.Replaced);

            ResolvedLifecycleSemantics sameProjectile = CalculateResolvedLifecycleSemantics(
                PhysicalCollisionOutcome.Penetrated,
                false);
            AssertTrue(
                "same-projectile continuation remains non-ballistic",
                !sameProjectile.BallisticTerminal && sameProjectile.ResolutionKnown);
            AssertTrue(
                "same-projectile continuation sets continued true and replaced false",
                sameProjectile.Continued && !sameProjectile.Replaced);

            ResolvedLifecycleSemantics replacement = CalculateResolvedLifecycleSemantics(
                PhysicalCollisionOutcome.Penetrated,
                true);
            AssertTrue(
                "replacement continuation sets continued true and replaced true",
                replacement.Continued && replacement.Replaced);

            tracker.ClearProjectile(parent.ProjectileId);
            AssertTrue(
                "dedupe state is cleared on retirement",
                tracker.TryRecord(
                    parent.ProjectileId,
                    expectedCollisionId,
                    "resolved"));
        }

        private static void ValidatePhysicalLifecycleTerminalDiagnostics()
        {
            var tracker = new PhysicalProjectileLifecycleTracker();
            PhysicalLifecycleSnapshot stopped = CreateLifecycleSnapshot("stopped-projectile", 1d);
            AssertTrue(
                "canonical created projectile enters active lifecycle tracking",
                tracker.TryRegister(stopped));
            PhysicalLifecycleTerminalAttempt stoppedTerminal = tracker.TryTerminate(
                stopped.ProjectileIdentity,
                PhysicalLifecycleTerminalReason.Stopped,
                2d);
            AssertTrue(
                "created projectile ends once as stopped",
                stoppedTerminal.Disposition == PhysicalLifecycleTerminalDisposition.Canonical
                    && stoppedTerminal.Tombstone?.TerminalReason
                        == PhysicalLifecycleTerminalReason.Stopped
                    && tracker.ActiveCount == 0);

            PhysicalLifecycleTerminalAttempt duplicateStopped = tracker.TryTerminate(
                stopped.ProjectileIdentity,
                PhysicalLifecycleTerminalReason.Stopped,
                3d);
            AssertEqual(
                "duplicate stopped-terminal attempt is detected",
                PhysicalLifecycleTerminalDisposition.Duplicate,
                duplicateStopped.Disposition);
            AssertTrue(
                "terminal-duplicate retains first and duplicate timestamps",
                duplicateStopped.Tombstone != null
                    && Math.Abs(duplicateStopped.Tombstone.TerminalTimestamp - 2d) <= Tolerance);
            AssertTrue(
                "retained retired identity is not reopened as a fresh lifecycle",
                !tracker.TryRegister(stopped));

            PhysicalLifecycleSnapshot original = CreateLifecycleSnapshot("original-projectile", 4d);
            AssertTrue("original replacement candidate is created", tracker.TryRegister(original));
            PhysicalLifecycleTerminalAttempt replacedTerminal = tracker.TryTerminate(
                original.ProjectileIdentity,
                PhysicalLifecycleTerminalReason.Replaced,
                5d);
            AssertTrue(
                "original projectile ends once as replaced",
                replacedTerminal.Disposition == PhysicalLifecycleTerminalDisposition.Canonical
                    && replacedTerminal.Tombstone?.TerminalReason
                        == PhysicalLifecycleTerminalReason.Replaced);

            PhysicalLifecycleSnapshot replacement = CreateLifecycleSnapshot(
                "replacement-projectile",
                6d);
            AssertTrue(
                "replacement receives a distinct physical projectile identity",
                !string.Equals(
                    original.ProjectileIdentity,
                    replacement.ProjectileIdentity,
                    StringComparison.Ordinal));
            AssertTrue(
                "replacement starts an independent active lifecycle",
                tracker.TryRegister(replacement));
            PhysicalLifecycleTerminalAttempt replacementTerminal = tracker.TryTerminate(
                replacement.ProjectileIdentity,
                PhysicalLifecycleTerminalReason.Stopped,
                7d);
            AssertEqual(
                "replacement later receives its own terminal event",
                PhysicalLifecycleTerminalDisposition.Canonical,
                replacementTerminal.Disposition);

            PhysicalLifecycleSnapshot aborted = CreateLifecycleSnapshot("aborted-projectile", 8d);
            AssertTrue("transaction candidate is created", tracker.TryRegister(aborted));
            PhysicalLifecycleTerminalAttempt abortTerminal = tracker.TryTerminate(
                aborted.ProjectileIdentity,
                PhysicalLifecycleTerminalReason.Aborted,
                9d);
            AssertTrue(
                "transaction-abort receives aborted termination",
                abortTerminal.Disposition == PhysicalLifecycleTerminalDisposition.Canonical
                    && abortTerminal.Tombstone?.TerminalReason
                        == PhysicalLifecycleTerminalReason.Aborted);

            PhysicalLifecycleTerminalAttempt duplicateReplacement = tracker.TryTerminate(
                original.ProjectileIdentity,
                PhysicalLifecycleTerminalReason.Replaced,
                10d);
            AssertEqual(
                "duplicate replacement-terminal attempt is detected",
                PhysicalLifecycleTerminalDisposition.Duplicate,
                duplicateReplacement.Disposition);
            int ordinaryReplacementRetirements =
                replacedTerminal.Disposition == PhysicalLifecycleTerminalDisposition.Canonical ? 1 : 0;
            ordinaryReplacementRetirements +=
                duplicateReplacement.Disposition == PhysicalLifecycleTerminalDisposition.Canonical ? 1 : 0;
            AssertEqual(
                "second ordinary replacement retirement record is suppressed",
                1,
                ordinaryReplacementRetirements);
            AssertTrue(
                "duplicate terminal disposition emits terminal-duplicate path",
                duplicateReplacement.Disposition == PhysicalLifecycleTerminalDisposition.Duplicate
                    && tracker.DuplicateTerminalViolationCount == 2);

            PhysicalLifecycleSnapshot missingSnapshot = CreateLifecycleSnapshot(
                "missing-projectile",
                11d);
            AssertTrue("missing-terminal candidate is created", tracker.TryRegister(missingSnapshot));
            PhysicalLifecycleMissingTerminal? missing = tracker.RemoveWithoutTerminal(
                missingSnapshot.ProjectileIdentity,
                "binding-incarnation-mismatch",
                12d);
            AssertTrue(
                "removal without terminal emits terminal-missing path",
                missing != null
                    && missing.RemovalReason == "binding-incarnation-mismatch"
                    && tracker.MissingTerminalViolationCount == 1);
            AssertTrue(
                "missing terminal does not masquerade as a normal terminal reason",
                missing != null
                    && missing.Tombstone.MarksMissingTerminal
                    && !missing.Tombstone.TerminalReason.HasValue);

            var shutdownTracker = new PhysicalProjectileLifecycleTracker();
            PhysicalLifecycleSnapshot shutdownFirst = CreateLifecycleSnapshot("shutdown-first", 13d);
            PhysicalLifecycleSnapshot shutdownSecond = CreateLifecycleSnapshot("shutdown-second", 14d);
            AssertTrue("first shutdown candidate is created", shutdownTracker.TryRegister(shutdownFirst));
            AssertTrue("second shutdown candidate is created", shutdownTracker.TryRegister(shutdownSecond));
            IReadOnlyList<PhysicalLifecycleSnapshot> shutdownClosed =
                shutdownTracker.CloseActiveForShutdown(15d);
            AssertTrue(
                "expected shutdown produces cleanup instead of terminal-missing",
                shutdownClosed.Count == 2
                    && shutdownTracker.MissingTerminalViolationCount == 0
                    && shutdownTracker.TryTerminate(
                        shutdownFirst.ProjectileIdentity,
                        PhysicalLifecycleTerminalReason.Stopped,
                        16d).Tombstone?.TerminalReason == PhysicalLifecycleTerminalReason.Shutdown);
            AssertEqual(
                "shutdown closes every active lifecycle tracker",
                0,
                shutdownTracker.ActiveCount);
            shutdownTracker.Clear();
            AssertTrue(
                "expected shutdown final clear removes tombstones and violation counters",
                shutdownTracker.ActiveCount == 0
                    && shutdownTracker.TombstoneCount == 0
                    && shutdownTracker.DuplicateTerminalViolationCount == 0
                    && shutdownTracker.MissingTerminalViolationCount == 0);

            var visualTracker = new PhysicalProjectileLifecycleTracker();
            PhysicalLifecycleSnapshot visual = CreateLifecycleSnapshot("visual-projectile", 17d);
            AssertTrue("visual lifecycle candidate is created", visualTracker.TryRegister(visual));
            AssertTrue(
                "visual retirement observation does not satisfy physical lifecycle termination",
                visualTracker.TryObserve(
                    visual.ProjectileIdentity,
                    new PhysicalVector3(1d, 2d, 3d),
                    new PhysicalVector3(4d, 5d, 6d),
                    null,
                    null)
                    && visualTracker.IsActive(visual.ProjectileIdentity)
                    && visualTracker.TombstoneCount == 0);

            AssertEqual(
                "fixed production tombstone capacity",
                1024,
                PhysicalProjectileLifecycleTracker.DefaultTerminalTombstoneCapacity);
            var bounded = new PhysicalProjectileLifecycleTracker(3);
            for (int index = 1; index <= 4; index++)
            {
                PhysicalLifecycleSnapshot snapshot = CreateLifecycleSnapshot(
                    "bounded-" + index.ToString(CultureInfo.InvariantCulture),
                    20d + index);
                AssertTrue(
                    "bounded tombstone projectile created " + index,
                    bounded.TryRegister(snapshot));
                AssertEqual(
                    "bounded tombstone projectile terminated " + index,
                    PhysicalLifecycleTerminalDisposition.Canonical,
                    bounded.TryTerminate(
                        snapshot.ProjectileIdentity,
                        PhysicalLifecycleTerminalReason.Stopped,
                        30d + index).Disposition);
            }

            AssertEqual("tombstone collection remains at fixed capacity", 3, bounded.TombstoneCount);
            AssertTrue(
                "tombstone eviction is deterministic oldest-first",
                !bounded.ContainsTombstone("bounded-1")
                    && bounded.ContainsTombstone("bounded-2")
                    && bounded.ContainsTombstone("bounded-3")
                    && bounded.ContainsTombstone("bounded-4"));
            AssertEqual(
                "delayed duplicate inside retained capacity is detected",
                PhysicalLifecycleTerminalDisposition.Duplicate,
                bounded.TryTerminate(
                    "bounded-2",
                    PhysicalLifecycleTerminalReason.Stopped,
                    40d).Disposition);
            AssertTrue(
                "evicted identity does not corrupt newer tombstones",
                bounded.ContainsTombstone("bounded-3")
                    && bounded.ContainsTombstone("bounded-4"));

            var correlation = new PhysicalCollisionEventDeduplicator();
            AssertTrue(
                "collision-observed correlation remains emitted once",
                correlation.TryRecord("correlated", "collision-1", "observed"));
            AssertTrue(
                "collision-resolved correlation remains independently emitted once",
                correlation.TryRecord("correlated", "collision-1", "resolved"));
            AssertTrue(
                "collision phase deduplication from 611e8a7 remains preserved",
                !correlation.TryRecord("correlated", "collision-1", "observed")
                    && !correlation.TryRecord("correlated", "collision-1", "resolved"));
        }

        private static PhysicalLifecycleSnapshot CreateLifecycleSnapshot(
            string projectileIdentity,
            double creationTimestamp)
        {
            return new PhysicalLifecycleSnapshot(
                projectileIdentity,
                "root-" + projectileIdentity,
                PhysicalProjectileKind.IntactProjectile.ToString(),
                0,
                0,
                creationTimestamp,
                new PhysicalVector3(1d, 2d, 3d),
                new PhysicalVector3(400d, 0d, 0d),
                string.Empty,
                0);
        }

        private static void ValidateProductionCollisionDeduplicationStress()
        {
            var deduplicator = new PhysicalCollisionEventDeduplicator();
            const int uniqueKeys = 5000;
            int observed = 0;
            int resolved = 0;
            for (int index = 0; index < uniqueKeys; index++)
            {
                string projectile = "stress-projectile-"
                    + (index % 997).ToString(CultureInfo.InvariantCulture);
                string collision = "stress-collision-"
                    + index.ToString(CultureInfo.InvariantCulture)
                    + "-"
                    + new string((char)('a' + (index % 26)), 24 + (index % 79));

                if (deduplicator.TryRecord(projectile, collision, "observed"))
                {
                    observed++;
                }

                if (deduplicator.TryRecord(projectile, collision, "resolved"))
                {
                    resolved++;
                }

                AssertTrue(
                    "duplicate observed suppressed " + index,
                    !deduplicator.TryRecord(projectile, collision, "observed"));
                AssertTrue(
                    "duplicate resolved suppressed " + index,
                    !deduplicator.TryRecord(projectile, collision, "resolved"));
            }

            AssertEqual("one observed event per unique key", uniqueKeys, observed);
            AssertEqual("one resolved event per unique key", uniqueKeys, resolved);
            AssertTrue(
                "observed and resolved phases are independent",
                deduplicator.TryRecord("phase-projectile", "phase-collision", "observed")
                    && deduplicator.TryRecord("phase-projectile", "phase-collision", "resolved"));
            AssertTrue(
                "same collision identity remains independent across projectiles",
                deduplicator.TryRecord("independent-a", "shared-collision", "observed")
                    && deduplicator.TryRecord("independent-b", "shared-collision", "observed"));
            deduplicator.ClearProjectile("independent-a");
            AssertTrue(
                "retired projectile dedupe state clears without touching peers",
                deduplicator.TryRecord("independent-a", "shared-collision", "observed")
                    && !deduplicator.TryRecord("independent-b", "shared-collision", "observed"));
        }

        private static void ValidateRuntimeErrorEvidence()
        {
            var accumulator = new FieldReportRuntimeErrorAccumulator();
            string privatePath = @"C:\Users\" + Environment.UserName + @"\private\report.txt";
            Exception exception;
            try
            {
                ThrowRuntimeErrorForValidation(privatePath);
                throw new InvalidOperationException("Runtime-error validation did not throw.");
            }
            catch (InvalidOperationException caught)
            {
                exception = caught;
            }
            DateTimeOffset firstAt = new DateTimeOffset(2026, 8, 20, 20, 0, 0, TimeSpan.Zero);
            FieldReportRuntimeErrorSnapshot first = accumulator.Capture(
                "Physical projectile lifecycle diagnostics",
                exception,
                firstAt);
            AssertTrue("first runtime error retains full detail", first.IncludeFullDetail);
            AssertEqual("first runtime error occurrence count", 1, first.OccurrenceCount);
            AssertEqual("runtime error HRESULT retained", exception.HResult, first.HResult);
            AssertTrue(
                "runtime error message removes filesystem path",
                !first.SanitizedMessage.Contains(privatePath, StringComparison.OrdinalIgnoreCase));
            AssertTrue(
                "runtime error message removes credentials",
                !first.SanitizedMessage.Contains("do-not-record", StringComparison.Ordinal));
            AssertTrue(
                "runtime error message removes machine name",
                string.IsNullOrWhiteSpace(Environment.MachineName)
                    || !first.SanitizedMessage.Contains(
                        Environment.MachineName,
                        StringComparison.OrdinalIgnoreCase));
            AssertTrue(
                "runtime error fingerprint is stable and bounded",
                first.StackFingerprint.Length == 24);
            AssertTrue("runtime error retains safe top method names", first.TopMethods.Length > 0);

            FieldReportRuntimeErrorSnapshot latest = first;
            for (int occurrence = 2; occurrence <= 4096; occurrence++)
            {
                latest = accumulator.Capture(
                    "Physical projectile lifecycle diagnostics",
                    exception,
                    firstAt.AddMilliseconds(occurrence));
                AssertTrue(
                    "repeated runtime error omits repeated full detail " + occurrence,
                    !latest.IncludeFullDetail);
            }

            AssertEqual("repeated runtime error count is bounded aggregate", 4096, latest.OccurrenceCount);
            AssertTrue(
                "power-of-two runtime error aggregate emitted",
                FieldReportRuntimeErrorAccumulator.ShouldEmitAggregate(latest.OccurrenceCount));
            AssertTrue(
                "non-power-of-two runtime error aggregate suppressed",
                !FieldReportRuntimeErrorAccumulator.ShouldEmitAggregate(4095));
            IReadOnlyList<FieldReportRuntimeErrorSnapshot> totals = accumulator.SnapshotTotals();
            AssertTrue(
                "runtime error final total is retained for session end",
                totals.Count == 1 && totals[0].OccurrenceCount == 4096);
        }

        private static void ThrowRuntimeErrorForValidation(string privatePath)
        {
            throw new InvalidOperationException(
                "Failure at " + privatePath
                    + " on " + Environment.MachineName
                    + " token=do-not-record");
        }

        private static void ValidateRecycledShotReportContext()
        {
            var tracker = new PhysicalProjectileLifecycleTracker();
            PhysicalLifecycleSnapshot creation = CreateLifecycleSnapshot("recycled-context", 1d);
            AssertTrue("recycled context lifecycle registered", tracker.TryRegister(creation));
            var verifiedPosition = new PhysicalVector3(10d, 20d, 30d);
            var verifiedVelocity = new PhysicalVector3(400d, 5d, -2d);
            AssertTrue(
                "recycled context tracker accepts verified observation",
                tracker.TryObserve(
                    creation.ProjectileIdentity,
                    verifiedPosition,
                    verifiedVelocity,
                    "collision-safe",
                    1));
            AssertTrue(
                "recycled context tracker snapshot available",
                tracker.TryGetActiveSnapshot(
                    creation.ProjectileIdentity,
                    out PhysicalLifecycleSnapshot? snapshot));
            PhysicalLifecycleReportContext stale = PhysicalLifecycleReportContext.Resolve(
                false,
                new PhysicalVector3(9e12d, 8e12d, 7e12d),
                new PhysicalVector3(6e12d, 5e12d, 4e12d),
                snapshot,
                creation.LastKnownPosition,
                creation.LastKnownVelocity);
            AssertTrue("recycled shot is marked mismatched", !stale.ShotBindingMatched);
            AssertEqual("recycled shot uses tracker snapshot", "tracker-snapshot", stale.ContextSource);
            AssertEqual("recycled shot rejects pooled position", verifiedPosition, stale.Position);
            AssertEqual("recycled shot rejects pooled velocity", verifiedVelocity, stale.Velocity);

            PhysicalLifecycleReportContext creationOnly = PhysicalLifecycleReportContext.Resolve(
                false,
                new PhysicalVector3(9e12d, 8e12d, 7e12d),
                new PhysicalVector3(6e12d, 5e12d, 4e12d),
                null,
                creation.LastKnownPosition,
                creation.LastKnownVelocity);
            AssertEqual("missing tracker falls back to binding creation", "binding-creation", creationOnly.ContextSource);
            AssertEqual("binding creation position retained", creation.LastKnownPosition, creationOnly.Position);
        }

        private static void ValidateNumericRunawayDetection()
        {
            const double massKilograms = 0.000035466922248116548d;
            const double assignedEnergyJoules = 5.8070849995439389d;
            var projectedVelocity = new PhysicalVector3(
                501.03125d,
                197.43785095214844d,
                193.52210998535156d);
            AssertTrue(
                "representative target spall creation is not runaway",
                !PhysicalNumericRunawayDetector.IsRunaway(
                    assignedEnergyJoules,
                    massKilograms,
                    projectedVelocity,
                    new PhysicalVector3(-14.38d, 0.79d, 25.79d)));
            AssertTrue(
                "representative corrupted host transition is detected",
                PhysicalNumericRunawayDetector.IsRunaway(
                    assignedEnergyJoules,
                    massKilograms,
                    new PhysicalVector3(-573790.875d, -226148.03125d, -221625.40625d),
                    new PhysicalVector3(-44697702400d, -17616693248d, -17264386048d)));
            AssertTrue(
                "non-finite numeric transition is detected",
                PhysicalNumericRunawayDetector.IsRunaway(
                    assignedEnergyJoules,
                    massKilograms,
                    new PhysicalVector3(double.PositiveInfinity, 0d, 0d),
                    PhysicalVector3.Zero));
        }

        private static void ValidateFieldReportSessionStart()
        {
            string directory = CreateFieldReportTemporaryDirectory();
            try
            {
                using FieldReportRecorder recorder = CreateFieldReportRecorder(directory);
                recorder.Stop();
                string reportPath = GetSingleCompletedFieldReport(directory);
                string[] lines = File.ReadAllLines(reportPath);
                AssertTrue("field report contains records", lines.Length >= 2);
                using JsonDocument first = JsonDocument.Parse(lines[0]);
                AssertEqual("first field report event", "session-start", first.RootElement.GetProperty("event").GetString());
                AssertEqual("field report schema version", FieldReportRecord.CurrentSchemaVersion, first.RootElement.GetProperty("schemaVersion").GetInt32());
                AssertEqual("first report sequence", 1L, first.RootElement.GetProperty("reportSequence").GetInt64());
            }
            finally
            {
                DeleteFieldReportTemporaryDirectory(directory);
            }
        }

        private static void ValidateFieldReportLifecycleSerialization()
        {
            FieldReportLifecycleEventSnapshot snapshot = CreateFieldReportLifecycleSnapshot("collision-resolved");
            string json = snapshot.ToRecord().ToJsonLine(17L);
            using JsonDocument document = JsonDocument.Parse(json);
            JsonElement root = document.RootElement;
            AssertEqual("lifecycle report sequence", 17L, root.GetProperty("reportSequence").GetInt64());
            AssertEqual("lifecycle record sequence retained", 4, root.GetProperty("recordSequence").GetInt32());
            AssertEqual("lifecycle collision ordinal retained", 4, root.GetProperty("collisionOrdinal").GetInt32());
            AssertEqual("lifecycle collision identity retained", "collision-4", root.GetProperty("collisionIdentity").GetString());
            AssertEqual("lifecycle root identity retained", "root-projectile-1", root.GetProperty("rootIdentity").GetString());
            AssertEqual("lifecycle incoming speed retained", 900d, root.GetProperty("incomingSpeed").GetDouble());
            AssertEqual("lifecycle outgoing speed retained", 600d, root.GetProperty("outgoingSpeed").GetDouble());
            AssertEqual("lifecycle replacement relationship retained", "source-collision:collision-3", root.GetProperty("replacementRelationship").GetString());
            AssertTrue("lifecycle binding match retained", root.GetProperty("shotBindingMatched").GetBoolean());
            AssertEqual("lifecycle context source retained", "current-shot", root.GetProperty("contextSource").GetString());
        }

        private static void ValidateFieldReportConcurrency()
        {
            string directory = CreateFieldReportTemporaryDirectory();
            try
            {
                using FieldReportRecorder recorder = CreateFieldReportRecorder(directory, queueCapacity: 4096);
                const int workers = 8;
                const int recordsPerWorker = 100;
                var tasks = new Task[workers];
                for (int worker = 0; worker < workers; worker++)
                {
                    int capturedWorker = worker;
                    tasks[worker] = Task.Run(delegate
                    {
                        for (int index = 0; index < recordsPerWorker; index++)
                        {
                            recorder.Record(
                                new FieldReportRecord(
                                    "concurrent-event",
                                    false,
                                    new[]
                                    {
                                        FieldReportField("worker", capturedWorker),
                                        FieldReportField("index", index)
                                    }));
                        }
                    });
                }

                Task.WaitAll(tasks);
                recorder.Stop();
                List<JsonElement> records = ReadFieldReportJson(GetSingleCompletedFieldReport(directory));
                long previous = 0L;
                for (int index = 0; index < records.Count; index++)
                {
                    long current = records[index].GetProperty("reportSequence").GetInt64();
                    AssertTrue("concurrent report sequence increases at " + index, current > previous);
                    previous = current;
                }

                AssertEqual("all concurrent records remained valid JSONL", workers * recordsPerWorker + 2, records.Count);
            }
            finally
            {
                DeleteFieldReportTemporaryDirectory(directory);
            }
        }

        private static void ValidateFieldReportIssueMarkerFlush()
        {
            string directory = CreateFieldReportTemporaryDirectory();
            try
            {
                using FieldReportRecorder recorder = CreateFieldReportRecorder(directory, flushSeconds: 30d);
                bool accepted = recorder.Record(
                    new FieldReportRecord(
                        "issue-marker",
                        true,
                        new[] { FieldReportField("markerSequence", 1) }),
                    true);
                AssertTrue("issue marker accepted", accepted);
                bool flushed = SpinWait.SpinUntil(
                    delegate
                    {
                        try
                        {
                            return ReadActiveFieldReport(recorder.PartialPath).Contains("\"event\":\"issue-marker\"", StringComparison.Ordinal);
                        }
                        catch (IOException)
                        {
                            return false;
                        }
                    },
                    TimeSpan.FromSeconds(3d));
                AssertTrue("issue marker promptly flushed", flushed);
                recorder.Stop();
                AssertTrue("marked report filename identifies marker", GetSingleCompletedFieldReport(directory).EndsWith("-marked.bpreport", StringComparison.Ordinal));
            }
            finally
            {
                DeleteFieldReportTemporaryDirectory(directory);
            }
        }

        private static void ValidateFieldReportShutdownFinalization()
        {
            string directory = CreateFieldReportTemporaryDirectory();
            try
            {
                using FieldReportRecorder recorder = CreateFieldReportRecorder(directory);
                string partialPath = recorder.PartialPath;
                recorder.Stop();
                AssertTrue("normal shutdown removes partial filename", !File.Exists(partialPath));
                string reportPath = GetSingleCompletedFieldReport(directory);
                List<JsonElement> records = ReadFieldReportJson(reportPath);
                JsonElement end = records[records.Count - 1];
                AssertEqual("normal shutdown writes session-end", "session-end", end.GetProperty("event").GetString());
                AssertEqual("session-end final length is exact", new FileInfo(reportPath).Length, end.GetProperty("finalReportLength").GetInt64());
            }
            finally
            {
                DeleteFieldReportTemporaryDirectory(directory);
            }
        }

        private static void ValidateFieldReportCrashRecovery()
        {
            string directory = CreateFieldReportTemporaryDirectory();
            try
            {
                string partial = Path.Combine(directory, "20260820T120000Z-crashed.partial.bpreport");
                const string content = "{\"event\":\"session-start\"}\n{\"event\":\"created\"}\n";
                File.WriteAllText(partial, content);
                IReadOnlyList<string> recovered = FieldReportRecorder.RecoverStalePartialReports(directory);
                AssertEqual("one stale partial recovered", 1, recovered.Count);
                AssertTrue("stale partial is preserved under recovered name", Path.GetFileName(recovered[0]).StartsWith("recovered-crash-", StringComparison.Ordinal));
                AssertEqual("crash recovery preserves exact bytes", content, File.ReadAllText(recovered[0]));
                AssertTrue("original stale partial renamed", !File.Exists(partial));
            }
            finally
            {
                DeleteFieldReportTemporaryDirectory(directory);
            }
        }

        private static void ValidateFieldReportExceptionIsolation()
        {
            string directory = CreateFieldReportTemporaryDirectory();
            try
            {
                string occupiedPath = Path.Combine(directory, "occupied");
                File.WriteAllText(occupiedPath, "not a directory");
                var options = CreateFieldReportOptions(occupiedPath);
                FieldReportRecorder recorder = FieldReportRecorder.Start(
                    options,
                    CreateFieldReportSessionStart());
                AssertTrue("initialization exception disables only recorder", !recorder.IsEnabled);
                AssertTrue("disabled recorder rejects later calls without throwing", !recorder.Record(new FieldReportRecord("created", false)));
                recorder.Stop();
            }
            finally
            {
                DeleteFieldReportTemporaryDirectory(directory);
            }
        }

        private static void ValidateDisabledFieldRecorder()
        {
            string parent = Path.Combine(Path.GetTempPath(), "BallisticPenetration-disabled-" + Guid.NewGuid().ToString("N"));
            FieldReportRecorder? recorder = FieldReportRecorder.StartIfEnabled(
                false,
                CreateFieldReportOptions(parent),
                CreateFieldReportSessionStart());
            AssertTrue("disabled recorder returns no instance", recorder == null);
            AssertTrue("disabled recorder creates no report directory", !Directory.Exists(parent));
        }

        private static void ValidateFieldReportQueueOverflow()
        {
            string directory = CreateFieldReportTemporaryDirectory();
            try
            {
                using FieldReportRecorder recorder = CreateFieldReportRecorder(directory, queueCapacity: 1);
                string payload = new string('x', 4096);
                Parallel.For(
                    0,
                    2000,
                    index => recorder.Record(
                        new FieldReportRecord(
                            "created",
                            false,
                            new[]
                            {
                                FieldReportField("index", index),
                                FieldReportField("payload", payload)
                            })));
                recorder.Stop();
                JsonElement end = ReadFieldReportJson(GetSingleCompletedFieldReport(directory)).Last();
                AssertTrue("queue overflow is counted", end.GetProperty("droppedEventCount").GetInt64() > 0L);
                AssertEqual("queue high-water mark is bounded", 1, end.GetProperty("writerQueueHighWaterMark").GetInt32());
            }
            finally
            {
                DeleteFieldReportTemporaryDirectory(directory);
            }
        }

        private static void ValidateFieldReportRetention()
        {
            string directory = CreateFieldReportTemporaryDirectory();
            try
            {
                string oldest = Path.Combine(directory, "20260818T120000Z-oldest.bpreport");
                string middle = Path.Combine(directory, "20260819T120000Z-middle.bpreport");
                string newest = Path.Combine(directory, "20260820T120000Z-newest.bpreport");
                string unknown = Path.Combine(directory, "tester-notes.bpreport");
                File.WriteAllText(oldest, "oldest");
                File.WriteAllText(middle, "middle");
                File.WriteAllText(newest, "newest");
                File.WriteAllText(unknown, "unknown");
                File.SetLastWriteTimeUtc(oldest, new DateTime(2026, 8, 18, 12, 0, 0, DateTimeKind.Utc));
                File.SetLastWriteTimeUtc(middle, new DateTime(2026, 8, 19, 12, 0, 0, DateTimeKind.Utc));
                File.SetLastWriteTimeUtc(newest, new DateTime(2026, 8, 20, 12, 0, 0, DateTimeKind.Utc));
                IReadOnlyList<string> deleted = FieldReportRecorder.ApplyRetention(directory, 1, long.MaxValue);
                AssertEqual("oldest owned completed reports deleted", 2, deleted.Count);
                AssertTrue("newest owned completed report retained", File.Exists(newest));
                AssertTrue("unknown report file retained", File.Exists(unknown));
            }
            finally
            {
                DeleteFieldReportTemporaryDirectory(directory);
            }
        }

        private static void ValidateFieldReportActivePartialProtection()
        {
            string directory = CreateFieldReportTemporaryDirectory();
            try
            {
                string active = Path.Combine(directory, "20260820T120000Z-active.partial.bpreport");
                string completed = Path.Combine(directory, "20260819T120000Z-complete.bpreport");
                File.WriteAllText(active, "active");
                File.WriteAllText(completed, "complete");
                FieldReportRecorder.ApplyRetention(directory, 1, 1L);
                AssertTrue("active partial is never deleted by retention", File.Exists(active));
            }
            finally
            {
                DeleteFieldReportTemporaryDirectory(directory);
            }
        }

        private static void ValidateFieldReportTruncation()
        {
            string directory = CreateFieldReportTemporaryDirectory();
            try
            {
                using FieldReportRecorder recorder = CreateFieldReportRecorder(directory, maximumFileBytes: 8192L);
                recorder.Record(
                    new FieldReportRecord(
                        "created",
                        false,
                        new[] { FieldReportField("payload", new string('x', 9000)) }));
                recorder.Record(
                    new FieldReportRecord(
                        "issue-marker",
                        true,
                        new[] { FieldReportField("markerSequence", 1) }),
                    true);
                recorder.Stop();
                List<JsonElement> records = ReadFieldReportJson(GetSingleCompletedFieldReport(directory));
                AssertTrue("size limit emits report-truncated", records.Any(record => record.GetProperty("event").GetString() == "report-truncated"));
                AssertTrue("critical marker remains eligible after truncation", records.Any(record => record.GetProperty("event").GetString() == "issue-marker"));
                AssertTrue("truncated report remains within maximum", new FileInfo(GetSingleCompletedFieldReport(directory)).Length <= 8192L);
            }
            finally
            {
                DeleteFieldReportTemporaryDirectory(directory);
            }
        }

        private static void ValidateFieldReportPrivacy()
        {
            string privateToken = "private-user-" + Guid.NewGuid().ToString("N");
            string directory = Path.Combine(Path.GetTempPath(), privateToken, "reports");
            try
            {
                Directory.CreateDirectory(directory);
                using FieldReportRecorder recorder = CreateFieldReportRecorder(directory);
                recorder.Stop();
                string contents = File.ReadAllText(GetSingleCompletedFieldReport(directory));
                AssertTrue("report omits full personal report path", !contents.Contains(directory, StringComparison.OrdinalIgnoreCase));
                AssertTrue("report omits username-like directory token", !contents.Contains(privateToken, StringComparison.OrdinalIgnoreCase));
                AssertTrue("report omits Windows username", string.IsNullOrWhiteSpace(Environment.UserName)
                    || !contents.Contains(Environment.UserName, StringComparison.OrdinalIgnoreCase));
                AssertTrue("report omits computer name", string.IsNullOrWhiteSpace(Environment.MachineName)
                    || !contents.Contains(Environment.MachineName, StringComparison.OrdinalIgnoreCase));
                foreach (string line in File.ReadLines(GetSingleCompletedFieldReport(directory)))
                {
                    using JsonDocument document = JsonDocument.Parse(line);
                    AssertEqual("privacy report line is a JSON object", JsonValueKind.Object, document.RootElement.ValueKind);
                }
            }
            finally
            {
                string root = Directory.GetParent(directory)?.FullName ?? directory;
                DeleteFieldReportTemporaryDirectory(root);
            }
        }

        private static FieldReportLifecycleEventSnapshot CreateFieldReportLifecycleSnapshot(string eventName)
        {
            return new FieldReportLifecycleEventSnapshot(
                eventName,
                DateTimeOffset.UtcNow,
                "projectile-1",
                "root-projectile-1",
                "IntactProjectile",
                0,
                0,
                4,
                4,
                "resolved",
                true,
                1d,
                2d,
                new PhysicalVector3(0d, 0d, 0d),
                new PhysicalVector3(0d, 0d, 1000d),
                new PhysicalVector3(0d, 0d, 10d),
                new PhysicalVector3(0d, 0d, 600d),
                "collision-4",
                "armor-steel",
                "ArmoredSteel",
                new PhysicalVector3(0d, 0d, 900d),
                new PhysicalVector3(0d, 0d, 600d),
                "Penetrated",
                true,
                true,
                false,
                false,
                "none",
                false,
                "surface-1",
                "Flying",
                "Flying",
                "resolved-continuation",
                true,
                "profile-a1b2c3d4",
                "weapon-template",
                "Test Weapon",
                "ammo-template",
                "Test Ammunition",
                "CaliberTest",
                1000d,
                new PhysicalVector3(0d, 0d, 0d),
                "ArmorPlateCollider",
                "Chest",
                "body-armor",
                "BoxCollider:armor",
                10d,
                10d,
                "source-collision:collision-3",
                true,
                "current-shot");
        }

        private static FieldReportRecorder CreateFieldReportRecorder(
            string directory,
            int queueCapacity = 128,
            double flushSeconds = 0.05d,
            long maximumFileBytes = 1024L * 1024L)
        {
            return FieldReportRecorder.Start(
                CreateFieldReportOptions(directory, queueCapacity, flushSeconds, maximumFileBytes),
                CreateFieldReportSessionStart());
        }

        private static FieldReportOptions CreateFieldReportOptions(
            string directory,
            int queueCapacity = 128,
            double flushSeconds = 0.05d,
            long maximumFileBytes = 1024L * 1024L)
        {
            return new FieldReportOptions(
                directory,
                Guid.NewGuid().ToString("N").Substring(0, 16),
                queueCapacity,
                TimeSpan.FromSeconds(flushSeconds),
                20,
                16L * 1024L * 1024L,
                maximumFileBytes);
        }

        private static FieldReportRecord CreateFieldReportSessionStart()
        {
            return new FieldReportRecord(
                "session-start",
                true,
                new[]
                {
                    FieldReportField("sessionId", "validation-session"),
                    FieldReportField("runningDllFileName", "BallisticPenetration.dll"),
                    FieldReportField("runningDllSha256", new string('A', 64)),
                    FieldReportField("runningDllLength", 123456L)
                });
        }

        private static string CreateFieldReportTemporaryDirectory()
        {
            string path = Path.Combine(
                Path.GetTempPath(),
                "BallisticPenetration.FieldReports.Validation",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return path;
        }

        private static void DeleteFieldReportTemporaryDirectory(string directory)
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }

        private static string GetSingleCompletedFieldReport(string directory)
        {
            string[] reports = Directory.GetFiles(directory, "*.bpreport", SearchOption.TopDirectoryOnly)
                .Where(path => !path.EndsWith(".partial.bpreport", StringComparison.OrdinalIgnoreCase))
                .ToArray();
            AssertEqual("single completed field report", 1, reports.Length);
            return reports[0];
        }

        private static List<JsonElement> ReadFieldReportJson(string path)
        {
            var records = new List<JsonElement>();
            foreach (string line in File.ReadLines(path))
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                using JsonDocument document = JsonDocument.Parse(line);
                records.Add(document.RootElement.Clone());
            }

            return records;
        }

        private static string ReadActiveFieldReport(string path)
        {
            using var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }

        private static KeyValuePair<string, object?> FieldReportField(string name, object? value)
        {
            return new KeyValuePair<string, object?>(name, value);
        }

        private static void ValidatePhysicalVisualGeometry()
        {
            for (PhysicalProjectileShapeClass shape = PhysicalProjectileShapeClass.Spitzer;
                 shape <= PhysicalProjectileShapeClass.Flechette;
                 shape++)
            {
                AssertTrue(
                    "unit render mesh accepted for " + shape,
                    PhysicalProjectileVisualGeometry.TryCreateUnitMesh(
                        shape,
                        out PhysicalVisualMeshDescriptor? descriptor,
                        out PhysicalVisualGeometryFailureReason reason));
                PhysicalVisualMeshDescriptor mesh = RequireValue(
                    "unit render mesh " + shape,
                    descriptor);
                AssertEqual(
                    "unit render mesh reason " + shape,
                    PhysicalVisualGeometryFailureReason.None,
                    reason);
                AssertEqual("unit render mesh shape " + shape, shape, mesh.ShapeClass);
                AssertTrue("unit render mesh has vertices " + shape, mesh.Vertices.Count >= 5);
                AssertTrue(
                    "unit render mesh has complete triangles " + shape,
                    mesh.Triangles.Count >= 12 && mesh.Triangles.Count % 3 == 0);

                double minimumZ = double.PositiveInfinity;
                double maximumZ = double.NegativeInfinity;
                for (int index = 0; index < mesh.Vertices.Count; index++)
                {
                    PhysicalVector3 vertex = mesh.Vertices[index];
                    minimumZ = Math.Min(minimumZ, vertex.Z);
                    maximumZ = Math.Max(maximumZ, vertex.Z);
                    AssertTrue("unit render vertex finite " + shape + " " + index, vertex.IsFinite);
                    AssertTrue(
                        "unit render vertex x bounded " + shape + " " + index,
                        Math.Abs(vertex.X) <= 0.500000000001d);
                    AssertTrue(
                        "unit render vertex y bounded " + shape + " " + index,
                        Math.Abs(vertex.Y) <= 0.500000000001d);
                    AssertTrue(
                        "unit render vertex z bounded " + shape + " " + index,
                        Math.Abs(vertex.Z) <= 0.500000000001d);
                }

                double maximumTransverseDistanceSquared = 0d;
                for (int index = 0; index < mesh.Vertices.Count; index++)
                {
                    for (int otherIndex = index + 1;
                         otherIndex < mesh.Vertices.Count;
                         otherIndex++)
                    {
                        PhysicalVector3 vertex = mesh.Vertices[index];
                        PhysicalVector3 other = mesh.Vertices[otherIndex];
                        double deltaX = other.X - vertex.X;
                        double deltaY = other.Y - vertex.Y;
                        maximumTransverseDistanceSquared = Math.Max(
                            maximumTransverseDistanceSquared,
                            (deltaX * deltaX) + (deltaY * deltaY));
                    }
                }

                AssertNear(
                    "unit render mesh longitudinal span " + shape,
                    1d,
                    maximumZ - minimumZ);
                AssertNear(
                    "unit render mesh transverse diameter " + shape,
                    1d,
                    Math.Sqrt(maximumTransverseDistanceSquared));

                double signedVolume = 0d;
                for (int index = 0; index < mesh.Triangles.Count; index += 3)
                {
                    int a = mesh.Triangles[index];
                    int b = mesh.Triangles[index + 1];
                    int c = mesh.Triangles[index + 2];
                    AssertTrue(
                        "unit render triangle indices bounded " + shape + " " + index,
                        a >= 0
                            && b >= 0
                            && c >= 0
                            && a < mesh.Vertices.Count
                            && b < mesh.Vertices.Count
                            && c < mesh.Vertices.Count);
                    PhysicalVector3 edgeOne = mesh.Vertices[b].Subtract(mesh.Vertices[a]);
                    PhysicalVector3 edgeTwo = mesh.Vertices[c].Subtract(mesh.Vertices[a]);
                    PhysicalVector3 normal = edgeOne.Cross(edgeTwo);
                    AssertTrue(
                        "unit render triangle nondegenerate " + shape + " " + index,
                        normal.MagnitudeSquared > 0.000000000001d);
                    PhysicalVector3 centroid = mesh.Vertices[a]
                        .Add(mesh.Vertices[b])
                        .Add(mesh.Vertices[c])
                        .Scale(1d / 3d);
                    if (shape != PhysicalProjectileShapeClass.Flechette)
                    {
                        AssertTrue(
                            "unit render triangle faces outward " + shape + " " + index,
                            normal.Dot(centroid) > 0d);
                    }
                    signedVolume += mesh.Vertices[a].Dot(
                        mesh.Vertices[b].Cross(mesh.Vertices[c])) / 6d;
                }
                AssertTrue(
                    "unit render mesh has consistent outward volume " + shape,
                    signedVolume > 0d);

                var edgeCounts = new Dictionary<(int Minimum, int Maximum), int>();
                var edgeDirections = new Dictionary<(int Minimum, int Maximum), int>();
                for (int index = 0; index < mesh.Triangles.Count; index += 3)
                {
                    int a = mesh.Triangles[index];
                    int b = mesh.Triangles[index + 1];
                    int c = mesh.Triangles[index + 2];
                    AddDirectedMeshEdge(edgeCounts, edgeDirections, a, b);
                    AddDirectedMeshEdge(edgeCounts, edgeDirections, b, c);
                    AddDirectedMeshEdge(edgeCounts, edgeDirections, c, a);
                }

                foreach (KeyValuePair<(int Minimum, int Maximum), int> edge in edgeCounts)
                {
                    AssertEqual(
                        "unit render mesh edge is two-manifold " + shape + " " + edge.Key,
                        2,
                        edge.Value);
                    AssertEqual(
                        "unit render mesh edge winding is consistent " + shape + " " + edge.Key,
                        0,
                        edgeDirections[edge.Key]);
                }

                AssertTrue(
                    "unit render mesh deterministic " + shape,
                    PhysicalProjectileVisualGeometry.TryCreateUnitMesh(
                        shape,
                        out PhysicalVisualMeshDescriptor? repeated,
                        out _));
                PhysicalVisualMeshDescriptor repeatedMesh = RequireValue(
                    "repeated unit render mesh " + shape,
                    repeated);
                AssertEqual(
                    "unit render vertex count deterministic " + shape,
                    mesh.Vertices.Count,
                    repeatedMesh.Vertices.Count);
                AssertEqual(
                    "unit render triangle count deterministic " + shape,
                    mesh.Triangles.Count,
                    repeatedMesh.Triangles.Count);
                for (int index = 0; index < mesh.Vertices.Count; index++)
                {
                    AssertEqual(
                        "unit render vertex deterministic " + shape + " " + index,
                        mesh.Vertices[index],
                        repeatedMesh.Vertices[index]);
                }

                for (int index = 0; index < mesh.Triangles.Count; index++)
                {
                    AssertEqual(
                        "unit render index deterministic " + shape + " " + index,
                        mesh.Triangles[index],
                        repeatedMesh.Triangles[index]);
                }
            }

            AssertTrue(
                "unknown render shape rejected",
                !PhysicalProjectileVisualGeometry.TryCreateUnitMesh(
                    PhysicalProjectileShapeClass.Unknown,
                    out _,
                    out PhysicalVisualGeometryFailureReason unknownReason));
            AssertEqual(
                "unknown render shape reason",
                PhysicalVisualGeometryFailureReason.ShapeUnsupported,
                unknownReason);
            AssertTrue(
                "undersampled round mesh rejected",
                !PhysicalProjectileVisualGeometry.TryCreateUnitMesh(
                    PhysicalProjectileShapeClass.Spitzer,
                    5,
                    out _,
                    out PhysicalVisualGeometryFailureReason segmentReason));
            AssertEqual(
                "undersampled round mesh reason",
                PhysicalVisualGeometryFailureReason.SegmentCountInvalid,
                segmentReason);

            PhysicalProjectileState root = CreatePhysicalStateOrThrow(
                CreateValidRootInput(800d, 0.01d, 0.01d));
            AssertTrue(
                "exact physical render pose accepted",
                PhysicalProjectileVisualGeometry.TryCreatePose(
                    root,
                    1d,
                    0d,
                    out PhysicalVisualPose exactPose,
                    out PhysicalVisualGeometryFailureReason poseReason));
            AssertEqual(
                "exact physical render pose reason",
                PhysicalVisualGeometryFailureReason.None,
                poseReason);
            AssertEqual(
                "steel-core render material",
                PhysicalVisualMaterialKey.SteelCore,
                exactPose.MaterialKey);
            AssertNear("exact render diameter x", root.DeformedDiameterMetres, exactPose.ScaleMetres.X);
            AssertNear("exact render diameter y", root.DeformedDiameterMetres, exactPose.ScaleMetres.Y);
            AssertNear("exact render length", root.LengthMetres, exactPose.ScaleMetres.Z);

            PhysicalProjectileStateInput yawedInput = CreateValidRootInput(800d, 0.01d, 0.01d);
            yawedInput.DeterministicSeed = 0x123456789ABCDEF0UL;
            yawedInput.YawAngleRadians = Math.PI / 4d;
            yawedInput.TumbleState = PhysicalProjectileTumbleState.Yawing;
            AssertTrue(
                "yawed physical attitude created",
                PhysicalOrientation.TryApplyYaw(
                    yawedInput.Orientation,
                    yawedInput.YawAngleRadians,
                    yawedInput.DeterministicSeed,
                    out PhysicalOrientation yawedOrientation));
            yawedInput.Orientation = yawedOrientation;
            PhysicalProjectileState yawed = CreatePhysicalStateOrThrow(yawedInput);
            AssertTrue(
                "yawed physical render pose accepted",
                PhysicalProjectileVisualGeometry.TryCreatePose(
                    yawed,
                    1d,
                    0d,
                    out PhysicalVisualPose yawedPose,
                    out _));
            AssertTrue("yawed render orientation remains unit", yawedPose.Orientation.IsUnit);
            double forwardZ = 1d
                - (2d * yawedPose.Orientation.X * yawedPose.Orientation.X)
                - (2d * yawedPose.Orientation.Y * yawedPose.Orientation.Y);
            AssertTrue(
                "yawed render orientation matches physical yaw",
                Math.Abs(Math.Cos(yawed.YawAngleRadians) - forwardZ) <= 0.000000001d);
            AssertTrue(
                "yawed render orientation deterministic",
                PhysicalProjectileVisualGeometry.TryCreatePose(
                    yawed,
                    1d,
                    0d,
                    out PhysicalVisualPose repeatedYawedPose,
                    out _));
            AssertEqual(
                "yawed render orientation repeats exactly",
                yawedPose.Orientation,
                repeatedYawedPose.Orientation);
            AssertTrue(
                "nonunit render base orientation rejected",
                !PhysicalOrientation.TryApplyYaw(
                    new PhysicalOrientation(1d, 1d, 1d, 1d),
                    yawed.YawAngleRadians,
                    yawed.DeterministicSeed,
                    out _));
            AssertTrue(
                "small deterministic yaw seed one accepted",
                PhysicalOrientation.TryApplyYaw(
                    PhysicalOrientation.Identity,
                    0.5d,
                    1UL,
                    out PhysicalOrientation firstSeedOrientation));
            AssertTrue(
                "small deterministic yaw seed two accepted",
                PhysicalOrientation.TryApplyYaw(
                    PhysicalOrientation.Identity,
                    0.5d,
                    2UL,
                    out PhysicalOrientation secondSeedOrientation));
            AssertTrue(
                "small deterministic yaw seeds produce distinct azimuths",
                RotateLocalForward(firstSeedOrientation).Dot(
                    RotateLocalForward(secondSeedOrientation)) < 0.999999d);

            const double minimumDiameter = 0.02d;
            AssertTrue(
                "minimum-diameter render pose accepted",
                PhysicalProjectileVisualGeometry.TryCreatePose(
                    root,
                    1d,
                    minimumDiameter,
                    out PhysicalVisualPose enlargedPose,
                    out _));
            AssertNear("minimum render diameter applied", minimumDiameter, enlargedPose.ScaleMetres.X);
            AssertNear(
                "minimum render diameter preserves aspect ratio",
                root.LengthMetres * (minimumDiameter / root.DeformedDiameterMetres),
                enlargedPose.ScaleMetres.Z);

            PhysicalProjectileState targetSpall = CreateChildState(
                root,
                PhysicalProjectileKind.TargetSpall,
                "visual-spall",
                0,
                0.0001d,
                250d);
            AssertTrue(
                "target-spall render pose accepted",
                PhysicalProjectileVisualGeometry.TryCreatePose(
                    targetSpall,
                    1d,
                    0d,
                    out PhysicalVisualPose spallPose,
                    out _));
            AssertEqual(
                "armored-steel spall render material",
                PhysicalVisualMaterialKey.TargetMetal,
                spallPose.MaterialKey);

            AssertTrue(
                "nonfinite render scale rejected",
                !PhysicalProjectileVisualGeometry.TryCreatePose(
                    root,
                    double.NaN,
                    0d,
                    out _,
                    out PhysicalVisualGeometryFailureReason invalidScaleReason));
            AssertEqual(
                "nonfinite render scale reason",
                PhysicalVisualGeometryFailureReason.ScaleInvalid,
                invalidScaleReason);
        }

        private static void ValidatePhysicalVisualLifecycle()
        {
            AssertTrue(
                "valid physical visual policy accepted",
                PhysicalVisualPolicy.TryCreate(
                    128,
                    512,
                    200d,
                    1d,
                    0d,
                    45d,
                    256,
                    out PhysicalVisualPolicy? policy,
                    out PhysicalVisualPolicyFailureReason policyReason));
            PhysicalVisualPolicy visualPolicy = RequireValue("physical visual policy", policy);
            AssertEqual(
                "valid physical visual policy reason",
                PhysicalVisualPolicyFailureReason.None,
                policyReason);
            AssertEqual(
                "valid physical command budget",
                256,
                visualPolicy.MaximumCommandsProcessedPerFrame);
            AssertTrue(
                "component at culling boundary remains eligible",
                visualPolicy.IsWithinCullingDistance(40000d));
            AssertTrue(
                "component beyond culling boundary is rejected",
                !visualPolicy.IsWithinCullingDistance(40000.0001d));
            AssertTrue(
                "nonfinite culling distance is rejected",
                !visualPolicy.IsWithinCullingDistance(double.NaN));
            AssertTrue(
                "tracked capacity cannot be below visible capacity",
                !PhysicalVisualPolicy.TryCreate(
                    128,
                    127,
                    200d,
                    1d,
                    0d,
                    45d,
                    256,
                    out _,
                    out PhysicalVisualPolicyFailureReason capacityReason));
            AssertEqual(
                "tracked capacity failure reason",
                PhysicalVisualPolicyFailureReason.TrackedCapacityInvalid,
                capacityReason);
            AssertTrue(
                "zero command processing budget rejected",
                !PhysicalVisualPolicy.TryCreate(
                    128,
                    512,
                    200d,
                    1d,
                    0d,
                    45d,
                    0,
                    out _,
                    out PhysicalVisualPolicyFailureReason commandBudgetReason));
            AssertEqual(
                "command processing budget failure reason",
                PhysicalVisualPolicyFailureReason.CommandProcessingBudgetInvalid,
                commandBudgetReason);

            var ledger = new PhysicalVisualOwnershipLedger(2);
            AssertTrue("first visual lease acquired", ledger.TryAcquire(101L, out PhysicalVisualLease first));
            AssertTrue("second visual lease acquired", ledger.TryAcquire(202L, out PhysicalVisualLease second));
            AssertEqual("visual ledger active count full", 2, ledger.ActiveCount);
            AssertTrue("first visual lease current", ledger.IsCurrent(first));
            AssertTrue("second visual lease current", ledger.IsCurrent(second));
            AssertTrue("visual ledger capacity enforced", !ledger.TryAcquire(303L, out _));
            AssertTrue("first visual lease released", ledger.Release(first));
            AssertTrue("released visual lease stale", !ledger.IsCurrent(first));
            AssertTrue("released slot reused", ledger.TryAcquire(303L, out PhysicalVisualLease reused));
            AssertEqual("reused visual slot", first.Slot, reused.Slot);
            AssertTrue("reused visual generation changed", first.Generation != reused.Generation);
            AssertTrue("stale release cannot evict new owner", !ledger.Release(first));
            AssertTrue("new visual lease survives stale release", ledger.IsCurrent(reused));
            AssertTrue("wrong owner cannot release slot", !ledger.Release(
                new PhysicalVisualLease(reused.Slot, reused.Generation, 404L)));
            AssertTrue("exact reused lease released", ledger.Release(reused));
            ledger.Reset();
            AssertEqual("visual ledger reset active count", 0, ledger.ActiveCount);
            AssertTrue("reset invalidates previous second lease", !ledger.IsCurrent(second));

            var limitedLedger = new PhysicalVisualOwnershipLedger(4);
            AssertTrue(
                "capacity-limited visual lease uses first slot",
                limitedLedger.TryAcquire(501L, 1, out PhysicalVisualLease limited));
            AssertEqual("capacity-limited visual slot", 0, limited.Slot);
            AssertTrue(
                "capacity-limited visual lease refuses an out-of-range free slot",
                !limitedLedger.TryAcquire(502L, 1, out _));
            AssertTrue("capacity-limited visual lease released", limitedLedger.Release(limited));
            AssertTrue(
                "expanded visual capacity reuses a valid low slot",
                limitedLedger.TryAcquire(503L, 3, out PhysicalVisualLease expanded));
            AssertTrue("expanded visual slot stays below limit", expanded.Slot < 3);
        }

        private static void ValidatePhysicalVisualCommandBudget()
        {
            var commands = new PhysicalVisualCommandBuffer<string>(4);
            AssertEqual("visual command capacity", 4, commands.Capacity);
            AssertTrue("register A queued", commands.Enqueue("register-a"));
            AssertTrue("retire A queued", commands.Enqueue("retire-a"));
            AssertTrue("register B queued", commands.Enqueue("register-b"));

            var batch = new List<string> { "stale" };
            AssertEqual("first one-command frame", 1, commands.DrainTo(batch, 1));
            AssertEqual("first command remains FIFO", "register-a", batch[0]);
            AssertEqual("two commands remain pending", 2, commands.Count);
            AssertEqual("second one-command frame", 1, commands.DrainTo(batch, 1));
            AssertEqual("retire follows its registration", "retire-a", batch[0]);
            AssertEqual("final command frame", 1, commands.DrainTo(batch, 4));
            AssertEqual("later registration remains last", "register-b", batch[0]);
            AssertEqual("command queue drained", 0, commands.Count);

            AssertTrue("overflow command one queued", commands.Enqueue("one"));
            AssertTrue("overflow command two queued", commands.Enqueue("two"));
            AssertTrue("overflow command three queued", commands.Enqueue("three"));
            AssertTrue("overflow command four queued", commands.Enqueue("four"));
            AssertTrue("overflow reports oldest eviction", !commands.Enqueue("five"));
            AssertEqual("bounded queue count", 4, commands.Count);
            AssertEqual("bounded drain count", 4, commands.DrainTo(batch, 4));
            AssertEqual("oldest retained command", "two", batch[0]);
            AssertEqual("newest retained command", "five", batch[3]);

            commands.Enqueue("clear-me");
            commands.Clear();
            AssertEqual("command queue clear", 0, commands.Count);
        }

        private static void ValidatePhysicalVisualCapacityStress()
        {
            const int QueueCapacity = 8192;
            const int EnqueuedCommandCount = 10000;
            const int CommandsPerFrame = 256;
            var commands = new PhysicalVisualCommandBuffer<int>(QueueCapacity);
            int evictionCount = 0;
            for (int command = 0; command < EnqueuedCommandCount; command++)
            {
                if (!commands.Enqueue(command))
                {
                    evictionCount++;
                }
            }

            AssertEqual(
                "full command queue retains exact capacity",
                QueueCapacity,
                commands.Count);
            AssertEqual(
                "full command queue reports every eviction",
                EnqueuedCommandCount - QueueCapacity,
                evictionCount);
            var commandBatch = new List<int>();
            int expectedCommand = EnqueuedCommandCount - QueueCapacity;
            int frameCount = 0;
            while (commands.Count > 0)
            {
                int drained = commands.DrainTo(commandBatch, CommandsPerFrame);
                AssertTrue(
                    "stress frame respects command budget " + frameCount,
                    drained > 0 && drained <= CommandsPerFrame);
                for (int index = 0; index < drained; index++)
                {
                    AssertEqual(
                        "stress command FIFO sequence " + expectedCommand,
                        expectedCommand,
                        commandBatch[index]);
                    expectedCommand++;
                }

                frameCount++;
            }

            AssertEqual(
                "stress command sequence reaches newest item",
                EnqueuedCommandCount,
                expectedCommand);
            AssertEqual(
                "stress queue drains in exact frame count",
                QueueCapacity / CommandsPerFrame,
                frameCount);

            int capacity = PhysicalVisualPolicy.MaximumVisibleCapacity;
            var ledger = new PhysicalVisualOwnershipLedger(capacity);
            var firstGeneration = new PhysicalVisualLease[capacity];
            for (int index = 0; index < capacity; index++)
            {
                AssertTrue(
                    "stress initial lease acquired " + index,
                    ledger.TryAcquire(index + 1L, out firstGeneration[index]));
                AssertEqual("stress initial slot order " + index, index, firstGeneration[index].Slot);
            }

            AssertEqual("stress ledger reaches capacity", capacity, ledger.ActiveCount);
            AssertTrue("stress ledger rejects over-capacity owner", !ledger.TryAcquire(9000L, out _));

            for (int index = 0; index < capacity; index += 2)
            {
                AssertTrue(
                    "stress even lease released " + index,
                    ledger.Release(firstGeneration[index]));
            }

            AssertEqual("stress ledger half occupied", capacity / 2, ledger.ActiveCount);
            var replacementGeneration = new List<PhysicalVisualLease>(capacity / 2);
            for (int index = 0; index < capacity / 2; index++)
            {
                AssertTrue(
                    "stress replacement lease acquired " + index,
                    ledger.TryAcquire(10000L + index, out PhysicalVisualLease replacement));
                AssertEqual("stress replacement reuses even slot " + index, index * 2, replacement.Slot);
                AssertTrue(
                    "stress replacement changes generation " + index,
                    replacement.Generation != firstGeneration[index * 2].Generation);
                AssertTrue(
                    "stress stale release cannot evict replacement " + index,
                    !ledger.Release(firstGeneration[index * 2]));
                replacementGeneration.Add(replacement);
            }

            for (int index = 1; index < capacity; index += 2)
            {
                AssertTrue(
                    "stress original odd lease released " + index,
                    ledger.Release(firstGeneration[index]));
            }
            foreach (PhysicalVisualLease replacement in replacementGeneration)
            {
                AssertTrue("stress replacement lease released", ledger.Release(replacement));
            }

            AssertEqual("stress ledger fully released", 0, ledger.ActiveCount);
            ledger.Reset();
            AssertEqual("stress ledger reset remains empty", 0, ledger.ActiveCount);
        }

        private static void AddDirectedMeshEdge(
            Dictionary<(int Minimum, int Maximum), int> counts,
            Dictionary<(int Minimum, int Maximum), int> directions,
            int from,
            int to)
        {
            var edge = from < to ? (from, to) : (to, from);
            counts.TryGetValue(edge, out int count);
            counts[edge] = count + 1;
            directions.TryGetValue(edge, out int direction);
            directions[edge] = direction + (from < to ? 1 : -1);
        }

        private static void ValidatePhysicalRendererIsolation()
        {
            string[] forbiddenReferencePrefixes =
            {
                "UnityEngine",
                "BepInEx",
                "0Harmony",
                "Assembly-CSharp",
                "spt-reflection"
            };
            System.Reflection.AssemblyName[] references =
                typeof(PhysicalProjectileVisualGeometry).Assembly.GetReferencedAssemblies();
            for (int referenceIndex = 0; referenceIndex < references.Length; referenceIndex++)
            {
                string referenceName = references[referenceIndex].Name ?? string.Empty;
                for (int prefixIndex = 0;
                     prefixIndex < forbiddenReferencePrefixes.Length;
                     prefixIndex++)
                {
                    AssertTrue(
                        "renderer core reference remains isolated from "
                            + forbiddenReferencePrefixes[prefixIndex],
                        !referenceName.StartsWith(
                            forbiddenReferencePrefixes[prefixIndex],
                            StringComparison.OrdinalIgnoreCase));
                }
            }
        }

        private static void ValidatePhysicalConservation()
        {
            PhysicalProjectileState parent = CreatePhysicalStateOrThrow(
                CreateValidRootInput(1000d, 0.01d, 0.01d));
            var outputs = new[]
            {
                CreateChildState(parent, PhysicalProjectileKind.DeformedProjectile, "core-1", 0, 0.006d, 700d),
                CreateChildState(parent, PhysicalProjectileKind.ProjectileFragment, "fragment-1", 1, 0.002d, 500d),
                CreateChildState(parent, PhysicalProjectileKind.ProjectileFragment, "fragment-2", 2, 0.001d, 400d),
                CreateChildState(parent, PhysicalProjectileKind.TargetSpall, "spall-1", 3, 0.003d, 300d)
            };
            var losses = new PhysicalLossBudget(600d, 150d, 100d, 100d, 50d);

            PhysicalConservationResult result;
            PhysicalConservationFailureReason reason;
            AssertTrue(
                "conserved fragmentation transition accepted",
                PhysicalProjectileConservation.TryValidateFragmentationTransition(
                    parent,
                    outputs,
                    losses,
                    out result,
                    out reason));
            AssertEqual("conserved fragmentation reason", PhysicalConservationFailureReason.None, reason);
            AssertNear("available parent mass", 0.01d, result.AvailableParentMassKilograms);
            AssertNear("allocated parent mass excludes new spall", 0.009d, result.AllocatedParentMassKilograms);
            AssertNear("retained parent mass excludes new spall", 0.009d, result.RetainedParentMassKilograms);
            AssertNear("target spall mass remains separate", 0.003d, result.TargetSpallMassKilograms);
            AssertNear("modeled energy losses", 1000d, result.ModeledLossEnergyJoules);
            AssertNear("residual child energy budget", 4000d, result.ResidualEnergyJoules);
            AssertNear("summed child kinetic energy", 1935d, result.ChildEnergyJoules);
            AssertNear("unallocated immediate-parent mass", 0.001d, result.UnallocatedParentMassKilograms);
            AssertNear("unallocated residual energy", 2065d, result.UnallocatedResidualEnergyJoules);
            AssertEqual("parent-derived output count", 3, result.ParentDerivedOutputCount);
            AssertEqual("target spall output count", 1, result.TargetSpallOutputCount);

            var massViolation = new List<PhysicalProjectileState>(outputs)
            {
                CreateChildState(parent, PhysicalProjectileKind.ProjectileFragment, "fragment-over-mass", 4, 0.002d, 100d)
            };
            AssertTrue(
                "projectile mass over-allocation rejected",
                !PhysicalProjectileConservation.TryValidateFragmentationTransition(
                    parent,
                    massViolation,
                    losses,
                    out result,
                    out reason));
            AssertEqual(
                "projectile mass over-allocation reason",
                PhysicalConservationFailureReason.ParentDerivedMassExceedsParent,
                reason);

            var energyViolation = new[]
            {
                CreateChildState(parent, PhysicalProjectileKind.ProjectileFragment, "fragment-over-energy", 0, 0.009d, 1000d)
            };
            AssertTrue(
                "child energy over-allocation rejected",
                !PhysicalProjectileConservation.TryValidateFragmentationTransition(
                    parent,
                    energyViolation,
                    losses,
                    out result,
                    out reason));
            AssertEqual(
                "child energy over-allocation reason",
                PhysicalConservationFailureReason.ChildEnergyExceedsResidual,
                reason);

            var largeSpallMass = new[]
            {
                CreateChildState(parent, PhysicalProjectileKind.ProjectileFragment, "fragment-valid", 0, 0.009d, 100d),
                CreateChildState(parent, PhysicalProjectileKind.TargetSpall, "spall-heavy", 1, 0.05d, 100d)
            };
            AssertTrue(
                "target spall mass does not consume projectile mass",
                PhysicalProjectileConservation.TryValidateFragmentationTransition(
                    parent,
                    largeSpallMass,
                    losses,
                    out result,
                    out reason));
            AssertNear("large target spall mass reported separately", 0.05d, result.TargetSpallMassKilograms);
            AssertNear("large target spall leaves parent allocation unchanged", 0.009d, result.AllocatedParentMassKilograms);

            var noProjectileFragment = new[]
            {
                CreateChildState(parent, PhysicalProjectileKind.DeformedProjectile, "core-only", 0, 0.009d, 100d),
                CreateChildState(parent, PhysicalProjectileKind.TargetSpall, "spall-only", 1, 0.002d, 100d)
            };
            AssertTrue(
                "fragmentation state without a projectile fragment rejected",
                !PhysicalProjectileConservation.TryValidateFragmentationTransition(
                    parent,
                    noProjectileFragment,
                    losses,
                    out result,
                    out reason));
            AssertEqual(
                "missing physical fragment reason",
                PhysicalConservationFailureReason.ParentFragmentMissing,
                reason);

            PhysicalProjectileStateInput mismatchedCollisionInput = CreateChildInput(
                parent,
                PhysicalProjectileKind.ProjectileFragment,
                "fragment-other-collision",
                4,
                0.0005d,
                100d);
            mismatchedCollisionInput.SourceCollisionId = "collision-other";
            var mismatchedCollisionOutputs = new[]
            {
                outputs[1],
                CreatePhysicalStateOrThrow(mismatchedCollisionInput)
            };
            AssertTrue(
                "mixed collision outputs rejected",
                !PhysicalProjectileConservation.TryValidateFragmentationTransition(
                    parent,
                    mismatchedCollisionOutputs,
                    losses,
                    out result,
                    out reason));
            AssertEqual(
                "mixed collision output reason",
                PhysicalConservationFailureReason.SourceCollisionMismatch,
                reason);

            var invalidLosses = new PhysicalLossBudget(0d, -1d, 0d, 0d, 0d);
            AssertTrue(
                "invalid loss budget fails open",
                !PhysicalProjectileConservation.TryValidateTransition(
                    parent,
                    outputs,
                    invalidLosses,
                    out result,
                    out reason));
            AssertEqual(
                "invalid loss budget reason",
                PhysicalConservationFailureReason.LossBudgetInvalid,
                reason);
        }

        private static void ValidatePhysicalTelemetry()
        {
            AssertEqual("physical telemetry schema", 2, PhysicalProjectileTelemetry.SchemaVersion);
            AssertTrue("physical telemetry begins without subscribers", !PhysicalProjectileTelemetry.HasSubscribers);

            PhysicalProjectileState parent = CreatePhysicalStateOrThrow(
                CreateValidRootInput(800d, 0.01d, 0.0095d));
            PhysicalProjectileState projectileOutput = CreateChildState(
                parent,
                PhysicalProjectileKind.ProjectileFragment,
                "telemetry-fragment",
                0,
                0.006d,
                500d);
            PhysicalProjectileState spallOutput = CreateChildState(
                parent,
                PhysicalProjectileKind.TargetSpall,
                "telemetry-spall",
                1,
                0.001d,
                200d);
            var mutableOutputs = new List<PhysicalProjectileState>
            {
                projectileOutput,
                spallOutput
            };
            var host = new PhysicalTelemetryHostIdentity(
                41,
                42,
                43,
                44,
                1,
                2,
                "profile",
                "ammo-template",
                "test ammunition");
            var impact = new PhysicalTelemetryImpact(
                new PhysicalVector3(1d, 2d, 3d),
                new PhysicalVector3(0d, 0d, -1d),
                0.01d,
                0.02d,
                "armored-steel",
                PhysicalMaterialClass.ArmoredSteel,
                "fixture/7/plate/0",
                7850d,
                900000000d,
                0.8d,
                0.9d,
                0.1d);
            var lossBudget = new PhysicalLossBudget(2000d, 200d, 50d, 20d, 0d);

            AssertTrue(
                "resolved physical telemetry created",
                PhysicalProjectileTelemetryFactory.TryCreateResolved(
                    "transition-1",
                    PhysicalCollisionOutcome.Fragmented,
                    host,
                    impact,
                    parent,
                    mutableOutputs,
                    lossBudget,
                    out PhysicalProjectileTelemetryEvent? resolved)
                && resolved != null);
            PhysicalProjectileTelemetryEvent value = RequireValue("resolved physical telemetry", resolved);
            mutableOutputs.Clear();
            AssertEqual("telemetry stage", PhysicalTelemetryStage.CollisionResolved, value.Stage);
            AssertEqual("telemetry outcome", PhysicalCollisionOutcome.Fragmented, value.Outcome);
            AssertEqual("telemetry transition id", "transition-1", value.TransitionId);
            AssertEqual("telemetry output copy remains immutable", 2, value.Outputs.Count);
            AssertEqual("telemetry parent retained by immutable identity", parent, value.Parent);
            AssertEqual("telemetry root fire index", 41, value.Host.RootFireIndex);
            AssertEqual("telemetry parent depth", 2, value.Host.ParentDepth);
            AssertEqual("telemetry target class", PhysicalMaterialClass.ArmoredSteel, value.Impact.TargetMaterialClass);
            AssertEqual(
                "telemetry target surface identity",
                "fixture/7/plate/0",
                value.Impact.TargetSurfaceIdentity);

            PhysicalTelemetryConservation conservation = RequireValue(
                "telemetry conservation",
                value.Conservation);
            AssertNear("telemetry parent mass", 0.0095d, conservation.ParentMassKilograms);
            AssertNear("telemetry allocated projectile mass", 0.006d, conservation.AllocatedParentMassKilograms);
            AssertNear("telemetry unallocated projectile mass", 0.0035d, conservation.UnallocatedParentMassKilograms);
            AssertNear("telemetry fresh target spall mass", 0.001d, conservation.TargetSpallMassKilograms);
            AssertNear("telemetry parent energy", 3040d, conservation.ParentEnergyJoules);
            AssertNear("telemetry modeled losses", 2270d, conservation.ModeledLossEnergyJoules);
            AssertNear("telemetry residual energy", 770d, conservation.ResidualEnergyJoules);
            AssertNear("telemetry output energy", 770d, conservation.OutputEnergyJoules);
            AssertNear("telemetry energy closure", 0d, conservation.EnergyClosureErrorJoules);
            AssertEqual("telemetry parent-derived count", 1, conservation.ParentDerivedOutputCount);
            AssertEqual("telemetry target-spall count", 1, conservation.TargetSpallOutputCount);

            PhysicalProjectileTelemetryEvent prepared = PhysicalProjectileTelemetryFactory.CreatePrepared(
                "transition-1",
                host,
                impact,
                parent);
            AssertEqual("prepared telemetry stage", PhysicalTelemetryStage.CollisionPrepared, prepared.Stage);
            AssertEqual("prepared telemetry output count", 0, prepared.Outputs.Count);
            AssertTrue("prepared telemetry has no conservation", prepared.Conservation == null);

            int deliveredCount = 0;
            object? deliveredValue = null;
            Action<object> failingObserver = delegate { throw new InvalidOperationException("observer failure"); };
            Action<object> recordingObserver = observed =>
            {
                deliveredCount++;
                deliveredValue = observed;
            };
            PhysicalProjectileTelemetry.Subscribe(failingObserver);
            PhysicalProjectileTelemetry.Subscribe(recordingObserver);
            try
            {
                AssertTrue("physical telemetry reports active subscribers", PhysicalProjectileTelemetry.HasSubscribers);
                PhysicalProjectileTelemetry.Publish(value);
            }
            finally
            {
                PhysicalProjectileTelemetry.Unsubscribe(failingObserver);
                PhysicalProjectileTelemetry.Unsubscribe(recordingObserver);
            }

            AssertEqual("later observer survives earlier failure", 1, deliveredCount);
            AssertTrue("observer receives exact immutable event", ReferenceEquals(value, deliveredValue));
            AssertTrue("physical telemetry removes all subscribers", !PhysicalProjectileTelemetry.HasSubscribers);

            var invalidLossBudget = new PhysicalLossBudget(double.NaN, 0d, 0d, 0d, 0d);
            AssertTrue(
                "invalid physical telemetry is rejected without a partial event",
                !PhysicalProjectileTelemetryFactory.TryCreateResolved(
                    "transition-invalid",
                    PhysicalCollisionOutcome.Fragmented,
                    host,
                    impact,
                    parent,
                    new[] { projectileOutput },
                    invalidLossBudget,
                    out PhysicalProjectileTelemetryEvent? invalid)
                && invalid == null);

            PhysicalProjectileState excessiveMass = CreateChildState(
                parent,
                PhysicalProjectileKind.ProjectileFragment,
                "telemetry-over-mass",
                2,
                0.02d,
                1d);
            AssertTrue(
                "telemetry rejects parent-derived mass above the incoming component",
                !PhysicalProjectileTelemetryFactory.TryCreateResolved(
                    "transition-over-mass",
                    PhysicalCollisionOutcome.Fragmented,
                    host,
                    impact,
                    parent,
                    new[] { excessiveMass },
                    new PhysicalLossBudget(0d, 0d, 0d, 0d, 0d),
                    out invalid)
                && invalid == null);

            PhysicalProjectileState excessiveEnergy = CreateChildState(
                parent,
                PhysicalProjectileKind.ProjectileFragment,
                "telemetry-over-energy",
                3,
                0.009d,
                1000d);
            AssertTrue(
                "telemetry rejects output energy above the residual budget",
                !PhysicalProjectileTelemetryFactory.TryCreateResolved(
                    "transition-over-energy",
                    PhysicalCollisionOutcome.Fragmented,
                    host,
                    impact,
                    parent,
                    new[] { excessiveEnergy },
                    new PhysicalLossBudget(0d, 0d, 0d, 0d, 0d),
                    out invalid)
                && invalid == null);

            AssertTrue(
                "telemetry rejects losses above parent energy",
                !PhysicalProjectileTelemetryFactory.TryCreateResolved(
                    "transition-over-loss",
                    PhysicalCollisionOutcome.Stopped,
                    host,
                    impact,
                    parent,
                    new[] { projectileOutput },
                    new PhysicalLossBudget(4000d, 0d, 0d, 0d, 0d),
                    out invalid)
                && invalid == null);
        }

        private static void ValidateDeterministicProjectileRandom()
        {
            uint[] expectedSequence =
            {
                0xA15C02B7u,
                0x7B47F409u,
                0xBA1D3330u,
                0x83D2F293u,
                0xBFA4784Bu,
                0xCBED606Eu
            };
            var known = new DeterministicProjectileRandom(42UL, 54UL);
            for (int index = 0; index < expectedSequence.Length; index++)
            {
                AssertEqual(
                    "stable PCG output " + index,
                    expectedSequence[index],
                    known.NextUInt32());
            }

            var first = new DeterministicProjectileRandom(0x123456789ABCDEF0UL, 7UL);
            var second = new DeterministicProjectileRandom(0x123456789ABCDEF0UL, 7UL);
            for (int index = 0; index < 64; index++)
            {
                AssertEqual(
                    "same projectile seed and stream remain deterministic " + index,
                    first.NextUInt32(),
                    second.NextUInt32());
            }

            first = new DeterministicProjectileRandom(99UL, 13UL);
            second = new DeterministicProjectileRandom(99UL, 13UL);
            for (int index = 0; index < 32; index++)
            {
                double firstValue = first.NextUnitDouble();
                double secondValue = second.NextUnitDouble();
                AssertNear("deterministic unit double " + index, firstValue, secondValue);
                AssertTrue("unit double lower bound " + index, firstValue >= 0d);
                AssertTrue("unit double upper bound " + index, firstValue < 1d);
            }

            var differentSeed = new DeterministicProjectileRandom(100UL, 13UL);
            var referenceSeed = new DeterministicProjectileRandom(99UL, 13UL);
            AssertTrue(
                "different projectile seed changes stream",
                differentSeed.NextUInt32() != referenceSeed.NextUInt32());
        }

        private static void ValidatePhysicalHostRandomSeed()
        {
            const int hostRandomCount = 512;
            AssertEqual("host seed zero", 0, PhysicalHostRandomSeed.Map(0UL, hostRandomCount));
            AssertEqual("host seed upper bound", 511, PhysicalHostRandomSeed.Map(511UL, hostRandomCount));
            AssertEqual("host seed wraps at capacity", 0, PhysicalHostRandomSeed.Map(512UL, hostRandomCount));
            AssertEqual(
                "maximum component seed remains bounded",
                511,
                PhysicalHostRandomSeed.Map(ulong.MaxValue, hostRandomCount));

            ulong componentSeed = 0xF1E2D3C4B5A69788UL;
            int first = PhysicalHostRandomSeed.Map(componentSeed, hostRandomCount);
            int second = PhysicalHostRandomSeed.Map(componentSeed, hostRandomCount);
            AssertEqual("host seed mapping remains deterministic", first, second);
            AssertTrue("host seed lower bound", first >= 0);
            AssertTrue("host seed strict upper bound", first < hostRandomCount);

            bool rejectedZeroCapacity = false;
            try
            {
                _ = PhysicalHostRandomSeed.Map(componentSeed, 0);
            }
            catch (ArgumentOutOfRangeException)
            {
                rejectedZeroCapacity = true;
            }

            AssertTrue("zero host random capacity rejected", rejectedZeroCapacity);
        }

        private static void ValidatePhysicalMaterialProfiles()
        {
            PhysicalProjectileMaterialProfile? projectileProfile;
            PhysicalMaterialProfileFailureReason reason;
            PhysicalProjectileMaterialProfileInput projectileInput =
                CreateTestProjectileProfileInput();
            AssertTrue(
                "valid projectile material profile accepted",
                PhysicalProjectileMaterialProfile.TryCreate(
                    projectileInput,
                    out projectileProfile,
                    out reason));
            PhysicalProjectileMaterialProfile validProjectileProfile = RequireValue(
                "valid projectile material profile",
                projectileProfile);
            AssertEqual(
                "valid projectile material profile reason",
                PhysicalMaterialProfileFailureReason.None,
                reason);
            AssertEqual(
                "projectile material construction preserved",
                PhysicalProjectileConstruction.SteelCoreJacketed,
                validProjectileProfile.Construction);
            AssertNear(
                "projectile material density preserved",
                8000d,
                validProjectileProfile.DensityKilogramsPerCubicMetre);

            projectileInput = CreateTestProjectileProfileInput();
            projectileInput.DensityKilogramsPerCubicMetre = double.NaN;
            AssertTrue(
                "nonfinite projectile density rejected",
                !PhysicalProjectileMaterialProfile.TryCreate(
                    projectileInput,
                    out projectileProfile,
                    out reason));
            AssertTrue("failed projectile profile returns null", projectileProfile == null);
            AssertEqual(
                "nonfinite projectile density reason",
                PhysicalMaterialProfileFailureReason.DensityInvalid,
                reason);

            PhysicalTargetMaterialProfile? targetProfile;
            PhysicalTargetMaterialProfileInput targetInput = CreateTestTargetProfileInput(
                50000000d,
                0.5d);
            AssertTrue(
                "valid target material profile accepted",
                PhysicalTargetMaterialProfile.TryCreate(
                    targetInput,
                    out targetProfile,
                    out reason));
            PhysicalTargetMaterialProfile validTargetProfile = RequireValue(
                "valid target material profile",
                targetProfile);
            AssertEqual(
                "valid target material profile reason",
                PhysicalMaterialProfileFailureReason.None,
                reason);
            AssertEqual(
                "target material class preserved",
                PhysicalMaterialClass.ArmoredSteel,
                validTargetProfile.MaterialClass);

            targetInput = CreateTestTargetProfileInput(50000000d, 0.5d);
            targetInput.HeatLossFraction = 1.01d;
            AssertTrue(
                "target heat fraction above one rejected",
                !PhysicalTargetMaterialProfile.TryCreate(
                    targetInput,
                    out targetProfile,
                    out reason));
            AssertEqual(
                "invalid target heat fraction reason",
                PhysicalMaterialProfileFailureReason.HeatFractionInvalid,
                reason);
        }

        private static void ValidatePhysicalProjectileDesignResponse()
        {
            AssertTrue(
                "hollow point expansion response exceeds full metal jacket",
                PhysicalProjectileDesignResponse.GetExpansionResponse(
                    PhysicalProjectileDesignClass.HollowPoint)
                > PhysicalProjectileDesignResponse.GetExpansionResponse(
                    PhysicalProjectileDesignClass.FullMetalJacket));
            AssertTrue(
                "full metal jacket expansion response exceeds sabot penetrator",
                PhysicalProjectileDesignResponse.GetExpansionResponse(
                    PhysicalProjectileDesignClass.FullMetalJacket)
                > PhysicalProjectileDesignResponse.GetExpansionResponse(
                    PhysicalProjectileDesignClass.SabotedPenetrator));
            AssertTrue(
                "frangible fracture response exceeds full metal jacket",
                PhysicalProjectileDesignResponse.GetFractureResponse(
                    PhysicalProjectileDesignClass.Frangible)
                > PhysicalProjectileDesignResponse.GetFractureResponse(
                    PhysicalProjectileDesignClass.FullMetalJacket));
            AssertTrue(
                "fragment drag response exceeds polymer tip",
                PhysicalProjectileDesignResponse.GetInitialDragMultiplier(
                    PhysicalProjectileDesignClass.Fragment)
                > PhysicalProjectileDesignResponse.GetInitialDragMultiplier(
                    PhysicalProjectileDesignClass.PolymerTipped));
            AssertTrue(
                "payload design has no kinetic expansion response",
                double.IsNaN(
                    PhysicalProjectileDesignResponse.GetExpansionResponse(
                        PhysicalProjectileDesignClass.Payload)));

            PhysicalProjectileStateInput hollowPointInput =
                CreateValidRootInput(1000d, 0.01d, 0.01d);
            hollowPointInput.DesignClass = PhysicalProjectileDesignClass.HollowPoint;
            PhysicalProjectileStateInput fullMetalJacketInput =
                CreateValidRootInput(1000d, 0.01d, 0.01d);
            fullMetalJacketInput.DesignClass = PhysicalProjectileDesignClass.FullMetalJacket;
            PhysicalProjectileStateInput sabotInput =
                CreateValidRootInput(1000d, 0.01d, 0.01d);
            sabotInput.DesignClass = PhysicalProjectileDesignClass.SabotedPenetrator;

            PhysicalDeformationResponse hollowPoint = SolveDeformationOrThrow(
                CreateValidDeformationInput(
                    CreatePhysicalStateOrThrow(hollowPointInput),
                    PhysicalCollisionOutcome.Penetrated,
                    0.01d,
                    0.01d,
                    50000000d,
                    0.5d,
                    "collision-design-hollow-point",
                    "root-projectile"));
            PhysicalDeformationResponse fullMetalJacket = SolveDeformationOrThrow(
                CreateValidDeformationInput(
                    CreatePhysicalStateOrThrow(fullMetalJacketInput),
                    PhysicalCollisionOutcome.Penetrated,
                    0.01d,
                    0.01d,
                    50000000d,
                    0.5d,
                    "collision-design-full-metal-jacket",
                    "root-projectile"));
            PhysicalDeformationResponse sabot = SolveDeformationOrThrow(
                CreateValidDeformationInput(
                    CreatePhysicalStateOrThrow(sabotInput),
                    PhysicalCollisionOutcome.Penetrated,
                    0.01d,
                    0.01d,
                    50000000d,
                    0.5d,
                    "collision-design-sabot",
                    "root-projectile"));

            AssertTrue(
                "identical material and impact expand hollow point more than full metal jacket",
                hollowPoint.DiameterExpansionRatio > fullMetalJacket.DiameterExpansionRatio);
            AssertTrue(
                "identical material and impact expand full metal jacket more than sabot penetrator",
                fullMetalJacket.DiameterExpansionRatio > sabot.DiameterExpansionRatio);
        }

        private static void ValidatePhysicalDeformationResponse()
        {
            PhysicalProjectileState parent = CreatePhysicalStateOrThrow(
                CreateValidRootInput(1000d, 0.01d, 0.01d));
            PhysicalDeformationInput input = CreateValidDeformationInput(
                parent,
                PhysicalCollisionOutcome.Penetrated,
                0.01d,
                0.01d,
                50000000d,
                0.5d,
                "collision-deformation-normal",
                parent.ProjectileId);
            PhysicalDeformationResponse response = SolveDeformationOrThrow(input);
            PhysicalProjectileState primaryState = RequireValue(
                "normal-impact primary state",
                response.PrimaryState);

            double expectedTargetWork = parent.ProjectedAreaSquareMetres * 0.01d * 50000000d;
            double expectedDeformationWork = 125d;
            double expectedResidualEnergy = parent.TranslationalKineticEnergyJoules
                - expectedTargetWork
                - expectedDeformationWork;
            AssertNear("normal impact angle", 0d, response.ImpactAngleRadians);
            AssertNear(
                "normal impact energy",
                parent.TranslationalKineticEnergyJoules,
                response.NormalImpactEnergyJoules);
            AssertNear("normal target resistance work", expectedTargetWork, response.RawTargetResistanceWorkJoules);
            AssertNear("normal allocated target work", expectedTargetWork, response.AllocatedTargetWorkJoules);
            AssertNear("normal deformation capacity", 125d, response.DeformationCapacityJoules);
            AssertNear("normal deformation severity", 1d, response.DeformationSeverity);
            AssertNear("normal diameter expansion", 1.22d, response.DiameterExpansionRatio);
            AssertNear("normal heat loss", expectedTargetWork * 0.2d, response.LossBudget.HeatLossJoules);
            AssertNear("normal penetration loss", expectedTargetWork * 0.8d, response.LossBudget.PenetrationLossJoules);
            AssertNear("normal deformation loss", expectedDeformationWork, response.LossBudget.DeformationLossJoules);
            AssertNear("normal fracture loss remains zero", 0d, response.LossBudget.FractureLossJoules);
            AssertNear("normal residual energy", expectedResidualEnergy, response.ResidualSystemEnergyJoules);
            AssertNear(
                "loss and residual energy close exactly",
                parent.TranslationalKineticEnergyJoules,
                response.LossBudget.TotalLossJoules + response.ResidualSystemEnergyJoules);
            AssertNear("normal fragment mass remains zero", 0d, response.AvailableFragmentMassKilograms);
            AssertNear("normal fragment energy remains zero", 0d, response.AvailableFragmentEnergyJoules);
            AssertEqual(
                "normal impact produces deformed projectile",
                PhysicalProjectileKind.DeformedProjectile,
                primaryState.Kind);
            AssertEqual(
                "normal impact output identity",
                parent.ProjectileId,
                primaryState.ProjectileId);
            AssertEqual(
                "normal impact preserves fragment generation",
                parent.FragmentGeneration,
                primaryState.FragmentGeneration);
            AssertEqual(
                "normal impact preserves fragment index",
                parent.FragmentIndex,
                primaryState.FragmentIndex);
            AssertNear(
                "primary state energy equals response allocation",
                response.PrimaryEnergyJoules,
                primaryState.TranslationalKineticEnergyJoules);
            AssertNear(
                "primary state mass remains conserved",
                parent.RetainedMassKilograms,
                primaryState.RetainedMassKilograms);
            AssertNear(
                "deformed physical attitude reproduces yaw",
                Math.Cos(primaryState.YawAngleRadians),
                RotateLocalForward(primaryState.Orientation).Dot(
                    RequireNormalized(
                        "deformed primary velocity",
                        primaryState.VelocityMetresPerSecond)));
            AssertEqual("collision history appended once", 1, primaryState.CollisionHistory.Count);
            AssertEqual(
                "observed penetration outcome preserved",
                PhysicalCollisionOutcome.Penetrated,
                response.CollisionRecord.Outcome);
            PhysicalVector3 expectedExitPosition = input.ImpactPositionMetres.Add(
                RequireNormalized("normal impact incoming direction", parent.VelocityMetresPerSecond)
                    .Scale(input.EffectivePathLengthMetres));
            AssertEqual(
                "penetrated response starts at measured far face",
                expectedExitPosition,
                response.OutputPositionMetres);
            AssertEqual(
                "penetrated primary starts at measured far face",
                expectedExitPosition,
                primaryState.PositionMetres);

            PhysicalConservationResult conservationResult;
            PhysicalConservationFailureReason conservationReason;
            AssertTrue(
                "deformation state revision conservation accepted",
                PhysicalProjectileConservation.TryValidateDeformationResponse(
                    parent,
                    primaryState,
                    response.CollisionRecord,
                    0d,
                    0d,
                    response.LossBudget,
                    out conservationResult,
                    out conservationReason));
            AssertEqual(
                "deformation state revision conservation reason",
                PhysicalConservationFailureReason.None,
                conservationReason);

            PhysicalProjectileStateInput alteredSeedInput = CopyPhysicalStateToInput(
                primaryState);
            alteredSeedInput.DeterministicSeed++;
            PhysicalProjectileState alteredSeedState = CreatePhysicalStateOrThrow(
                alteredSeedInput);
            AssertTrue(
                "state revision seed change rejected",
                !PhysicalProjectileConservation.TryValidateDeformationResponse(
                    parent,
                    alteredSeedState,
                    response.CollisionRecord,
                    0d,
                    0d,
                    response.LossBudget,
                    out conservationResult,
                    out conservationReason));
            AssertEqual(
                "state revision seed change reason",
                PhysicalConservationFailureReason.StateRevisionLineageMismatch,
                conservationReason);

            PhysicalProjectileStateInput alteredNominalDiameterInput = CopyPhysicalStateToInput(
                primaryState);
            alteredNominalDiameterInput.NominalDiameterMetres *= 1.01d;
            PhysicalProjectileState alteredNominalDiameterState = CreatePhysicalStateOrThrow(
                alteredNominalDiameterInput);
            AssertTrue(
                "state revision nominal diameter change rejected",
                !PhysicalProjectileConservation.TryValidateDeformationResponse(
                    parent,
                    alteredNominalDiameterState,
                    response.CollisionRecord,
                    0d,
                    0d,
                    response.LossBudget,
                    out conservationResult,
                    out conservationReason));
            AssertEqual(
                "state revision nominal diameter reason",
                PhysicalConservationFailureReason.StateRevisionNominalGeometryMismatch,
                conservationReason);

            PhysicalCollisionRecordInput alteredIncomingCollisionInput = CopyCollisionToInput(
                response.CollisionRecord);
            alteredIncomingCollisionInput.IncomingVelocityMetresPerSecond =
                new PhysicalVector3(0d, 0d, 999d);
            PhysicalCollisionRecord alteredIncomingCollision = CreateCollisionRecordOrThrow(
                alteredIncomingCollisionInput);
            PhysicalProjectileStateInput alteredIncomingStateInput = CopyPhysicalStateToInput(
                primaryState);
            alteredIncomingStateInput.CollisionHistory = new[] { alteredIncomingCollision };
            PhysicalProjectileState alteredIncomingState = CreatePhysicalStateOrThrow(
                alteredIncomingStateInput);
            AssertTrue(
                "state revision collision input change rejected",
                !PhysicalProjectileConservation.TryValidateDeformationResponse(
                    parent,
                    alteredIncomingState,
                    response.CollisionRecord,
                    0d,
                    0d,
                    response.LossBudget,
                    out conservationResult,
                    out conservationReason));
            AssertEqual(
                "state revision collision input reason",
                PhysicalConservationFailureReason.StateRevisionCollisionMismatch,
                conservationReason);
            AssertTrue(
                "nonfragment mass reservation rejected",
                !PhysicalProjectileConservation.TryValidateDeformationResponse(
                    parent,
                    primaryState,
                    response.CollisionRecord,
                    0.001d,
                    0d,
                    response.LossBudget,
                    out conservationResult,
                    out conservationReason));
            AssertEqual(
                "nonfragment mass reservation reason",
                PhysicalConservationFailureReason.FragmentReservationOutcomeMismatch,
                conservationReason);
            AssertTrue(
                "nonfragment energy reservation rejected",
                !PhysicalProjectileConservation.TryValidateDeformationResponse(
                    parent,
                    primaryState,
                    response.CollisionRecord,
                    0d,
                    1d,
                    response.LossBudget,
                    out conservationResult,
                    out conservationReason));
            AssertEqual(
                "nonfragment energy reservation reason",
                PhysicalConservationFailureReason.FragmentReservationOutcomeMismatch,
                conservationReason);

            AssertTrue(
                "missing response collision rejected",
                !PhysicalProjectileConservation.TryValidateDeformationResponse(
                    parent,
                    primaryState,
                    null,
                    0d,
                    0d,
                    response.LossBudget,
                    out conservationResult,
                    out conservationReason));
            AssertEqual(
                "missing response collision reason",
                PhysicalConservationFailureReason.ResponseCollisionMissing,
                conservationReason);

            PhysicalProjectileStateInput wrongTerminalInput = CopyPhysicalStateToInput(
                primaryState);
            wrongTerminalInput.TerminalState = PhysicalProjectileTerminalState.Continuing;
            PhysicalProjectileState wrongTerminalState = CreatePhysicalStateOrThrow(
                wrongTerminalInput);
            AssertTrue(
                "state revision terminal mismatch rejected",
                !PhysicalProjectileConservation.TryValidateDeformationResponse(
                    parent,
                    wrongTerminalState,
                    response.CollisionRecord,
                    0d,
                    0d,
                    response.LossBudget,
                    out conservationResult,
                    out conservationReason));
            AssertEqual(
                "state revision terminal mismatch reason",
                PhysicalConservationFailureReason.StateRevisionTerminalStateMismatch,
                conservationReason);

            PhysicalDeformationInput longerPathInput = CreateValidDeformationInput(
                parent,
                PhysicalCollisionOutcome.Penetrated,
                0.01d,
                0.02d,
                50000000d,
                0.5d,
                "collision-deformation-longer",
                parent.ProjectileId);
            PhysicalDeformationResponse longerPath = SolveDeformationOrThrow(longerPathInput);
            AssertNear(
                "doubling material path doubles resistance work",
                expectedTargetWork * 2d,
                longerPath.RawTargetResistanceWorkJoules);
            AssertTrue(
                "longer material path leaves less energy",
                longerPath.ResidualSystemEnergyJoules < response.ResidualSystemEnergyJoules);

            PhysicalDeformationInput obliqueInput = CreateValidDeformationInput(
                parent,
                PhysicalCollisionOutcome.Deviated,
                0.01d,
                Math.Sqrt(2d) * 0.01d,
                50000000d,
                0.5d,
                "collision-deformation-oblique",
                parent.ProjectileId);
            double inverseSquareRootOfTwo = 1d / Math.Sqrt(2d);
            obliqueInput.SurfaceNormal = new PhysicalVector3(
                0d,
                inverseSquareRootOfTwo,
                -inverseSquareRootOfTwo);
            obliqueInput.ObservedOutgoingDirection = new PhysicalVector3(1d, 0d, 1d);
            PhysicalDeformationResponse oblique = SolveDeformationOrThrow(obliqueInput);
            PhysicalProjectileState obliquePrimaryState = RequireValue(
                "oblique primary state",
                oblique.PrimaryState);
            AssertNear("oblique impact angle", Math.PI * 0.25d, oblique.ImpactAngleRadians);
            AssertNear(
                "oblique normal-energy component",
                parent.TranslationalKineticEnergyJoules * 0.5d,
                oblique.NormalImpactEnergyJoules);
            AssertNear(
                "host deviated direction is normalized and preserved x",
                inverseSquareRootOfTwo,
                obliquePrimaryState.VelocityMetresPerSecond.X
                    / obliquePrimaryState.SpeedMetresPerSecond);
            AssertNear(
                "host deviated direction is normalized and preserved z",
                inverseSquareRootOfTwo,
                obliquePrimaryState.VelocityMetresPerSecond.Z
                    / obliquePrimaryState.SpeedMetresPerSecond);
            AssertEqual(
                "host deviated outcome is preserved",
                PhysicalCollisionOutcome.Deviated,
                oblique.CollisionRecord.Outcome);
            AssertEqual(
                "deviated response starts at measured far face",
                obliqueInput.ImpactPositionMetres.Add(
                    RequireNormalized(
                        "oblique incoming direction",
                        parent.VelocityMetresPerSecond)
                        .Scale(obliqueInput.EffectivePathLengthMetres)),
                oblique.OutputPositionMetres);

            PhysicalDeformationInput secondImpactInput = CreateValidDeformationInput(
                primaryState,
                PhysicalCollisionOutcome.Penetrated,
                0.005d,
                0.005d,
                1000000d,
                0.2d,
                "collision-deformation-second",
                primaryState.ProjectileId);
            PhysicalDeformationResponse secondImpact = SolveDeformationOrThrow(
                secondImpactInput);
            PhysicalProjectileState secondPrimaryState = RequireValue(
                "second-impact primary state",
                secondImpact.PrimaryState);
            PhysicalProjectileStateInput revertedKindInput = CopyPhysicalStateToInput(
                secondPrimaryState);
            revertedKindInput.Kind = PhysicalProjectileKind.IntactProjectile;
            PhysicalProjectileState revertedKindState = CreatePhysicalStateOrThrow(
                revertedKindInput);
            AssertTrue(
                "deformed projectile cannot revert to intact kind",
                !PhysicalProjectileConservation.TryValidateDeformationResponse(
                    primaryState,
                    revertedKindState,
                    secondImpact.CollisionRecord,
                    0d,
                    0d,
                    secondImpact.LossBudget,
                    out conservationResult,
                    out conservationReason));
            AssertEqual(
                "deformed projectile kind reversion reason",
                PhysicalConservationFailureReason.StateRevisionKindMismatch,
                conservationReason);

            PhysicalCollisionRecordInput changedPriorCollisionInput = CopyCollisionToInput(
                primaryState.CollisionHistory[0]);
            changedPriorCollisionInput.MaterialId = "rewritten-prior-material";
            PhysicalProjectileStateInput changedPriorHistoryInput = CopyPhysicalStateToInput(
                secondPrimaryState);
            changedPriorHistoryInput.CollisionHistory = new[]
            {
                CreateCollisionRecordOrThrow(changedPriorCollisionInput),
                secondImpact.CollisionRecord
            };
            PhysicalProjectileState changedPriorHistoryState = CreatePhysicalStateOrThrow(
                changedPriorHistoryInput);
            AssertTrue(
                "state revision cannot rewrite prior collision history",
                !PhysicalProjectileConservation.TryValidateDeformationResponse(
                    primaryState,
                    changedPriorHistoryState,
                    secondImpact.CollisionRecord,
                    0d,
                    0d,
                    secondImpact.LossBudget,
                    out conservationResult,
                    out conservationReason));
            AssertEqual(
                "rewritten prior collision history reason",
                PhysicalConservationFailureReason.StateRevisionHistoryMismatch,
                conservationReason);

            PhysicalDeformationResponse repeated = SolveDeformationOrThrow(input);
            PhysicalProjectileState repeatedPrimaryState = RequireValue(
                "repeated primary state",
                repeated.PrimaryState);
            AssertNear(
                "identical deformation input is deterministic",
                response.ResidualSystemEnergyJoules,
                repeated.ResidualSystemEnergyJoules);
            AssertNear(
                "deterministic deformed diameter",
                primaryState.DeformedDiameterMetres,
                repeatedPrimaryState.DeformedDiameterMetres);
            AssertNear(
                "deterministic projected area",
                primaryState.ProjectedAreaSquareMetres,
                repeatedPrimaryState.ProjectedAreaSquareMetres);

            PhysicalDeformationInput stoppedInput = CreateValidDeformationInput(
                parent,
                PhysicalCollisionOutcome.Stopped,
                0.01d,
                0.01d,
                50000000d,
                0.5d,
                "collision-deformation-stopped",
                parent.ProjectileId);
            stoppedInput.ObservedOutgoingDirection = PhysicalVector3.Zero;
            PhysicalDeformationResponse stopped = SolveDeformationOrThrow(stoppedInput);
            PhysicalProjectileState stoppedPrimaryState = RequireValue(
                "stopped primary state",
                stopped.PrimaryState);
            AssertNear("stopped response has no residual energy", 0d, stopped.ResidualSystemEnergyJoules);
            AssertNear(
                "stopped response accounts for all parent energy",
                parent.TranslationalKineticEnergyJoules,
                stopped.LossBudget.TotalLossJoules);
            AssertTrue("stopped response records remaining loss", stopped.LossBudget.OtherLossJoules > 0d);
            AssertEqual(
                "stopped state is terminal",
                PhysicalProjectileTerminalState.Stopped,
                stoppedPrimaryState.TerminalState);
            AssertNear("stopped state has zero speed", 0d, stoppedPrimaryState.SpeedMetresPerSecond);
            AssertEqual(
                "stopped response remains at impact face",
                stoppedInput.ImpactPositionMetres,
                stopped.OutputPositionMetres);
            AssertEqual(
                "stopped primary center is embedded inside the target",
                stoppedInput.ImpactPositionMetres.Add(
                    RequireNormalized(
                        "stopped incoming direction",
                        parent.VelocityMetresPerSecond)
                        .Scale(
                            Math.Min(
                                stoppedPrimaryState.LengthMetres * 0.5d,
                                stoppedInput.EffectivePathLengthMetres))),
                stoppedPrimaryState.PositionMetres);

            PhysicalDeformationInput ricochetInput = CreateValidDeformationInput(
                parent,
                PhysicalCollisionOutcome.Ricocheted,
                0.01d,
                0.01d,
                50000000d,
                0.5d,
                "collision-deformation-ricochet",
                parent.ProjectileId);
            ricochetInput.ObservedOutgoingDirection = new PhysicalVector3(1d, 0d, 1d);
            PhysicalDeformationResponse ricochet = SolveDeformationOrThrow(ricochetInput);
            PhysicalProjectileState ricochetPrimaryState = RequireValue(
                "ricochet primary state",
                ricochet.PrimaryState);
            AssertEqual(
                "ricochet response remains at impact face",
                ricochetInput.ImpactPositionMetres,
                ricochet.OutputPositionMetres);
            AssertEqual(
                "ricochet primary remains at impact face",
                ricochetInput.ImpactPositionMetres,
                ricochetPrimaryState.PositionMetres);

            PhysicalDeformationInput fragmentedInput = CreateValidDeformationInput(
                parent,
                PhysicalCollisionOutcome.Fragmented,
                0.01d,
                0.01d,
                50000000d,
                0.5d,
                "collision-deformation-fragmented",
                parent.ProjectileId);
            PhysicalDeformationResponse fragmented = SolveDeformationOrThrow(fragmentedInput);
            PhysicalProjectileState fragmentedPrimaryState = RequireValue(
                "fragmented primary remainder",
                fragmented.PrimaryState);
            AssertTrue("confirmed fragmentation remains marked", fragmented.RequiresFragmentation);
            AssertTrue("fracture probability is positive", fragmented.FractureProbability > 0d);
            AssertTrue("fracture probability is bounded", fragmented.FractureProbability <= 1d);
            AssertTrue("confirmed fragmentation spends fracture energy", fragmented.LossBudget.FractureLossJoules > 0d);
            AssertTrue("confirmed fragmentation reserves projectile mass", fragmented.AvailableFragmentMassKilograms > 0d);
            AssertTrue("confirmed fragmentation reserves fragment energy", fragmented.AvailableFragmentEnergyJoules > 0d);
            AssertNear(
                "fragment and primary mass partition parent",
                parent.RetainedMassKilograms,
                fragmented.RetainedPrimaryMassKilograms + fragmented.AvailableFragmentMassKilograms);
            AssertNear(
                "fragment and primary energy partition residual",
                fragmented.ResidualSystemEnergyJoules,
                fragmented.PrimaryEnergyJoules + fragmented.AvailableFragmentEnergyJoules);
            AssertNear(
                "fragmented response remains energy-conserved",
                parent.TranslationalKineticEnergyJoules,
                fragmented.LossBudget.TotalLossJoules + fragmented.ResidualSystemEnergyJoules);
            AssertEqual(
                "host fragmentation outcome is preserved",
                PhysicalCollisionOutcome.Fragmented,
                fragmented.CollisionRecord.Outcome);
            AssertTrue(
                "unclosed fragmented mass reservation rejected",
                !PhysicalProjectileConservation.TryValidateDeformationResponse(
                    parent,
                    fragmentedPrimaryState,
                    fragmented.CollisionRecord,
                    fragmented.AvailableFragmentMassKilograms + 0.001d,
                    fragmented.AvailableFragmentEnergyJoules,
                    fragmented.LossBudget,
                    out conservationResult,
                    out conservationReason));
            AssertEqual(
                "unclosed fragmented mass reason",
                PhysicalConservationFailureReason.ResponseMassNotClosed,
                conservationReason);
            AssertTrue(
                "unclosed fragmented energy reservation rejected",
                !PhysicalProjectileConservation.TryValidateDeformationResponse(
                    parent,
                    fragmentedPrimaryState,
                    fragmented.CollisionRecord,
                    fragmented.AvailableFragmentMassKilograms,
                    fragmented.AvailableFragmentEnergyJoules + 1d,
                    fragmented.LossBudget,
                    out conservationResult,
                    out conservationReason));
            AssertEqual(
                "unclosed fragmented energy reason",
                PhysicalConservationFailureReason.ResponseEnergyNotClosed,
                conservationReason);

            AssertTrue(
                "tungsten penetrator profile available",
                PhysicalDefaultProfileCatalog.TryGetProjectileProfile(
                    PhysicalProjectileConstruction.TungstenCoreJacketed,
                    out PhysicalProjectileMaterialProfile? tungstenProfileValue)
                && tungstenProfileValue != null);
            PhysicalProjectileMaterialProfile tungstenProfile = RequireValue(
                "tungsten penetrator profile",
                tungstenProfileValue);
            var tungstenRootInput = new PhysicalRootProjectileInput
            {
                ProjectileId = "ssa-ap-root",
                RootShotId = "ssa-ap-shot",
                DeterministicSeed = 0x5353414150UL,
                Construction = PhysicalProjectileConstruction.TungstenCoreJacketed,
                DesignClass = PhysicalProjectileDesignClass.SabotedPenetrator,
                ShapeClass = PhysicalProjectileShapeClass.Spitzer,
                MassKilograms = 0.00337d,
                NominalDiameterMetres = 0.0057d,
                MaterialDensityKilogramsPerCubicMetre =
                    tungstenProfile.DensityKilogramsPerCubicMetre,
                DragCoefficient = PhysicalDefaultProfileCatalog.GetNominalDragCoefficient(
                    PhysicalProjectileConstruction.TungstenCoreJacketed,
                    PhysicalProjectileDesignClass.SabotedPenetrator,
                    PhysicalProjectileShapeClass.Spitzer),
                PositionMetres = PhysicalVector3.Zero,
                VelocityMetresPerSecond = new PhysicalVector3(0d, 0d, 1013d)
            };
            AssertTrue(
                "SSA AP tungsten root created",
                PhysicalRootProjectileFactory.TryCreate(
                    tungstenRootInput,
                    out PhysicalProjectileState? tungstenRoot,
                    out PhysicalRootProjectileFailureReason tungstenRootReason));
            AssertEqual(
                "SSA AP tungsten root reason",
                PhysicalRootProjectileFailureReason.None,
                tungstenRootReason);
            PhysicalProjectileState tungstenParent = RequireValue(
                "SSA AP tungsten root",
                tungstenRoot);
            PhysicalDeformationInput tungstenInput = CreateValidDeformationInput(
                tungstenParent,
                PhysicalCollisionOutcome.Penetrated,
                0.01d,
                0.01d,
                50000000d,
                0.5d,
                "collision-ssa-ap-penetrator",
                tungstenParent.ProjectileId);
            tungstenInput.ProjectileProfile = tungstenProfile;
            PhysicalDeformationResponse tungstenResponse = SolveDeformationOrThrow(tungstenInput);
            PhysicalProjectileState tungstenPrimary = RequireValue(
                "SSA AP tungsten primary",
                tungstenResponse.PrimaryState);
            AssertTrue(
                "hard penetrator deformation severity is exercised",
                tungstenResponse.DeformationSeverity > 0.1d);
            AssertTrue(
                "hard penetrator diameter remains below mushroom threshold",
                tungstenResponse.DiameterExpansionRatio <= 1.02d);
            AssertEqual(
                "hard penetrator does not become a mushroom from severity alone",
                PhysicalProjectileShapeClass.Spitzer,
                tungstenPrimary.ShapeClass);
        }

        private static void ValidatePhysicalDeformationFallback()
        {
            PhysicalProjectileState parent = CreatePhysicalStateOrThrow(
                CreateValidRootInput(1000d, 0.01d, 0.01d));
            PhysicalDeformationInput input = CreateValidDeformationInput(
                parent,
                PhysicalCollisionOutcome.Penetrated,
                0.02d,
                0.01d,
                50000000d,
                0.5d,
                "collision-invalid-path",
                parent.ProjectileId);
            AssertDeformationFailure(
                "path shorter than physical thickness",
                input,
                PhysicalDeformationFailureReason.EffectivePathLengthInvalid);

            input = CreateValidDeformationInput(
                parent,
                PhysicalCollisionOutcome.Penetrated,
                0.01d,
                0.01d,
                1000000000000000d,
                0.5d,
                "collision-exhausted",
                parent.ProjectileId);
            AssertDeformationFailure(
                "moving outcome without residual energy",
                input,
                PhysicalDeformationFailureReason.MovingOutcomeHasNoResidualEnergy);

            input = CreateValidDeformationInput(
                parent,
                PhysicalCollisionOutcome.Penetrated,
                0.01d,
                0.01d,
                50000000d,
                0.5d,
                "collision-back-facing",
                parent.ProjectileId);
            input.SurfaceNormal = new PhysicalVector3(0d, 0d, 1d);
            AssertDeformationFailure(
                "surface normal pointing with travel",
                input,
                PhysicalDeformationFailureReason.ImpactDirectionInvalid);

            input = CreateValidDeformationInput(
                parent,
                PhysicalCollisionOutcome.Fragmented,
                0.01d,
                0.01d,
                50000000d,
                0d,
                "collision-no-fracture",
                parent.ProjectileId);
            AssertDeformationFailure(
                "confirmed fragmentation without physical coupling",
                input,
                PhysicalDeformationFailureReason.FragmentationUnsupportedByProfile);

            input = CreateValidDeformationInput(
                parent,
                PhysicalCollisionOutcome.Penetrated,
                0.01d,
                0.01d,
                50000000d,
                0.5d,
                "collision-same-id",
                "different-projectile-id");
            AssertDeformationFailure(
                "state revision cannot change physical projectile identity",
                input,
                PhysicalDeformationFailureReason.OutputProjectileIdentityInvalid);
        }

        private static void ValidatePhysicalDeformationStressSweep()
        {
            const int caseCount = 4096;
            var random = new DeterministicProjectileRandom(
                0xD3F04A7105EEDUL,
                0x51A7EUL);
            for (int index = 0; index < caseCount; index++)
            {
                double speedMetresPerSecond = Lerp(250d, 1400d, random.NextUnitDouble());
                PhysicalProjectileState parent = CreatePhysicalStateOrThrow(
                    CreateValidRootInput(speedMetresPerSecond, 0.01d, 0.01d));
                double effectivePathMetres = Lerp(0.001d, 0.02d, random.NextUnitDouble());
                double physicalThicknessMetres = effectivePathMetres
                    * Lerp(0.05d, 1d, random.NextUnitDouble());
                double resistancePressurePascals = Lerp(
                    100000d,
                    20000000d,
                    random.NextUnitDouble());
                double fractureCoupling = Lerp(0.05d, 0.8d, random.NextUnitDouble());
                var outcome = (PhysicalCollisionOutcome)((index % 5) + 1);
                PhysicalDeformationInput input = CreateValidDeformationInput(
                    parent,
                    outcome,
                    physicalThicknessMetres,
                    effectivePathMetres,
                    resistancePressurePascals,
                    fractureCoupling,
                    "collision-property-" + index.ToString(CultureInfo.InvariantCulture),
                    parent.ProjectileId);

                double impactAngleRadians = Lerp(
                    0d,
                    Math.PI * 0.4722222222222222d,
                    random.NextUnitDouble());
                input.SurfaceNormal = new PhysicalVector3(
                    Math.Sin(impactAngleRadians),
                    0d,
                    -Math.Cos(impactAngleRadians));
                if (outcome == PhysicalCollisionOutcome.Stopped)
                {
                    input.ObservedOutgoingDirection = PhysicalVector3.Zero;
                }
                else
                {
                    double azimuthRadians = Math.PI * 2d * random.NextUnitDouble();
                    double forwardShare = Lerp(0.05d, 1d, random.NextUnitDouble());
                    double lateralShare = Math.Sqrt(1d - (forwardShare * forwardShare));
                    input.ObservedOutgoingDirection = new PhysicalVector3(
                        lateralShare * Math.Cos(azimuthRadians),
                        lateralShare * Math.Sin(azimuthRadians),
                        forwardShare);
                }

                PhysicalDeformationResponse? response;
                PhysicalDeformationFailureReason reason;
                if (!PhysicalDeformationSolver.TrySolve(input, out response, out reason)
                    || response == null)
                {
                    throw new InvalidOperationException(
                        "Valid deformation property case "
                        + index.ToString(CultureInfo.InvariantCulture)
                        + " failed: "
                        + reason
                        + ".");
                }

                PhysicalProjectileState propertyPrimaryState = RequireValue(
                    "property primary state " + index.ToString(CultureInfo.InvariantCulture),
                    response.PrimaryState);

                AssertFiniteNonNegative(
                    "property residual energy " + index.ToString(CultureInfo.InvariantCulture),
                    response.ResidualSystemEnergyJoules);
                AssertFiniteNonNegative(
                    "property fragment mass " + index.ToString(CultureInfo.InvariantCulture),
                    response.AvailableFragmentMassKilograms);
                AssertFiniteNonNegative(
                    "property fragment energy " + index.ToString(CultureInfo.InvariantCulture),
                    response.AvailableFragmentEnergyJoules);
                AssertNear(
                    "property energy closure " + index.ToString(CultureInfo.InvariantCulture),
                    parent.TranslationalKineticEnergyJoules,
                    response.LossBudget.TotalLossJoules
                        + response.ResidualSystemEnergyJoules);
                AssertNear(
                    "property mass closure " + index.ToString(CultureInfo.InvariantCulture),
                    parent.RetainedMassKilograms,
                    response.RetainedPrimaryMassKilograms
                        + response.AvailableFragmentMassKilograms);
                AssertNear(
                    "property residual partition " + index.ToString(CultureInfo.InvariantCulture),
                    response.ResidualSystemEnergyJoules,
                    response.PrimaryEnergyJoules
                        + response.AvailableFragmentEnergyJoules);
                AssertEqual(
                    "property host outcome " + index.ToString(CultureInfo.InvariantCulture),
                    outcome,
                    response.CollisionRecord.Outcome);

                if (outcome == PhysicalCollisionOutcome.Fragmented)
                {
                    AssertTrue(
                        "property fragmented mass reservation "
                            + index.ToString(CultureInfo.InvariantCulture),
                        response.AvailableFragmentMassKilograms > 0d);
                    AssertTrue(
                        "property fragmented energy reservation "
                            + index.ToString(CultureInfo.InvariantCulture),
                        response.AvailableFragmentEnergyJoules > 0d);
                }
                else
                {
                    AssertNear(
                        "property nonfragment mass reservation "
                            + index.ToString(CultureInfo.InvariantCulture),
                        0d,
                        response.AvailableFragmentMassKilograms);
                    AssertNear(
                        "property nonfragment energy reservation "
                            + index.ToString(CultureInfo.InvariantCulture),
                        0d,
                        response.AvailableFragmentEnergyJoules);
                }

                if (outcome == PhysicalCollisionOutcome.Stopped)
                {
                    AssertNear(
                        "property stopped residual "
                            + index.ToString(CultureInfo.InvariantCulture),
                        0d,
                        response.ResidualSystemEnergyJoules);
                    AssertEqual(
                        "property stopped terminal state "
                            + index.ToString(CultureInfo.InvariantCulture),
                        PhysicalProjectileTerminalState.Stopped,
                        propertyPrimaryState.TerminalState);
                }
                else
                {
                    AssertTrue(
                        "property moving residual "
                            + index.ToString(CultureInfo.InvariantCulture),
                        response.ResidualSystemEnergyJoules > 0d);
                    AssertTrue(
                        "property moving speed "
                            + index.ToString(CultureInfo.InvariantCulture),
                        propertyPrimaryState.SpeedMetresPerSecond > 0d);
                }

                if ((index & 255) == 0)
                {
                    PhysicalDeformationResponse repeated = SolveDeformationOrThrow(input);
                    PhysicalProjectileState repeatedPropertyPrimaryState = RequireValue(
                        "repeated property primary state "
                            + index.ToString(CultureInfo.InvariantCulture),
                        repeated.PrimaryState);
                    AssertNear(
                        "property deterministic residual "
                            + index.ToString(CultureInfo.InvariantCulture),
                        response.ResidualSystemEnergyJoules,
                        repeated.ResidualSystemEnergyJoules);
                    AssertNear(
                        "property deterministic diameter "
                            + index.ToString(CultureInfo.InvariantCulture),
                        propertyPrimaryState.DeformedDiameterMetres,
                        repeatedPropertyPrimaryState.DeformedDiameterMetres);
                }
            }
        }

        private static void ValidatePhysicalFragmentationProfile()
        {
            PhysicalFragmentationProfile? profile;
            PhysicalFragmentationProfileFailureReason reason;
            AssertTrue(
                "valid fragmentation profile accepted",
                PhysicalFragmentationProfile.TryCreate(
                    CreateTestFragmentationProfileInput(),
                    out profile,
                    out reason));
            PhysicalFragmentationProfile validProfile = RequireValue(
                "valid fragmentation profile",
                profile);
            AssertEqual(
                "valid fragmentation profile reason",
                PhysicalFragmentationProfileFailureReason.None,
                reason);
            AssertEqual(
                "projectile fragment count cap preserved",
                16,
                validProfile.MaximumProjectileFragmentCount);
            AssertTrue("target spall enabled", validProfile.ProducesTargetSpall);

            PhysicalFragmentationProfileInput fullEnergyInput = CreateTestFragmentationProfileInput();
            fullEnergyInput.TargetSpallKineticEnergyFraction = 1d;
            AssertTrue(
                "full penetration-work spall fraction is valid",
                PhysicalFragmentationProfile.TryCreate(
                    fullEnergyInput,
                    out profile,
                    out reason));
            AssertTrue("full penetration-work spall profile returned", profile != null);
            AssertEqual(
                "full penetration-work spall profile reason",
                PhysicalFragmentationProfileFailureReason.None,
                reason);

            PhysicalFragmentationProfileInput invalidInput = CreateTestFragmentationProfileInput();
            invalidInput.MaximumProjectileFragmentCount = 257;
            AssertFragmentationProfileFailure(
                "fragment count above hard cap",
                invalidInput,
                PhysicalFragmentationProfileFailureReason.ProjectileCountInvalid);

            invalidInput = CreateTestFragmentationProfileInput();
            invalidInput.MinimumProjectileFragmentMassKilograms = 0d;
            AssertFragmentationProfileFailure(
                "zero minimum projectile fragment mass",
                invalidInput,
                PhysicalFragmentationProfileFailureReason.ProjectileMassInvalid);

            invalidInput = CreateTestFragmentationProfileInput();
            invalidInput.TargetSpallKineticEnergyFraction = 0d;
            AssertFragmentationProfileFailure(
                "spall mass without spall energy",
                invalidInput,
                PhysicalFragmentationProfileFailureReason.TargetSpallFractionInvalid);

            invalidInput = CreateTestFragmentationProfileInput();
            invalidInput.MaximumTargetSpallDragCoefficient = double.PositiveInfinity;
            AssertFragmentationProfileFailure(
                "nonfinite target spall drag",
                invalidInput,
                PhysicalFragmentationProfileFailureReason.TargetSpallDragCoefficientInvalid);
        }

        private static void ValidatePhysicalFragmentationResponse()
        {
            PhysicalProjectileState parent;
            PhysicalProjectileMaterialProfile projectileProfile;
            PhysicalTargetMaterialProfile targetProfile;
            PhysicalDeformationResponse deformation;
            CreateFragmentationScenario(
                1000d,
                "collision-fragmentation-response",
                out parent,
                out projectileProfile,
                out targetProfile,
                out deformation);
            PhysicalFragmentationProfile fragmentationProfile = CreateTestFragmentationProfile();
            PhysicalFragmentationInput input = CreateValidFragmentationInput(
                parent,
                deformation,
                projectileProfile,
                targetProfile,
                fragmentationProfile,
                4,
                "projectile-fragment",
                "target-spall");
            PhysicalFragmentationResponse response = SolveFragmentationOrThrow(input);

            AssertTrue(
                "fragmentation keeps exact primary state revision",
                ReferenceEquals(deformation.PrimaryState, response.PrimaryState));
            AssertEqual("observed host fragment count retained", 4, response.ObservedProjectileFragmentCount);
            AssertEqual("four physical projectile fragments emitted", 4, response.ProducedProjectileFragmentCount);
            AssertTrue("target spall components emitted", response.TargetSpall.Count > 0);
            AssertEqual(
                "all-secondary count",
                response.ProjectileFragments.Count + response.TargetSpall.Count,
                response.AllSecondaryComponents.Count);

            double projectileMass = 0d;
            double projectileEnergy = 0d;
            double spallMass = 0d;
            double spallEnergy = 0d;
            var identities = new HashSet<string>(StringComparer.Ordinal);
            var indices = new HashSet<int>();
            for (int index = 0; index < response.ProjectileFragments.Count; index++)
            {
                PhysicalProjectileState fragment = response.ProjectileFragments[index];
                AssertPhysicalSecondary(
                    "projectile fragment " + index.ToString(CultureInfo.InvariantCulture),
                    fragment,
                    parent,
                    deformation,
                    projectileProfile.DensityKilogramsPerCubicMetre,
                    fragmentationProfile.ProjectileConeHalfAngleRadians,
                    deformation.OutgoingDirection,
                    identities,
                    indices);
                AssertEqual(
                    "projectile fragment kind " + index.ToString(CultureInfo.InvariantCulture),
                    PhysicalProjectileKind.ProjectileFragment,
                    fragment.Kind);
                AssertEqual(
                    "projectile fragment construction " + index.ToString(CultureInfo.InvariantCulture),
                    parent.Construction,
                    fragment.Construction);
                AssertEqual(
                    "projectile fragment shape " + index.ToString(CultureInfo.InvariantCulture),
                    PhysicalProjectileShapeClass.IrregularProjectileFragment,
                    fragment.ShapeClass);
                AssertTrue(
                    "projectile fragment has less mass than parent " + index.ToString(CultureInfo.InvariantCulture),
                    fragment.RetainedMassKilograms < parent.RetainedMassKilograms);
                AssertTrue(
                    "projectile fragment has independent ballistic coefficient " + index.ToString(CultureInfo.InvariantCulture),
                    !fragment.BallisticCoefficientKilogramsPerSquareMetre.Equals(
                        parent.BallisticCoefficientKilogramsPerSquareMetre));
                projectileMass += fragment.RetainedMassKilograms;
                projectileEnergy += fragment.TranslationalKineticEnergyJoules;
            }

            PhysicalVector3 spallAxis = RequireNormalized(
                "target spall axis",
                deformation.OutgoingDirection.Scale(0.75d)
                    .Add(deformation.SurfaceNormal.Negate().Scale(0.25d)));
            for (int index = 0; index < response.TargetSpall.Count; index++)
            {
                PhysicalProjectileState spall = response.TargetSpall[index];
                AssertPhysicalSecondary(
                    "target spall " + index.ToString(CultureInfo.InvariantCulture),
                    spall,
                    parent,
                    deformation,
                    targetProfile.DensityKilogramsPerCubicMetre,
                    fragmentationProfile.TargetSpallConeHalfAngleRadians,
                    spallAxis,
                    identities,
                    indices);
                AssertEqual(
                    "target spall kind " + index.ToString(CultureInfo.InvariantCulture),
                    PhysicalProjectileKind.TargetSpall,
                    spall.Kind);
                AssertEqual(
                    "target spall construction " + index.ToString(CultureInfo.InvariantCulture),
                    PhysicalProjectileConstruction.TargetMaterial,
                    spall.Construction);
                AssertTrue(
                    "target spall records target-material origin "
                        + index.ToString(CultureInfo.InvariantCulture),
                    spall.IsTargetMaterialOrigin);
                AssertTrue(
                    "fresh target spall does not consume parent mass "
                        + index.ToString(CultureInfo.InvariantCulture),
                    !spall.IsParentDerivedMass);
                AssertTrue(
                    "target spall has target-material shape " + index.ToString(CultureInfo.InvariantCulture),
                    spall.ShapeClass == PhysicalProjectileShapeClass.TargetSpallFlake
                        || spall.ShapeClass == PhysicalProjectileShapeClass.TargetSpallChunk);
                spallMass += spall.RetainedMassKilograms;
                spallEnergy += spall.TranslationalKineticEnergyJoules;
            }

            PhysicalProjectileState primary = RequireValue(
                "fragmentation primary state",
                response.PrimaryState);
            AssertNear(
                "projectile fragment mass reservation closes",
                deformation.AvailableFragmentMassKilograms,
                projectileMass);
            AssertNear(
                "projectile fragment energy reservation closes",
                deformation.AvailableFragmentEnergyJoules,
                projectileEnergy);
            AssertNear("target spall mass reservation closes", response.TargetSpallMassKilograms, spallMass);
            AssertNear("target spall energy reservation closes", response.TargetSpallEnergyJoules, spallEnergy);
            AssertNear(
                "target spall energy comes from penetration work",
                deformation.LossBudget.PenetrationLossJoules
                    * fragmentationProfile.TargetSpallKineticEnergyFraction,
                response.TargetSpallEnergyJoules);
            AssertNear(
                "effective penetration loss reclassifies target spall energy",
                deformation.LossBudget.PenetrationLossJoules - response.TargetSpallEnergyJoules,
                response.EffectiveLossBudget.PenetrationLossJoules);
            AssertNear(
                "fragmentation closes projectile mass",
                parent.RetainedMassKilograms,
                primary.RetainedMassKilograms + projectileMass);
            AssertNear(
                "fragmentation closes complete energy system",
                parent.TranslationalKineticEnergyJoules,
                response.EffectiveLossBudget.TotalLossJoules
                    + primary.TranslationalKineticEnergyJoules
                    + projectileEnergy
                    + spallEnergy);
            AssertNear(
                "conservation report target spall mass",
                spallMass,
                response.ConservationResult.TargetSpallMassKilograms);

            PhysicalFragmentationResponse repeated = SolveFragmentationOrThrow(input);
            AssertFragmentationEquivalent("deterministic full response", response, repeated);

            PhysicalFragmentationProfileInput fullSpallEnergyInput =
                CreateTestFragmentationProfileInput();
            fullSpallEnergyInput.TargetSpallKineticEnergyFraction = 1d;
            PhysicalFragmentationProfile? fullSpallEnergyProfile;
            PhysicalFragmentationProfileFailureReason fullSpallProfileReason;
            AssertTrue(
                "full penetration-work spall profile accepted for solving",
                PhysicalFragmentationProfile.TryCreate(
                    fullSpallEnergyInput,
                    out fullSpallEnergyProfile,
                    out fullSpallProfileReason));
            AssertEqual(
                "full penetration-work spall solving profile reason",
                PhysicalFragmentationProfileFailureReason.None,
                fullSpallProfileReason);
            PhysicalFragmentationResponse fullSpallEnergyResponse = SolveFragmentationOrThrow(
                CreateValidFragmentationInput(
                    parent,
                    deformation,
                    projectileProfile,
                    targetProfile,
                    RequireValue("full penetration-work spall profile", fullSpallEnergyProfile),
                    4,
                    "full-energy-projectile-fragment",
                    "full-energy-target-spall"));
            AssertNear(
                "all penetration work can become target spall kinetic energy",
                deformation.LossBudget.PenetrationLossJoules,
                fullSpallEnergyResponse.TargetSpallEnergyJoules);
            AssertNear(
                "full spall-energy transfer leaves zero penetration loss",
                0d,
                fullSpallEnergyResponse.EffectiveLossBudget.PenetrationLossJoules);

            var corruptedOutputs = new List<PhysicalProjectileState?>(response.AllSecondaryComponents.Count);
            PhysicalProjectileState firstFragment = response.ProjectileFragments[0];
            PhysicalProjectileStateInput changedMassInput = CopyPhysicalStateToInput(firstFragment);
            changedMassInput.OriginalMassKilograms *= 0.99d;
            changedMassInput.RetainedMassKilograms *= 0.99d;
            changedMassInput.DamageCapabilityJoules *= 0.99d;
            corruptedOutputs.Add(CreatePhysicalStateOrThrow(changedMassInput));
            for (int index = 1; index < response.AllSecondaryComponents.Count; index++)
            {
                corruptedOutputs.Add(response.AllSecondaryComponents[index]);
            }

            PhysicalConservationResult conservationResult;
            PhysicalConservationFailureReason conservationReason;
            AssertTrue(
                "corrupted projectile fragment mass rejected",
                !PhysicalProjectileConservation.TryValidateFragmentationResolution(
                    parent,
                    primary,
                    deformation.CollisionRecord,
                    corruptedOutputs,
                    deformation.AvailableFragmentMassKilograms,
                    deformation.AvailableFragmentEnergyJoules,
                    response.TargetSpallMassKilograms,
                    response.TargetSpallEnergyJoules,
                    deformation.LossBudget,
                    response.EffectiveLossBudget,
                    out conservationResult,
                    out conservationReason));
            AssertEqual(
                "corrupted projectile fragment mass reason",
                PhysicalConservationFailureReason.ParentFragmentMassNotClosed,
                conservationReason);

            PhysicalCollisionRecordInput changedCollisionInput = CopyCollisionToInput(
                deformation.CollisionRecord);
            changedCollisionInput.MaterialId = "rewritten-fragment-history-material";
            PhysicalProjectileStateInput changedHistoryInput = CopyPhysicalStateToInput(
                firstFragment);
            changedHistoryInput.CollisionHistory = new[]
            {
                CreateCollisionRecordOrThrow(changedCollisionInput)
            };
            PhysicalProjectileState?[] changedHistoryOutputs = ToNullableStates(
                response.AllSecondaryComponents);
            changedHistoryOutputs[0] = CreatePhysicalStateOrThrow(changedHistoryInput);
            AssertTrue(
                "rewritten fragment collision history rejected",
                !PhysicalProjectileConservation.TryValidateFragmentationResolution(
                    parent,
                    primary,
                    deformation.CollisionRecord,
                    changedHistoryOutputs,
                    deformation.AvailableFragmentMassKilograms,
                    deformation.AvailableFragmentEnergyJoules,
                    response.TargetSpallMassKilograms,
                    response.TargetSpallEnergyJoules,
                    deformation.LossBudget,
                    response.EffectiveLossBudget,
                    out conservationResult,
                    out conservationReason));
            AssertEqual(
                "rewritten fragment collision history reason",
                PhysicalConservationFailureReason.FragmentationHistoryMismatch,
                conservationReason);

            AssertTrue(
                "incorrect target spall reservation rejected",
                !PhysicalProjectileConservation.TryValidateFragmentationResolution(
                    parent,
                    primary,
                    deformation.CollisionRecord,
                    ToNullableStates(response.AllSecondaryComponents),
                    deformation.AvailableFragmentMassKilograms,
                    deformation.AvailableFragmentEnergyJoules,
                    response.TargetSpallMassKilograms * 1.01d,
                    response.TargetSpallEnergyJoules,
                    deformation.LossBudget,
                    response.EffectiveLossBudget,
                    out conservationResult,
                    out conservationReason));
            AssertEqual(
                "incorrect target spall reservation reason",
                PhysicalConservationFailureReason.TargetSpallMassNotClosed,
                conservationReason);
        }

        private static void ValidateIndependentTargetSpall()
        {
            PhysicalProjectileState parent = CreatePhysicalStateOrThrow(
                CreateValidRootInput(1000d, 0.01d, 0.01d));
            PhysicalProjectileMaterialProfile projectileProfile = CreateTestProjectileProfile();
            PhysicalTargetMaterialProfile targetProfile = CreateTestTargetProfile(
                50000000d,
                0.5d);
            PhysicalDeformationInput deformationInput = CreateValidDeformationInput(
                parent,
                PhysicalCollisionOutcome.Penetrated,
                0.01d,
                0.01d,
                50000000d,
                0.5d,
                "collision-independent-spall",
                parent.ProjectileId);
            deformationInput.ProjectileProfile = projectileProfile;
            deformationInput.TargetProfile = targetProfile;
            PhysicalDeformationResponse deformation = SolveDeformationOrThrow(
                deformationInput);
            AssertTrue(
                "independent target spall does not require projectile fragmentation",
                !deformation.RequiresFragmentation);
            AssertNear(
                "nonfragmenting penetration reserves no projectile mass",
                0d,
                deformation.AvailableFragmentMassKilograms);

            PhysicalFragmentationProfile fragmentationProfile = CreateTestFragmentationProfile();
            var spallInput = new PhysicalTargetSpallInput
            {
                Parent = parent,
                DeformationResponse = deformation,
                TargetProfile = targetProfile,
                FragmentationProfile = fragmentationProfile,
                TargetSpallIdPrefix = "independent-spall"
            };
            AssertTrue(
                "independent target spall solves",
                PhysicalFragmentationSolver.TrySolveTargetSpall(
                    spallInput,
                    out PhysicalTargetSpallResponse? response,
                    out PhysicalTargetSpallFailureReason reason));
            AssertEqual(
                "independent target spall reason",
                PhysicalTargetSpallFailureReason.None,
                reason);
            PhysicalTargetSpallResponse value = RequireValue(
                "independent target spall response",
                response);
            AssertTrue("independent target spall emits components", value.Components.Count > 0);

            double massKilograms = 0d;
            double energyJoules = 0d;
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var indices = new HashSet<int>();
            for (int index = 0; index < value.Components.Count; index++)
            {
                PhysicalProjectileState component = value.Components[index];
                AssertEqual(
                    "independent target spall kind " + index.ToString(CultureInfo.InvariantCulture),
                    PhysicalProjectileKind.TargetSpall,
                    component.Kind);
                AssertEqual(
                    "independent target spall position " + index.ToString(CultureInfo.InvariantCulture),
                    deformation.OutputPositionMetres,
                    component.PositionMetres);
                AssertEqual(
                    "independent target spall source material " + index.ToString(CultureInfo.InvariantCulture),
                    targetProfile.ProfileId,
                    component.SourceMaterialId);
                AssertEqual(
                    "independent target spall source collision " + index.ToString(CultureInfo.InvariantCulture),
                    deformation.CollisionRecord.CollisionId,
                    component.SourceCollisionId);
                AssertTrue(
                    "independent target spall id unique " + index.ToString(CultureInfo.InvariantCulture),
                    ids.Add(component.ProjectileId));
                AssertTrue(
                    "independent target spall index unique " + index.ToString(CultureInfo.InvariantCulture),
                    indices.Add(component.FragmentIndex));
                massKilograms += component.RetainedMassKilograms;
                energyJoules += component.TranslationalKineticEnergyJoules;
            }

            PhysicalProjectileState primary = RequireValue(
                "independent target spall primary",
                deformation.PrimaryState);
            AssertNear("independent target spall mass closes", value.MassKilograms, massKilograms);
            AssertNear("independent target spall energy closes", value.EnergyJoules, energyJoules);
            AssertNear(
                "target spall mass does not reduce projectile mass",
                parent.RetainedMassKilograms,
                primary.RetainedMassKilograms);
            AssertNear(
                "independent target spall closes system energy",
                parent.TranslationalKineticEnergyJoules,
                value.EffectiveLossBudget.TotalLossJoules
                    + primary.TranslationalKineticEnergyJoules
                    + value.EnergyJoules);
            AssertNear(
                "independent target spall conservation reports target mass",
                value.MassKilograms,
                value.ConservationResult.TargetSpallMassKilograms);

            AssertTrue(
                "independent target spall is deterministic",
                PhysicalFragmentationSolver.TrySolveTargetSpall(
                    spallInput,
                    out PhysicalTargetSpallResponse? repeated,
                    out reason));
            PhysicalTargetSpallResponse repeatedValue = RequireValue(
                "repeated independent target spall response",
                repeated);
            AssertEqual(
                "independent target spall deterministic count",
                value.Components.Count,
                repeatedValue.Components.Count);
            for (int index = 0; index < value.Components.Count; index++)
            {
                AssertPhysicalStateEquivalent(
                    "independent target spall deterministic component "
                        + index.ToString(CultureInfo.InvariantCulture),
                    value.Components[index],
                    repeatedValue.Components[index]);
            }

            CreateFragmentationScenario(
                1000d,
                "collision-fragmented-spall-owner",
                out PhysicalProjectileState fragmentedParent,
                out _,
                out PhysicalTargetMaterialProfile fragmentedTarget,
                out PhysicalDeformationResponse fragmentedDeformation);
            var fragmentedInput = new PhysicalTargetSpallInput
            {
                Parent = fragmentedParent,
                DeformationResponse = fragmentedDeformation,
                TargetProfile = fragmentedTarget,
                FragmentationProfile = fragmentationProfile,
                TargetSpallIdPrefix = "fragmented-spall-owner"
            };
            AssertTrue(
                "fragmentation solver retains ownership of fragmented target spall",
                !PhysicalFragmentationSolver.TrySolveTargetSpall(
                    fragmentedInput,
                    out response,
                    out reason));
            AssertEqual(
                "fragmented target spall ownership reason",
                PhysicalTargetSpallFailureReason.FragmentationOutcomeOwnedByFragmentationSolver,
                reason);

            PhysicalFragmentationProfileInput noSpallProfileInput =
                CreateTestFragmentationProfileInput();
            noSpallProfileInput.TargetSpallEjectedMassFraction = 0d;
            noSpallProfileInput.TargetSpallKineticEnergyFraction = 0d;
            noSpallProfileInput.NominalTargetSpallMassKilograms = 0d;
            noSpallProfileInput.MaximumTargetSpallCount = 0;
            noSpallProfileInput.TargetSpallConeHalfAngleRadians = 0d;
            noSpallProfileInput.MinimumTargetSpallAspectRatio = 0d;
            noSpallProfileInput.MaximumTargetSpallAspectRatio = 0d;
            noSpallProfileInput.MinimumTargetSpallDragCoefficient = 0d;
            noSpallProfileInput.MaximumTargetSpallDragCoefficient = 0d;
            noSpallProfileInput.TargetSpallPenetrationEfficiency = 0d;
            AssertTrue(
                "no-spall profile accepted",
                PhysicalFragmentationProfile.TryCreate(
                    noSpallProfileInput,
                    out PhysicalFragmentationProfile? noSpallProfile,
                    out _));
            spallInput.FragmentationProfile = RequireValue(
                "no-spall profile",
                noSpallProfile);
            AssertTrue(
                "disabled target spall remains absent",
                !PhysicalFragmentationSolver.TrySolveTargetSpall(
                    spallInput,
                    out response,
                    out reason));
            AssertEqual(
                "disabled target spall reason",
                PhysicalTargetSpallFailureReason.TargetSpallDisabled,
                reason);
        }

        private static void ValidateTargetSpallContinuation()
        {
            CreateFragmentationScenario(
                1400d,
                "collision-spall-origin",
                out PhysicalProjectileState originalProjectile,
                out PhysicalProjectileMaterialProfile originalProjectileProfile,
                out PhysicalTargetMaterialProfile originalTargetProfile,
                out PhysicalDeformationResponse originalDeformation);
            PhysicalFragmentationResponse originalResponse = SolveFragmentationOrThrow(
                CreateValidFragmentationInput(
                    originalProjectile,
                    originalDeformation,
                    originalProjectileProfile,
                    originalTargetProfile,
                    CreateTestFragmentationProfile(),
                    2,
                    "origin-projectile-fragment",
                    "origin-target-spall"));
            AssertTrue("origin collision emits target spall", originalResponse.TargetSpall.Count > 0);
            PhysicalProjectileState spallParent = originalResponse.TargetSpall[0];

            PhysicalProjectileMaterialProfile? spallProfile;
            AssertTrue(
                "target spall resolves its own material profile",
                PhysicalDefaultProfileCatalog.TryGetSpallProjectileProfile(
                    spallParent.SourceMaterialClass,
                    out spallProfile));
            PhysicalProjectileMaterialProfile spallProfileValue = RequireValue(
                "target spall material profile",
                spallProfile);
            AssertEqual(
                "target spall profile construction",
                PhysicalProjectileConstruction.TargetMaterial,
                spallProfileValue.Construction);

            PhysicalTargetMaterialProfile? secondTargetProfile;
            PhysicalFragmentationProfile? secondFragmentationProfile;
            AssertTrue(
                "second target profile resolves",
                PhysicalDefaultProfileCatalog.TryGetTargetProfile(
                    PhysicalMaterialClass.Glass,
                    out secondTargetProfile));
            AssertTrue(
                "second target fragmentation profile resolves",
                PhysicalDefaultProfileCatalog.TryGetFragmentationProfile(
                    PhysicalMaterialClass.Glass,
                    out secondFragmentationProfile));
            PhysicalTargetMaterialProfile secondTarget = RequireValue(
                "second target material profile",
                secondTargetProfile);
            PhysicalFragmentationProfile secondFragmentation = RequireValue(
                "second target fragmentation profile",
                secondFragmentationProfile);

            var secondDeformationInput = new PhysicalDeformationInput
            {
                Parent = spallParent,
                ProjectileProfile = spallProfileValue,
                TargetProfile = secondTarget,
                CollisionId = "collision-spall-second-impact",
                OutputProjectileId = spallParent.ProjectileId,
                ImpactPositionMetres = spallParent.PositionMetres.Add(
                    new PhysicalVector3(0d, 0d, 0.5d)),
                SurfaceNormal = new PhysicalVector3(0d, 0d, -1d),
                PhysicalThicknessMetres = 0.0005d,
                EffectivePathLengthMetres = 0.0005d,
                ObservedOutcome = PhysicalCollisionOutcome.Fragmented,
                ObservedOutgoingDirection = new PhysicalVector3(0d, 0d, 1d)
            };
            PhysicalDeformationResponse secondDeformation = SolveDeformationOrThrow(
                secondDeformationInput);
            PhysicalProjectileState secondPrimary = RequireValue(
                "target spall second-impact primary",
                secondDeformation.PrimaryState);
            AssertEqual(
                "target spall primary receives parent-derived kind",
                PhysicalProjectileKind.TargetSpallFragment,
                secondPrimary.Kind);
            AssertEqual(
                "target spall primary preserves material construction",
                PhysicalProjectileConstruction.TargetMaterial,
                secondPrimary.Construction);
            AssertEqual(
                "target spall primary preserves origin material",
                spallParent.SourceMaterialClass,
                secondPrimary.SourceMaterialClass);
            AssertEqual(
                "target spall primary appends second collision",
                secondDeformation.CollisionRecord,
                secondPrimary.CollisionHistory[secondPrimary.CollisionHistory.Count - 1]);

            PhysicalFragmentationResponse secondResponse = SolveFragmentationOrThrow(
                CreateValidFragmentationInput(
                    spallParent,
                    secondDeformation,
                    spallProfileValue,
                    secondTarget,
                    secondFragmentation,
                    2,
                    "secondary-spall-fragment",
                    "second-target-spall"));
            AssertTrue(
                "target spall emits independently simulated child fragments",
                secondResponse.ProjectileFragments.Count > 0);
            double derivedMassKilograms = 0d;
            for (int index = 0; index < secondResponse.ProjectileFragments.Count; index++)
            {
                PhysicalProjectileState fragment = secondResponse.ProjectileFragments[index];
                AssertEqual(
                    "target spall child kind " + index.ToString(CultureInfo.InvariantCulture),
                    PhysicalProjectileKind.TargetSpallFragment,
                    fragment.Kind);
                AssertEqual(
                    "target spall child construction " + index.ToString(CultureInfo.InvariantCulture),
                    PhysicalProjectileConstruction.TargetMaterial,
                    fragment.Construction);
                AssertTrue(
                    "target spall child preserves target-material origin "
                        + index.ToString(CultureInfo.InvariantCulture),
                    fragment.IsTargetMaterialOrigin);
                AssertTrue(
                    "target spall child consumes immediate-parent mass "
                        + index.ToString(CultureInfo.InvariantCulture),
                    fragment.IsParentDerivedMass);
                AssertTrue(
                    "target spall child shape " + index.ToString(CultureInfo.InvariantCulture),
                    fragment.ShapeClass == PhysicalProjectileShapeClass.TargetSpallFlake
                        || fragment.ShapeClass == PhysicalProjectileShapeClass.TargetSpallChunk);
                AssertEqual(
                    "target spall child preserves origin material "
                        + index.ToString(CultureInfo.InvariantCulture),
                    spallParent.SourceMaterialClass,
                    fragment.SourceMaterialClass);
                AssertEqual(
                    "target spall child records current collision "
                        + index.ToString(CultureInfo.InvariantCulture),
                    secondDeformation.CollisionRecord.CollisionId,
                    fragment.SourceCollisionId);
                derivedMassKilograms += fragment.RetainedMassKilograms;
            }

            AssertNear(
                "target spall child mass closes current parent reservation",
                secondDeformation.AvailableFragmentMassKilograms,
                derivedMassKilograms);
            AssertTrue(
                "second hard target emits separately classified new spall",
                secondResponse.TargetSpall.Count > 0);
            for (int index = 0; index < secondResponse.TargetSpall.Count; index++)
            {
                PhysicalProjectileState newSpall = secondResponse.TargetSpall[index];
                AssertEqual(
                    "second target new spall kind " + index.ToString(CultureInfo.InvariantCulture),
                    PhysicalProjectileKind.TargetSpall,
                    newSpall.Kind);
                AssertEqual(
                    "second target new spall material " + index.ToString(CultureInfo.InvariantCulture),
                    PhysicalMaterialClass.Glass,
                    newSpall.SourceMaterialClass);
            }
        }

        private static void ValidatePhysicalFragmentationMinimumOutput()
        {
            PhysicalProjectileState parent;
            PhysicalProjectileMaterialProfile projectileProfile;
            PhysicalTargetMaterialProfile targetProfile;
            PhysicalDeformationResponse deformation;
            CreateFragmentationScenario(
                900d,
                "collision-zero-host-fragments",
                out parent,
                out projectileProfile,
                out targetProfile,
                out deformation);
            PhysicalFragmentationResponse response = SolveFragmentationOrThrow(
                CreateValidFragmentationInput(
                    parent,
                    deformation,
                    projectileProfile,
                    targetProfile,
                    CreateTestFragmentationProfile(),
                    0,
                    "minimum-projectile-fragment",
                    "minimum-target-spall"));
            AssertEqual("zero host fragment count remains observable", 0, response.ObservedProjectileFragmentCount);
            AssertEqual("physical closure emits one projectile fragment", 1, response.ProducedProjectileFragmentCount);
            AssertNear(
                "minimum fragment owns full reserved mass",
                deformation.AvailableFragmentMassKilograms,
                response.ProjectileFragments[0].RetainedMassKilograms);
            AssertNear(
                "minimum fragment owns full reserved energy",
                deformation.AvailableFragmentEnergyJoules,
                response.ProjectileFragments[0].TranslationalKineticEnergyJoules);

            PhysicalFragmentationProfileInput noSpallInput = CreateTestFragmentationProfileInput();
            noSpallInput.TargetSpallEjectedMassFraction = 0d;
            noSpallInput.TargetSpallKineticEnergyFraction = 0d;
            PhysicalFragmentationProfile? noSpallProfile;
            PhysicalFragmentationProfileFailureReason noSpallProfileReason;
            AssertTrue(
                "no-spall profile accepted",
                PhysicalFragmentationProfile.TryCreate(
                    noSpallInput,
                    out noSpallProfile,
                    out noSpallProfileReason));
            AssertEqual(
                "no-spall profile reason",
                PhysicalFragmentationProfileFailureReason.None,
                noSpallProfileReason);
            PhysicalFragmentationResponse noSpallResponse = SolveFragmentationOrThrow(
                CreateValidFragmentationInput(
                    parent,
                    deformation,
                    projectileProfile,
                    targetProfile,
                    RequireValue("no-spall profile", noSpallProfile),
                    0,
                    "no-spall-projectile-fragment",
                    null));
            AssertEqual("no-spall profile emits no target components", 0, noSpallResponse.TargetSpall.Count);
            AssertNear("no-spall profile target mass", 0d, noSpallResponse.TargetSpallMassKilograms);
            AssertNear("no-spall profile target energy", 0d, noSpallResponse.TargetSpallEnergyJoules);
        }

        private static void ValidatePhysicalDefaultProfileCatalog()
        {
            foreach (PhysicalProjectileConstruction construction in SupportedProjectileConstructions)
            {
                PhysicalProjectileMaterialProfile? projectileProfile;
                AssertTrue(
                    "built-in projectile profile " + construction,
                    PhysicalDefaultProfileCatalog.TryGetProjectileProfile(
                        construction,
                        out projectileProfile));
                AssertTrue(
                    "built-in projectile profile value " + construction,
                    projectileProfile != null);
                AssertEqual(
                    "built-in projectile profile construction " + construction,
                    construction,
                    RequireValue("built-in projectile profile", projectileProfile).Construction);
                AssertTrue(
                    "built-in projectile drag " + construction,
                    PhysicalDefaultProfileCatalog.GetNominalDragCoefficient(construction) > 0d);
            }

            for (PhysicalMaterialClass materialClass = PhysicalMaterialClass.SoftTissue;
                materialClass <= PhysicalMaterialClass.Titanium;
                materialClass++)
            {
                PhysicalTargetMaterialProfile? targetProfile;
                PhysicalProjectileMaterialProfile? spallProjectileProfile;
                PhysicalFragmentationProfile? fragmentationProfile;
                AssertTrue(
                    "built-in target profile " + materialClass,
                    PhysicalDefaultProfileCatalog.TryGetTargetProfile(
                        materialClass,
                        out targetProfile));
                AssertTrue(
                    "built-in target profile value " + materialClass,
                    targetProfile != null);
                AssertEqual(
                    "built-in target profile class " + materialClass,
                    materialClass,
                    RequireValue("built-in target profile", targetProfile).MaterialClass);
                AssertTrue(
                    "built-in target-spall projectile profile " + materialClass,
                    PhysicalDefaultProfileCatalog.TryGetSpallProjectileProfile(
                        materialClass,
                        out spallProjectileProfile));
                AssertEqual(
                    "built-in target-spall construction " + materialClass,
                    PhysicalProjectileConstruction.TargetMaterial,
                    RequireValue(
                        "built-in target-spall projectile profile",
                        spallProjectileProfile).Construction);
                AssertTrue(
                    "built-in fragmentation profile " + materialClass,
                    PhysicalDefaultProfileCatalog.TryGetFragmentationProfile(
                        materialClass,
                        out fragmentationProfile));
                AssertTrue(
                    "built-in fragmentation profile value " + materialClass,
                    fragmentationProfile != null);
            }

            PhysicalTargetMaterialProfile? airProfile;
            AssertTrue(
                "air has no collision material profile",
                !PhysicalDefaultProfileCatalog.TryGetTargetProfile(
                    PhysicalMaterialClass.Air,
                    out airProfile));
            AssertTrue("air profile remains null", airProfile == null);
        }

        private static void ValidatePhysicalSurfaceMaterialContract()
        {
            PhysicalMaterialClass materialClass;
            AssertEqual(
                "surface metadata absent",
                PhysicalSurfaceMaterialMetadataStatus.Absent,
                PhysicalSurfaceMaterialContract.TryRead(new object(), out materialClass));
            AssertEqual("absent metadata class", PhysicalMaterialClass.Unknown, materialClass);

            var valid = new ValidPhysicalSurface("Titanium");
            AssertEqual(
                "valid surface metadata resolved",
                PhysicalSurfaceMaterialMetadataStatus.Resolved,
                PhysicalSurfaceMaterialContract.TryRead(valid, out materialClass));
            AssertEqual("valid surface material class", PhysicalMaterialClass.Titanium, materialClass);
            AssertEqual(
                "cached valid surface metadata resolved",
                PhysicalSurfaceMaterialMetadataStatus.Resolved,
                PhysicalSurfaceMaterialContract.TryRead(valid, out materialClass));

            var identified = new IdentifiedPhysicalSurface("Ceramic", "fixture/19/plate/2");
            AssertEqual(
                "identified surface metadata resolved",
                PhysicalSurfaceMaterialMetadataStatus.Resolved,
                PhysicalSurfaceMaterialContract.TryRead(
                    identified,
                    out materialClass,
                    out string surfaceIdentity));
            AssertEqual("identified surface material class", PhysicalMaterialClass.Ceramic, materialClass);
            AssertEqual("identified surface identity", "fixture/19/plate/2", surfaceIdentity);

            object[] invalidSurfaces =
            {
                new UnsupportedPhysicalSurface(),
                new PartialPhysicalSurface(),
                new WrongTypePhysicalSurface(),
                new ValidPhysicalSurface("titanium"),
                new ValidPhysicalSurface("16"),
                new EmptyIdentityPhysicalSurface(),
                new WrongIdentityTypePhysicalSurface(),
                new ThrowingPhysicalSurface()
            };
            for (int index = 0; index < invalidSurfaces.Length; index++)
            {
                AssertEqual(
                    "invalid surface metadata " + index,
                    PhysicalSurfaceMaterialMetadataStatus.Invalid,
                    PhysicalSurfaceMaterialContract.TryRead(
                        invalidSurfaces[index],
                        out materialClass));
                AssertEqual(
                    "invalid surface metadata class " + index,
                    PhysicalMaterialClass.Unknown,
                    materialClass);
            }

            PhysicalMaterialClass[] canonicalClasses =
            {
                PhysicalMaterialClass.SoftTissue,
                PhysicalMaterialClass.Bone,
                PhysicalMaterialClass.Fabric,
                PhysicalMaterialClass.Polymer,
                PhysicalMaterialClass.Wood,
                PhysicalMaterialClass.Glass,
                PhysicalMaterialClass.Aluminum,
                PhysicalMaterialClass.MildSteel,
                PhysicalMaterialClass.ArmoredSteel,
                PhysicalMaterialClass.Ceramic,
                PhysicalMaterialClass.CompositeArmor,
                PhysicalMaterialClass.Concrete,
                PhysicalMaterialClass.Soil,
                PhysicalMaterialClass.Other,
                PhysicalMaterialClass.Titanium
            };
            foreach (PhysicalMaterialClass expected in canonicalClasses)
            {
                AssertTrue(
                    "canonical surface class parses " + expected,
                    PhysicalSurfaceMaterialContract.TryParseCanonicalMaterialClass(
                        expected.ToString(),
                        out materialClass));
                AssertEqual("canonical surface class value " + expected, expected, materialClass);
            }

            AssertTrue(
                "air is not a collision target material contract value",
                !PhysicalSurfaceMaterialContract.TryParseCanonicalMaterialClass(
                    nameof(PhysicalMaterialClass.Air),
                    out materialClass));
            AssertTrue(
                "unknown is not a material contract value",
                !PhysicalSurfaceMaterialContract.TryParseCanonicalMaterialClass(
                    nameof(PhysicalMaterialClass.Unknown),
                    out materialClass));
        }

        private static void ValidatePhysicalRootProjectileFactory()
        {
            const double MassKilograms = 0.0102d;
            const double DiameterMetres = 0.00562d;
            const double DensityKilogramsPerCubicMetre = 10300d;
            var input = new PhysicalRootProjectileInput
            {
                ProjectileId = "root-projectile",
                RootShotId = "root-shot",
                DeterministicSeed = 0xAABBCCDDEEFF0011UL,
                Construction = PhysicalProjectileConstruction.SteelCoreJacketed,
                DesignClass = PhysicalProjectileDesignClass.FullMetalJacket,
                ShapeClass = PhysicalProjectileShapeClass.Spitzer,
                MassKilograms = MassKilograms,
                NominalDiameterMetres = DiameterMetres,
                MaterialDensityKilogramsPerCubicMetre = DensityKilogramsPerCubicMetre,
                DragCoefficient = 0.31d,
                PositionMetres = new PhysicalVector3(4d, 1.5d, -2d),
                VelocityMetresPerSecond = new PhysicalVector3(120d, -10d, 780d)
            };
            PhysicalProjectileState? state;
            PhysicalRootProjectileFailureReason reason;
            AssertTrue(
                "valid measured root projectile accepted",
                PhysicalRootProjectileFactory.TryCreate(input, out state, out reason));
            AssertEqual(
                "valid measured root projectile reason",
                PhysicalRootProjectileFailureReason.None,
                reason);
            PhysicalProjectileState value = RequireValue("measured root projectile", state);
            double expectedArea = Math.PI * DiameterMetres * DiameterMetres * 0.25d;
            double expectedLength = (MassKilograms / DensityKilogramsPerCubicMetre) / expectedArea;
            double expectedEnergy = 0.5d
                * MassKilograms
                * input.VelocityMetresPerSecond.MagnitudeSquared;
            AssertNear("root projected area", expectedArea, value.ProjectedAreaSquareMetres);
            AssertNear("root equivalent cylinder length", expectedLength, value.LengthMetres);
            AssertNear("root measured energy", expectedEnergy, value.TranslationalKineticEnergyJoules);
            AssertNear("root damage capability", expectedEnergy, value.DamageCapabilityJoules);
            AssertNear(
                "root penetration capability",
                expectedEnergy / expectedArea,
                value.PenetrationCapabilityJoulesPerSquareMetre);
            AssertTrue("root orientation is unit length", value.Orientation.IsUnit);
            AssertEqual("root starts with empty history", 0, value.CollisionHistory.Count);
            AssertEqual(
                "root starts as intact projectile",
                PhysicalProjectileKind.IntactProjectile,
                value.Kind);

            input.VelocityMetresPerSecond = PhysicalVector3.Zero;
            AssertTrue(
                "stationary root projectile fails open",
                !PhysicalRootProjectileFactory.TryCreate(input, out state, out reason));
            AssertTrue("stationary root returns no state", state == null);
            AssertEqual(
                "stationary root failure reason",
                PhysicalRootProjectileFailureReason.VelocityInvalid,
                reason);
        }

        private static void ValidatePhysicalEftProjection()
        {
            PhysicalProjectileState parent;
            PhysicalProjectileMaterialProfile projectileProfile;
            PhysicalTargetMaterialProfile targetProfile;
            PhysicalDeformationResponse deformation;
            CreateFragmentationScenario(
                1000d,
                "collision-eft-projection",
                out parent,
                out projectileProfile,
                out targetProfile,
                out deformation);
            PhysicalFragmentationResponse response = SolveFragmentationOrThrow(
                CreateValidFragmentationInput(
                    parent,
                    deformation,
                    projectileProfile,
                    targetProfile,
                    CreateTestFragmentationProfile(),
                    4,
                    "eft-projectile-fragment",
                    "eft-target-spall"));
            PhysicalProjectileState fragment = response.ProjectileFragments[0];
            var input = new PhysicalEftProjectionInput
            {
                Parent = parent,
                Component = fragment,
                ParentEftBallisticCoefficient = 0.42d,
                ParentEftDamage = 80d,
                ParentEftPenetrationPower = 55d,
                DamageTransferMultiplier = 0.65d,
                PenetrationTransferMultiplier = 0.55d
            };
            PhysicalEftProjectileProjection? projection;
            PhysicalEftProjectionFailureReason reason;
            AssertTrue(
                "valid physical fragment projection accepted",
                PhysicalEftProjectileProjector.TryProject(input, out projection, out reason));
            AssertEqual(
                "valid physical fragment projection reason",
                PhysicalEftProjectionFailureReason.None,
                reason);
            PhysicalEftProjectileProjection value = RequireValue(
                "physical fragment projection",
                projection);
            AssertNear(
                "fragment mass projected in grams",
                fragment.RetainedMassKilograms * 1000d,
                value.MassGrams);
            AssertNear(
                "fragment equivalent diameter projected in millimetres",
                fragment.EquivalentDiameterMetres * 1000d,
                value.EquivalentDiameterMillimetres);
            AssertNear(
                "fragment speed projected",
                fragment.SpeedMetresPerSecond,
                value.SpeedMetresPerSecond);
            AssertNear(
                "fragment unstabilized EFT drag uses physical sectional ratio",
                input.ParentEftBallisticCoefficient
                    * fragment.BallisticCoefficientKilogramsPerSquareMetre
                    / parent.BallisticCoefficientKilogramsPerSquareMetre,
                value.UnstabilizedBallisticCoefficient);
            AssertTrue(
                "fragment EFT drag never falls below host stability requirement",
                value.BallisticCoefficient >= value.UnstabilizedBallisticCoefficient);
            AssertNear(
                "fragment damage capability ratio",
                fragment.DamageCapabilityJoules / parent.DamageCapabilityJoules,
                value.DamageCapabilityRatio);
            AssertNear(
                "fragment penetration capability ratio",
                fragment.PenetrationCapabilityJoulesPerSquareMetre
                    / parent.PenetrationCapabilityJoulesPerSquareMetre,
                value.PenetrationCapabilityRatio);
            AssertNear(
                "host damage transfer remains applied after physical share",
                input.ParentEftDamage
                    * value.DamageCapabilityRatio
                    * input.DamageTransferMultiplier,
                value.Damage);
            AssertNear(
                "host penetration transfer remains applied after physical share",
                input.ParentEftPenetrationPower
                    * value.PenetrationCapabilityRatio
                    * input.PenetrationTransferMultiplier,
                value.PenetrationPower);
            PhysicalVector3 expectedDirection = RequireNormalized(
                "projected fragment direction",
                fragment.VelocityMetresPerSecond);
            AssertEqual("projected direction follows physical velocity", expectedDirection, value.Direction);
            AssertTrue(
                "projected fragment does not retain whole-projectile mass",
                !value.MassGrams.Equals(parent.RetainedMassKilograms * 1000d));
            AssertTrue(
                "projected fragment does not retain whole-projectile diameter",
                !value.EquivalentDiameterMillimetres.Equals(parent.EquivalentDiameterMetres * 1000d));
        }

        private static void ValidateTargetSpallHostTrajectoryStability()
        {
            PhysicalProjectileState parent = CreatePhysicalStateOrThrow(
                CreateValidRootInput(895.18021715102418d, 0.016100000381469727d, 0.00858d));
            PhysicalProjectileMaterialProfile projectileProfile = CreateTestProjectileProfile();
            PhysicalTargetMaterialProfile targetProfile = CreateTestTargetProfile(
                50000000d,
                0.5d);
            PhysicalDeformationInput deformationInput = CreateValidDeformationInput(
                parent,
                PhysicalCollisionOutcome.Penetrated,
                0.01d,
                0.01d,
                50000000d,
                0.5d,
                "collision-reported-target-spall",
                parent.ProjectileId);
            deformationInput.ProjectileProfile = projectileProfile;
            deformationInput.TargetProfile = targetProfile;
            PhysicalDeformationResponse deformation = SolveDeformationOrThrow(deformationInput);
            PhysicalFragmentationProfileInput profileInput = CreateTestFragmentationProfileInput();
            profileInput.TargetSpallEjectedMassFraction = 0.02d;
            profileInput.TargetSpallKineticEnergyFraction = 0.10d;
            profileInput.NominalTargetSpallMassKilograms = 0.00005d;
            profileInput.MaximumTargetSpallCount = 24;
            profileInput.TargetSpallConeHalfAngleRadians = 0.70d;
            profileInput.MinimumTargetSpallAspectRatio = 0.20d;
            profileInput.MaximumTargetSpallAspectRatio = 2.5d;
            profileInput.MinimumTargetSpallDragCoefficient = 0.8d;
            profileInput.MaximumTargetSpallDragCoefficient = 2.5d;
            profileInput.TargetSpallPenetrationEfficiency = 0.35d;
            AssertTrue(
                "reported target-spall profile accepted",
                PhysicalFragmentationProfile.TryCreate(
                    profileInput,
                    out PhysicalFragmentationProfile? fragmentationProfile,
                    out PhysicalFragmentationProfileFailureReason profileReason)
                    && profileReason == PhysicalFragmentationProfileFailureReason.None
                    && fragmentationProfile != null);
            var spallInput = new PhysicalTargetSpallInput
            {
                Parent = parent,
                DeformationResponse = deformation,
                TargetProfile = targetProfile,
                FragmentationProfile = fragmentationProfile,
                TargetSpallIdPrefix = "reported-target-spall"
            };
            AssertTrue(
                "reported target-spall scenario solves",
                PhysicalFragmentationSolver.TrySolveTargetSpall(
                    spallInput,
                    out PhysicalTargetSpallResponse? response,
                    out PhysicalTargetSpallFailureReason spallReason)
                    && spallReason == PhysicalTargetSpallFailureReason.None
                    && response != null);
            PhysicalTargetSpallResponse responseValue = RequireValue(
                "reported target-spall response",
                response);
            AssertTrue("target-spall stability scenario emits components", responseValue.Components.Count > 0);

            PhysicalProjectileStateInput representativeInput = CopyPhysicalStateToInput(
                responseValue.Components[0]);
            const double reportedMassKilograms = 0.000035466922248116548d;
            var reportedVelocity = new PhysicalVector3(
                501.03126133229051d,
                197.43785586575765d,
                193.52211157852554d);
            const double representativeDiameterMetres = 0.004d;
            double representativeAreaSquareMetres = Math.PI
                * representativeDiameterMetres
                * representativeDiameterMetres
                / 4d;
            double reportedEnergyJoules = 0.5d
                * reportedMassKilograms
                * reportedVelocity.Magnitude
                * reportedVelocity.Magnitude;
            representativeInput.ProjectileId = "reported-target-spall-12";
            representativeInput.FragmentIndex = 12;
            representativeInput.OriginalMassKilograms = reportedMassKilograms;
            representativeInput.RetainedMassKilograms = reportedMassKilograms;
            representativeInput.NominalDiameterMetres = representativeDiameterMetres;
            representativeInput.DeformedDiameterMetres = representativeDiameterMetres;
            representativeInput.ProjectedAreaSquareMetres = representativeAreaSquareMetres;
            representativeInput.LengthMetres = 0.0008d;
            representativeInput.DragCoefficient = 2.5d;
            representativeInput.VelocityMetresPerSecond = reportedVelocity;
            AssertTrue(
                "reported target-spall orientation accepted",
                PhysicalOrientation.TryFromForward(
                    reportedVelocity,
                    out PhysicalOrientation reportedOrientation));
            representativeInput.Orientation = reportedOrientation;
            representativeInput.YawAngleRadians = 0d;
            representativeInput.TumbleState = PhysicalProjectileTumbleState.Stable;
            representativeInput.PenetrationCapabilityJoulesPerSquareMetre =
                reportedEnergyJoules / representativeAreaSquareMetres * 0.35d;
            representativeInput.DamageCapabilityJoules = reportedEnergyJoules;
            PhysicalProjectileState component = CreatePhysicalStateOrThrow(representativeInput);
            var projectionInput = new PhysicalEftProjectionInput
            {
                Parent = parent,
                Component = component,
                ParentEftBallisticCoefficient = 0.477d,
                ParentEftDamage = 117.46242523193359d,
                ParentEftPenetrationPower = 85.495849609375d
            };
            AssertTrue(
                "reported target-spall projection accepted",
                PhysicalEftProjectileProjector.TryProject(
                    projectionInput,
                    out PhysicalEftProjectileProjection? stabilized,
                    out PhysicalEftProjectionFailureReason projectionReason)
                    && projectionReason == PhysicalEftProjectionFailureReason.None
                    && stabilized != null);
            PhysicalEftProjectileProjection value = RequireValue(
                "stabilized target-spall projection",
                stabilized);
            AssertTrue(
                "target-spall stability floor raises only unstable host coefficient",
                value.TrajectoryStabilityApplied
                    && value.BallisticCoefficient > value.UnstabilizedBallisticCoefficient);

            const double maximumG1DragTableValue = 0.6625d;
            const double hostStepSeconds = 0.01d;
            double speed = value.SpeedMetresPerSecond;
            double hostDragAcceleration = (1.2d * Math.PI / 8d)
                * 0.0014223d
                * maximumG1DragTableValue
                * speed
                * speed
                / value.BallisticCoefficient;
            double nextSpeed = speed - (hostDragAcceleration * hostStepSeconds);
            AssertTrue(
                "stabilized host step cannot reverse target-spall velocity",
                nextSpeed >= -0.000000001d && nextSpeed <= speed);
            double energyPermittedSpeed = Math.Sqrt(
                (2d * component.TranslationalKineticEnergyJoules)
                    / component.RetainedMassKilograms);
            AssertNear(
                "target-spall child speed is limited by assigned energy and mass",
                energyPermittedSpeed,
                value.SpeedMetresPerSecond);
            AssertNear(
                "target-spall assigned energy closes after projection",
                component.TranslationalKineticEnergyJoules,
                0.5d
                    * component.RetainedMassKilograms
                    * value.SpeedMetresPerSecond
                    * value.SpeedMetresPerSecond);
        }

        private static void ValidatePhysicalEftProjectionFallback()
        {
            PhysicalProjectileState parent;
            PhysicalProjectileMaterialProfile projectileProfile;
            PhysicalTargetMaterialProfile targetProfile;
            PhysicalDeformationResponse deformation;
            CreateFragmentationScenario(
                900d,
                "collision-eft-projection-fallback",
                out parent,
                out projectileProfile,
                out targetProfile,
                out deformation);
            PhysicalFragmentationResponse response = SolveFragmentationOrThrow(
                CreateValidFragmentationInput(
                    parent,
                    deformation,
                    projectileProfile,
                    targetProfile,
                    CreateTestFragmentationProfile(),
                    2,
                    "fallback-eft-fragment",
                    "fallback-eft-spall"));
            PhysicalProjectileState fragment = response.ProjectileFragments[0];
            var input = new PhysicalEftProjectionInput
            {
                Parent = parent,
                Component = fragment,
                ParentEftBallisticCoefficient = 0.4d,
                ParentEftDamage = 60d,
                ParentEftPenetrationPower = 40d
            };
            AssertPhysicalEftProjectionFailure(
                "missing projection input",
                null,
                PhysicalEftProjectionFailureReason.InputMissing);

            input.Component = null;
            AssertPhysicalEftProjectionFailure(
                "missing physical component",
                input,
                PhysicalEftProjectionFailureReason.ComponentMissing);
            input.Component = fragment;

            input.ParentEftBallisticCoefficient = double.NaN;
            AssertPhysicalEftProjectionFailure(
                "nonfinite EFT ballistic coefficient",
                input,
                PhysicalEftProjectionFailureReason.ParentEftValuesInvalid);
            input.ParentEftBallisticCoefficient = 0.4d;

            input.DamageTransferMultiplier = -1d;
            AssertPhysicalEftProjectionFailure(
                "negative host transfer multiplier",
                input,
                PhysicalEftProjectionFailureReason.TransferMultiplierInvalid);
            input.DamageTransferMultiplier = 1d;

            PhysicalProjectileStateInput unrelatedInput = CopyPhysicalStateToInput(fragment);
            unrelatedInput.RootShotId = "unrelated-root";
            input.Component = CreatePhysicalStateOrThrow(unrelatedInput);
            AssertPhysicalEftProjectionFailure(
                "unrelated fragment lineage",
                input,
                PhysicalEftProjectionFailureReason.LineageMismatch);
        }

        private static void ValidatePhysicalFlightState()
        {
            PhysicalProjectileState initial = CreatePhysicalStateOrThrow(
                CreateValidRootInput(800d, 0.01d, 0.01d));
            var input = new PhysicalFlightStateInput
            {
                State = initial,
                PositionMetres = new PhysicalVector3(10d, 1d, 25d),
                VelocityMetresPerSecond = new PhysicalVector3(10d, -4d, 599d)
            };
            PhysicalProjectileState? advanced;
            PhysicalFlightStateFailureReason reason;
            AssertTrue(
                "valid measured flight state accepted",
                PhysicalProjectileFlightState.TryAdvance(input, out advanced, out reason));
            AssertEqual(
                "valid measured flight state reason",
                PhysicalFlightStateFailureReason.None,
                reason);
            PhysicalProjectileState value = RequireValue("advanced physical flight state", advanced);
            double energyRatio = value.TranslationalKineticEnergyJoules
                / initial.TranslationalKineticEnergyJoules;
            AssertEqual("flight keeps projectile id", initial.ProjectileId, value.ProjectileId);
            AssertEqual("flight keeps root id", initial.RootShotId, value.RootShotId);
            AssertEqual("flight keeps collision history", initial.CollisionHistory.Count, value.CollisionHistory.Count);
            AssertEqual("flight accepts measured position", input.PositionMetres, value.PositionMetres);
            AssertEqual("flight accepts measured velocity", input.VelocityMetresPerSecond, value.VelocityMetresPerSecond);
            AssertTrue(
                "measured flight direction produces orientation",
                PhysicalOrientation.TryFromForward(
                    input.VelocityMetresPerSecond,
                    out PhysicalOrientation expectedOrientation));
            AssertNear(
                "flight orientation follows measured velocity",
                1d,
                RotateLocalForward(expectedOrientation).Dot(
                    RotateLocalForward(value.Orientation)));
            AssertNear(
                "flight scales damage capability with measured energy",
                initial.DamageCapabilityJoules * energyRatio,
                value.DamageCapabilityJoules);
            AssertNear(
                "flight scales penetration capability with measured energy",
                initial.PenetrationCapabilityJoulesPerSquareMetre * energyRatio,
                value.PenetrationCapabilityJoulesPerSquareMetre);

            PhysicalProjectileStateInput yawedInput = CreateValidRootInput(800d, 0.01d, 0.01d);
            yawedInput.DeterministicSeed = 0x0FEDCBA987654321UL;
            yawedInput.YawAngleRadians = 0.65d;
            yawedInput.TumbleState = PhysicalProjectileTumbleState.Yawing;
            AssertTrue(
                "yawed flight attitude created",
                PhysicalOrientation.TryApplyYaw(
                    yawedInput.Orientation,
                    yawedInput.YawAngleRadians,
                    yawedInput.DeterministicSeed,
                    out PhysicalOrientation yawedOrientation));
            yawedInput.Orientation = yawedOrientation;
            PhysicalProjectileState yawedInitial = CreatePhysicalStateOrThrow(yawedInput);
            var yawedFlightInput = new PhysicalFlightStateInput
            {
                State = yawedInitial,
                PositionMetres = new PhysicalVector3(2d, 3d, 4d),
                VelocityMetresPerSecond = new PhysicalVector3(-120d, 40d, 610d)
            };
            AssertTrue(
                "yawed measured flight state accepted",
                PhysicalProjectileFlightState.TryAdvance(
                    yawedFlightInput,
                    out PhysicalProjectileState? yawedAdvanced,
                    out _));
            PhysicalProjectileState yawedValue = RequireValue(
                "yawed advanced physical flight state",
                yawedAdvanced);
            PhysicalVector3 yawedBodyAxis = RotateLocalForward(yawedValue.Orientation);
            PhysicalVector3 yawedVelocityDirection = RequireNormalized(
                "yawed advanced velocity",
                yawedValue.VelocityMetresPerSecond);
            AssertNear(
                "flight transport preserves physical yaw",
                Math.Cos(yawedValue.YawAngleRadians),
                yawedBodyAxis.Dot(yawedVelocityDirection));
            AssertTrue(
                "opposite flight transport remains valid",
                PhysicalOrientation.TryTransport(
                    PhysicalOrientation.Identity,
                    new PhysicalVector3(0d, 0d, 1d),
                    new PhysicalVector3(0d, 0d, -1d),
                    out PhysicalOrientation reversedOrientation));
            AssertEqual(
                "opposite flight transport rotates local forward",
                new PhysicalVector3(0d, 0d, -1d),
                RotateLocalForward(reversedOrientation));

            input.VelocityMetresPerSecond = PhysicalVector3.Zero;
            PhysicalProjectileState? rejected;
            AssertTrue(
                "zero measured velocity fails open",
                !PhysicalProjectileFlightState.TryAdvance(input, out rejected, out reason));
            AssertTrue("zero measured velocity returns no state", rejected == null);
            AssertEqual(
                "zero measured velocity reason",
                PhysicalFlightStateFailureReason.VelocityInvalid,
                reason);
        }

        private static void AssertPhysicalEftProjectionFailure(
            string name,
            PhysicalEftProjectionInput? input,
            PhysicalEftProjectionFailureReason expectedReason)
        {
            PhysicalEftProjectileProjection? projection;
            PhysicalEftProjectionFailureReason actualReason;
            bool success = PhysicalEftProjectileProjector.TryProject(
                input,
                out projection,
                out actualReason);
            AssertTrue(name + " fails open", !success);
            AssertTrue(name + " returns no projection", projection == null);
            AssertEqual(name + " reason", expectedReason, actualReason);
        }

        private static void ValidatePhysicalFragmentationFallback()
        {
            PhysicalProjectileState parent;
            PhysicalProjectileMaterialProfile projectileProfile;
            PhysicalTargetMaterialProfile targetProfile;
            PhysicalDeformationResponse deformation;
            CreateFragmentationScenario(
                1000d,
                "collision-fragmentation-fallback",
                out parent,
                out projectileProfile,
                out targetProfile,
                out deformation);
            PhysicalFragmentationProfile profile = CreateTestFragmentationProfile();

            PhysicalFragmentationInput input = CreateValidFragmentationInput(
                parent,
                deformation,
                projectileProfile,
                targetProfile,
                profile,
                -1,
                "fallback-fragment",
                "fallback-spall");
            AssertFragmentationFailure(
                "negative observed host count",
                input,
                PhysicalFragmentationFailureReason.ObservedFragmentCountInvalid);

            input = CreateValidFragmentationInput(
                parent,
                deformation,
                projectileProfile,
                targetProfile,
                profile,
                3,
                string.Empty,
                "fallback-spall");
            AssertFragmentationFailure(
                "missing projectile id prefix",
                input,
                PhysicalFragmentationFailureReason.ProjectileIdPrefixMissing);

            input = CreateValidFragmentationInput(
                parent,
                deformation,
                projectileProfile,
                targetProfile,
                profile,
                3,
                "fallback-fragment",
                null);
            AssertFragmentationFailure(
                "missing target spall id prefix",
                input,
                PhysicalFragmentationFailureReason.TargetSpallIdPrefixMissing);

            PhysicalDeformationInput penetrationInput = CreateValidDeformationInput(
                parent,
                PhysicalCollisionOutcome.Penetrated,
                0.01d,
                0.01d,
                50000000d,
                0.5d,
                "collision-nonfragment-fallback",
                parent.ProjectileId);
            penetrationInput.ProjectileProfile = projectileProfile;
            penetrationInput.TargetProfile = targetProfile;
            input = CreateValidFragmentationInput(
                parent,
                SolveDeformationOrThrow(penetrationInput),
                projectileProfile,
                targetProfile,
                profile,
                3,
                "fallback-fragment",
                "fallback-spall");
            AssertFragmentationFailure(
                "nonfragment host outcome",
                input,
                PhysicalFragmentationFailureReason.FragmentationOutcomeMissing);
        }

        private static void ValidatePhysicalFragmentationStressSweep()
        {
            const int caseCount = 4096;
            PhysicalProjectileMaterialProfile projectileProfile = CreateTestProjectileProfile();
            PhysicalFragmentationProfile fragmentationProfile = CreateTestFragmentationProfile();
            var random = new DeterministicProjectileRandom(
                0x465241474D454E54UL,
                0x50524F5045525459UL);
            for (int index = 0; index < caseCount; index++)
            {
                double speedMetresPerSecond = Lerp(250d, 1400d, random.NextUnitDouble());
                PhysicalProjectileState parent = CreatePhysicalStateOrThrow(
                    CreateValidRootInput(speedMetresPerSecond, 0.01d, 0.01d));
                double effectivePathMetres = Lerp(0.001d, 0.02d, random.NextUnitDouble());
                double physicalThicknessMetres = effectivePathMetres
                    * Lerp(0.05d, 1d, random.NextUnitDouble());
                double resistancePressurePascals = Lerp(
                    100000d,
                    20000000d,
                    random.NextUnitDouble());
                double fractureCoupling = Lerp(0.05d, 0.8d, random.NextUnitDouble());
                PhysicalTargetMaterialProfile targetProfile = CreateTestTargetProfile(
                    resistancePressurePascals,
                    fractureCoupling);
                PhysicalDeformationInput deformationInput = CreateValidDeformationInput(
                    parent,
                    PhysicalCollisionOutcome.Fragmented,
                    physicalThicknessMetres,
                    effectivePathMetres,
                    resistancePressurePascals,
                    fractureCoupling,
                    "collision-fragment-property-" + index.ToString(CultureInfo.InvariantCulture),
                    parent.ProjectileId);
                deformationInput.ProjectileProfile = projectileProfile;
                deformationInput.TargetProfile = targetProfile;
                double impactAngleRadians = Lerp(
                    0d,
                    Math.PI * 0.4722222222222222d,
                    random.NextUnitDouble());
                deformationInput.SurfaceNormal = new PhysicalVector3(
                    Math.Sin(impactAngleRadians),
                    0d,
                    -Math.Cos(impactAngleRadians));
                double azimuthRadians = Math.PI * 2d * random.NextUnitDouble();
                double forwardShare = Lerp(0.05d, 1d, random.NextUnitDouble());
                double lateralShare = Math.Sqrt(1d - (forwardShare * forwardShare));
                deformationInput.ObservedOutgoingDirection = new PhysicalVector3(
                    lateralShare * Math.Cos(azimuthRadians),
                    lateralShare * Math.Sin(azimuthRadians),
                    forwardShare);
                PhysicalDeformationResponse deformation = SolveDeformationOrThrow(
                    deformationInput);
                PhysicalFragmentationInput input = CreateValidFragmentationInput(
                    parent,
                    deformation,
                    projectileProfile,
                    targetProfile,
                    fragmentationProfile,
                    index % 49,
                    "property-projectile-" + index.ToString(CultureInfo.InvariantCulture),
                    "property-spall-" + index.ToString(CultureInfo.InvariantCulture));
                PhysicalFragmentationResponse response = SolveFragmentationOrThrow(input);
                string caseName = index.ToString(CultureInfo.InvariantCulture);
                AssertTrue("property projectile fragments present " + caseName, response.ProjectileFragments.Count > 0);
                AssertTrue(
                    "property projectile count bounded " + caseName,
                    response.ProjectileFragments.Count <= fragmentationProfile.MaximumProjectileFragmentCount);
                AssertTrue(
                    "property target spall count bounded " + caseName,
                    response.TargetSpall.Count <= fragmentationProfile.MaximumTargetSpallCount);
                AssertNear(
                    "property fragmentation mass closure " + caseName,
                    parent.RetainedMassKilograms,
                    response.ConservationResult.AllocatedParentMassKilograms);
                AssertNear(
                    "property fragmentation energy closure " + caseName,
                    parent.TranslationalKineticEnergyJoules,
                    response.ConservationResult.ModeledLossEnergyJoules
                        + response.ConservationResult.ChildEnergyJoules);
                AssertFiniteNonNegative(
                    "property target spall mass " + caseName,
                    response.TargetSpallMassKilograms);
                AssertFiniteNonNegative(
                    "property target spall energy " + caseName,
                    response.TargetSpallEnergyJoules);

                if ((index & 255) == 0)
                {
                    AssertFragmentationEquivalent(
                        "property deterministic response " + caseName,
                        response,
                        SolveFragmentationOrThrow(input));
                }
            }
        }

        private static double Lerp(double minimum, double maximum, double fraction)
        {
            return minimum + ((maximum - minimum) * fraction);
        }

        private static void AssertFiniteNonNegative(string name, double value)
        {
            AssertTrue(name, !double.IsNaN(value) && !double.IsInfinity(value) && value >= 0d);
        }

        private static PhysicalProjectileMaterialProfileInput CreateTestProjectileProfileInput()
        {
            return new PhysicalProjectileMaterialProfileInput
            {
                ProfileId = "test-steel-core",
                Construction = PhysicalProjectileConstruction.SteelCoreJacketed,
                DensityKilogramsPerCubicMetre = 8000d,
                PlasticDeformationWorkJoulesPerCubicMetre = 100000000d,
                FractureEnergyJoulesPerKilogram = 20000d,
                Ductility = 0.8d,
                Brittleness = 0.4d,
                DeformationEnergyCoupling = 0.5d,
                MaximumDiameterExpansionRatio = 1.5d,
                MinimumFragmentMassFraction = 0.2d,
                MaximumFragmentMassFraction = 0.6d,
                MaximumPenetrationShapePenalty = 0.5d,
                MaximumDragCoefficientMultiplier = 2d,
                MaximumDeformationYawRadians = 0.4d,
                YawingThresholdRadians = 0.1d,
                TumblingThresholdRadians = 0.3d
            };
        }

        private static PhysicalTargetMaterialProfileInput CreateTestTargetProfileInput(
            double resistancePressurePascals,
            double fractureCoupling)
        {
            return new PhysicalTargetMaterialProfileInput
            {
                ProfileId = "test-armored-steel",
                MaterialClass = PhysicalMaterialClass.ArmoredSteel,
                DensityKilogramsPerCubicMetre = 7850d,
                EffectiveResistancePressurePascals = resistancePressurePascals,
                ProjectileDeformationCoupling = 0.5d,
                ProjectileFractureCoupling = fractureCoupling,
                HeatLossFraction = 0.2d
            };
        }

        private static PhysicalProjectileMaterialProfile CreateTestProjectileProfile()
        {
            PhysicalProjectileMaterialProfile? profile;
            PhysicalMaterialProfileFailureReason reason;
            if (!PhysicalProjectileMaterialProfile.TryCreate(
                CreateTestProjectileProfileInput(),
                out profile,
                out reason)
                || profile == null)
            {
                throw new InvalidOperationException(
                    "Test projectile material profile creation failed: " + reason + ".");
            }

            return profile;
        }

        private static PhysicalTargetMaterialProfile CreateTestTargetProfile(
            double resistancePressurePascals,
            double fractureCoupling)
        {
            PhysicalTargetMaterialProfile? profile;
            PhysicalMaterialProfileFailureReason reason;
            if (!PhysicalTargetMaterialProfile.TryCreate(
                CreateTestTargetProfileInput(resistancePressurePascals, fractureCoupling),
                out profile,
                out reason)
                || profile == null)
            {
                throw new InvalidOperationException(
                    "Test target material profile creation failed: " + reason + ".");
            }

            return profile;
        }

        private static PhysicalFragmentationProfileInput CreateTestFragmentationProfileInput()
        {
            return new PhysicalFragmentationProfileInput
            {
                MaximumProjectileFragmentCount = 16,
                MinimumProjectileFragmentMassKilograms = 0.00005d,
                ProjectileConeHalfAngleRadians = Math.PI / 6d,
                MinimumProjectileAspectRatio = 0.4d,
                MaximumProjectileAspectRatio = 2.4d,
                MinimumProjectileDragMultiplier = 0.8d,
                MaximumProjectileDragMultiplier = 2.2d,
                ProjectilePenetrationEfficiency = 0.7d,
                TargetSpallEjectedMassFraction = 0.08d,
                TargetSpallKineticEnergyFraction = 0.1d,
                NominalTargetSpallMassKilograms = 0.00005d,
                MaximumTargetSpallCount = 32,
                TargetSpallConeHalfAngleRadians = Math.PI / 3d,
                MinimumTargetSpallAspectRatio = 0.1d,
                MaximumTargetSpallAspectRatio = 0.8d,
                MinimumTargetSpallDragCoefficient = 0.9d,
                MaximumTargetSpallDragCoefficient = 1.8d,
                TargetSpallPenetrationEfficiency = 0.4d
            };
        }

        private static PhysicalFragmentationProfile CreateTestFragmentationProfile()
        {
            PhysicalFragmentationProfile? profile;
            PhysicalFragmentationProfileFailureReason reason;
            if (!PhysicalFragmentationProfile.TryCreate(
                CreateTestFragmentationProfileInput(),
                out profile,
                out reason)
                || profile == null)
            {
                throw new InvalidOperationException(
                    "Test fragmentation profile creation failed: " + reason + ".");
            }

            return profile;
        }

        private static void AssertFragmentationProfileFailure(
            string name,
            PhysicalFragmentationProfileInput input,
            PhysicalFragmentationProfileFailureReason expectedReason)
        {
            PhysicalFragmentationProfile? profile;
            PhysicalFragmentationProfileFailureReason actualReason;
            bool success = PhysicalFragmentationProfile.TryCreate(
                input,
                out profile,
                out actualReason);
            AssertTrue(name + " fails", !success);
            AssertTrue(name + " returns no profile", profile == null);
            AssertEqual(name + " reason", expectedReason, actualReason);
        }

        private static void CreateFragmentationScenario(
            double speedMetresPerSecond,
            string collisionId,
            out PhysicalProjectileState parent,
            out PhysicalProjectileMaterialProfile projectileProfile,
            out PhysicalTargetMaterialProfile targetProfile,
            out PhysicalDeformationResponse deformation)
        {
            parent = CreatePhysicalStateOrThrow(
                CreateValidRootInput(speedMetresPerSecond, 0.01d, 0.01d));
            projectileProfile = CreateTestProjectileProfile();
            targetProfile = CreateTestTargetProfile(50000000d, 0.5d);
            PhysicalDeformationInput deformationInput = CreateValidDeformationInput(
                parent,
                PhysicalCollisionOutcome.Fragmented,
                0.01d,
                0.01d,
                50000000d,
                0.5d,
                collisionId,
                parent.ProjectileId);
            deformationInput.ProjectileProfile = projectileProfile;
            deformationInput.TargetProfile = targetProfile;
            deformation = SolveDeformationOrThrow(deformationInput);
        }

        private static PhysicalFragmentationInput CreateValidFragmentationInput(
            PhysicalProjectileState parent,
            PhysicalDeformationResponse deformation,
            PhysicalProjectileMaterialProfile projectileProfile,
            PhysicalTargetMaterialProfile targetProfile,
            PhysicalFragmentationProfile fragmentationProfile,
            int observedProjectileFragmentCount,
            string projectileIdPrefix,
            string? targetSpallIdPrefix)
        {
            return new PhysicalFragmentationInput
            {
                Parent = parent,
                DeformationResponse = deformation,
                ProjectileProfile = projectileProfile,
                TargetProfile = targetProfile,
                FragmentationProfile = fragmentationProfile,
                ObservedProjectileFragmentCount = observedProjectileFragmentCount,
                ProjectileIdPrefix = projectileIdPrefix,
                TargetSpallIdPrefix = targetSpallIdPrefix
            };
        }

        private static PhysicalFragmentationResponse SolveFragmentationOrThrow(
            PhysicalFragmentationInput input)
        {
            PhysicalFragmentationResponse? response;
            PhysicalFragmentationFailureReason reason;
            if (!PhysicalFragmentationSolver.TrySolve(input, out response, out reason)
                || response == null)
            {
                throw new InvalidOperationException(
                    "Valid physical fragmentation calculation failed: " + reason + ".");
            }

            return response;
        }

        private static void AssertFragmentationFailure(
            string name,
            PhysicalFragmentationInput input,
            PhysicalFragmentationFailureReason expectedReason)
        {
            PhysicalFragmentationResponse? response;
            PhysicalFragmentationFailureReason actualReason;
            bool success = PhysicalFragmentationSolver.TrySolve(
                input,
                out response,
                out actualReason);
            AssertTrue(name + " fails open", !success);
            AssertTrue(name + " returns no response", response == null);
            AssertEqual(name + " reason", expectedReason, actualReason);
        }

        private static void AssertPhysicalSecondary(
            string name,
            PhysicalProjectileState state,
            PhysicalProjectileState parent,
            PhysicalDeformationResponse deformation,
            double densityKilogramsPerCubicMetre,
            double coneHalfAngleRadians,
            PhysicalVector3 coneAxis,
            HashSet<string> identities,
            HashSet<int> indices)
        {
            AssertTrue(name + " identity is unique", identities.Add(state.ProjectileId));
            AssertTrue(name + " fragment index is unique", indices.Add(state.FragmentIndex));
            AssertEqual(name + " root lineage", parent.RootShotId, state.RootShotId);
            AssertEqual(name + " parent lineage", parent.ProjectileId, state.ParentProjectileId);
            AssertEqual(name + " source lineage", parent.ProjectileId, state.SourceProjectileId);
            AssertEqual(name + " source material", deformation.CollisionRecord.MaterialId, state.SourceMaterialId);
            AssertEqual(
                name + " source material class",
                deformation.CollisionRecord.MaterialClass,
                state.SourceMaterialClass);
            AssertEqual(
                name + " source collision",
                deformation.CollisionRecord.CollisionId,
                state.SourceCollisionId);
            AssertEqual(
                name + " starts at deformation output face",
                deformation.OutputPositionMetres,
                state.PositionMetres);
            AssertEqual(
                name + " fragment generation",
                parent.FragmentGeneration + 1,
                state.FragmentGeneration);
            AssertEqual(
                name + " inherited history count",
                parent.CollisionHistory.Count + 1,
                state.CollisionHistory.Count);
            for (int historyIndex = 0; historyIndex < parent.CollisionHistory.Count; historyIndex++)
            {
                AssertEqual(
                    name + " inherited history " + historyIndex.ToString(CultureInfo.InvariantCulture),
                    parent.CollisionHistory[historyIndex],
                    state.CollisionHistory[historyIndex]);
            }

            AssertEqual(
                name + " appended fragmentation collision",
                deformation.CollisionRecord,
                state.CollisionHistory[state.CollisionHistory.Count - 1]);
            AssertTrue(name + " mass is finite and positive", IsFinite(state.RetainedMassKilograms) && state.RetainedMassKilograms > 0d);
            AssertNear(name + " starts without mass loss", state.OriginalMassKilograms, state.RetainedMassKilograms);
            AssertTrue(name + " diameter is finite and positive", IsFinite(state.DeformedDiameterMetres) && state.DeformedDiameterMetres > 0d);
            AssertTrue(name + " area is finite and positive", IsFinite(state.ProjectedAreaSquareMetres) && state.ProjectedAreaSquareMetres > 0d);
            AssertTrue(name + " length is finite and positive", IsFinite(state.LengthMetres) && state.LengthMetres > 0d);
            AssertTrue(name + " drag is finite and positive", IsFinite(state.DragCoefficient) && state.DragCoefficient > 0d);
            AssertTrue(name + " speed is finite and positive", IsFinite(state.SpeedMetresPerSecond) && state.SpeedMetresPerSecond > 0d);
            AssertTrue(name + " energy is finite and positive", IsFinite(state.TranslationalKineticEnergyJoules) && state.TranslationalKineticEnergyJoules > 0d);
            AssertTrue(name + " ballistic coefficient is finite and positive", IsFinite(state.BallisticCoefficientKilogramsPerSquareMetre) && state.BallisticCoefficientKilogramsPerSquareMetre > 0d);
            AssertNear(
                name + " ballistic coefficient uses own mass area and drag",
                state.RetainedMassKilograms
                    / (state.DragCoefficient * state.ProjectedAreaSquareMetres),
                state.BallisticCoefficientKilogramsPerSquareMetre);
            AssertNear(
                name + " geometry volume closes mass and density",
                state.RetainedMassKilograms / densityKilogramsPerCubicMetre,
                Math.PI
                    * state.DeformedDiameterMetres
                    * state.DeformedDiameterMetres
                    * 0.25d
                    * state.LengthMetres);
            AssertTrue(name + " orientation is unit", state.Orientation.IsUnit);
            PhysicalVector3 velocityDirection = RequireNormalized(
                name + " velocity direction",
                state.VelocityMetresPerSecond);
            PhysicalVector3 normalizedAxis = RequireNormalized(name + " cone axis", coneAxis);
            AssertTrue(
                name + " direction remains inside configured cone",
                velocityDirection.Dot(normalizedAxis)
                    >= Math.Cos(coneHalfAngleRadians) - 0.000000000001d);
            PhysicalVector3 renderedForward = RotateLocalForward(state.Orientation);
            AssertNear(
                name + " orientation and velocity reproduce yaw",
                Math.Cos(state.YawAngleRadians),
                renderedForward.Dot(velocityDirection));
            AssertTrue(
                name + " nonzero yaw changes physical attitude",
                renderedForward != velocityDirection);
            AssertTrue(
                name + " penetration capability is finite and nonnegative",
                IsFinite(state.PenetrationCapabilityJoulesPerSquareMetre)
                    && state.PenetrationCapabilityJoulesPerSquareMetre >= 0d);
            AssertTrue(
                name + " damage capability is bounded by kinetic energy",
                IsFinite(state.DamageCapabilityJoules)
                    && state.DamageCapabilityJoules >= 0d
                    && state.DamageCapabilityJoules
                        <= state.TranslationalKineticEnergyJoules + 0.000000001d);
            AssertEqual(name + " terminal state", PhysicalProjectileTerminalState.Exited, state.TerminalState);
            AssertEqual(name + " render state", PhysicalProjectileRenderState.NotRendered, state.RenderState);
        }

        private static void AssertFragmentationEquivalent(
            string name,
            PhysicalFragmentationResponse expected,
            PhysicalFragmentationResponse actual)
        {
            AssertTrue(name + " primary identity", ReferenceEquals(expected.PrimaryState, actual.PrimaryState));
            AssertEqual(name + " observed count", expected.ObservedProjectileFragmentCount, actual.ObservedProjectileFragmentCount);
            AssertEqual(name + " projectile count", expected.ProjectileFragments.Count, actual.ProjectileFragments.Count);
            AssertEqual(name + " target spall count", expected.TargetSpall.Count, actual.TargetSpall.Count);
            AssertNear(name + " target spall mass", expected.TargetSpallMassKilograms, actual.TargetSpallMassKilograms);
            AssertNear(name + " target spall energy", expected.TargetSpallEnergyJoules, actual.TargetSpallEnergyJoules);
            AssertNear(
                name + " effective loss",
                expected.EffectiveLossBudget.TotalLossJoules,
                actual.EffectiveLossBudget.TotalLossJoules);
            for (int index = 0; index < expected.AllSecondaryComponents.Count; index++)
            {
                AssertPhysicalStateEquivalent(
                    name + " component " + index.ToString(CultureInfo.InvariantCulture),
                    expected.AllSecondaryComponents[index],
                    actual.AllSecondaryComponents[index]);
            }
        }

        private static void AssertPhysicalStateEquivalent(
            string name,
            PhysicalProjectileState expected,
            PhysicalProjectileState actual)
        {
            AssertEqual(name + " kind", expected.Kind, actual.Kind);
            AssertEqual(name + " id", expected.ProjectileId, actual.ProjectileId);
            AssertEqual(name + " root", expected.RootShotId, actual.RootShotId);
            AssertEqual(name + " parent", expected.ParentProjectileId, actual.ParentProjectileId);
            AssertEqual(name + " source", expected.SourceProjectileId, actual.SourceProjectileId);
            AssertEqual(name + " material", expected.SourceMaterialId, actual.SourceMaterialId);
            AssertEqual(name + " material class", expected.SourceMaterialClass, actual.SourceMaterialClass);
            AssertEqual(name + " collision", expected.SourceCollisionId, actual.SourceCollisionId);
            AssertEqual(name + " index", expected.FragmentIndex, actual.FragmentIndex);
            AssertEqual(name + " generation", expected.FragmentGeneration, actual.FragmentGeneration);
            AssertEqual(name + " seed", expected.DeterministicSeed, actual.DeterministicSeed);
            AssertEqual(name + " construction", expected.Construction, actual.Construction);
            AssertEqual(name + " design", expected.DesignClass, actual.DesignClass);
            AssertEqual(name + " shape", expected.ShapeClass, actual.ShapeClass);
            AssertNear(name + " original mass", expected.OriginalMassKilograms, actual.OriginalMassKilograms);
            AssertNear(name + " retained mass", expected.RetainedMassKilograms, actual.RetainedMassKilograms);
            AssertNear(name + " diameter", expected.DeformedDiameterMetres, actual.DeformedDiameterMetres);
            AssertNear(name + " area", expected.ProjectedAreaSquareMetres, actual.ProjectedAreaSquareMetres);
            AssertNear(name + " length", expected.LengthMetres, actual.LengthMetres);
            AssertNear(name + " drag", expected.DragCoefficient, actual.DragCoefficient);
            AssertEqual(name + " position", expected.PositionMetres, actual.PositionMetres);
            AssertEqual(name + " velocity", expected.VelocityMetresPerSecond, actual.VelocityMetresPerSecond);
            AssertEqual(name + " orientation", expected.Orientation, actual.Orientation);
            AssertNear(name + " yaw", expected.YawAngleRadians, actual.YawAngleRadians);
            AssertNear(name + " penetration", expected.PenetrationCapabilityJoulesPerSquareMetre, actual.PenetrationCapabilityJoulesPerSquareMetre);
            AssertNear(name + " damage", expected.DamageCapabilityJoules, actual.DamageCapabilityJoules);
            AssertEqual(name + " terminal", expected.TerminalState, actual.TerminalState);
            AssertEqual(name + " render", expected.RenderState, actual.RenderState);
            AssertEqual(name + " history count", expected.CollisionHistory.Count, actual.CollisionHistory.Count);
            for (int index = 0; index < expected.CollisionHistory.Count; index++)
            {
                AssertEqual(
                    name + " history " + index.ToString(CultureInfo.InvariantCulture),
                    expected.CollisionHistory[index],
                    actual.CollisionHistory[index]);
            }
        }

        private static PhysicalProjectileState?[] ToNullableStates(
            IReadOnlyList<PhysicalProjectileState> states)
        {
            var result = new PhysicalProjectileState?[states.Count];
            for (int index = 0; index < states.Count; index++)
            {
                result[index] = states[index];
            }

            return result;
        }

        private static PhysicalVector3 RequireNormalized(string name, PhysicalVector3 vector)
        {
            PhysicalVector3 normalized;
            if (!vector.TryNormalize(out normalized))
            {
                throw new InvalidOperationException(name + " could not be normalized.");
            }

            return normalized;
        }

        private static PhysicalVector3 RotateLocalForward(PhysicalOrientation orientation)
        {
            return new PhysicalVector3(
                2d * ((orientation.X * orientation.Z) + (orientation.W * orientation.Y)),
                2d * ((orientation.Y * orientation.Z) - (orientation.W * orientation.X)),
                1d - (2d * ((orientation.X * orientation.X) + (orientation.Y * orientation.Y))));
        }

        private static PhysicalDeformationInput CreateValidDeformationInput(
            PhysicalProjectileState parent,
            PhysicalCollisionOutcome outcome,
            double physicalThicknessMetres,
            double effectivePathLengthMetres,
            double resistancePressurePascals,
            double fractureCoupling,
            string collisionId,
            string outputProjectileId)
        {
            return new PhysicalDeformationInput
            {
                Parent = parent,
                ProjectileProfile = CreateTestProjectileProfile(),
                TargetProfile = CreateTestTargetProfile(
                    resistancePressurePascals,
                    fractureCoupling),
                CollisionId = collisionId,
                OutputProjectileId = outputProjectileId,
                ImpactPositionMetres = new PhysicalVector3(1d, 2d, 4d),
                SurfaceNormal = new PhysicalVector3(0d, 0d, -1d),
                PhysicalThicknessMetres = physicalThicknessMetres,
                EffectivePathLengthMetres = effectivePathLengthMetres,
                ObservedOutcome = outcome,
                ObservedOutgoingDirection = new PhysicalVector3(0d, 0d, 1d)
            };
        }

        private static PhysicalDeformationResponse SolveDeformationOrThrow(
            PhysicalDeformationInput input)
        {
            PhysicalDeformationResponse? response;
            PhysicalDeformationFailureReason reason;
            if (!PhysicalDeformationSolver.TrySolve(input, out response, out reason)
                || response == null)
            {
                throw new InvalidOperationException(
                    "Valid physical deformation calculation failed: " + reason + ".");
            }

            return response;
        }

        private static void AssertDeformationFailure(
            string name,
            PhysicalDeformationInput input,
            PhysicalDeformationFailureReason expectedReason)
        {
            PhysicalDeformationResponse? response;
            PhysicalDeformationFailureReason actualReason;
            bool success = PhysicalDeformationSolver.TrySolve(
                input,
                out response,
                out actualReason);
            AssertTrue(name + " fails open", !success);
            AssertTrue(name + " returns no response", response == null);
            AssertEqual(name + " reason", expectedReason, actualReason);
        }

        private static PhysicalCollisionRecordInput CreateValidCollisionInput()
        {
            return new PhysicalCollisionRecordInput
            {
                CollisionId = "collision-1",
                MaterialId = "armor-steel",
                MaterialClass = PhysicalMaterialClass.ArmoredSteel,
                Sequence = 0,
                PositionMetres = new PhysicalVector3(1d, 2d, 3d),
                IncomingVelocityMetresPerSecond = new PhysicalVector3(0d, 0d, 1000d),
                OutgoingVelocityMetresPerSecond = new PhysicalVector3(0d, 0d, 800d),
                IncomingTranslationalEnergyJoules = 5000d,
                OutgoingTranslationalEnergyJoules = 3040d,
                ImpactAngleRadians = 0.2d,
                EffectivePathLengthMetres = 0.012d,
                Outcome = PhysicalCollisionOutcome.Penetrated
            };
        }

        private static PhysicalCollisionRecordInput CreateCollisionInput(
            int sequence,
            PhysicalCollisionOutcome outcome)
        {
            PhysicalCollisionRecordInput input = CreateValidCollisionInput();
            input.Sequence = sequence;
            input.Outcome = outcome;
            return input;
        }

        private static PhysicalCollisionRecord CreateValidCollisionRecord()
        {
            return CreateCollisionRecordOrThrow(CreateValidCollisionInput());
        }

        private static PhysicalCollisionRecord CreateCollisionRecordOrThrow(
            PhysicalCollisionRecordInput input)
        {
            PhysicalCollisionRecord? record;
            PhysicalCollisionRecordFailureReason reason;
            if (!PhysicalCollisionRecord.TryCreate(input, out record, out reason)
                || record == null)
            {
                throw new InvalidOperationException(
                    "Valid collision record creation failed: " + reason + ".");
            }

            return record;
        }

        private static PhysicalCollisionRecordInput CopyCollisionToInput(
            PhysicalCollisionRecord record)
        {
            return new PhysicalCollisionRecordInput
            {
                CollisionId = record.CollisionId,
                MaterialId = record.MaterialId,
                MaterialClass = record.MaterialClass,
                Sequence = record.Sequence,
                PositionMetres = record.PositionMetres,
                IncomingVelocityMetresPerSecond = record.IncomingVelocityMetresPerSecond,
                OutgoingVelocityMetresPerSecond = record.OutgoingVelocityMetresPerSecond,
                IncomingTranslationalEnergyJoules = record.IncomingTranslationalEnergyJoules,
                OutgoingTranslationalEnergyJoules = record.OutgoingTranslationalEnergyJoules,
                ImpactAngleRadians = record.ImpactAngleRadians,
                EffectivePathLengthMetres = record.EffectivePathLengthMetres,
                Outcome = record.Outcome
            };
        }

        private static int ResolveCollisionOrdinalFromSequence(int? recordSequence)
        {
            return recordSequence ?? 0;
        }

        private static ResolvedLifecycleSemantics CalculateResolvedLifecycleSemantics(
            PhysicalCollisionOutcome outcome,
            bool replaced)
        {
            bool ballisticTerminal = outcome == PhysicalCollisionOutcome.Stopped;
            return new ResolvedLifecycleSemantics
            {
                BallisticTerminal = ballisticTerminal,
                LifecycleTerminal = false,
                ResolutionKnown = true,
                Continued = outcome != PhysicalCollisionOutcome.Stopped,
                Replaced = replaced
            };
        }

        private sealed class ResolvedLifecycleSemantics
        {
            internal bool BallisticTerminal { get; set; }

            internal bool LifecycleTerminal { get; set; }

            internal bool ResolutionKnown { get; set; }

            internal bool Continued { get; set; }

            internal bool Replaced { get; set; }
        }

        private static PhysicalProjectileStateInput CopyPhysicalStateToInput(
            PhysicalProjectileState state)
        {
            return new PhysicalProjectileStateInput
            {
                Kind = state.Kind,
                ProjectileId = state.ProjectileId,
                RootShotId = state.RootShotId,
                ParentProjectileId = state.ParentProjectileId,
                SourceProjectileId = state.SourceProjectileId,
                SourceMaterialId = state.SourceMaterialId,
                SourceMaterialClass = state.SourceMaterialClass,
                SourceCollisionId = state.SourceCollisionId,
                FragmentIndex = state.FragmentIndex,
                FragmentGeneration = state.FragmentGeneration,
                DeterministicSeed = state.DeterministicSeed,
                Construction = state.Construction,
                DesignClass = state.DesignClass,
                ShapeClass = state.ShapeClass,
                OriginalMassKilograms = state.OriginalMassKilograms,
                RetainedMassKilograms = state.RetainedMassKilograms,
                NominalDiameterMetres = state.NominalDiameterMetres,
                DeformedDiameterMetres = state.DeformedDiameterMetres,
                ProjectedAreaSquareMetres = state.ProjectedAreaSquareMetres,
                LengthMetres = state.LengthMetres,
                DragCoefficient = state.DragCoefficient,
                PositionMetres = state.PositionMetres,
                VelocityMetresPerSecond = state.VelocityMetresPerSecond,
                Orientation = state.Orientation,
                YawAngleRadians = state.YawAngleRadians,
                TumbleState = state.TumbleState,
                PenetrationCapabilityJoulesPerSquareMetre =
                    state.PenetrationCapabilityJoulesPerSquareMetre,
                DamageCapabilityJoules = state.DamageCapabilityJoules,
                TerminalState = state.TerminalState,
                RenderState = state.RenderState,
                CollisionHistory = state.CollisionHistory
            };
        }

        private static PhysicalProjectileStateInput CreateValidRootInput(
            double speedMetresPerSecond,
            double originalMassKilograms,
            double retainedMassKilograms)
        {
            const double diameterMetres = 0.00762d;
            double area;
            if (!PhysicalProjectileGeometry.TryCalculateCircularAreaSquareMetres(diameterMetres, out area))
            {
                throw new InvalidOperationException("Could not calculate root projectile area.");
            }

            double energyJoules = 0.5d
                * retainedMassKilograms
                * speedMetresPerSecond
                * speedMetresPerSecond;
            return new PhysicalProjectileStateInput
            {
                Kind = PhysicalProjectileKind.IntactProjectile,
                ProjectileId = "root-projectile",
                RootShotId = "root-shot",
                FragmentIndex = -1,
                FragmentGeneration = 0,
                DeterministicSeed = 0xC0FFEEUL,
                Construction = PhysicalProjectileConstruction.SteelCoreJacketed,
                DesignClass = PhysicalProjectileDesignClass.FullMetalJacket,
                ShapeClass = PhysicalProjectileShapeClass.Spitzer,
                OriginalMassKilograms = originalMassKilograms,
                RetainedMassKilograms = retainedMassKilograms,
                NominalDiameterMetres = diameterMetres,
                DeformedDiameterMetres = diameterMetres,
                ProjectedAreaSquareMetres = area,
                LengthMetres = 0.028d,
                DragCoefficient = 0.295d,
                PositionMetres = new PhysicalVector3(1d, 2d, 3d),
                VelocityMetresPerSecond = new PhysicalVector3(0d, 0d, speedMetresPerSecond),
                Orientation = PhysicalOrientation.Identity,
                YawAngleRadians = 0d,
                TumbleState = PhysicalProjectileTumbleState.Stable,
                PenetrationCapabilityJoulesPerSquareMetre = energyJoules / area,
                DamageCapabilityJoules = energyJoules * 0.75d,
                TerminalState = PhysicalProjectileTerminalState.Continuing,
                RenderState = PhysicalProjectileRenderState.NotRendered,
                CollisionHistory = Array.Empty<PhysicalCollisionRecord>()
            };
        }

        private static PhysicalProjectileStateInput CreateChildInput(
            PhysicalProjectileState parent,
            PhysicalProjectileKind kind,
            string projectileId,
            int fragmentIndex,
            double massKilograms,
            double speedMetresPerSecond)
        {
            bool isSpall = kind == PhysicalProjectileKind.TargetSpall
                || kind == PhysicalProjectileKind.TargetSpallFragment;
            double diameterMetres = isSpall ? 0.004d : 0.003d;
            double area;
            if (!PhysicalProjectileGeometry.TryCalculateCircularAreaSquareMetres(diameterMetres, out area))
            {
                throw new InvalidOperationException("Could not calculate child projectile area.");
            }

            double energyJoules = 0.5d * massKilograms * speedMetresPerSecond * speedMetresPerSecond;
            ulong deterministicSeed = parent.DeterministicSeed + (ulong)(fragmentIndex + 1);
            double yawAngleRadians = isSpall ? 0.7d : 0.35d;
            if (!PhysicalOrientation.TryApplyYaw(
                    PhysicalOrientation.Identity,
                    yawAngleRadians,
                    deterministicSeed,
                    out PhysicalOrientation orientation))
            {
                throw new InvalidOperationException("Could not calculate child projectile attitude.");
            }

            return new PhysicalProjectileStateInput
            {
                Kind = kind,
                ProjectileId = projectileId,
                RootShotId = parent.RootShotId,
                ParentProjectileId = parent.ProjectileId,
                SourceProjectileId = parent.ProjectileId,
                SourceMaterialId = "armor-steel",
                SourceMaterialClass = PhysicalMaterialClass.ArmoredSteel,
                SourceCollisionId = "collision-fragmentation",
                FragmentIndex = fragmentIndex,
                FragmentGeneration = parent.FragmentGeneration + 1,
                DeterministicSeed = deterministicSeed,
                Construction = isSpall
                    ? PhysicalProjectileConstruction.TargetMaterial
                    : parent.Construction,
                DesignClass = isSpall || kind == PhysicalProjectileKind.ProjectileFragment
                    ? PhysicalProjectileDesignClass.Fragment
                    : parent.DesignClass,
                ShapeClass = isSpall
                    ? PhysicalProjectileShapeClass.TargetSpallFlake
                    : kind == PhysicalProjectileKind.DeformedProjectile
                        ? PhysicalProjectileShapeClass.FlattenedDisc
                        : PhysicalProjectileShapeClass.IrregularProjectileFragment,
                OriginalMassKilograms = massKilograms,
                RetainedMassKilograms = massKilograms,
                NominalDiameterMetres = diameterMetres,
                DeformedDiameterMetres = diameterMetres,
                ProjectedAreaSquareMetres = area,
                LengthMetres = isSpall ? 0.001d : 0.006d,
                DragCoefficient = isSpall ? 1.15d : 0.85d,
                PositionMetres = parent.PositionMetres,
                VelocityMetresPerSecond = new PhysicalVector3(0d, 0d, speedMetresPerSecond),
                Orientation = orientation,
                YawAngleRadians = yawAngleRadians,
                TumbleState = PhysicalProjectileTumbleState.Tumbling,
                PenetrationCapabilityJoulesPerSquareMetre = energyJoules / area,
                DamageCapabilityJoules = energyJoules * 0.5d,
                TerminalState = PhysicalProjectileTerminalState.Continuing,
                RenderState = PhysicalProjectileRenderState.NotRendered,
                CollisionHistory = Array.Empty<PhysicalCollisionRecord>()
            };
        }

        private static PhysicalProjectileState CreateChildState(
            PhysicalProjectileState parent,
            PhysicalProjectileKind kind,
            string projectileId,
            int fragmentIndex,
            double massKilograms,
            double speedMetresPerSecond)
        {
            return CreatePhysicalStateOrThrow(
                CreateChildInput(
                    parent,
                    kind,
                    projectileId,
                    fragmentIndex,
                    massKilograms,
                    speedMetresPerSecond));
        }

        private static PhysicalProjectileState CreatePhysicalStateOrThrow(
            PhysicalProjectileStateInput input)
        {
            PhysicalProjectileState? state;
            PhysicalProjectileStateFailureReason reason;
            if (!PhysicalProjectileState.TryCreate(input, out state, out reason)
                || state == null)
            {
                throw new InvalidOperationException("Valid physical state creation failed: " + reason + ".");
            }

            return state;
        }

        private static void AssertPhysicalStateFailure(
            string name,
            PhysicalProjectileStateInput input,
            PhysicalProjectileStateFailureReason expectedReason)
        {
            PhysicalProjectileState? state;
            PhysicalProjectileStateFailureReason actualReason;
            bool success = PhysicalProjectileState.TryCreate(input, out state, out actualReason);
            AssertTrue(name + " fails", !success);
            AssertTrue(name + " returns no state", state == null);
            AssertEqual(name + " reason", expectedReason, actualReason);
        }

        private static void ValidateSnbRows(BallisticTemplate snb)
        {
            var rows = new[]
            {
                new SnbCalculationRow(0d, 0d, 0d),
                new SnbCalculationRow(0.5d, 0.37892914162759955d, 0.757858283255199d),
                new SnbCalculationRow(0.8d, 0.73168808308372224d, 0.91461010385465269d),
                new SnbCalculationRow(1d, 1d, 1d),
                new SnbCalculationRow(1.2d, 1.2907845083190841d, 1.0756537569325701d),
                new SnbCalculationRow(2d, 2.6390158215457884d, 1.3195079107728942d)
            };

            foreach (var row in rows)
            {
                var impactSpeed = snb.InitialSpeed * row.SpeedFraction;
                var factors = CalculateOrThrow(impactSpeed, snb.InitialSpeed);

                AssertNear("SNB ratio " + row.SpeedFraction, row.SpeedFraction, factors.SpeedFraction);
                AssertNear("SNB penetration factor " + row.SpeedFraction, row.PenetrationFactor, factors.PenetrationFactor);
                AssertNear("SNB damage factor " + row.SpeedFraction, row.DamageFactor, factors.DamageFactor);
                AssertNear(
                    "SNB scaled penetration " + row.SpeedFraction,
                    snb.PenetrationPower * row.PenetrationFactor,
                    snb.PenetrationPower * factors.PenetrationFactor);
                AssertNear(
                    "SNB scaled damage " + row.SpeedFraction,
                    snb.Damage * row.DamageFactor,
                    snb.Damage * factors.DamageFactor);
            }
        }

        private static void ValidateWeaponIndependence(BallisticTemplate snb)
        {
            var impactSpeed = snb.InitialSpeed * 0.8d;
            var expected = CalculateOrThrow(impactSpeed, snb.InitialSpeed);

            // Weapon muzzle speed is not an input to the calculator.
            var weaponMuzzleSpeeds = new[] { 350d, 875d, 1200d };
            foreach (var weaponMuzzleSpeed in weaponMuzzleSpeeds)
            {
                AssertTrue("weapon speed is finite", IsFinite(weaponMuzzleSpeed));
                var actual = CalculateOrThrow(impactSpeed, snb.InitialSpeed);

                AssertNear("weapon-independent penetration " + weaponMuzzleSpeed, expected.PenetrationFactor, actual.PenetrationFactor);
                AssertNear("weapon-independent damage " + weaponMuzzleSpeed, expected.DamageFactor, actual.DamageFactor);
            }
        }

        private static void ValidateUnboundedFactors()
        {
            var atOnePointTwo = CalculateOrThrow(120d, 100d);
            AssertNear("1.2 ratio", 1.2d, atOnePointTwo.SpeedFraction);
            AssertNear("1.2 penetration", 1.2907845083190841d, atOnePointTwo.PenetrationFactor);
            AssertNear("1.2 damage", 1.0756537569325701d, atOnePointTwo.DamageFactor);
            AssertTrue("1.2 penetration is not clamped", atOnePointTwo.PenetrationFactor > 1d);
            AssertTrue("1.2 damage is not clamped", atOnePointTwo.DamageFactor > 1d);

            var atTwo = CalculateOrThrow(200d, 100d);
            AssertNear("2.0 ratio", 2d, atTwo.SpeedFraction);
            AssertNear("2.0 penetration", 2.6390158215457884d, atTwo.PenetrationFactor);
            AssertNear("2.0 damage", 1.3195079107728942d, atTwo.DamageFactor);
            AssertTrue("2.0 penetration is not clamped", atTwo.PenetrationFactor > 1d);
            AssertTrue("2.0 damage is not clamped", atTwo.DamageFactor > 1d);
        }

        private static void ValidateZeroImpact()
        {
            BallisticFalloffFactors factors;
            BallisticFalloffFailureReason reason;
            var success = BallisticFalloffCalculator.TryCalculate(0d, 875d, out factors, out reason);

            AssertTrue("zero impact is valid", success);
            AssertEqual("zero impact reason", BallisticFalloffFailureReason.None, reason);
            AssertNear("zero impact ratio", 0d, factors.SpeedFraction);
            AssertNear("zero impact penetration", 0d, factors.PenetrationFactor);
            AssertNear("zero impact damage", 0d, factors.DamageFactor);
        }

        private static void ValidateInvalidInputs()
        {
            AssertFailure(double.NaN, 875d, FalloffExponentConfiguration.Default, BallisticFalloffFailureReason.ImpactSpeedNotFinite);
            AssertFailure(double.PositiveInfinity, 875d, FalloffExponentConfiguration.Default, BallisticFalloffFailureReason.ImpactSpeedNotFinite);
            AssertFailure(-0.01d, 875d, FalloffExponentConfiguration.Default, BallisticFalloffFailureReason.ImpactSpeedNegative);
            AssertFailure(100d, double.NaN, FalloffExponentConfiguration.Default, BallisticFalloffFailureReason.TemplateSpeedNotFinite);
            AssertFailure(100d, double.PositiveInfinity, FalloffExponentConfiguration.Default, BallisticFalloffFailureReason.TemplateSpeedNotFinite);
            AssertFailure(100d, 0d, FalloffExponentConfiguration.Default, BallisticFalloffFailureReason.TemplateSpeedNotPositive);
            AssertFailure(100d, -1d, FalloffExponentConfiguration.Default, BallisticFalloffFailureReason.TemplateSpeedNotPositive);
            AssertFailure(100d, 875d, new FalloffExponentConfiguration(0d, 0.4d), BallisticFalloffFailureReason.PenetrationExponentNotPositive);
            AssertFailure(100d, 875d, new FalloffExponentConfiguration(1.4d, double.PositiveInfinity), BallisticFalloffFailureReason.DamageExponentNotFinite);
        }

        private static void ValidateConfigurableExponents()
        {
            var configuration = new FalloffExponentConfiguration(2d, 3d);
            BallisticFalloffFactors factors;
            BallisticFalloffFailureReason reason;
            var success = BallisticFalloffCalculator.TryCalculate(50d, 100d, configuration, out factors, out reason);

            AssertTrue("custom exponent calculation succeeds", success);
            AssertEqual("custom exponent reason", BallisticFalloffFailureReason.None, reason);
            AssertNear("custom penetration exponent", 0.25d, factors.PenetrationFactor);
            AssertNear("custom damage exponent", 0.125d, factors.DamageFactor);
        }

        private static void ValidateMonotonicAndCumulativeCalculations(BallisticTemplate snb)
        {
            var fractions = new[] { 0d, 0.25d, 0.5d, 0.8d, 1d, 1.2d, 2d };
            var previousPenetration = -1d;
            var previousDamage = -1d;

            foreach (var fraction in fractions)
            {
                var factors = CalculateOrThrow(snb.InitialSpeed * fraction, snb.InitialSpeed);
                AssertTrue("penetration is monotonic at " + fraction, factors.PenetrationFactor >= previousPenetration);
                AssertTrue("damage is monotonic at " + fraction, factors.DamageFactor >= previousDamage);
                previousPenetration = factors.PenetrationFactor;
                previousDamage = factors.DamageFactor;
            }

            const double ReferenceSpeed = 849d;
            BallisticFalloffFactors aboveReference = CalculateOrThrow(939.9d, ReferenceSpeed);
            AssertNear(
                ".338 AP reproduced penetration factor",
                1.1530377949840798d,
                aboveReference.PenetrationFactor);
            AssertNear(
                ".338 AP reproduced damage factor",
                1.0415247238444205d,
                aboveReference.DamageFactor);
            BallisticNormalizationState aboveState = CreateRootNormalizationState(
                "338-ap-component",
                "338-ap-root");
            double aboveDamage = 115d;
            double abovePenetration = 79.35d;
            for (int layer = 1; layer <= 4; layer++)
            {
                BallisticNormalizationTransition transition = AdvanceNormalizationOrThrow(
                    ref aboveState,
                    "338-ap-layer-" + layer,
                    aboveDamage,
                    abovePenetration,
                    aboveReference);
                aboveDamage = transition.OutputDamage;
                abovePenetration = transition.OutputPenetrationPower;
                AssertNear(
                    "above-reference layer " + layer + " damage",
                    115d * aboveReference.DamageFactor,
                    aboveDamage);
                AssertNear(
                    "above-reference layer " + layer + " penetration",
                    79.35d * aboveReference.PenetrationFactor,
                    abovePenetration);
                AssertNear(
                    "above-reference layer " + layer + " damage ratio",
                    layer == 1 ? aboveReference.DamageFactor : 1d,
                    transition.AppliedDamageRatio);
                AssertNear(
                    "above-reference layer " + layer + " penetration ratio",
                    layer == 1 ? aboveReference.PenetrationFactor : 1d,
                    transition.AppliedPenetrationRatio);
            }
            AssertNear(
                ".338 AP four-layer corrected damage",
                119.77534324210836d,
                aboveDamage);
            AssertNear(
                ".338 AP four-layer corrected penetration",
                91.49354903198674d,
                abovePenetration);

            BallisticFalloffFactors belowReference = CalculateOrThrow(700d, ReferenceSpeed);
            BallisticNormalizationState belowState = CreateRootNormalizationState(
                "below-component",
                "below-root");
            double belowDamage = 115d;
            double belowPenetration = 79.35d;
            for (int layer = 1; layer <= 4; layer++)
            {
                BallisticNormalizationTransition transition = AdvanceNormalizationOrThrow(
                    ref belowState,
                    "below-layer-" + layer,
                    belowDamage,
                    belowPenetration,
                    belowReference);
                belowDamage = transition.OutputDamage;
                belowPenetration = transition.OutputPenetrationPower;
                AssertNear(
                    "below-reference layer " + layer + " damage",
                    115d * belowReference.DamageFactor,
                    belowDamage);
                AssertNear(
                    "below-reference layer " + layer + " penetration",
                    79.35d * belowReference.PenetrationFactor,
                    belowPenetration);
            }

            BallisticNormalizationState changingState = CreateRootNormalizationState(
                "changing-component",
                "changing-root");
            double changingDamage = 115d;
            double changingPenetration = 79.35d;
            var speeds = new[] { 939.9d, 900d, 800d, 700d };
            for (int index = 0; index < speeds.Length; index++)
            {
                BallisticFalloffFactors factors = CalculateOrThrow(speeds[index], ReferenceSpeed);
                BallisticNormalizationTransition transition = AdvanceNormalizationOrThrow(
                    ref changingState,
                    "changing-layer-" + index,
                    changingDamage,
                    changingPenetration,
                    factors);
                changingDamage = transition.OutputDamage;
                changingPenetration = transition.OutputPenetrationPower;
            }

            BallisticFalloffFactors finalFactors = CalculateOrThrow(700d, ReferenceSpeed);
            AssertNear(
                "decreasing-speed final damage",
                115d * finalFactors.DamageFactor,
                changingDamage);
            AssertNear(
                "decreasing-speed final penetration",
                79.35d * finalFactors.PenetrationFactor,
                changingPenetration);

            BallisticNormalizationState reducedState = CreateRootNormalizationState(
                "reduced-component",
                "reduced-root");
            BallisticNormalizationTransition firstReduced = AdvanceNormalizationOrThrow(
                ref reducedState,
                "reduced-layer-1",
                115d,
                79.35d,
                aboveReference);
            const double MaterialReduction = 0.63d;
            const double ArmorReduction = 0.72d;
            BallisticNormalizationTransition secondReduced = AdvanceNormalizationOrThrow(
                ref reducedState,
                "reduced-layer-2",
                firstReduced.OutputDamage * MaterialReduction * ArmorReduction,
                firstReduced.OutputPenetrationPower * MaterialReduction * ArmorReduction,
                belowReference);
            AssertNear(
                "material and armor damage reductions survive normalization",
                115d * MaterialReduction * ArmorReduction * belowReference.DamageFactor,
                secondReduced.OutputDamage);
            AssertNear(
                "material and armor penetration reductions survive normalization",
                79.35d * MaterialReduction * ArmorReduction * belowReference.PenetrationFactor,
                secondReduced.OutputPenetrationPower);

            BallisticNormalizationState? derivedState;
            AssertTrue(
                "derived child inherits represented factors",
                BallisticNormalizationState.TryCreateDerivedChild(
                    "derived-child",
                    aboveState,
                    out derivedState,
                    out BallisticNormalizationFailureReason derivedReason));
            AssertEqual(
                "derived child creation reason",
                BallisticNormalizationFailureReason.None,
                derivedReason);
            BallisticNormalizationState derived = RequireValue(
                "derived normalization state",
                derivedState);
            BallisticNormalizationTransition derivedTransition = AdvanceNormalizationOrThrow(
                ref derived,
                "derived-layer-1",
                aboveDamage * 0.8d,
                abovePenetration * 0.8d,
                aboveReference);
            AssertNear(
                "derived child same-speed damage is neutral",
                aboveDamage * 0.8d,
                derivedTransition.OutputDamage);
            AssertNear(
                "derived child same-speed penetration is neutral",
                abovePenetration * 0.8d,
                derivedTransition.OutputPenetrationPower);

            foreach (string componentId in new[]
            {
                "physical-primary",
                "physical-fragment",
                "target-spall",
                "target-spall-fragment"
            })
            {
                BallisticNormalizationState physical = CreatePhysicalNormalizationState(
                    componentId,
                    "physical-root",
                    aboveReference);
                BallisticNormalizationTransition physicalTransition = AdvanceNormalizationOrThrow(
                    ref physical,
                    componentId + "-layer-1",
                    17.25d,
                    6.5d,
                    aboveReference);
                AssertEqual(
                    componentId + " capability owns normalization",
                    BallisticNormalizationDisposition.PhysicalCapabilityBypass,
                    physicalTransition.Disposition);
                AssertNear(componentId + " damage preserved", 17.25d, physicalTransition.OutputDamage);
                AssertNear(
                    componentId + " penetration preserved",
                    6.5d,
                    physicalTransition.OutputPenetrationPower);

                AssertTrue(
                    componentId + " derived child state is created",
                    BallisticNormalizationState.TryCreateDerivedChild(
                        componentId + "-derived",
                        physical,
                        out BallisticNormalizationState? physicalDerivedState,
                        out BallisticNormalizationFailureReason physicalDerivedReason));
                AssertEqual(
                    componentId + " derived child reason",
                    BallisticNormalizationFailureReason.None,
                    physicalDerivedReason);
                AssertEqual(
                    componentId + " derived child retains physical ownership",
                    BallisticNormalizationOwnership.PhysicalCapability,
                    RequireValue(
                        componentId + " derived child",
                        physicalDerivedState).Ownership);
            }

            BallisticNormalizationState duplicateState = CreateRootNormalizationState(
                "duplicate-component",
                "duplicate-root");
            BallisticNormalizationTransition firstDuplicate = AdvanceNormalizationOrThrow(
                ref duplicateState,
                "duplicate-layer-1",
                115d,
                79.35d,
                aboveReference);
            BallisticNormalizationState committedDuplicateState = duplicateState;
            BallisticNormalizationTransition duplicate = AdvanceNormalizationOrThrow(
                ref duplicateState,
                "duplicate-layer-1",
                firstDuplicate.OutputDamage,
                firstDuplicate.OutputPenetrationPower,
                belowReference);
            AssertEqual(
                "duplicate disposition",
                BallisticNormalizationDisposition.Duplicate,
                duplicate.Disposition);
            AssertTrue(
                "duplicate does not replace state",
                ReferenceEquals(committedDuplicateState, duplicateState));
            AssertNear(
                "duplicate does not alter damage",
                firstDuplicate.OutputDamage,
                duplicate.OutputDamage);
            AssertNear(
                "duplicate does not alter penetration",
                firstDuplicate.OutputPenetrationPower,
                duplicate.OutputPenetrationPower);

            BallisticNormalizationState failedState = duplicateState;
            AssertTrue(
                "failed calculation is rejected",
                !BallisticNormalizationCalculator.TryAdvance(
                    failedState,
                    "invalid-layer",
                    double.NaN,
                    10d,
                    aboveReference,
                    out _,
                    out BallisticNormalizationFailureReason invalidReason));
            AssertEqual(
                "invalid calculation reason",
                BallisticNormalizationFailureReason.CurrentStatisticsInvalid,
                invalidReason);
            AssertTrue(
                "failed calculation cannot advance immutable state",
                ReferenceEquals(failedState, duplicateState));

            var pooledHost = new PooledNormalizationHost(1);
            var pooledStore = new PoolSafeReferenceBindingStore<
                PooledNormalizationHost,
                PooledNormalizationBinding>(
                    delegate (PooledNormalizationBinding binding, PooledNormalizationHost host)
                    {
                        return binding.Matches(host);
                    });
            BallisticNormalizationState pooledOriginal = CreateRootNormalizationState(
                "reused-host-old-component",
                "reused-host-old-root");
            var originalBinding = new PooledNormalizationBinding(pooledHost, pooledOriginal);
            AssertTrue(
                "pooled host accepts its original incarnation",
                pooledStore.TryGetOrSet(pooledHost, originalBinding, out PooledNormalizationBinding storedOriginal));
            AssertTrue(
                "pooled host stores the exact original binding",
                ReferenceEquals(originalBinding, storedOriginal));

            pooledHost.Incarnation = 2;
            AssertTrue(
                "pooled host rejects and removes stale incarnation",
                !pooledStore.TryGet(pooledHost, out _));

            BallisticNormalizationState pooledReplacement = CreateRootNormalizationState(
                "reused-host-new-component",
                "reused-host-new-root");
            var replacementBinding = new PooledNormalizationBinding(pooledHost, pooledReplacement);
            AssertTrue(
                "pooled host accepts replacement incarnation",
                pooledStore.TryGetOrSet(
                    pooledHost,
                    replacementBinding,
                    out PooledNormalizationBinding storedReplacement));
            AssertTrue(
                "pooled host stores exact replacement binding",
                ReferenceEquals(replacementBinding, storedReplacement));
            pooledStore.RemoveIfSame(pooledHost, originalBinding);
            AssertTrue(
                "stale cleanup cannot remove replacement binding",
                pooledStore.TryGet(pooledHost, out PooledNormalizationBinding? afterStaleCleanup)
                    && ReferenceEquals(replacementBinding, afterStaleCleanup));
            var staleReplacementAttempt = new PooledNormalizationBinding(
                pooledHost,
                pooledOriginal);
            AssertTrue(
                "stale commit cannot overwrite replacement binding",
                !pooledStore.TryReplace(
                    pooledHost,
                    originalBinding,
                    staleReplacementAttempt,
                    out _));
            AssertNear(
                "pooled replacement starts with neutral penetration factor",
                1d,
                pooledReplacement.RepresentedPenetrationFactor);
            AssertNear(
                "pooled replacement starts with neutral damage factor",
                1d,
                pooledReplacement.RepresentedDamageFactor);
            AssertTrue(
                "pooled replacement component identity is isolated",
                !string.Equals(
                    duplicateState.ComponentId,
                    pooledReplacement.ComponentId,
                    StringComparison.Ordinal));

            for (int layerCount = 1; layerCount <= 6; layerCount++)
            {
                BallisticNormalizationState layered = CreateRootNormalizationState(
                    "layered-component-" + layerCount,
                    "layered-root-" + layerCount);
                double layeredDamage = 115d;
                double layeredPenetration = 79.35d;
                for (int layer = 0; layer < layerCount; layer++)
                {
                    BallisticNormalizationTransition transition = AdvanceNormalizationOrThrow(
                        ref layered,
                        "layered-" + layerCount + "-" + layer,
                        layeredDamage,
                        layeredPenetration,
                        aboveReference);
                    layeredDamage = transition.OutputDamage;
                    layeredPenetration = transition.OutputPenetrationPower;
                }

                AssertNear(
                    "layer-count damage remains idempotent " + layerCount,
                    115d * aboveReference.DamageFactor,
                    layeredDamage);
                AssertNear(
                    "layer-count penetration remains idempotent " + layerCount,
                    79.35d * aboveReference.PenetrationFactor,
                    layeredPenetration);
            }

            BallisticNormalizationState zeroState = CreateRootNormalizationState(
                "zero-component",
                "zero-root");
            BallisticFalloffFactors zeroFactors = CalculateOrThrow(0d, ReferenceSpeed);
            BallisticNormalizationTransition zeroFirst = AdvanceNormalizationOrThrow(
                ref zeroState,
                "zero-layer-1",
                115d,
                79.35d,
                zeroFactors);
            AssertNear("zero-speed damage", 0d, zeroFirst.OutputDamage);
            AssertNear("zero-speed penetration", 0d, zeroFirst.OutputPenetrationPower);
            BallisticNormalizationTransition zeroSecond = AdvanceNormalizationOrThrow(
                ref zeroState,
                "zero-layer-2",
                zeroFirst.OutputDamage,
                zeroFirst.OutputPenetrationPower,
                zeroFactors);
            AssertNear("repeated zero-speed damage", 0d, zeroSecond.OutputDamage);
            AssertNear("repeated zero-speed penetration", 0d, zeroSecond.OutputPenetrationPower);
        }

        private static void AssertFailure(
            double impactSpeed,
            double templateSpeed,
            FalloffExponentConfiguration configuration,
            BallisticFalloffFailureReason expectedReason)
        {
            BallisticFalloffFactors factors;
            BallisticFalloffFailureReason actualReason;
            var success = BallisticFalloffCalculator.TryCalculate(
                impactSpeed,
                templateSpeed,
                configuration,
                out factors,
                out actualReason);

            AssertTrue("invalid calculation fails for " + expectedReason, !success);
            AssertEqual("invalid reason " + expectedReason, expectedReason, actualReason);
            AssertNear("invalid fallback penetration " + expectedReason, 1d, factors.PenetrationFactor);
            AssertNear("invalid fallback damage " + expectedReason, 1d, factors.DamageFactor);
        }

        private static BallisticFalloffFactors CalculateOrThrow(double impactSpeed, double templateSpeed)
        {
            BallisticFalloffFactors factors;
            BallisticFalloffFailureReason reason;
            if (!BallisticFalloffCalculator.TryCalculate(impactSpeed, templateSpeed, out factors, out reason))
            {
                throw new InvalidOperationException("Calculation unexpectedly failed: " + reason + ".");
            }

            return factors;
        }

        private static BallisticNormalizationState CreateRootNormalizationState(
            string componentId,
            string rootShotId)
        {
            if (!BallisticNormalizationState.TryCreateRoot(
                    componentId,
                    rootShotId,
                    out BallisticNormalizationState? state,
                    out BallisticNormalizationFailureReason reason)
                || state == null)
            {
                throw new InvalidOperationException(
                    "Root normalization state creation failed: " + reason + ".");
            }

            return state;
        }

        private static BallisticNormalizationState CreatePhysicalNormalizationState(
            string componentId,
            string rootShotId,
            BallisticFalloffFactors baselineFactors)
        {
            if (!BallisticNormalizationState.TryCreatePhysicalComponent(
                    componentId,
                    rootShotId,
                    baselineFactors,
                    out BallisticNormalizationState? state,
                    out BallisticNormalizationFailureReason reason)
                || state == null)
            {
                throw new InvalidOperationException(
                    "Physical normalization state creation failed: " + reason + ".");
            }

            return state;
        }

        private static BallisticNormalizationTransition AdvanceNormalizationOrThrow(
            ref BallisticNormalizationState state,
            string collisionIdentity,
            double currentDamage,
            double currentPenetrationPower,
            BallisticFalloffFactors factors)
        {
            if (!BallisticNormalizationCalculator.TryAdvance(
                    state,
                    collisionIdentity,
                    currentDamage,
                    currentPenetrationPower,
                    factors,
                    out BallisticNormalizationTransition? transition,
                    out BallisticNormalizationFailureReason reason)
                || transition == null)
            {
                throw new InvalidOperationException(
                    "Normalization transition failed: " + reason + ".");
            }

            state = transition.NextState;
            return transition;
        }

        private static void ValidateFullTemplateSweep(IReadOnlyList<BallisticTemplate> templates)
        {
            AssertEqual("templates with numeric InitialSpeed, Damage, and PenetrationPower", 210, templates.Count);

            var positiveFiniteTemplates = 0;
            var abstractFallbackTemplates = 0;
            var successfulCalculations = 0;
            var fallbackIds = new List<string>();

            foreach (var template in templates)
            {
                if (template.InitialSpeed > 0d && IsFinite(template.InitialSpeed))
                {
                    positiveFiniteTemplates++;

                    foreach (var fraction in SweepFractions)
                    {
                        var factors = CalculateOrThrow(template.InitialSpeed * fraction, template.InitialSpeed);
                        AssertNear(template.InternalName + " ratio " + fraction, fraction, factors.SpeedFraction);
                        AssertTrue(template.InternalName + " penetration factor is finite at " + fraction, IsFinite(factors.PenetrationFactor));
                        AssertTrue(template.InternalName + " damage factor is finite at " + fraction, IsFinite(factors.DamageFactor));
                        successfulCalculations++;
                    }
                }
                else
                {
                    abstractFallbackTemplates++;
                    fallbackIds.Add(template.Id + " (" + template.InternalName + ")");

                    BallisticFalloffFactors factors;
                    BallisticFalloffFailureReason reason;
                    var success = BallisticFalloffCalculator.TryCalculate(
                        1d,
                        template.InitialSpeed,
                        out factors,
                        out reason);

                    AssertTrue(template.InternalName + " uses abstract fallback", !success);
                    AssertEqual(template.InternalName + " fallback reason", BallisticFalloffFailureReason.TemplateSpeedNotPositive, reason);
                    AssertNear(template.InternalName + " fallback penetration", 1d, factors.PenetrationFactor);
                    AssertNear(template.InternalName + " fallback damage", 1d, factors.DamageFactor);
                }
            }

            AssertEqual("positive finite InitialSpeed templates", 208, positiveFiniteTemplates);
            AssertEqual("abstract nonpositive InitialSpeed templates", 2, abstractFallbackTemplates);
            AssertEqual(
                "positive-template calculations",
                positiveFiniteTemplates * SweepFractions.Length,
                successfulCalculations);

            Console.WriteLine(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Sweep counts: {0} numeric templates; {1} positive finite templates x {2} fractions = {3} calculations; {4} abstract fallback entries: {5}.",
                    templates.Count,
                    positiveFiniteTemplates,
                    SweepFractions.Length,
                    successfulCalculations,
                    abstractFallbackTemplates,
                    string.Join(", ", fallbackIds)));
        }

        private static void ValidateFullTemplateNormalizationSweep(
            IReadOnlyList<BallisticTemplate> templates)
        {
            int positiveTemplates = 0;
            int applications = 0;
            foreach (BallisticTemplate template in templates)
            {
                if (template.InitialSpeed <= 0d || !IsFinite(template.InitialSpeed))
                {
                    continue;
                }

                positiveTemplates++;
                foreach (double fraction in SweepFractions)
                {
                    BallisticFalloffFactors factors = CalculateOrThrow(
                        template.InitialSpeed * fraction,
                        template.InitialSpeed);
                    BallisticNormalizationState state = CreateRootNormalizationState(
                        template.Id + "-" + fraction.ToString("R", CultureInfo.InvariantCulture),
                        template.Id);
                    double damage = template.Damage;
                    double penetration = template.PenetrationPower;
                    for (int layer = 1; layer <= 6; layer++)
                    {
                        BallisticNormalizationTransition transition = AdvanceNormalizationOrThrow(
                            ref state,
                            template.Id + "-" + fraction.ToString("R", CultureInfo.InvariantCulture)
                                + "-layer-" + layer,
                            damage,
                            penetration,
                            factors);
                        damage = transition.OutputDamage;
                        penetration = transition.OutputPenetrationPower;
                        AssertNear(
                            template.InternalName + " normalized damage layer " + layer,
                            template.Damage * factors.DamageFactor,
                            damage);
                        AssertNear(
                            template.InternalName + " normalized penetration layer " + layer,
                            template.PenetrationPower * factors.PenetrationFactor,
                            penetration);
                        applications++;
                    }
                }
            }

            AssertEqual("normalization sweep positive templates", 208, positiveTemplates);
            AssertEqual(
                "normalization sweep applications",
                positiveTemplates * SweepFractions.Length * 6,
                applications);
            Console.WriteLine(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Normalization sweep: {0} positive templates x {1} fractions x 6 layers = {2} applications.",
                    positiveTemplates,
                    SweepFractions.Length,
                    applications));
        }

        private static void ValidateUsTemplate(IReadOnlyList<BallisticTemplate> templates)
        {
            var us = FindTemplate(templates, UsInternalName);
            AssertNear("5.45x39 US InitialSpeed", 303d, us.InitialSpeed);
            AssertNear("5.45x39 US PenetrationPower", 17d, us.PenetrationPower);
            AssertNear("5.45x39 US Damage", 65d, us.Damage);

            var halfSpeed = CalculateOrThrow(us.InitialSpeed * 0.5d, us.InitialSpeed);
            AssertNear("5.45x39 US half-speed ratio", 0.5d, halfSpeed.SpeedFraction);
            AssertNear("5.45x39 US half-speed penetration", 0.37892914162759955d, halfSpeed.PenetrationFactor);
            AssertNear("5.45x39 US half-speed damage", 0.757858283255199d, halfSpeed.DamageFactor);
        }

        private static List<BallisticTemplate> LoadTemplatesWithBallisticStats(string itemsPath)
        {
            if (!File.Exists(itemsPath))
            {
                throw new FileNotFoundException("SPT items.json was not found.", itemsPath);
            }

            var templates = new List<BallisticTemplate>();

            using (var stream = File.OpenRead(itemsPath))
            using (var document = JsonDocument.Parse(stream))
            {
                if (document.RootElement.ValueKind != JsonValueKind.Object)
                {
                    throw new InvalidDataException("SPT items.json must contain an object keyed by template id.");
                }

                foreach (var itemProperty in document.RootElement.EnumerateObject())
                {
                    var item = itemProperty.Value;
                    JsonElement properties;
                    if (!item.TryGetProperty("_props", out properties)
                        || properties.ValueKind != JsonValueKind.Object)
                    {
                        continue;
                    }

                    string internalName;
                    if (!TryReadString(item, "_name", out internalName))
                    {
                        internalName = itemProperty.Name;
                    }

                    double initialSpeed;
                    double penetrationPower;
                    double damage;
                    if (!TryReadFiniteNumber(properties, "InitialSpeed", out initialSpeed)
                        || !TryReadFiniteNumber(properties, "PenetrationPower", out penetrationPower)
                        || !TryReadFiniteNumber(properties, "Damage", out damage))
                    {
                        continue;
                    }

                    templates.Add(new BallisticTemplate(itemProperty.Name, internalName, initialSpeed, penetrationPower, damage));
                }
            }

            return templates;
        }

        private static BallisticTemplate FindTemplate(
            IReadOnlyList<BallisticTemplate> templates,
            string internalName)
        {
            foreach (var template in templates)
            {
                if (string.Equals(template.InternalName, internalName, StringComparison.Ordinal))
                {
                    return template;
                }
            }

            throw new InvalidDataException("Could not find template " + internalName + " in items.json.");
        }

        private static bool TryReadString(JsonElement objectElement, string propertyName, out string value)
        {
            JsonElement property;
            if (objectElement.TryGetProperty(propertyName, out property)
                && property.ValueKind == JsonValueKind.String)
            {
                value = property.GetString() ?? string.Empty;
                return true;
            }

            value = string.Empty;
            return false;
        }

        private static bool TryReadFiniteNumber(JsonElement objectElement, string propertyName, out double value)
        {
            JsonElement property;
            if (objectElement.TryGetProperty(propertyName, out property)
                && property.ValueKind == JsonValueKind.Number
                && property.TryGetDouble(out value)
                && IsFinite(value))
            {
                return true;
            }

            value = 0d;
            return false;
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }

        private static void Run(string name, Action test)
        {
            try
            {
                test();
                _passed++;
                Console.WriteLine("PASS " + name);
            }
            catch (InvalidOperationException exception)
            {
                RecordValidationFailure(name, exception);
            }
            catch (IOException exception)
            {
                RecordValidationFailure(name, exception);
            }
            catch (UnauthorizedAccessException exception)
            {
                RecordValidationFailure(name, exception);
            }
            catch (JsonException exception)
            {
                RecordValidationFailure(name, exception);
            }
            catch (ArgumentException exception)
            {
                RecordValidationFailure(name, exception);
            }
            catch (NotSupportedException exception)
            {
                RecordValidationFailure(name, exception);
            }
            catch (OverflowException exception)
            {
                RecordValidationFailure(name, exception);
            }
            catch (SecurityException exception)
            {
                RecordValidationFailure(name, exception);
            }
        }

        private static int ReportValidationSetupFailure(Exception exception)
        {
            Console.Error.WriteLine("Validation setup failed: " + exception.Message);
            return 2;
        }

        private static void RecordValidationFailure(string name, Exception exception)
        {
            _failed++;
            Console.Error.WriteLine("FAIL " + name + ": " + exception.Message);
        }

        private static T RequireValue<T>(string name, T? value)
            where T : class
        {
            if (value == null)
            {
                throw new InvalidOperationException(name + " was null.");
            }

            return value;
        }

        private static void AssertTrue(string name, bool condition)
        {
            if (!condition)
            {
                throw new InvalidOperationException(name + " was false.");
            }
        }

        private static void AssertEqual<T>(string name, T expected, T actual)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
            {
                throw new InvalidOperationException(
                    name
                    + " expected "
                    + expected
                    + " but was "
                    + actual
                    + ".");
            }
        }

        private static void AssertNear(string name, double expected, double actual)
        {
            if (!IsFinite(actual))
            {
                throw new InvalidOperationException(name + " was not finite: " + actual + ".");
            }

            var allowedDifference = Tolerance * Math.Max(1d, Math.Abs(expected));
            if (Math.Abs(expected - actual) > allowedDifference)
            {
                throw new InvalidOperationException(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "{0} expected {1:R} but was {2:R} (tolerance {3:R}).",
                        name,
                        expected,
                        actual,
                        allowedDifference));
            }
        }

        private sealed class BallisticTemplate
        {
            public BallisticTemplate(
                string id,
                string internalName,
                double initialSpeed,
                double penetrationPower,
                double damage)
            {
                Id = id;
                InternalName = internalName;
                InitialSpeed = initialSpeed;
                PenetrationPower = penetrationPower;
                Damage = damage;
            }

            public string Id { get; private set; }

            public string InternalName { get; private set; }

            public double InitialSpeed { get; private set; }

            public double PenetrationPower { get; private set; }

            public double Damage { get; private set; }
        }

        private sealed class ValidPhysicalSurface
        {
            private readonly int _schema = 1;

            internal ValidPhysicalSurface(string materialClass)
            {
                PhysicalBallisticsMaterialClass = materialClass;
            }

            public int PhysicalBallisticsSurfaceSchema => _schema;

            public string PhysicalBallisticsMaterialClass { get; }
        }

        private sealed class UnsupportedPhysicalSurface
        {
            private readonly int _schema = 2;
            private readonly string _materialClass = "ArmoredSteel";

            public int PhysicalBallisticsSurfaceSchema => _schema;

            public string PhysicalBallisticsMaterialClass => _materialClass;
        }

        private sealed class IdentifiedPhysicalSurface
        {
            private readonly int _schema = 1;

            internal IdentifiedPhysicalSurface(string materialClass, string surfaceIdentity)
            {
                PhysicalBallisticsMaterialClass = materialClass;
                PhysicalBallisticsSurfaceIdentity = surfaceIdentity;
            }

            public int PhysicalBallisticsSurfaceSchema => _schema;

            public string PhysicalBallisticsMaterialClass { get; }

            public string PhysicalBallisticsSurfaceIdentity { get; }
        }

        private sealed class EmptyIdentityPhysicalSurface
        {
            private readonly int _schema = 1;
            private readonly string _materialClass = "ArmoredSteel";
            private readonly string _surfaceIdentity = string.Empty;

            public int PhysicalBallisticsSurfaceSchema => _schema;

            public string PhysicalBallisticsMaterialClass => _materialClass;

            public string PhysicalBallisticsSurfaceIdentity => _surfaceIdentity;
        }

        private sealed class WrongIdentityTypePhysicalSurface
        {
            private readonly int _schema = 1;
            private readonly string _materialClass = "ArmoredSteel";
            private readonly int _surfaceIdentity = 7;

            public int PhysicalBallisticsSurfaceSchema => _schema;

            public string PhysicalBallisticsMaterialClass => _materialClass;

            public int PhysicalBallisticsSurfaceIdentity => _surfaceIdentity;
        }

        private sealed class PartialPhysicalSurface
        {
            private readonly int _schema = 1;

            public int PhysicalBallisticsSurfaceSchema => _schema;
        }

        private sealed class WrongTypePhysicalSurface
        {
            private readonly string _schema = "1";
            private readonly string _materialClass = "ArmoredSteel";

            public string PhysicalBallisticsSurfaceSchema => _schema;

            public string PhysicalBallisticsMaterialClass => _materialClass;
        }

        private sealed class ThrowingPhysicalSurface
        {
            private readonly int _schema = 1;
            private readonly string _failureMessage = "Synthetic getter failure.";

            public int PhysicalBallisticsSurfaceSchema => _schema;

            public string PhysicalBallisticsMaterialClass =>
                throw new InvalidOperationException(_failureMessage);
        }

        private readonly struct SnbCalculationRow
        {
            public SnbCalculationRow(double speedFraction, double penetrationFactor, double damageFactor)
            {
                SpeedFraction = speedFraction;
                PenetrationFactor = penetrationFactor;
                DamageFactor = damageFactor;
            }

            public double SpeedFraction { get; }

            public double PenetrationFactor { get; }

            public double DamageFactor { get; }
        }
    }
}
