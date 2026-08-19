namespace BallisticPenetration.Runtime.Diagnostics
{
    /// <summary>
    /// Holds only the latest collision diagnostic event. The lock supports a
    /// safe hand-off between the collision hook and Unity presentation callbacks.
    /// </summary>
    internal static class DiagnosticsRecorder
    {
        private static readonly object Gate = new object();
        private static long _nextSequence;
        private static AdjustmentDiagnosticRecord? _latest;

        internal static void Record(AdjustmentDiagnosticRecord record)
        {
            lock (Gate)
            {
                record.Sequence = ++_nextSequence;
                _latest = record;
            }
        }

        internal static bool TryGetLatest(out AdjustmentDiagnosticRecord? record)
        {
            lock (Gate)
            {
                record = _latest;
                return record != null;
            }
        }

        internal static void Clear()
        {
            lock (Gate)
            {
                _latest = null;
            }
        }
    }
}
