#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using BallisticPenetration.Core;
using BallisticPenetration.Core.Diagnostics;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using UnityEngine;

namespace BallisticPenetration.Runtime.Diagnostics
{
    internal static class FieldReportRuntime
    {
        internal const int QueueCapacity = FieldReportRecorder.DefaultQueueCapacity;
        private const int MaximumRecentProjectileIdentities = 16;
        private const double IssueMarkerDebounceSeconds = 0.35d;

        private static readonly object Gate = new object();
        private static readonly Queue<string> RecentProjectileIdentities = new Queue<string>();
        private static readonly FieldReportRuntimeErrorAccumulator RuntimeErrors =
            new FieldReportRuntimeErrorAccumulator();
        private static FieldReportRecorder? _recorder;
        private static string _sessionId = string.Empty;
        private static int _markerSequence;
        private static double _lastMarkerAt = double.NegativeInfinity;

        internal static bool IsEnabled
        {
            get
            {
                lock (Gate)
                {
                    return _recorder?.IsEnabled == true;
                }
            }
        }

        [SuppressMessage(
            "Design",
            "CA1031:Do not catch general exception types",
            Justification = "The optional recorder must never prevent plugin startup.")]
        internal static void Initialize(PluginConfiguration configuration)
        {
            if (configuration == null || !configuration.EnableFieldBugReports.Value)
            {
                return;
            }

            try
            {
                lock (Gate)
                {
                    if (_recorder != null)
                    {
                        return;
                    }

                    _sessionId = Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture).Substring(0, 16);
                    string reportDirectory = Path.Combine(
                        BepInEx.Paths.BepInExRootPath,
                        "FieldReports",
                        "BallisticPenetration");
                    var options = new FieldReportOptions(
                        reportDirectory,
                        _sessionId,
                        QueueCapacity,
                        TimeSpan.FromSeconds(configuration.FieldReportFlushIntervalSeconds.Value),
                        configuration.FieldReportMaximumCompletedFiles.Value,
                        ToBytes(configuration.FieldReportMaximumFolderMiB.Value),
                        ToBytes(configuration.FieldReportMaximumFileMiB.Value));
                    _recorder = FieldReportRecorder.StartIfEnabled(
                        configuration.EnableFieldBugReports.Value,
                        options,
                        BuildSessionStart(configuration),
                        LogRecorderError);
                    if (_recorder?.IsEnabled != true)
                    {
                        _recorder = null;
                    }
                }
            }
            catch (Exception exception)
            {
                LogRecorderError("Field report startup failed", exception);
                lock (Gate)
                {
                    _recorder = null;
                }
            }
        }

        [SuppressMessage(
            "Design",
            "CA1031:Do not catch general exception types",
            Justification = "Field-report production must never escape into projectile processing.")]
        internal static void RecordLifecycle(FieldReportLifecycleEventSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return;
            }

