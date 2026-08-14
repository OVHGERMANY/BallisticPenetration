using BepInEx.Configuration;
using BallisticPenetration.Core.Physics;

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
                "Enable BallisticPenetration gameplay changes.");

            DamageArmorOnCorpses = config.Bind(
                "General",
                "Damage Armor On Corpses",
                true,
                "Apply Tarkov's normal armor durability damage when a forward shot hits armor worn by a corpse.");

            EnableExperimentalPhysicalProjectiles = config.Bind(
                "Experimental",
                "Enable Physical Projectiles",
                false,
                "Enable the unaccepted physical projectile, deformation, fragmentation, and individual-child runtime path. Keep disabled outside controlled testing.");

            RenderPhysicalComponents = config.Bind(
                "Physical Rendering",
                "Render Physical Components",
                true,
                "Draw dedicated geometry for physically modeled bullets, fragments, embedded components, and target spall. This is active only when experimental physical projectiles are enabled.");

            MaximumVisiblePhysicalComponents = config.Bind(
                "Physical Rendering",
                "Maximum Visible Components",
                128,
                new ConfigDescription(
                    "Maximum number of physical component meshes visible at once. Nearest components win the budget.",
                    new AcceptableValueRange<int>(
                        PhysicalVisualPolicy.MinimumVisibleCapacity,
                        PhysicalVisualPolicy.MaximumVisibleCapacity)));

            MaximumTrackedPhysicalComponents = config.Bind(
                "Physical Rendering",
                "Maximum Tracked Components",
                512,
                new ConfigDescription(
                    "Maximum live and embedded physical components retained by the visual tracker.",
                    new AcceptableValueRange<int>(
                        PhysicalVisualPolicy.MinimumTrackedCapacity,
                        PhysicalVisualPolicy.MaximumTrackedCapacity)));

            MaximumPhysicalCommandsProcessedPerFrame = config.Bind(
                "Physical Rendering",
                "Maximum Commands Processed Per Frame",
                256,
                new ConfigDescription(
                    "Maximum queued physical-render lifecycle commands processed during one frame. Remaining commands retain FIFO order for later frames.",
                    new AcceptableValueRange<int>(
                        PhysicalVisualPolicy.MinimumCommandProcessingBudget,
                        PhysicalVisualPolicy.MaximumCommandProcessingBudget)));

            PhysicalComponentCullingDistanceMeters = config.Bind(
                "Physical Rendering",
                "Culling Distance Meters",
                200f,
                new ConfigDescription(
                    "Components farther than this distance from the active camera release their pooled visual slot.",
                    new AcceptableValueRange<float>(
                        (float)PhysicalVisualPolicy.MinimumCullingDistanceMetres,
                        (float)PhysicalVisualPolicy.MaximumCullingDistanceMetres)));

            PhysicalComponentDimensionScale = config.Bind(
                "Physical Rendering",
                "Dimension Scale",
                1f,
                new ConfigDescription(
                    "Uniform visual-only multiplier for component diameter and length. One preserves calculated physical size.",
                    new AcceptableValueRange<float>(
                        (float)PhysicalVisualPolicy.MinimumDimensionScale,
                        (float)PhysicalVisualPolicy.MaximumDimensionScale)));

            MinimumRenderedPhysicalDiameterMillimeters = config.Bind(
                "Physical Rendering",
                "Minimum Rendered Diameter Millimeters",
                0f,
                new ConfigDescription(
                    "Visual-only minimum diameter that preserves aspect ratio when enlarging tiny fragments. Zero preserves calculated dimensions exactly.",
                    new AcceptableValueRange<float>(
                        0f,
                        (float)(PhysicalVisualPolicy.MaximumMinimumDiameterMetres * 1000d))));

            EmbeddedPhysicalComponentLifetimeSeconds = config.Bind(
                "Physical Rendering",
                "Embedded Component Lifetime Seconds",
                45f,
                new ConfigDescription(
                    "How long stopped or embedded physical component geometry remains eligible for rendering.",
                    new AcceptableValueRange<float>(
                        (float)PhysicalVisualPolicy.MinimumEmbeddedLifetimeSeconds,
                        (float)PhysicalVisualPolicy.MaximumEmbeddedLifetimeSeconds)));

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

        internal ConfigEntry<bool> DamageArmorOnCorpses { get; private set; }

        internal ConfigEntry<bool> EnableExperimentalPhysicalProjectiles { get; private set; }

        internal ConfigEntry<bool> RenderPhysicalComponents { get; private set; }

        internal ConfigEntry<int> MaximumVisiblePhysicalComponents { get; private set; }

        internal ConfigEntry<int> MaximumTrackedPhysicalComponents { get; private set; }

        internal ConfigEntry<int> MaximumPhysicalCommandsProcessedPerFrame { get; private set; }

        internal ConfigEntry<float> PhysicalComponentCullingDistanceMeters { get; private set; }

        internal ConfigEntry<float> PhysicalComponentDimensionScale { get; private set; }

        internal ConfigEntry<float> MinimumRenderedPhysicalDiameterMillimeters { get; private set; }

        internal ConfigEntry<float> EmbeddedPhysicalComponentLifetimeSeconds { get; private set; }

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

        internal bool TryGetVisualPolicy(out PhysicalVisualPolicy? policy)
        {
            return PhysicalVisualPolicy.TryCreate(
                MaximumVisiblePhysicalComponents.Value,
                MaximumTrackedPhysicalComponents.Value,
                PhysicalComponentCullingDistanceMeters.Value,
                PhysicalComponentDimensionScale.Value,
                MinimumRenderedPhysicalDiameterMillimeters.Value / 1000d,
                EmbeddedPhysicalComponentLifetimeSeconds.Value,
                MaximumPhysicalCommandsProcessedPerFrame.Value,
                out policy,
                out _);
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
