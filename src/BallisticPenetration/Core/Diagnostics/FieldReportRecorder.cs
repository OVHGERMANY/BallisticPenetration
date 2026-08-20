#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace BallisticPenetration.Core.Diagnostics
{
    internal sealed class FieldReportOptions
    {
        [SuppressMessage(
            "Maintainability",
            "CA1512:Use ArgumentOutOfRangeException throw helper",
            Justification = "The production target is netstandard2.1 and does not expose the modern throw helpers.")]
        internal FieldReportOptions(
            string directoryPath,
            string sessionId,
            int queueCapacity,
            TimeSpan flushInterval,
            int maximumCompletedFiles,
            long maximumFolderBytes,
            long maximumFileBytes)
        {
            if (string.IsNullOrWhiteSpace(directoryPath))
            {
                throw new ArgumentException("A field-report directory is required.", nameof(directoryPath));
            }

            if (string.IsNullOrWhiteSpace(sessionId))
            {
                throw new ArgumentException("A field-report session identity is required.", nameof(sessionId));
            }

            if (queueCapacity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(queueCapacity));
            }

            if (flushInterval <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(flushInterval));
            }

            if (maximumCompletedFiles <= 0 || maximumFolderBytes <= 0 || maximumFileBytes <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maximumCompletedFiles));
            }

            DirectoryPath = Path.GetFullPath(directoryPath);
            SessionId = SanitizeSessionId(sessionId);
            QueueCapacity = queueCapacity;
            FlushInterval = flushInterval;
            MaximumCompletedFiles = maximumCompletedFiles;
            MaximumFolderBytes = maximumFolderBytes;
            MaximumFileBytes = Math.Min(maximumFileBytes, maximumFolderBytes);
        }

        internal string DirectoryPath { get; }
        internal string SessionId { get; }
        internal int QueueCapacity { get; }
        internal TimeSpan FlushInterval { get; }
        internal int MaximumCompletedFiles { get; }
        internal long MaximumFolderBytes { get; }
        internal long MaximumFileBytes { get; }

        private static string SanitizeSessionId(string value)
        {
            var builder = new StringBuilder(value.Length);
            for (int index = 0; index < value.Length; index++)
            {
                char character = value[index];
                if ((character >= 'a' && character <= 'z')
                    || (character >= 'A' && character <= 'Z')
                    || (character >= '0' && character <= '9'))
                {
                    builder.Append(character);
                }
            }

            if (builder.Length == 0)
            {
                throw new ArgumentException("The field-report session identity has no safe filename characters.", nameof(value));
            }

            return builder.ToString();
        }
    }

    internal sealed class FieldReportRecorder : IDisposable
    {
        internal const int DefaultQueueCapacity = 4096;

        private static readonly UTF8Encoding Utf8WithoutBom = new UTF8Encoding(false);
        private readonly object _gate = new object();
        private readonly Queue<FieldReportRecord> _queue = new Queue<FieldReportRecord>();
        private readonly FieldReportOptions _options;
        private readonly Action<string, Exception?>? _errorSink;
        private readonly DateTimeOffset _startedAt;
        private readonly Thread _writerThread;
        private readonly string _partialPath;
        private readonly string _completedPath;
        private FileStream? _stream;
        private StreamWriter? _writer;
        private bool _enabled;
        private bool _stopping;
        private bool _completed;
        private bool _flushRequested;
        private bool _truncated;
        private long _nextReportSequence;
        private long _logicalLengthBytes;
        private long _writtenEventCount;
        private long _droppedEventCount;
        private long _suppressedEventCount;
        private int _queueHighWaterMark;
        private int _recorderErrorCount;
        private int _issueMarkerCount;
        private int _projectilesCreated;
        private int _observedCollisions;
        private int _resolvedCollisions;
        private int _retiredProjectiles;
        private int _terminalMissingCount;
        private int _terminalDuplicateCount;
        private int _shutdownCleanupCount;
        private string _recorderErrorSummary = string.Empty;

        private FieldReportRecorder(
            FieldReportOptions options,
            FieldReportRecord sessionStart,
            Action<string, Exception?>? errorSink)
        {
            _options = options;
            _errorSink = errorSink;
            _startedAt = DateTimeOffset.Now;
            string timestamp = _startedAt.UtcDateTime.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);
            string baseName = timestamp + "-" + options.SessionId;
            _partialPath = Path.Combine(options.DirectoryPath, baseName + ".partial.bpreport");
            _completedPath = Path.Combine(options.DirectoryPath, baseName + ".bpreport");
            _writerThread = new Thread(WriterLoop)
            {
                IsBackground = true,
                Name = "BallisticPenetration field-report writer"
            };

            Initialize(sessionStart);
        }

        internal bool IsEnabled
        {
            get
            {
                lock (_gate)
                {
                    return _enabled && !_stopping;
                }
            }
        }

        internal string PartialPath => _partialPath;

        [SuppressMessage(
            "Maintainability",
            "CA1510:Use ArgumentNullException throw helper",
            Justification = "The production target is netstandard2.1 and does not expose the modern throw helpers.")]
        internal static FieldReportRecorder Start(
            FieldReportOptions options,
            FieldReportRecord sessionStart,
            Action<string, Exception?>? errorSink = null)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            if (sessionStart == null || sessionStart.EventName != "session-start")
            {
                throw new ArgumentException("The first field-report record must be session-start.", nameof(sessionStart));
            }

            return new FieldReportRecorder(options, sessionStart, errorSink);
        }

        internal static FieldReportRecorder? StartIfEnabled(
            bool enabled,
            FieldReportOptions options,
            FieldReportRecord sessionStart,
            Action<string, Exception?>? errorSink = null)
        {
            return enabled ? Start(options, sessionStart, errorSink) : null;
        }

        internal bool Record(FieldReportRecord record, bool requestFlush = false)
        {
            if (record == null)
            {
                return false;
            }

            lock (_gate)
            {
                if (!_enabled || _stopping)
                {
                    return false;
                }

                if (_truncated && !record.Critical)
                {
                    _suppressedEventCount++;
                    return false;
                }

                if (_queue.Count >= _options.QueueCapacity)
                {
                    if (!record.Critical || !RemoveOldestOrdinaryRecord())
                    {
                        _droppedEventCount++;
                        return false;
                    }

                    _droppedEventCount++;
                }

                _queue.Enqueue(record);
                if (_queue.Count > _queueHighWaterMark)
                {
                    _queueHighWaterMark = _queue.Count;
                }

                _flushRequested |= requestFlush || record.Critical;
                Monitor.Pulse(_gate);
                return true;
            }
        }

        internal void Stop()
        {
            RequestStop();

            if (_enabled && Thread.CurrentThread != _writerThread)
            {
                _writerThread.Join(TimeSpan.FromSeconds(15d));
            }
        }

        internal void RequestStop()
        {
            lock (_gate)
            {
                if (_stopping || _completed)
                {
                    return;
                }

                _stopping = true;
                _flushRequested = true;
                Monitor.PulseAll(_gate);
            }
        }

        public void Dispose()
        {
            Stop();
        }

        internal static IReadOnlyList<string> RecoverStalePartialReports(string directoryPath)
        {
            var recovered = new List<string>();
            if (!Directory.Exists(directoryPath))
            {
                return recovered;
            }

            string[] partialPaths = Directory.GetFiles(directoryPath, "*.partial.bpreport", SearchOption.TopDirectoryOnly);
            Array.Sort(partialPaths, StringComparer.Ordinal);
            for (int index = 0; index < partialPaths.Length; index++)
            {
                string partialPath = partialPaths[index];
                string fileName = Path.GetFileName(partialPath);
                string originalStem = fileName.Substring(0, fileName.Length - ".partial.bpreport".Length);
                string recoveredName = "recovered-crash-" + originalStem + ".bpreport";
                string destination = Path.Combine(directoryPath, recoveredName);
                int suffix = 2;
                while (File.Exists(destination))
                {
                    destination = Path.Combine(
                        directoryPath,
                        "recovered-crash-" + originalStem + "-" + suffix.ToString(CultureInfo.InvariantCulture) + ".bpreport");
                    suffix++;
                }

                File.Move(partialPath, destination);
                recovered.Add(destination);
            }

            return recovered;
        }

        internal static IReadOnlyList<string> ApplyRetention(
            string directoryPath,
            int maximumCompletedFiles,
            long maximumFolderBytes)
        {
            var deleted = new List<string>();
            if (!Directory.Exists(directoryPath))
            {
                return deleted;
            }

            var owned = new List<FileInfo>();
            string[] paths = Directory.GetFiles(directoryPath, "*.bpreport", SearchOption.TopDirectoryOnly);
            for (int index = 0; index < paths.Length; index++)
            {
                if (IsOwnedCompletedReport(Path.GetFileName(paths[index])))
                {
                    owned.Add(new FileInfo(paths[index]));
                }
            }

            owned.Sort(CompareOldestFirst);
            long totalBytes = owned.Sum(file => file.Length);
            int remaining = owned.Count;
            for (int index = 0;
                index < owned.Count && (remaining > maximumCompletedFiles || totalBytes > maximumFolderBytes);
                index++)
            {
                FileInfo file = owned[index];
                long length = file.Length;
                file.Delete();
                deleted.Add(file.FullName);
                totalBytes -= length;
                remaining--;
            }

            return deleted;
        }

        private void Initialize(FieldReportRecord sessionStart)
        {
            try
            {
                Directory.CreateDirectory(_options.DirectoryPath);
                ApplyRetention(
                    _options.DirectoryPath,
                    Math.Max(1, _options.MaximumCompletedFiles - 1),
                    _options.MaximumFolderBytes);
                RecoverStalePartialReports(_options.DirectoryPath);
                _stream = new FileStream(
                    _partialPath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.Read,
                    65536,
                    FileOptions.SequentialScan);
                _writer = new StreamWriter(_stream, Utf8WithoutBom, 65536, true)
                {
                    NewLine = "\n"
                };
                WriteRecord(sessionStart);
                _writer.Flush();
                _stream.Flush();
                _enabled = true;
                _writerThread.Start();
            }
            catch (Exception exception)
            {
                RegisterRecorderError("Field report initialization failed", exception);
                CloseWriterBestEffort();
            }
        }

        private void WriterLoop()
        {
            try
            {
                DateTime nextFlushUtc = DateTime.UtcNow + _options.FlushInterval;
                while (true)
                {
                    FieldReportRecord? record = null;
                    bool shouldStop;
                    bool flushNow;
                    lock (_gate)
                    {
                        while (_queue.Count == 0
                            && !_stopping
                            && !_flushRequested
                            && DateTime.UtcNow < nextFlushUtc)
                        {
                            TimeSpan wait = nextFlushUtc - DateTime.UtcNow;
                            Monitor.Wait(_gate, wait > TimeSpan.Zero ? wait : TimeSpan.FromMilliseconds(1d));
                        }

                        if (_queue.Count > 0)
                        {
                            record = _queue.Dequeue();
                        }

                        flushNow = _flushRequested || DateTime.UtcNow >= nextFlushUtc;
                        _flushRequested = false;
                        shouldStop = _stopping && record == null && _queue.Count == 0;
                    }

                    if (record != null)
                    {
                        WriteRecordWithLimit(record);
                    }

                    if (flushNow)
                    {
                        FlushWriter();
                        nextFlushUtc = DateTime.UtcNow + _options.FlushInterval;
                    }

                    if (shouldStop)
                    {
                        break;
                    }
                }

                WriteSessionEnd();
                FlushWriter();
                CloseWriterBestEffort();
                FinalizeCompletedReport();
                lock (_gate)
                {
                    _completed = true;
                    _enabled = false;
                }
            }
            catch (Exception exception)
            {
                RegisterRecorderError("Field report writer failed", exception);
                CloseWriterBestEffort();
                lock (_gate)
                {
                    _enabled = false;
                    _completed = true;
                }
            }
        }

        private void WriteRecordWithLimit(FieldReportRecord record)
        {
            if (_stream == null)
            {
                return;
            }

            long reserveBytes = Math.Min(65536L, Math.Max(1024L, _options.MaximumFileBytes / 4L));
            long applicableLimit = record.Critical
                ? _options.MaximumFileBytes
                : Math.Max(1L, _options.MaximumFileBytes - reserveBytes);
            string line = record.ToJsonLine(_nextReportSequence + 1L);
            long lineBytes = Utf8WithoutBom.GetByteCount(line) + 1L;
            if (_logicalLengthBytes + lineBytes <= applicableLimit)
            {
                CountWrittenEvent(record.EventName);
                WriteLine(line);
                return;
            }

            lock (_gate)
            {
                if (record.Critical)
                {
                    _droppedEventCount++;
                    return;
                }

                _suppressedEventCount++;
                if (_truncated)
                {
                    return;
                }

                _truncated = true;
            }

            var truncation = new FieldReportRecord(
                "report-truncated",
                true,
                new[]
                {
                    Field("maximumFileBytes", _options.MaximumFileBytes),
                    Field("suppressedEventCount", _suppressedEventCount)
                });
            string truncationLine = truncation.ToJsonLine(_nextReportSequence + 1L);
            if (_logicalLengthBytes + Utf8WithoutBom.GetByteCount(truncationLine) + 1L
                <= _options.MaximumFileBytes)
            {
                WriteLine(truncationLine);
            }
        }

        private void WriteRecord(FieldReportRecord record)
        {
            WriteLine(record.ToJsonLine(_nextReportSequence + 1L));
        }

        private void WriteLine(string line)
        {
            if (_writer == null)
            {
                throw new InvalidOperationException("The field-report writer is unavailable.");
            }

            _writer.WriteLine(line);
            _logicalLengthBytes += Utf8WithoutBom.GetByteCount(line) + 1L;
            _nextReportSequence++;
            _writtenEventCount++;
        }

        private void WriteSessionEnd()
        {
            if (_stream == null || _writer == null)
            {
                return;
            }

            _writer.Flush();
            _stream.Flush();
            DateTimeOffset endedAt = DateTimeOffset.Now;
            long finalLength = _logicalLengthBytes;
            string line = string.Empty;
            for (int attempt = 0; attempt < 4; attempt++)
            {
                var sessionEnd = new FieldReportRecord(
                    "session-end",
                    true,
                    BuildSessionEndFields(endedAt, finalLength));
                line = sessionEnd.ToJsonLine(_nextReportSequence + 1L);
                long calculated = _logicalLengthBytes + Utf8WithoutBom.GetByteCount(line) + 1L;
                if (calculated == finalLength)
                {
                    break;
                }

                finalLength = calculated;
            }

            if (_logicalLengthBytes + Utf8WithoutBom.GetByteCount(line) + 1L <= _options.MaximumFileBytes)
            {
                WriteLine(line);
            }
            else
            {
                lock (_gate)
                {
                    _droppedEventCount++;
                }
            }
        }

        private KeyValuePair<string, object?>[] BuildSessionEndFields(
            DateTimeOffset endedAt,
            long finalLength)
        {
            lock (_gate)
            {
                return new[]
                {
                    Field("sessionId", _options.SessionId),
                    Field("startTimestamp", _startedAt.UtcDateTime.ToString("O")),
                    Field("endTimestamp", endedAt.UtcDateTime.ToString("O")),
                    Field("totalDurationSeconds", Math.Max(0d, (endedAt - _startedAt).TotalSeconds)),
                    Field("totalEventsRecorded", _writtenEventCount + 1L),
                    Field("totalProjectilesCreated", _projectilesCreated),
                    Field("totalObservedCollisions", _observedCollisions),
                    Field("totalResolvedCollisions", _resolvedCollisions),
                    Field("totalRetiredProjectiles", _retiredProjectiles),
                    Field("terminalMissingCount", _terminalMissingCount),
                    Field("terminalDuplicateCount", _terminalDuplicateCount),
                    Field("shutdownCleanupCount", _shutdownCleanupCount),
                    Field("issueMarkerCount", _issueMarkerCount),
                    Field("writerQueueHighWaterMark", _queueHighWaterMark),
                    Field("droppedEventCount", _droppedEventCount),
                    Field("suppressedEventCount", _suppressedEventCount),
                    Field("reportTruncated", _truncated),
                    Field("recorderErrorCount", _recorderErrorCount),
                    Field("recorderErrors", string.IsNullOrWhiteSpace(_recorderErrorSummary) ? null : _recorderErrorSummary),
                    Field("finalReportLength", finalLength)
                };
            }
        }

        private void FinalizeCompletedReport()
        {
            string destination = _issueMarkerCount > 0
                ? Path.Combine(
                    _options.DirectoryPath,
                    Path.GetFileNameWithoutExtension(_completedPath) + "-marked.bpreport")
                : _completedPath;
            File.Move(_partialPath, destination);
            ApplyRetention(
                _options.DirectoryPath,
                _options.MaximumCompletedFiles,
                _options.MaximumFolderBytes);
        }

        private bool RemoveOldestOrdinaryRecord()
        {
            if (_queue.Count == 0)
            {
                return false;
            }

            var retained = new Queue<FieldReportRecord>(_queue.Count);
            bool removed = false;
            while (_queue.Count > 0)
            {
                FieldReportRecord candidate = _queue.Dequeue();
                if (!removed && !candidate.Critical)
                {
                    removed = true;
                    continue;
                }

                retained.Enqueue(candidate);
            }

            while (retained.Count > 0)
            {
                _queue.Enqueue(retained.Dequeue());
            }

            return removed;
        }

        private void CountWrittenEvent(string eventName)
        {
            switch (eventName)
            {
                case "created":
                    _projectilesCreated++;
                    break;
                case "collision-observed":
                    _observedCollisions++;
                    break;
                case "collision-resolved":
                    _resolvedCollisions++;
                    break;
                case "retired":
                    _retiredProjectiles++;
                    break;
                case "terminal-missing":
                    _terminalMissingCount++;
                    break;
                case "terminal-duplicate":
                    _terminalDuplicateCount++;
                    break;
                case "shutdown-cleanup":
                    _shutdownCleanupCount++;
                    break;
                case "issue-marker":
                    _issueMarkerCount++;
                    break;
            }
        }

        private void FlushWriter()
        {
            _writer?.Flush();
            _stream?.Flush();
        }

        private void RegisterRecorderError(string message, Exception exception)
        {
            lock (_gate)
            {
                _recorderErrorCount++;
                _recorderErrorSummary = message + ": " + exception.GetType().Name;
                _enabled = false;
            }

            try
            {
                _errorSink?.Invoke(message, exception);
            }
            catch
            {
                // A diagnostic error callback must not escape into recorder callers.
            }
        }

        private void CloseWriterBestEffort()
        {
            try
            {
                _writer?.Dispose();
            }
            catch
            {
                // Preserve the first recorder error.
            }

            try
            {
                _stream?.Dispose();
            }
            catch
            {
                // Preserve the first recorder error.
            }

            _writer = null;
            _stream = null;
        }

        private static bool IsOwnedCompletedReport(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName)
                || !fileName.EndsWith(".bpreport", StringComparison.OrdinalIgnoreCase)
                || fileName.EndsWith(".partial.bpreport", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string candidate = fileName.StartsWith("recovered-crash-", StringComparison.Ordinal)
                ? fileName.Substring("recovered-crash-".Length)
                : fileName;
            if (candidate.Length < 18)
            {
                return false;
            }

            string timestamp = candidate.Substring(0, 16);
            return DateTime.TryParseExact(
                timestamp,
                "yyyyMMdd'T'HHmmss'Z'",
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out _)
                && candidate[16] == '-';
        }

        private static int CompareOldestFirst(FileInfo left, FileInfo right)
        {
            int time = left.LastWriteTimeUtc.CompareTo(right.LastWriteTimeUtc);
            return time != 0
                ? time
                : string.Compare(left.Name, right.Name, StringComparison.Ordinal);
        }

        private static KeyValuePair<string, object?> Field(string name, object? value)
        {
            return new KeyValuePair<string, object?>(name, value);
        }
    }
}
