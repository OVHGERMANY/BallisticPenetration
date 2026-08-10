using BepInEx.Configuration;

namespace BallisticPenetration.Runtime
{
    /// <summary>
    /// Runtime-only BepInEx configuration.  The range attributes keep normal
    /// configuration UI edits within the domain accepted by the pure calculator;
    /// callers still validate values because configuration files can be edited by hand.
    /// </summary>
    internal sealed class PluginConfiguration
    {
        internal const float MinimumExponent = 0.1f;
        internal const float MaximumExponent = 4.0f;

        internal PluginConfiguration(ConfigFile config)
        {
            Enabled = config.Bind(
                "General",
                "Enabled",
                true,
                "Enable velocity-based damage and penetration falloff for forward hits.");

            PenetrationExponent = config.Bind(
                "Falloff",
                "Penetration Exponent",
                1.4f,
                new ConfigDescription(
                    "Exponent applied to the speed fraction when calculating penetration power.",
                    new AcceptableValueRange<float>(MinimumExponent, MaximumExponent)));

            DamageExponent = config.Bind(
                "Falloff",
                "Damage Exponent",
                0.4f,
                new ConfigDescription(
                    "Exponent applied to the speed fraction when calculating damage.",
                    new AcceptableValueRange<float>(MinimumExponent, MaximumExponent)));

            LogAdjustments = config.Bind(
                "Diagnostics",
                "Log Adjustments",
                false,
                "Log each successfully applied collision adjustment. This can be noisy during raids.");

            EnableInGameDiagnostics = config.Bind(
                "Diagnostics",
                "Enable In-Game Diagnostics",
                false,
                "Show visual diagnostics for applied and skipped collisions. This never changes ballistic values.");

            ShowLatestAdjustmentOverlay = config.Bind(
                "Diagnostics",
                "Show Latest Adjustment Overlay",
                true,
                "Display the latest applied or skipped collision while diagnostics are enabled.");

            ShowWorldSpaceTraceAndImpactMarker = config.Bind(
                "Diagnostics",
                "Show World-Space Trace And Impact Marker",
                true,
                "Draw the recent trajectory path and an impact marker for the latest diagnostic collision.");

            OverlayLifetimeSeconds = config.Bind(
                "Diagnostics",
                "Overlay Lifetime Seconds",
                6f,
                new ConfigDescription(
                    "How long the latest adjustment overlay remains visible.",
                    new AcceptableValueRange<float>(0.25f, 30f)));

            TraceLifetimeSeconds = config.Bind(
                "Diagnostics",
                "Trace Lifetime Seconds",
                2f,
                new ConfigDescription(
                    "How long the world-space trace and impact marker remain visible.",
                    new AcceptableValueRange<float>(0.1f, 20f)));

            MaximumTraceSegmentMeters = config.Bind(
                "Diagnostics",
                "Maximum Trace Segment Meters",
                30f,
                new ConfigDescription(
                    "Maximum rendered length of the recent captured trajectory path or fallback segment.",
                    new AcceptableValueRange<float>(0.25f, 120f)));

            ImpactMarkerSizeMeters = config.Bind(
                "Diagnostics",
                "Impact Marker Size Meters",
                0.15f,
                new ConfigDescription(
                    "World-space size of the cross marker drawn at a captured impact point.",
                    new AcceptableValueRange<float>(0.02f, 2f)));
        }

        internal ConfigEntry<bool> Enabled { get; private set; }

        internal ConfigEntry<float> PenetrationExponent { get; private set; }

        internal ConfigEntry<float> DamageExponent { get; private set; }

        internal ConfigEntry<bool> LogAdjustments { get; private set; }

        internal ConfigEntry<bool> EnableInGameDiagnostics { get; private set; }

        internal ConfigEntry<bool> ShowLatestAdjustmentOverlay { get; private set; }

        internal ConfigEntry<bool> ShowWorldSpaceTraceAndImpactMarker { get; private set; }

        internal ConfigEntry<float> OverlayLifetimeSeconds { get; private set; }

        internal ConfigEntry<float> TraceLifetimeSeconds { get; private set; }

        internal ConfigEntry<float> MaximumTraceSegmentMeters { get; private set; }

        internal ConfigEntry<float> ImpactMarkerSizeMeters { get; private set; }

        internal bool TryGetExponentValues(out double penetrationExponent, out double damageExponent)
        {
            penetrationExponent = PenetrationExponent.Value;
            damageExponent = DamageExponent.Value;

            return IsValidExponent(penetrationExponent) && IsValidExponent(damageExponent);
        }

        private static bool IsValidExponent(double value)
        {
            return !double.IsNaN(value)
                && !double.IsInfinity(value)
                && value >= MinimumExponent
                && value <= MaximumExponent;
        }
    }
}