            try
            {
                lock (Gate)
                {
                    RememberProjectile(snapshot.ProjectileIdentity);
                    bool critical = snapshot.EventName == "terminal-missing"
                        || snapshot.EventName == "terminal-duplicate"
                        || snapshot.EventName == "shutdown-cleanup";
                    _recorder?.Record(snapshot.ToRecord(critical), critical);
                }
            }
            catch (Exception exception)
            {
                DisableAfterProducerError("Field report lifecycle recording failed", exception);
            }
        }

        [SuppressMessage(
            "Design",
            "CA1031:Do not catch general exception types",
            Justification = "Field-report production must never escape into gameplay or shutdown callers.")]
        internal static void RecordEvent(
            string eventName,
            bool critical,
            params KeyValuePair<string, object?>[] fields)
        {
            try
            {
                lock (Gate)
                {
                    _recorder?.Record(new FieldReportRecord(eventName, critical, fields), critical);
                }
            }
            catch (Exception exception)
            {
                DisableAfterProducerError("Field report event recording failed", exception);
            }
        }

        [SuppressMessage(
            "Design",
            "CA1031:Do not catch general exception types",
            Justification = "Runtime-error reporting must never replace the original failure.")]
        internal static void RecordRuntimeError(string source, Exception exception)
        {
            if (exception == null)
            {
                return;
            }

            try
            {
                lock (Gate)
                {
                    if (_recorder?.IsEnabled != true)
                    {
                        return;
                    }

                    FieldReportRuntimeErrorSnapshot snapshot = RuntimeErrors.Capture(
                        source,
                        exception,
                        DateTimeOffset.Now);
                    if (snapshot.IncludeFullDetail)
                    {
                        _recorder.Record(CreateRuntimeErrorRecord("runtime-error", snapshot, true), true);
                    }
                    else if (FieldReportRuntimeErrorAccumulator.ShouldEmitAggregate(
                        snapshot.OccurrenceCount))
                    {
                        _recorder.Record(
                            CreateRuntimeErrorRecord("runtime-error-aggregate", snapshot, false),
                            true);
                    }
                }
            }
            catch (Exception reportingException)
            {
                DisableAfterProducerError(
                    "Field report runtime-error recording failed",
                    reportingException);
            }
        }

        internal static string CreateProfileAlias(string? rawIdentity)
        {
            if (string.IsNullOrWhiteSpace(rawIdentity))
            {
                return string.Empty;
            }

            string sessionId;
            lock (Gate)
            {
                sessionId = _sessionId;
            }

            try
            {
                using (SHA256 algorithm = SHA256.Create())
                {
                    byte[] bytes = Encoding.UTF8.GetBytes(sessionId + ":" + rawIdentity);
                    byte[] hash = algorithm.ComputeHash(bytes);
                    return "profile-" + ToHex(hash).Substring(0, 16).ToLowerInvariant();
                }
            }
            catch
            {
                return string.Empty;
            }
        }

        [SuppressMessage(
            "Design",
            "CA1031:Do not catch general exception types",
            Justification = "Input polling for the optional marker must fail open.")]
        internal static void UpdateIssueMarker(PluginConfiguration configuration)
        {
            try
            {
                if (!IsEnabled || !configuration.FieldReportIssueMarkerKey.Value.IsDown())
                {
                    return;
                }

                double now = Time.realtimeSinceStartupAsDouble;
                lock (Gate)
                {
                    if (now - _lastMarkerAt < IssueMarkerDebounceSeconds)
                    {
                        return;
                    }

                    _lastMarkerAt = now;
                    _markerSequence++;
                    DateTimeOffset timestamp = DateTimeOffset.Now;
                    string[] identities = RecentProjectileIdentities.ToArray();
                    _recorder?.Record(
                        new FieldReportRecord(
                            "issue-marker",
                            true,
                            new[]
                            {
                                Field("sessionId", _sessionId),
                                Field("markerSequence", _markerSequence),
                                Field("utcTimestamp", timestamp.UtcDateTime.ToString("O")),
                                Field("localTimestamp", timestamp.ToString("O")),
                                Field("activeOrRecentProjectileIdentities", identities),
                                Field("lastKnownPlayerPosition", null)
                            }),
                        true);
                }

                Plugin.Log?.LogInfo("BallisticPenetration field report issue marker " + _markerSequence + " recorded.");
            }
            catch (Exception exception)
            {
                LogRecorderError("Field report issue marker failed", exception);
            }
        }

        [SuppressMessage(
            "Design",
            "CA1031:Do not catch general exception types",
            Justification = "The optional recorder must never interrupt plugin shutdown.")]
        internal static void Shutdown()
        {
            FieldReportRecorder? recorder;
            lock (Gate)
            {
                if (_recorder?.IsEnabled == true)
                {
                    IReadOnlyList<FieldReportRuntimeErrorSnapshot> totals =
                        RuntimeErrors.SnapshotTotals();
                    for (int index = 0; index < totals.Count; index++)
                    {
                        try
                        {
                            _recorder.Record(
                                CreateRuntimeErrorRecord(
                                    "runtime-error-summary",
                                    totals[index],
                                    false),
                                true);
                        }
                        catch (Exception exception)
                        {
                            LogRecorderError(
                                "Field report runtime-error summary failed",
                                exception);
                            break;
                        }
                    }
                }

                recorder = _recorder;
                _recorder = null;
            }

            try
            {
                recorder?.Stop();
            }
            catch (Exception exception)
            {
                LogRecorderError("Field report shutdown failed", exception);
            }
            finally
            {
                lock (Gate)
                {
                    RecentProjectileIdentities.Clear();
                    _sessionId = string.Empty;
                    _markerSequence = 0;
                    _lastMarkerAt = double.NegativeInfinity;
                    RuntimeErrors.Clear();
                }
            }
        }

        private static FieldReportRecord CreateRuntimeErrorRecord(
            string eventName,
            FieldReportRuntimeErrorSnapshot snapshot,
            bool includeFullDetail)
        {
            var fields = new List<KeyValuePair<string, object?>>
            {
                Field("source", snapshot.Source),
                Field("utcTimestamp", snapshot.LastTimestamp.UtcDateTime.ToString("O")),
                Field("localTimestamp", snapshot.LastTimestamp.ToString("O")),
                Field("exceptionType", snapshot.ExceptionType),
                Field("hResult", snapshot.HResult),
                Field("stackFingerprint", snapshot.StackFingerprint),
                Field("occurrenceCount", snapshot.OccurrenceCount),
                Field("firstUtcTimestamp", snapshot.FirstTimestamp.UtcDateTime.ToString("O")),
                Field("lastUtcTimestamp", snapshot.LastTimestamp.UtcDateTime.ToString("O"))
            };
            if (includeFullDetail)
            {
                fields.Add(Field("sanitizedMessage", snapshot.SanitizedMessage));
                fields.Add(Field("topMethods", snapshot.TopMethods));
            }

            return new FieldReportRecord(eventName, true, fields);
        }

        private static FieldReportRecord BuildSessionStart(PluginConfiguration configuration)
        {
            DateTimeOffset timestamp = DateTimeOffset.Now;
            Assembly assembly = typeof(Plugin).Assembly;
            string assemblyPath = assembly.Location;
            string informationalVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion ?? string.Empty;
            string[] loadedPlugins = Chainloader.PluginInfos.Values
                .Where(info => info?.Metadata != null)
                .Select(info => info.Metadata.Name + "@" + info.Metadata.Version)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            string sptVersion = Chainloader.PluginInfos.TryGetValue(
                    SptVersionCompatibility.CorePluginGuid,
                    out PluginInfo? sptPlugin)
                ? sptPlugin?.Metadata?.Version?.ToString() ?? string.Empty
                : string.Empty;
            string hollywoodPath = Path.Combine(BepInEx.Paths.PluginPath, "HollywoodFX", "HollywoodFX.dll");
            FileMetadata hollywood = ReadFileMetadata(hollywoodPath);
            FileMetadata runningAssembly = ReadFileMetadata(assemblyPath);
            string[] relevantConfiguration =
            {
                "Enabled=" + configuration.Enabled.Value,
                "DamageArmorOnCorpses=" + configuration.DamageArmorOnCorpses.Value,
                "EnableExperimentalPhysicalProjectiles=" + configuration.EnableExperimentalPhysicalProjectiles.Value,
                "PenetrationExponent=" + configuration.PenetrationExponent.Value.ToString(CultureInfo.InvariantCulture),
                "DamageExponent=" + configuration.DamageExponent.Value.ToString(CultureInfo.InvariantCulture),
                "LogPhysicalProjectileLifecycle=" + configuration.LogPhysicalProjectileLifecycle.Value,
                "EnableInGameDiagnostics=" + configuration.EnableInGameDiagnostics.Value
            };
            string[] fieldConfiguration =
            {
                "Enabled=" + configuration.EnableFieldBugReports.Value,
                "IssueMarkerKey=" + configuration.FieldReportIssueMarkerKey.Value,
                "FlushIntervalSeconds=" + configuration.FieldReportFlushIntervalSeconds.Value.ToString(CultureInfo.InvariantCulture),
                "MaximumCompletedFiles=" + configuration.FieldReportMaximumCompletedFiles.Value,
                "MaximumFolderMiB=" + configuration.FieldReportMaximumFolderMiB.Value,
                "MaximumFileMiB=" + configuration.FieldReportMaximumFileMiB.Value,
                "EffectiveMaximumFileMiB=" + Math.Min(
                    configuration.FieldReportMaximumFileMiB.Value,
                    configuration.FieldReportMaximumFolderMiB.Value),
                "QueueCapacity=" + QueueCapacity
            };

            return new FieldReportRecord(
                "session-start",
                true,
                new[]
                {
                    Field("sessionId", _sessionId),
                    Field("utcTimestamp", timestamp.UtcDateTime.ToString("O")),
                    Field("localTimestamp", timestamp.ToString("O")),
                    Field("utcOffset", timestamp.Offset.ToString()),
                    Field("assemblyVersion", assembly.GetName().Version?.ToString()),
                    Field("assemblyInformationalVersion", informationalVersion),
                    Field("runningDllFileName", Path.GetFileName(assemblyPath)),
                    Field("runningDllSha256", runningAssembly.Sha256),
                    Field("runningDllLength", runningAssembly.Length),
                    Field("bepInExVersion", typeof(BaseUnityPlugin).Assembly.GetName().Version?.ToString()),
                    Field("sptVersion", sptVersion),
                    Field("gameVersion", Application.version),
                    Field("runtimeVersion", Environment.Version.ToString()),
                    Field("operatingSystemVersion", Environment.OSVersion.VersionString),
                    Field("loadedPlugins", loadedPlugins),
                    Field("ballisticPenetrationConfiguration", relevantConfiguration),
                    Field("diagnosticsEnabled", configuration.EnableInGameDiagnostics.Value),
                    Field("fieldRecorderConfiguration", fieldConfiguration),
                    Field("reportDirectory", @"BepInEx\FieldReports\BallisticPenetration"),
                    Field("hollywoodFxDllFileName", hollywood.Exists ? "HollywoodFX.dll" : null),
                    Field("hollywoodFxDllSha256", hollywood.Sha256),
                    Field("hollywoodFxDllLength", hollywood.Length)
                });
        }

        private static FileMetadata ReadFileMetadata(string path)
        {
            try
            {
                if (!File.Exists(path))
                {
                    return new FileMetadata(false, null, null);
                }

                var info = new FileInfo(path);
                using (FileStream stream = File.OpenRead(path))
                using (SHA256 algorithm = SHA256.Create())
                {
                    return new FileMetadata(true, info.Length, ToHex(algorithm.ComputeHash(stream)));
                }
            }
            catch
            {
                return new FileMetadata(false, null, null);
            }
        }

        private static void RememberProjectile(string projectileIdentity)
        {
            if (string.IsNullOrWhiteSpace(projectileIdentity))
            {
                return;
            }

            string[] current = RecentProjectileIdentities.ToArray();
            RecentProjectileIdentities.Clear();
            for (int index = 0; index < current.Length; index++)
            {
                if (!string.Equals(current[index], projectileIdentity, StringComparison.Ordinal))
                {
                    RecentProjectileIdentities.Enqueue(current[index]);
                }
            }

            RecentProjectileIdentities.Enqueue(projectileIdentity);
            while (RecentProjectileIdentities.Count > MaximumRecentProjectileIdentities)
            {
                RecentProjectileIdentities.Dequeue();
            }
        }

        private static long ToBytes(int mebibytes)
        {
            return mebibytes * 1024L * 1024L;
        }

        private static string ToHex(byte[] bytes)
        {
            return BitConverter.ToString(bytes).Replace("-", string.Empty);
        }

        private static void LogRecorderError(string message, Exception? exception)
        {
            try
            {
                Plugin.Log?.LogWarning(
                    message
                    + "; field recording is disabled for this session. Error type: "
                    + (exception?.GetType().Name ?? "unknown")
                    + ".");
            }
            catch
            {
                // Recorder logging must never interrupt the game.
            }
        }

        private static void DisableAfterProducerError(string message, Exception exception)
        {
            FieldReportRecorder? recorder;
            lock (Gate)
            {
                recorder = _recorder;
                _recorder = null;
            }

            try
            {
                recorder?.RequestStop();
            }
            catch
            {
                // The recorder is already detached from projectile processing.
            }

            LogRecorderError(message, exception);
        }

        private static KeyValuePair<string, object?> Field(string name, object? value)
        {
            return new KeyValuePair<string, object?>(name, value);
        }

        private sealed class FileMetadata
        {
            internal FileMetadata(bool exists, long? length, string? sha256)
            {
                Exists = exists;
                Length = length;
                Sha256 = sha256;
            }

            internal bool Exists { get; }
            internal long? Length { get; }
            internal string? Sha256 { get; }
        }
    }
}
