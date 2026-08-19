#nullable enable

using System;
using System.Runtime.CompilerServices;

namespace BallisticPenetration.Core
{
    /// <summary>
    /// Weak host-keyed storage that rejects a binding after its pooled host has changed incarnation.
    /// Replacement and cleanup operations require exact binding ownership so stale work cannot
    /// overwrite or remove a newer binding.
    /// </summary>
    public sealed class PoolSafeReferenceBindingStore<THost, TBinding>
        where THost : class
        where TBinding : class
    {
        private readonly object _gate = new object();
        private readonly ConditionalWeakTable<THost, TBinding> _bindings =
            new ConditionalWeakTable<THost, TBinding>();
        private readonly Func<TBinding, THost, bool> _matches;

        public PoolSafeReferenceBindingStore(Func<TBinding, THost, bool> matches)
        {
            _matches = matches ?? throw new ArgumentNullException(nameof(matches));
        }

        public bool TryGet(THost host, out TBinding? binding)
        {
            _ = host ?? throw new ArgumentNullException(nameof(host));

            lock (_gate)
            {
                if (!_bindings.TryGetValue(host, out TBinding? stored) || stored == null)
                {
                    binding = null;
                    return false;
                }

                if (!_matches(stored, host))
                {
                    _bindings.Remove(host);
                    binding = null;
                    return false;
                }

                binding = stored;
                return true;
            }
        }

        public bool TryGetOrSet(THost host, TBinding candidate, out TBinding binding)
        {
            _ = host ?? throw new ArgumentNullException(nameof(host));
            _ = candidate ?? throw new ArgumentNullException(nameof(candidate));

            lock (_gate)
            {
                if (_bindings.TryGetValue(host, out TBinding? current) && current != null)
                {
                    if (_matches(current, host))
                    {
                        binding = current;
                        return true;
                    }

                    _bindings.Remove(host);
                }

                if (!_matches(candidate, host))
                {
                    binding = candidate;
                    return false;
                }

                _bindings.Add(host, candidate);
                binding = candidate;
                return true;
            }
        }

        public bool TryReplace(
            THost host,
            TBinding expected,
            TBinding replacement,
            out TBinding? committed)
        {
            _ = host ?? throw new ArgumentNullException(nameof(host));
            _ = expected ?? throw new ArgumentNullException(nameof(expected));
            _ = replacement ?? throw new ArgumentNullException(nameof(replacement));

            lock (_gate)
            {
                if (!_bindings.TryGetValue(host, out TBinding? current)
                    || current == null
                    || !ReferenceEquals(current, expected)
                    || !_matches(current, host)
                    || !_matches(replacement, host))
                {
                    committed = null;
                    return false;
                }

                _bindings.Remove(host);
                _bindings.Add(host, replacement);
                committed = replacement;
                return true;
            }
        }

        public bool Set(THost host, TBinding binding)
        {
            _ = host ?? throw new ArgumentNullException(nameof(host));
            _ = binding ?? throw new ArgumentNullException(nameof(binding));

            lock (_gate)
            {
                if (!_matches(binding, host))
                {
                    return false;
                }

                _bindings.Remove(host);
                _bindings.Add(host, binding);
                return true;
            }
        }

        public void RemoveIfSame(THost host, TBinding expected)
        {
            _ = host ?? throw new ArgumentNullException(nameof(host));
            _ = expected ?? throw new ArgumentNullException(nameof(expected));

            lock (_gate)
            {
                if (_bindings.TryGetValue(host, out TBinding? current)
                    && current != null
                    && ReferenceEquals(current, expected))
                {
                    _bindings.Remove(host);
                }
            }
        }
    }
}
