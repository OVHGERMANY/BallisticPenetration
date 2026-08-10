using System;
using System.Collections.Generic;
using System.Globalization;
using EFT.Ballistics;
using UnityEngine;
using BallisticPenetration.Core;
using BallisticPenetration.Runtime.State;

namespace BallisticPenetration.Runtime.Diagnostics
{
    /// <summary>
    /// Values shown by the live display for one collision. Local output is set
    /// only when BallisticPenetration writes new damage and penetration values.
    /// </summary>
    internal sealed class AdjustmentDiagnosticRecord
    {
        private static readonly CultureInfo InvariantCulture = CultureInfo.InvariantCulture;

        private AdjustmentDiagnosticRecord(
            float recordedAtSeconds,
            string templateId,
            string templateName,
            float impactSpeed,
            float templateSpeed,
            double speedFraction,
            double penetrationFactor,
            double damageFactor,
            float entryDamage,
            float patchInputDamage,
            float localOutputDamage,
            float entryPenetrationPower,
            float patchInputPenetrationPower,
            float localOutputPenetrationPower,
            CollisionAdjustmentResult adjustmentResult,
            bool hasImpactPosition,
            Vector3 impactPosition,
            bool hasTraceSegment,
            Vector3 traceStart,
            bool hasTrajectoryPath,
            Vector3[] trajectoryPoints)
        {
            RecordedAtSeconds = recordedAtSeconds;
            TemplateId = templateId;
            TemplateName = templateName;
            ImpactSpeed = impactSpeed;
            TemplateSpeed = templateSpeed;
            SpeedFraction = speedFraction;
            PenetrationFactor = penetrationFactor;
            DamageFactor = damageFactor;
            EntryDamage = entryDamage;
            PatchInputDamage = patchInputDamage;
            LocalOutputDamage = localOutputDamage;
            EntryPenetrationPower = entryPenetrationPower;
            PatchInputPenetrationPower = patchInputPenetrationPower;
            LocalOutputPenetrationPower = localOutputPenetrationPower;
            AdjustmentResult = adjustmentResult;
            HasImpactPosition = hasImpactPosition;
            ImpactPosition = impactPosition;
            HasTraceSegment = hasTraceSegment;
            TraceStart = traceStart;
            HasTrajectoryPath = hasTrajectoryPath;
            TrajectoryPoints = trajectoryPoints;
        }

        internal long Sequence { get; set; }

        internal float RecordedAtSeconds { get; private set; }

        internal string TemplateId { get; private set; }

        internal string TemplateName { get; private set; }

        internal float ImpactSpeed { get; private set; }

        internal float TemplateSpeed { get; private set; }

        internal double SpeedFraction { get; private set; }

        internal double PenetrationFactor { get; private set; }

        internal double DamageFactor { get; private set; }

        internal float EntryDamage { get; private set; }

        internal float PatchInputDamage { get; private set; }

        internal float LocalOutputDamage { get; private set; }

        internal float EntryPenetrationPower { get; private set; }

        internal float PatchInputPenetrationPower { get; private set; }

        internal float LocalOutputPenetrationPower { get; private set; }

        internal CollisionAdjustmentResult AdjustmentResult { get; private set; }

        internal bool HasImpactPosition { get; private set; }

        internal Vector3 ImpactPosition { get; private set; }

        internal bool HasTraceSegment { get; private set; }

        internal Vector3 TraceStart { get; private set; }

        /// <summary>
        /// A bounded copy of the game's current PositionHistory. It is present
        /// only when world tracing was enabled for this collision.
        /// Its final point is always the public HitPoint captured below.
        /// </summary>
        internal bool HasTrajectoryPath { get; private set; }

        internal Vector3[] TrajectoryPoints { get; private set; }

        internal static AdjustmentDiagnosticRecord Capture(
            Shot shot,
            CollisionContext context,
            float impactSpeed,
            BallisticFalloffFactors factors,
            bool captureTrajectoryPath)
        {
            Vector3 impactPosition = default(Vector3);
            bool hasImpactPosition = false;

            // HitPoint is read only after HandleCollision has resolved the hit.
            // It is intentionally not substituted with _currentPosition.
            try
            {
                impactPosition = shot.HitPoint;
                hasImpactPosition = IsFiniteVector3(impactPosition);
            }
            catch
            {
                // Diagnostics are best effort only.
            }

            bool hasTraceSegment = hasImpactPosition && context.HasPreviousFramePosition;
            Vector3[] trajectoryPoints = captureTrajectoryPath && hasImpactPosition
                ? CaptureBoundedTrajectory(shot, impactPosition)
                : null;
            bool hasTrajectoryPath = trajectoryPoints != null && trajectoryPoints.Length >= 2;
            return new AdjustmentDiagnosticRecord(
                GetSafeRealtimeSeconds(),
                context.TemplateId ?? "(unknown)",
                context.TemplateName ?? context.TemplateId ?? "(unknown)",
                impactSpeed,
                context.TemplateInitialSpeed,
                GetDisplaySpeedFraction(impactSpeed, context.TemplateInitialSpeed, factors.SpeedFraction),
                factors.PenetrationFactor,
                factors.DamageFactor,
                context.EntryDamage,
                context.PatchInputDamage,
                context.LocalOutputDamage,
                context.EntryPenetrationPower,
                context.PatchInputPenetrationPower,
                context.LocalOutputPenetrationPower,
                context.AdjustmentResult,
                hasImpactPosition,
                impactPosition,
                hasTraceSegment,
                context.PreviousFramePosition,
                hasTrajectoryPath,
                trajectoryPoints);
        }

