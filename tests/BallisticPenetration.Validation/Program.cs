using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;
using BallisticPenetration.Core;

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
