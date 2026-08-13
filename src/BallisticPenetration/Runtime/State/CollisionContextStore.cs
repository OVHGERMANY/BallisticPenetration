using System.Runtime.CompilerServices;
using EFT.Ballistics;

namespace BallisticPenetration.Runtime.State
{
    /// <summary>
    /// Associates a short-lived collision snapshot with the originating Shot
    /// without extending the Shot lifetime. The lock makes the exact-context
    /// check and removal in the finalizer atomic with respect to another hook.
    /// </summary>
    internal static class CollisionContextStore
    {
        private static readonly object Gate = new object();
        private static readonly ConditionalWeakTable<Shot, CollisionContext> Contexts =
            new ConditionalWeakTable<Shot, CollisionContext>();

        internal static void Set(Shot shot, CollisionContext context)
        {
            lock (Gate)
            {
                CollisionContext ignored;
                if (Contexts.TryGetValue(shot, out ignored))
                {
                    Contexts.Remove(shot);
                }

                Contexts.Add(shot, context);
            }
        }

        /// <summary>
        /// Atomically returns and removes the context. CreateFragments can only
        /// consume a collision snapshot once, even if another patch re-enters it.
        /// </summary>
        internal static bool TryTake(Shot shot, out CollisionContext? context)
        {
            lock (Gate)
            {
                CollisionContext storedContext;
                if (Contexts.TryGetValue(shot, out storedContext))
                {
                    Contexts.Remove(shot);
                    context = storedContext;
                    return true;
                }

                context = null;
                return false;
            }
        }

        internal static void RemoveIfSame(Shot shot, CollisionContext expectedContext)
        {
            lock (Gate)
            {
                CollisionContext currentContext;
                if (Contexts.TryGetValue(shot, out currentContext)
                    && object.ReferenceEquals(currentContext, expectedContext))
                {
                    Contexts.Remove(shot);
                }
            }
        }
    }
}