        internal string BuildOverlayText()
        {
            string impactPosition = HasImpactPosition
                ? "(" + Format(ImpactPosition.x) + ", " + Format(ImpactPosition.y) + ", " + Format(ImpactPosition.z) + ")"
                : "unavailable";

            return "Janky-BallisticPenetration Diagnostics\n"
                + "Ammo: " + TemplateName + " [" + TemplateId + "]\n"
                + "Result: " + BuildResultText() + "\n"
                + "Impact / template speed: " + Format(ImpactSpeed) + " / " + Format(TemplateSpeed)
                + " m/s   fraction: " + Format(SpeedFraction) + "\n"
                + BuildStatLine(
                    "Damage     ",
                    EntryDamage,
                    PatchInputDamage,
                    LocalOutputDamage) + "\n"
                + BuildStatLine(
                    "Penetration",
                    EntryPenetrationPower,
                    PatchInputPenetrationPower,
                    LocalOutputPenetrationPower) + "\n"
                + BuildFactorText() + "\n"
                + "Impact: " + impactPosition + BuildTraceSummary();
        }

        private string BuildResultText()
        {
            return AdjustmentResult == CollisionAdjustmentResult.Applied
                ? "APPLIED LOCALLY"
                : "SKIPPED BY BP - " + AdjustmentResult + " (no local stat write)";
        }

        private string BuildFactorText()
        {
            if (AdjustmentResult != CollisionAdjustmentResult.Applied)
            {
                return "Curve factors: not applied";
            }

            return "Curve factors D/P: " + Format(DamageFactor) + " / " + Format(PenetrationFactor);
        }

        private string BuildStatLine(
            string label,
            float entry,
            float patchInput,
            float localOutput)
        {
            string prefix = label
                + "   ENTRY " + Format(entry)
                + "   BP INPUT " + Format(patchInput);
            if (AdjustmentResult != CollisionAdjustmentResult.Applied)
            {
                return prefix + "   BP OUTPUT NOT WRITTEN";
            }

            return prefix
                + "   BP OUTPUT " + Format(localOutput)
                + BuildLocalDeltaText(entry, patchInput, localOutput);
        }

        private static string BuildLocalDeltaText(float entry, float patchInput, float localOutput)
        {
            float delta = localOutput - patchInput;
            string rawDelta = delta >= 0f ? "+" + Format(delta) : Format(delta);
            float denominator = Mathf.Abs(patchInput);
            float minimumDenominator = Mathf.Max(0.01f, Mathf.Abs(entry) * 0.0001f);
            if (denominator < minimumDenominator)
            {
                return "   local delta " + rawDelta;
            }

            float ratio = Mathf.Abs(delta) / denominator;
            if (!IsFinite(ratio) || ratio > 10f)
            {
                return "   local delta " + rawDelta;
            }

            float percentage = delta / denominator * 100f;
            string formattedPercentage = percentage >= 0f
                ? "+" + percentage.ToString("F1", InvariantCulture)
                : percentage.ToString("F1", InvariantCulture);
            return "   local delta " + rawDelta + " (" + formattedPercentage + "%)";
        }

        private string BuildTraceSummary()
        {
            if (HasTrajectoryPath)
            {
                return "   trajectory: " + TrajectoryPoints.Length + " points";
            }

            return HasTraceSegment ? "   trace: captured" : "   trace: unavailable";
        }

        private static Vector3[] CaptureBoundedTrajectory(Shot shot, Vector3 impactPosition)
        {
            try
            {
                IList<Vector3> history = shot.PositionHistory;
                if (history == null || history.Count == 0)
                {
                    return null;
                }

                const int maximumPoints = 30;
                int firstIndex = Math.Max(0, history.Count - maximumPoints);
                List<Vector3> finitePoints = new List<Vector3>(history.Count - firstIndex);
                for (int index = firstIndex; index < history.Count; index++)
                {
                    Vector3 point = history[index];
                    if (IsFiniteVector3(point))
                    {
                        finitePoints.Add(point);
                    }
                }

                if (finitePoints.Count == 0)
                {
                    return null;
                }

                // HandleCollision updates PositionHistory's final entry, but the
                // public HitPoint is the source of truth for this record.
                finitePoints[finitePoints.Count - 1] = impactPosition;
                return finitePoints.ToArray();
            }
            catch
            {
                return null;
            }
        }

        private static float GetSafeRealtimeSeconds()
        {
            float value = Time.realtimeSinceStartup;
            return IsFinite(value) ? value : 0f;
        }

        private static double GetDisplaySpeedFraction(
            float impactSpeed,
            float templateSpeed,
            double calculatedFraction)
        {
            if (IsFinite(calculatedFraction) && calculatedFraction > 0d)
            {
                return calculatedFraction;
            }

            if (!IsFinite(impactSpeed) || !IsFinite(templateSpeed) || templateSpeed <= 0f)
            {
                return 0d;
            }

            double fraction = impactSpeed / templateSpeed;
            return IsFinite(fraction) && fraction >= 0d ? fraction : 0d;
        }

        private static bool IsFiniteVector3(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }

        private static string Format(float value)
        {
            return value.ToString("F2", InvariantCulture);
        }

        private static string Format(double value)
        {
            return value.ToString("F4", InvariantCulture);
        }
    }
}
