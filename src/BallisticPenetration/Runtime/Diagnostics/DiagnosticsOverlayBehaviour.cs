using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;

namespace BallisticPenetration.Runtime.Diagnostics
{
    /// <summary>
    /// Optional Unity presentation for the latest recorded adjustment. World
    /// visuals use a lazy, fixed pool of four LineRenderers: one trajectory line
    /// and three impact-cross lines. The text overlay calls IMGUI through
    /// reflection so the standalone project does not need an IMGUI reference.
    /// </summary>
    public sealed class DiagnosticsOverlayBehaviour : MonoBehaviour
    {
        private const float TraceLineWidth = 0.025f;
        private const float MinimumRenderableSegmentLength = 0.001f;

        private static Type? _guiType;
        private static MethodInfo? _guiBox;
        private static MethodInfo? _guiLabel;
        private static float _lastGuiLookupAt;
        private static bool _hasAttemptedGuiLookup;

        private LineRenderer? _trajectoryLine;
        private readonly LineRenderer?[] _impactCrossLines = new LineRenderer?[3];
        private Material? _traceMaterial;
        private long _lastVisualizedSequence = -1L;
        private float _traceExpiresAt;

        private void Update()
        {
            try
            {
                PluginConfiguration? configuration = Plugin.Configuration;
                if (configuration == null || !configuration.EnableInGameDiagnostics.Value)
                {
                    HideVisuals();
                    return;
                }

                float now = GetSafeRealtimeSeconds();
                if (HasVisibleVisuals() && HasExpired(now, _traceExpiresAt))
                {
                    HideVisuals();
                }

                AdjustmentDiagnosticRecord? latest;
                if (!DiagnosticsRuntime.TryGetLatest(out latest) || latest == null)
                {
                    return;
                }

                if (latest.Sequence == _lastVisualizedSequence)
                {
                    return;
                }

                _lastVisualizedSequence = latest.Sequence;
                if (!configuration.ShowWorldSpaceTraceAndImpactMarker.Value
                    || !IsPositiveFinite(configuration.TraceLifetimeSeconds.Value)
                    || !IsPositiveFinite(configuration.MaximumTraceSegmentMeters.Value)
                    || !IsPositiveFinite(configuration.ImpactMarkerSizeMeters.Value)
                    || HasExpired(now, latest.RecordedAtSeconds + configuration.TraceLifetimeSeconds.Value))
                {
                    HideVisuals();
                    return;
                }

                TryShowTraceAndImpactMarker(latest, configuration, now);
            }
            catch
            {
                // Optional visuals are never allowed to interfere with gameplay.
            }
        }

        private void OnGUI()
        {
            if (!isActiveAndEnabled)
            {
                return;
            }

            try
            {
                PluginConfiguration? configuration = Plugin.Configuration;
                if (configuration == null
                    || !configuration.EnableInGameDiagnostics.Value
                    || !configuration.ShowLatestAdjustmentOverlay.Value
                    || !IsPositiveFinite(configuration.OverlayLifetimeSeconds.Value))
                {
                    return;
                }

                AdjustmentDiagnosticRecord? latest;
                if (!DiagnosticsRuntime.TryGetLatest(out latest)
                    || latest == null
                    || HasExpired(GetSafeRealtimeSeconds(), latest.RecordedAtSeconds + configuration.OverlayLifetimeSeconds.Value))
                {
                    return;
                }

                TryDrawOverlay(latest.BuildOverlayText());
            }
            catch
            {
                // A missing or incompatible IMGUI implementation just disables
                // this optional overlay for the current callback.
            }
        }

        private void OnDestroy()
        {
            HideVisuals();
            DestroyLinePool();
            if (_traceMaterial != null)
            {
                UnityEngine.Object.Destroy(_traceMaterial);
                _traceMaterial = null;
            }
        }

