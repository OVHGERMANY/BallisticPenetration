using System;
using System.Diagnostics.CodeAnalysis;
using EFT.Ballistics;
using UnityEngine;
using BallisticPenetration.Core;
using BallisticPenetration.Runtime.State;

namespace BallisticPenetration.Runtime.Diagnostics
{
    /// <summary>
    /// Isolates optional Unity presentation from the collision hook. Any failure
    /// here is swallowed so visual diagnostics can never alter a shot result.
    /// </summary>
    internal static class DiagnosticsRuntime
    {
        private static readonly object HostGate = new object();
        private static DiagnosticsOverlayBehaviour? _host;

        [SuppressMessage(
            "Design",
            "CA1031:Do not catch general exception types",
            Justification = "Diagnostic capture must fail open for arbitrary pooled EFT shot data failures.")]
        internal static void TryRecordAdjustment(
            Shot shot,
            CollisionContext context,
            float impactSpeed,
            BallisticFalloffFactors factors)
        {
            try
            {
                PluginConfiguration? configuration = Plugin.Configuration;
                if (configuration == null || !configuration.EnableInGameDiagnostics.Value)
                {
                    return;
                }

                AdjustmentDiagnosticRecord record = AdjustmentDiagnosticRecord.Capture(
                    shot,
                    context,
                    impactSpeed,
                    factors,
                    configuration.ShowWorldSpaceTraceAndImpactMarker.Value);
                DiagnosticsRecorder.Record(record);
            }
            catch
            {
                // Diagnostics must not interrupt a shot after its stats change.
            }
        }

        internal static bool TryGetLatest(out AdjustmentDiagnosticRecord? record)
        {
            return DiagnosticsRecorder.TryGetLatest(out record);
        }

        /// <summary>
        /// Called exclusively from BaseUnityPlugin.Update on Unity's main thread.
        /// Collision hooks record data only and never create Unity objects.
        /// </summary>
        [SuppressMessage(
            "Design",
            "CA1031:Do not catch general exception types",
            Justification = "Optional presentation must not propagate Unity or rendering failures into the game loop.")]
        internal static void UpdatePresentation()
        {
            try
            {
                PluginConfiguration? configuration = Plugin.Configuration;
                if (configuration == null || !configuration.EnableInGameDiagnostics.Value)
                {
                    return;
                }

                AdjustmentDiagnosticRecord? latest;
                if (!DiagnosticsRecorder.TryGetLatest(out latest) || latest == null)
                {
                    return;
                }

                EnsurePresentationHost();
            }
            catch
            {
                // Presentation initialization is optional and must remain outside
                // the ballistic hook's exception and timing domain.
            }
        }

        [SuppressMessage(
            "Design",
            "CA1031:Do not catch general exception types",
            Justification = "Shutdown must clear diagnostic state even when Unity object destruction fails.")]
        internal static void Shutdown()
        {
            try
            {
                DiagnosticsOverlayBehaviour? host;
                lock (HostGate)
                {
                    host = _host;
                    _host = null;
                }

                if (host != null)
                {
                    UnityEngine.Object.Destroy(host.gameObject);
                }

                DiagnosticsRecorder.Clear();
            }
            catch
            {
                // Plugin teardown must remain harmless if Unity is already down.
            }
        }

        [SuppressMessage(
            "Design",
            "CA1031:Do not catch general exception types",
            Justification = "Optional Unity host creation must disable diagnostics instead of breaking a frame.")]
        private static void EnsurePresentationHost()
        {
            lock (HostGate)
            {
                if (_host != null)
                {
                    return;
                }

                GameObject? hostObject = null;
                try
                {
                    hostObject = new GameObject("Janky-BallisticPenetration.Diagnostics");
                    hostObject.hideFlags = HideFlags.HideAndDontSave;
                    UnityEngine.Object.DontDestroyOnLoad(hostObject);
                    _host = hostObject.AddComponent<DiagnosticsOverlayBehaviour>();
                }
                catch
                {
                    if (hostObject != null)
                    {
                        UnityEngine.Object.Destroy(hostObject);
                    }
                }
            }
        }
    }
}
