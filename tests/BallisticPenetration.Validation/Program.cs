using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;
using BallisticPenetration.Core;
using BallisticPenetration.Core.Physics;

namespace BallisticPenetration.Validation
{
    internal static class Program
    {
        private const string ItemsRelativePath = @"SPT_Runtime\SPT_Data\database\templates\items.json";
        private const string SnbInternalName = "patron_762x54R_SNB";
        private const string UsInternalName = "patron_545x39_US";
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

        private static int _passed;
        private static int _failed;

        public static int Main(string[] args)
        {
            string itemsPath;
            try
            {
                itemsPath = ResolveItemsPath(args);
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine("Validation setup failed: " + exception.Message);
                return 2;
            }

            IReadOnlyList<BallisticTemplate> templates = null;
            BallisticTemplate snb = null;
            Run("Exact SPT core version gate", ValidateExactSptCoreVersionGate);
            Run("Postmortem armor hit guards", ValidatePostmortemArmorHitGuards);
            Run("Postmortem armor traversal", ValidatePostmortemArmorTraversal);
            Run("Physical collision history invariants", ValidatePhysicalCollisionHistory);
            Run("Physical projectile state and derived SI values", ValidatePhysicalProjectileState);
            Run("Physical projectile invalid-state fallback", ValidatePhysicalProjectileInvalidFallback);
            Run("Projectile and target-spall conservation", ValidatePhysicalConservation);
            Run("Deterministic projectile random stream", ValidateDeterministicProjectileRandom);
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
                Run("Monotonic and cumulative calculations", delegate { ValidateMonotonicAndCumulativeCalculations(snb); });
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

            string sptRoot = Environment.GetEnvironmentVariable("SPT_ROOT");
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
                    new[] { false, false, false },
                    -1));
        }

        private static int CountPostmortemArmorApplications(
            IReadOnlyList<bool> matches,
            int stoppingArmorIndex)
        {
            int applied = 0;
            for (int index = 0; index < matches.Count; index++)
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
            PhysicalCollisionRecord record;
            PhysicalCollisionRecordFailureReason reason;
            PhysicalCollisionRecordInput input = CreateValidCollisionInput();
            AssertTrue(
                "valid physical collision record accepted",
                PhysicalCollisionRecord.TryCreate(input, out record, out reason));
            AssertEqual("valid physical collision reason", PhysicalCollisionRecordFailureReason.None, reason);
            AssertEqual("collision id preserved", "collision-1", record.CollisionId);
            AssertNear("collision path length preserved", 0.012d, record.EffectivePathLengthMetres);
            AssertEqual("collision outcome preserved", PhysicalCollisionOutcome.Penetrated, record.Outcome);

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
            input.DamageCapabilityJoules = 2500d;
            input.PenetrationCapabilityJoulesPerSquareMetre = 2500d / area;
            input.CollisionHistory = mutableHistory;

            PhysicalProjectileState state;
            PhysicalProjectileStateFailureReason reason;
            AssertTrue(
                "valid physical projectile accepted",
                PhysicalProjectileState.TryCreate(input, out state, out reason));
            AssertEqual("valid physical projectile reason", PhysicalProjectileStateFailureReason.None, reason);
            AssertEqual("physical state schema", 1, PhysicalProjectileState.SchemaVersion);
            AssertNear("physical projectile speed", 800d, state.SpeedMetresPerSecond);
            AssertNear("physical projectile momentum x", 0d, state.MomentumKilogramMetresPerSecond.X);
            AssertNear("physical projectile momentum z", 7.6d, state.MomentumKilogramMetresPerSecond.Z);
            AssertNear("physical projectile kinetic energy", 3040d, state.TranslationalKineticEnergyJoules);
            AssertNear("physical projectile equivalent diameter", 0.008d, state.EquivalentDiameterMetres);
            AssertNear("physical projectile aspect ratio", 3.125d, state.AspectRatio);
            AssertNear(
                "component-specific physical ballistic coefficient",
                0.0095d / (0.32d * area),
                state.BallisticCoefficientKilogramsPerSquareMetre);
            AssertEqual("physical projectile history copied", 1, state.CollisionHistory.Count);
            AssertTrue("physical projectile mass classified as projectile", state.IsProjectileDerivedMass);

            mutableHistory.Clear();
            AssertEqual("physical projectile history is immutable", 1, state.CollisionHistory.Count);

            double equivalentDiameter;
            AssertTrue(
                "equivalent diameter calculation succeeds",
                PhysicalProjectileGeometry.TryCalculateEquivalentDiameterMetres(
                    state.ProjectedAreaSquareMetres,
                    out equivalentDiameter));
            AssertNear("equivalent diameter round trip", 0.008d, equivalentDiameter);
        }

        private static void ValidatePhysicalProjectileInvalidFallback()
        {
            PhysicalProjectileStateInput input = CreateValidRootInput(800d, 0.01d, 0.0095d);
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
            AssertNear("available projectile mass", 0.01d, result.AvailableProjectileMassKilograms);
            AssertNear("allocated projectile mass excludes spall", 0.009d, result.AllocatedProjectileMassKilograms);
            AssertNear("retained projectile mass excludes spall", 0.009d, result.RetainedProjectileMassKilograms);
            AssertNear("target spall mass remains separate", 0.003d, result.TargetSpallMassKilograms);
            AssertNear("modeled energy losses", 1000d, result.ModeledLossEnergyJoules);
            AssertNear("residual child energy budget", 4000d, result.ResidualEnergyJoules);
            AssertNear("summed child kinetic energy", 1935d, result.ChildEnergyJoules);
            AssertNear("unallocated parent projectile mass", 0.001d, result.UnallocatedProjectileMassKilograms);
            AssertNear("unallocated residual energy", 2065d, result.UnallocatedResidualEnergyJoules);
            AssertEqual("projectile output count", 3, result.ProjectileOutputCount);
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
                PhysicalConservationFailureReason.ProjectileMassExceedsParent,
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
            AssertNear("large target spall leaves projectile allocation unchanged", 0.009d, result.AllocatedProjectileMassKilograms);

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
                PhysicalConservationFailureReason.ProjectileFragmentMissing,
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

        private static PhysicalCollisionRecord CreateValidCollisionRecord()
        {
            PhysicalCollisionRecord record;
            PhysicalCollisionRecordFailureReason reason;
            if (!PhysicalCollisionRecord.TryCreate(CreateValidCollisionInput(), out record, out reason))
            {
                throw new InvalidOperationException("Valid collision record creation failed: " + reason + ".");
            }

            return record;
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
            bool isSpall = kind == PhysicalProjectileKind.TargetSpall;
            double diameterMetres = isSpall ? 0.004d : 0.003d;
            double area;
            if (!PhysicalProjectileGeometry.TryCalculateCircularAreaSquareMetres(diameterMetres, out area))
            {
                throw new InvalidOperationException("Could not calculate child projectile area.");
            }

            double energyJoules = 0.5d * massKilograms * speedMetresPerSecond * speedMetresPerSecond;
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
                DeterministicSeed = parent.DeterministicSeed + (ulong)(fragmentIndex + 1),
                Construction = isSpall
                    ? PhysicalProjectileConstruction.TargetMaterial
                    : parent.Construction,
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
                Orientation = PhysicalOrientation.Identity,
                YawAngleRadians = isSpall ? 0.7d : 0.35d,
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
            PhysicalProjectileState state;
            PhysicalProjectileStateFailureReason reason;
            if (!PhysicalProjectileState.TryCreate(input, out state, out reason))
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
            PhysicalProjectileState state;
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

            var half = CalculateOrThrow(snb.InitialSpeed * 0.5d, snb.InitialSpeed);
            var full = CalculateOrThrow(snb.InitialSpeed, snb.InitialSpeed);
            var accelerated = CalculateOrThrow(snb.InitialSpeed * 1.2d, snb.InitialSpeed);

            // Each new shot begins with the prior shot's already scaled stat value, so factors multiply
            // sequentially instead of being added as independent contributions.
            var cumulativePenetration = snb.PenetrationPower;
            cumulativePenetration *= half.PenetrationFactor;
            cumulativePenetration *= full.PenetrationFactor;
            cumulativePenetration *= accelerated.PenetrationFactor;

            var cumulativeDamage = snb.Damage;
            cumulativeDamage *= half.DamageFactor;
            cumulativeDamage *= full.DamageFactor;
            cumulativeDamage *= accelerated.DamageFactor;

            AssertNear("sequential SNB penetration", 30.325183677340327d, cumulativePenetration);
            AssertNear("sequential SNB damage", 61.139483220444205d, cumulativeDamage);
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

        private static IReadOnlyList<BallisticTemplate> LoadTemplatesWithBallisticStats(string itemsPath)
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
            catch (Exception exception)
            {
                _failed++;
                Console.Error.WriteLine("FAIL " + name + ": " + exception.Message);
            }
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