        private void TryShowTraceAndImpactMarker(
            AdjustmentDiagnosticRecord record,
            PluginConfiguration configuration,
            float now)
        {
            HideVisuals();
            if (!record.HasImpactPosition)
            {
                return;
            }

            Material? material = GetTraceMaterial();
            if (material == null || !EnsureLinePool(material))
            {
                return;
            }

            if (record.HasTrajectoryPath)
            {
                Vector3[]? boundedPath = CreateBoundedPath(
                    record.TrajectoryPoints,
                    configuration.MaximumTraceSegmentMeters.Value);
                if (boundedPath != null && boundedPath.Length >= 2)
                {
                    ConfigurePolyline(_trajectoryLine, boundedPath, new Color(0.2f, 0.9f, 1f, 1f), TraceLineWidth);
                }
            }
            else if (record.HasTraceSegment)
            {
                Vector3 traceStart = record.TraceStart;
                Vector3 impact = record.ImpactPosition;
                Vector3 segment = impact - traceStart;
                float segmentLength = segment.magnitude;
                if (IsFinite(segmentLength) && segmentLength > MinimumRenderableSegmentLength)
                {
                    float maximumLength = configuration.MaximumTraceSegmentMeters.Value;
                    if (segmentLength > maximumLength)
                    {
                        traceStart = impact - segment / segmentLength * maximumLength;
                    }

                    ConfigureLine(_trajectoryLine, traceStart, impact, new Color(0.2f, 0.9f, 1f, 1f), TraceLineWidth);
                }
            }

            float halfMarkerSize = configuration.ImpactMarkerSizeMeters.Value * 0.5f;
            Vector3 impactPosition = record.ImpactPosition;
            Color markerColor = new Color(1f, 0.72f, 0.15f, 1f);
            ConfigureLine(
                _impactCrossLines[0],
                impactPosition - Vector3.right * halfMarkerSize,
                impactPosition + Vector3.right * halfMarkerSize,
                markerColor,
                TraceLineWidth);
            ConfigureLine(
                _impactCrossLines[1],
                impactPosition - Vector3.up * halfMarkerSize,
                impactPosition + Vector3.up * halfMarkerSize,
                markerColor,
                TraceLineWidth);
            ConfigureLine(
                _impactCrossLines[2],
                impactPosition - Vector3.forward * halfMarkerSize,
                impactPosition + Vector3.forward * halfMarkerSize,
                markerColor,
                TraceLineWidth);

            _traceExpiresAt = now + configuration.TraceLifetimeSeconds.Value;
        }

        private bool EnsureLinePool(Material material)
        {
            try
            {
                if (_trajectoryLine == null)
                {
                    _trajectoryLine = CreatePooledLine("Trajectory", material);
                }

                for (int index = 0; index < _impactCrossLines.Length; index++)
                {
                    if (_impactCrossLines[index] == null)
                    {
                        _impactCrossLines[index] = CreatePooledLine("ImpactCross" + index, material);
                    }
                }

                return _trajectoryLine != null
                    && _impactCrossLines[0] != null
                    && _impactCrossLines[1] != null
                    && _impactCrossLines[2] != null;
            }
            catch
            {
                HideVisuals();
                return false;
            }
        }

        private LineRenderer? CreatePooledLine(string suffix, Material material)
        {
            GameObject? lineObject = null;
            try
            {
                lineObject = new GameObject("Janky-BallisticPenetration." + suffix);
                lineObject.hideFlags = HideFlags.HideAndDontSave;
                lineObject.transform.SetParent(transform, true);

                LineRenderer line = lineObject.AddComponent<LineRenderer>();
                line.useWorldSpace = true;
                line.alignment = LineAlignment.View;
                line.shadowCastingMode = ShadowCastingMode.Off;
                line.receiveShadows = false;
                line.sharedMaterial = material;
                line.startWidth = TraceLineWidth;
                line.endWidth = TraceLineWidth;
                line.enabled = false;
                return line;
            }
            catch
            {
                if (lineObject != null)
                {
                    UnityEngine.Object.Destroy(lineObject);
                }

                return null;
            }
        }

        private static void ConfigureLine(LineRenderer? line, Vector3 start, Vector3 end, Color color, float width)
        {
            if (line == null)
            {
                return;
            }

            line.positionCount = 2;
            line.startWidth = width;
            line.endWidth = width;
            line.startColor = color;
            line.endColor = color;
            line.SetPosition(0, start);
            line.SetPosition(1, end);
            line.enabled = true;
        }

        private static void ConfigurePolyline(LineRenderer? line, Vector3[]? points, Color color, float width)
        {
            if (line == null || points == null || points.Length < 2)
            {
                return;
            }

            line.positionCount = points.Length;
            line.startWidth = width;
            line.endWidth = width;
            line.startColor = color;
            line.endColor = color;
            line.SetPositions(points);
            line.enabled = true;
        }

