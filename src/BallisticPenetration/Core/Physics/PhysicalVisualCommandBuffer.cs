#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace BallisticPenetration.Core.Physics
{
    /// <summary>
    /// Thread-safe bounded FIFO used to move immutable render commands onto the main thread.
    /// Overflow retains the newest bounded window while preserving the order of every retained item.
    /// </summary>
    internal sealed class PhysicalVisualCommandBuffer<T>
    {
        private readonly object _gate = new object();
        private readonly Queue<T> _commands = new Queue<T>();

        internal PhysicalVisualCommandBuffer(int capacity)
        {
            if (capacity <= 0)
            {
                ThrowOutOfRange(nameof(capacity));
            }

            Capacity = capacity;
        }

        internal int Capacity { get; }

        internal int Count
        {
            get
            {
                lock (_gate)
                {
                    return _commands.Count;
                }
            }
        }

        internal bool Enqueue(T command)
        {
            lock (_gate)
            {
                bool retainedWithoutEviction = true;
                while (_commands.Count >= Capacity)
                {
                    _commands.Dequeue();
                    retainedWithoutEviction = false;
                }

                _commands.Enqueue(command);
                return retainedWithoutEviction;
            }
        }

        internal int DrainTo(List<T> destination, int maximumItems)
        {
            if (destination == null)
            {
                ThrowDestinationNull();
            }
            if (maximumItems <= 0)
            {
                ThrowOutOfRange(nameof(maximumItems));
            }

            destination.Clear();
            lock (_gate)
            {
                int count = Math.Min(maximumItems, _commands.Count);
                for (int index = 0; index < count; index++)
                {
                    destination.Add(_commands.Dequeue());
                }

                return count;
            }
        }

        internal void Clear()
        {
            lock (_gate)
            {
                _commands.Clear();
            }
        }

        [DoesNotReturn]
        private static void ThrowDestinationNull()
        {
            throw new ArgumentNullException("destination");
        }

        [DoesNotReturn]
        private static void ThrowOutOfRange(string parameterName)
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }
    }
}
