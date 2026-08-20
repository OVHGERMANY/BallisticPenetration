#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace BallisticPenetration.Core.Diagnostics
{
    internal sealed class FieldReportRuntimeErrorSnapshot
    {
        internal FieldReportRuntimeErrorSnapshot(
            string source,
            string exceptionType,
            string sanitizedMessage,
            int hResult,
            string[] topMethods,
            string stackFingerprint,
            int occurrenceCount,
            DateTimeOffset firstTimestamp,
            DateTimeOffset lastTimestamp,
            bool includeFullDetail)
        {
            Source = source;
            ExceptionType = exceptionType;
            SanitizedMessage = sanitizedMessage;
            HResult = hResult;
            TopMethods = topMethods;
            StackFingerprint = stackFingerprint;
            OccurrenceCount = occurrenceCount;
            FirstTimestamp = firstTimestamp;
            LastTimestamp = lastTimestamp;
            IncludeFullDetail = includeFullDetail;
        }

        internal string Source { get; }
        internal string ExceptionType { get; }
        internal string SanitizedMessage { get; }
        internal int HResult { get; }
        internal string[] TopMethods { get; }
        internal string StackFingerprint { get; }
        internal int OccurrenceCount { get; }
        internal DateTimeOffset FirstTimestamp { get; }
        internal DateTimeOffset LastTimestamp { get; }
        internal bool IncludeFullDetail { get; }
    }

    internal sealed class FieldReportRuntimeErrorAccumulator
    {
        private const int MaximumUniqueErrors = 128;
        private const int MaximumMessageLength = 512;
        private const int MaximumTopMethods = 5;

        private static readonly Regex PathPattern = new Regex(
            @"(?i)(?:[a-z]:\\|\\\\)[^\s\""']+",
            RegexOptions.CultureInvariant);
        private static readonly Regex CredentialPattern = new Regex(
            @"(?i)\b(password|passwd|token|secret|authorization|cookie|api[-_]?key)\s*[:=]\s*[^\s,;]+",
            RegexOptions.CultureInvariant);

        private readonly Dictionary<string, Entry> _byFingerprint =
            new Dictionary<string, Entry>(StringComparer.Ordinal);

        internal FieldReportRuntimeErrorSnapshot Capture(
            string source,
            Exception exception,
            DateTimeOffset timestamp)
        {
            string safeSource = SanitizeText(source, 160);
            string exceptionType = exception.GetType().Name;
            string safeMessage = SanitizeText(exception.Message, MaximumMessageLength);
            string[] topMethods = GetTopMethods(exception);
            string fingerprint = CreateFingerprint(
                safeSource,
                exceptionType,
                safeMessage,
                topMethods);

            if (!_byFingerprint.TryGetValue(fingerprint, out Entry? entry))
            {
                if (_byFingerprint.Count >= MaximumUniqueErrors)
                {
                    fingerprint = "overflow-unique-runtime-errors";
                    safeSource = "runtime-error-accumulator";
                    exceptionType = "SuppressedUniqueError";
                    safeMessage = "Additional unique runtime errors were aggregated.";
                    topMethods = Array.Empty<string>();
                }

                if (!_byFingerprint.TryGetValue(fingerprint, out entry))
                {
                    entry = new Entry(
                        safeSource,
                        exceptionType,
                        safeMessage,
                        exception.HResult,
                        topMethods,
                        fingerprint,
                        timestamp);
                    _byFingerprint.Add(fingerprint, entry);
                }
            }

            entry.Count++;
            entry.LastTimestamp = timestamp;
            bool includeFullDetail = entry.Count == 1;
            return entry.ToSnapshot(includeFullDetail);
        }

        internal static bool ShouldEmitAggregate(int occurrenceCount)
        {
            return occurrenceCount > 1
                && (occurrenceCount & (occurrenceCount - 1)) == 0;
        }

        internal IReadOnlyList<FieldReportRuntimeErrorSnapshot> SnapshotTotals()
        {
            var snapshots = new List<FieldReportRuntimeErrorSnapshot>(_byFingerprint.Count);
            foreach (Entry entry in _byFingerprint.Values)
            {
                snapshots.Add(entry.ToSnapshot(false));
            }

            snapshots.Sort(delegate (
                FieldReportRuntimeErrorSnapshot left,
                FieldReportRuntimeErrorSnapshot right)
            {
                return string.Compare(
                    left.StackFingerprint,
                    right.StackFingerprint,
                    StringComparison.Ordinal);
            });
            return snapshots;
        }

        internal void Clear()
        {
            _byFingerprint.Clear();
        }

        internal static string SanitizeText(string? value, int maximumLength = MaximumMessageLength)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            string sanitized = value.Replace('\r', ' ').Replace('\n', ' ').Replace('\t', ' ');
            sanitized = ReplaceOrdinalIgnoreCase(sanitized, Environment.UserName, "[user]");
            sanitized = ReplaceOrdinalIgnoreCase(sanitized, Environment.MachineName, "[computer]");
            sanitized = PathPattern.Replace(sanitized, "[path]");
            sanitized = CredentialPattern.Replace(sanitized, "$1=[redacted]");
            return sanitized.Length <= maximumLength
                ? sanitized
                : sanitized.Substring(0, maximumLength);
        }

        private static string[] GetTopMethods(Exception exception)
        {
            try
            {
                StackFrame[]? frames = new StackTrace(exception, false).GetFrames();
                if (frames == null || frames.Length == 0)
                {
                    return Array.Empty<string>();
                }

                int count = Math.Min(MaximumTopMethods, frames.Length);
                var methods = new string[count];
                for (int index = 0; index < count; index++)
                {
                    System.Reflection.MethodBase? method = frames[index].GetMethod();
                    string typeName = method?.DeclaringType?.Name ?? "unknown";
                    methods[index] = typeName + "." + (method?.Name ?? "unknown");
                }

                return methods;
            }
            catch
            {
                return Array.Empty<string>();
            }
        }

        [SuppressMessage(
            "Performance",
            "CA1850:Prefer static HashData method over ComputeHash",
            Justification = "The production target is netstandard2.1, where SHA256.HashData is unavailable.")]
        private static string CreateFingerprint(
            string source,
            string exceptionType,
            string message,
            string[] topMethods)
        {
            try
            {
                using (SHA256 algorithm = SHA256.Create())
                {
                    string material = source + "\n" + exceptionType + "\n" + message
                        + "\n" + string.Join("\n", topMethods);
                    byte[] hash = algorithm.ComputeHash(Encoding.UTF8.GetBytes(material));
                    return ToHex(hash).Substring(0, 24).ToLowerInvariant();
                }
            }
            catch
            {
                return "fingerprint-unavailable";
            }
        }

        [SuppressMessage(
            "Performance",
            "CA1845:Use span based string.Concat",
            Justification = "The production target is netstandard2.1 and this bounded error path favors compatibility.")]
        private static string ReplaceOrdinalIgnoreCase(
            string value,
            string token,
            string replacement)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return value;
            }

            int index = value.IndexOf(token, StringComparison.OrdinalIgnoreCase);
            while (index >= 0)
            {
                value = value.Substring(0, index)
                    + replacement
                    + value.Substring(index + token.Length);
                index = value.IndexOf(
                    token,
                    index + replacement.Length,
                    StringComparison.OrdinalIgnoreCase);
            }

            return value;
        }

        [SuppressMessage(
            "Performance",
            "CA1872:Prefer Convert.ToHexString",
            Justification = "The production target is netstandard2.1, where Convert.ToHexString is unavailable.")]
        private static string ToHex(byte[] bytes)
        {
            return BitConverter.ToString(bytes).Replace("-", string.Empty);
        }

        private sealed class Entry
        {
            internal Entry(
                string source,
                string exceptionType,
                string sanitizedMessage,
                int hResult,
                string[] topMethods,
                string fingerprint,
                DateTimeOffset timestamp)
            {
                Source = source;
                ExceptionType = exceptionType;
                SanitizedMessage = sanitizedMessage;
                HResult = hResult;
                TopMethods = (string[])topMethods.Clone();
                Fingerprint = fingerprint;
                FirstTimestamp = timestamp;
                LastTimestamp = timestamp;
            }

            internal string Source { get; }
            internal string ExceptionType { get; }
            internal string SanitizedMessage { get; }
            internal int HResult { get; }
            internal string[] TopMethods { get; }
            internal string Fingerprint { get; }
            internal DateTimeOffset FirstTimestamp { get; }
            internal DateTimeOffset LastTimestamp { get; set; }
            internal int Count { get; set; }

            internal FieldReportRuntimeErrorSnapshot ToSnapshot(bool includeFullDetail)
            {
                return new FieldReportRuntimeErrorSnapshot(
                    Source,
                    ExceptionType,
                    SanitizedMessage,
                    HResult,
                    (string[])TopMethods.Clone(),
                    Fingerprint,
                    Count,
                    FirstTimestamp,
                    LastTimestamp,
                    includeFullDetail);
            }
        }
    }
}