        private static Vector3[]? CreateBoundedPath(Vector3[]? points, float maximumLength)
        {
            if (points == null || points.Length < 2 || !IsPositiveFinite(maximumLength))
            {
                return null;
            }

            List<Vector3> backwardsPath = new List<Vector3>();
            Vector3 current = points[points.Length - 1];
            backwardsPath.Add(current);
            float accumulatedLength = 0f;

            for (int index = points.Length - 2; index >= 0; index--)
            {
                Vector3 candidate = points[index];
                Vector3 segment = current - candidate;
                float segmentLength = segment.magnitude;
                if (!IsFinite(segmentLength) || segmentLength <= MinimumRenderableSegmentLength)
                {
                    continue;
                }

                float remainingLength = maximumLength - accumulatedLength;
                if (remainingLength <= 0f)
                {
                    break;
                }

                if (segmentLength <= remainingLength)
                {
                    backwardsPath.Add(candidate);
                    accumulatedLength += segmentLength;
                    current = candidate;
                    continue;
                }

                backwardsPath.Add(current - segment / segmentLength * remainingLength);
                break;
            }

            if (backwardsPath.Count < 2)
            {
                return null;
            }

            backwardsPath.Reverse();
            return backwardsPath.ToArray();
        }

        private Material? GetTraceMaterial()
        {
            if (_traceMaterial != null)
            {
                return _traceMaterial;
            }

            Shader? shader = Shader.Find("Sprites/Default")
                ?? Shader.Find("Unlit/Color")
                ?? Shader.Find("Hidden/Internal-Colored");
            if (shader == null)
            {
                return null;
            }

            _traceMaterial = new Material(shader)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            return _traceMaterial;
        }

        private bool HasVisibleVisuals()
        {
            if (_trajectoryLine != null && _trajectoryLine.enabled)
            {
                return true;
            }

            for (int index = 0; index < _impactCrossLines.Length; index++)
            {
                LineRenderer? line = _impactCrossLines[index];
                if (line != null && line.enabled)
                {
                    return true;
                }
            }

            return false;
        }

        private void HideVisuals()
        {
            if (_trajectoryLine != null)
            {
                _trajectoryLine.enabled = false;
            }

            for (int index = 0; index < _impactCrossLines.Length; index++)
            {
                LineRenderer? line = _impactCrossLines[index];
                if (line != null)
                {
                    line.enabled = false;
                }
            }

            _traceExpiresAt = 0f;
        }

        private void DestroyLinePool()
        {
            DestroyPooledLine(_trajectoryLine);
            _trajectoryLine = null;

            for (int index = 0; index < _impactCrossLines.Length; index++)
            {
                DestroyPooledLine(_impactCrossLines[index]);
                _impactCrossLines[index] = null;
            }
        }

        private static void DestroyPooledLine(LineRenderer? line)
        {
            if (line != null)
            {
                UnityEngine.Object.Destroy(line.gameObject);
            }
        }

        private static void TryDrawOverlay(string text)
        {
            try
            {
                MethodInfo? box;
                MethodInfo? label;
                if (!TryGetGuiMethods(out box, out label) || box == null || label == null)
                {
                    return;
                }

                box.Invoke(null, new object[] { new Rect(14f, 14f, 760f, 208f), string.Empty });
                label.Invoke(null, new object[] { new Rect(26f, 24f, 736f, 192f), text });
            }
            catch
            {
                // Do not repeatedly invoke a cached method that was rejected by
                // a different IMGUI implementation; retry resolution later.
                _guiBox = null;
                _guiLabel = null;
            }
        }

        private static bool TryGetGuiMethods(out MethodInfo? box, out MethodInfo? label)
        {
            box = _guiBox;
            label = _guiLabel;
            if (box != null && label != null)
            {
                return true;
            }

            float now = GetSafeRealtimeSeconds();
            if (_hasAttemptedGuiLookup && now - _lastGuiLookupAt < 5f)
            {
                return false;
            }

            _hasAttemptedGuiLookup = true;
            _lastGuiLookupAt = now;
            try
            {
                _guiType = Type.GetType("UnityEngine.GUI, UnityEngine.IMGUIModule", false);
                if (_guiType == null)
                {
                    Assembly imGuiAssembly = Assembly.Load("UnityEngine.IMGUIModule");
                    _guiType = imGuiAssembly.GetType("UnityEngine.GUI", false);
                }

                if (_guiType == null)
                {
                    return false;
                }

                Type[] signature = { typeof(Rect), typeof(string) };
                _guiBox = _guiType.GetMethod("Box", BindingFlags.Public | BindingFlags.Static, null, signature, null);
                _guiLabel = _guiType.GetMethod("Label", BindingFlags.Public | BindingFlags.Static, null, signature, null);
                box = _guiBox;
                label = _guiLabel;
                return box != null && label != null;
            }
            catch
            {
                return false;
            }
        }

        private static float GetSafeRealtimeSeconds()
        {
            float value = Time.realtimeSinceStartup;
            return IsFinite(value) ? value : 0f;
        }

        private static bool HasExpired(float now, float expiry)
        {
            return IsFinite(now) && IsFinite(expiry) && now > expiry;
        }

        private static bool IsPositiveFinite(float value)
        {
            return IsFinite(value) && value > 0f;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
