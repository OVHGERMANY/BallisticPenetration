#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using BallisticPenetration.Core.Physics;

namespace BallisticPenetration.Core.Diagnostics
{
    internal sealed class FieldReportRecord
    {
        internal const int CurrentSchemaVersion = 1;

        private readonly KeyValuePair<string, object?>[] _fields;

        internal FieldReportRecord(
            string eventName,
            bool critical,
            IEnumerable<KeyValuePair<string, object?>>? fields = null)
        {
            if (string.IsNullOrWhiteSpace(eventName))
            {
                throw new ArgumentException("A field-report event name is required.", nameof(eventName));
            }

            EventName = eventName;
            Critical = critical;
            _fields = fields == null
                ? Array.Empty<KeyValuePair<string, object?>>()
                : CopyFields(fields);
        }

        internal string EventName { get; }

        internal bool Critical { get; }

        internal string ToJsonLine(long reportSequence)
        {
            var builder = new StringBuilder(512);
            builder.Append('{');
            AppendProperty(builder, "schemaVersion", CurrentSchemaVersion, false);
            AppendProperty(builder, "event", EventName, true);
            AppendProperty(builder, "reportSequence", reportSequence, true);
            for (int index = 0; index < _fields.Length; index++)
            {
                KeyValuePair<string, object?> field = _fields[index];
                if (string.IsNullOrWhiteSpace(field.Key)
                    || field.Key == "schemaVersion"
                    || field.Key == "event"
                    || field.Key == "reportSequence")
                {
                    continue;
                }

                AppendProperty(builder, field.Key, field.Value, true);
            }

            builder.Append('}');
            return builder.ToString();
        }

        private static KeyValuePair<string, object?>[] CopyFields(
            IEnumerable<KeyValuePair<string, object?>> fields)
        {
            var copy = new List<KeyValuePair<string, object?>>();
            foreach (KeyValuePair<string, object?> field in fields)
            {
                object? value = field.Value;
                if (value is string[] strings)
                {
                    value = (string[])strings.Clone();
                }
                else if (value is IReadOnlyList<string> stringList)
                {
                    var stringCopy = new string[stringList.Count];
                    for (int index = 0; index < stringList.Count; index++)
                    {
                        stringCopy[index] = stringList[index] ?? string.Empty;
                    }

                    value = stringCopy;
                }

                copy.Add(new KeyValuePair<string, object?>(field.Key, value));
            }

            return copy.ToArray();
        }

        private static void AppendProperty(
            StringBuilder builder,
            string name,
            object? value,
            bool prependComma)
        {
            if (prependComma)
            {
                builder.Append(',');
            }

            AppendString(builder, name);
            builder.Append(':');
            AppendValue(builder, value);
        }

        private static void AppendValue(StringBuilder builder, object? value)
        {
            switch (value)
            {
                case null:
                    builder.Append("null");
                    return;
                case string text:
                    AppendString(builder, text);
                    return;
                case bool boolean:
                    builder.Append(boolean ? "true" : "false");
                    return;
                case int integer:
                    builder.Append(integer.ToString(CultureInfo.InvariantCulture));
                    return;
                case long longInteger:
                    builder.Append(longInteger.ToString(CultureInfo.InvariantCulture));
                    return;
                case float single:
                    AppendFiniteNumber(builder, single);
                    return;
                case double number:
                    AppendFiniteNumber(builder, number);
                    return;
                case DateTimeOffset timestamp:
                    AppendString(builder, timestamp.ToString("O", CultureInfo.InvariantCulture));
                    return;
                case PhysicalVector3 vector:
                    builder.Append("{\"x\":");
                    AppendFiniteNumber(builder, vector.X);
                    builder.Append(",\"y\":");
                    AppendFiniteNumber(builder, vector.Y);
                    builder.Append(",\"z\":");
                    AppendFiniteNumber(builder, vector.Z);
                    builder.Append('}');
                    return;
                case string[] strings:
                    builder.Append('[');
                    for (int index = 0; index < strings.Length; index++)
                    {
                        if (index > 0)
                        {
                            builder.Append(',');
                        }

                        AppendString(builder, strings[index] ?? string.Empty);
                    }

                    builder.Append(']');
                    return;
                default:
                    AppendString(builder, Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty);
                    return;
            }
        }

        private static void AppendFiniteNumber(StringBuilder builder, double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                builder.Append("null");
                return;
            }

            builder.Append(value.ToString("R", CultureInfo.InvariantCulture));
        }

        private static void AppendString(StringBuilder builder, string value)
        {
            builder.Append('"');
            for (int index = 0; index < value.Length; index++)
            {
                char character = value[index];
                switch (character)
                {
                    case '"':
                        builder.Append("\\\"");
                        break;
                    case '\\':
                        builder.Append("\\\\");
                        break;
                    case '\b':
                        builder.Append("\\b");
                        break;
                    case '\f':
                        builder.Append("\\f");
                        break;
                    case '\n':
                        builder.Append("\\n");
                        break;
                    case '\r':
                        builder.Append("\\r");
                        break;
                    case '\t':
                        builder.Append("\\t");
                        break;
                    default:
                        if (character < 0x20)
                        {
                            builder.Append("\\u");
                            builder.Append(((int)character).ToString("x4", CultureInfo.InvariantCulture));
                        }
                        else
                        {
                            builder.Append(character);
                        }

                        break;
                }
            }

            builder.Append('"');
        }
    }
}
