#nullable enable

using BallisticPenetration.Core.Physics;

namespace BallisticPenetration.Core.Diagnostics
{
    internal readonly struct PhysicalLifecycleReportContext
    {
        internal PhysicalLifecycleReportContext(
            PhysicalVector3 position,
            PhysicalVector3 velocity,
            bool shotBindingMatched,
            string contextSource)
        {
            Position = position;
            Velocity = velocity;
            ShotBindingMatched = shotBindingMatched;
            ContextSource = contextSource;
        }

        internal PhysicalVector3 Position { get; }
        internal PhysicalVector3 Velocity { get; }
        internal bool ShotBindingMatched { get; }
        internal string ContextSource { get; }

        internal static PhysicalLifecycleReportContext Resolve(
            bool shotBindingMatched,
            PhysicalVector3 currentShotPosition,
            PhysicalVector3 currentShotVelocity,
            PhysicalLifecycleSnapshot? trackerSnapshot,
            PhysicalVector3 bindingCreationPosition,
            PhysicalVector3 bindingCreationVelocity)
        {
            if (shotBindingMatched)
            {
                return new PhysicalLifecycleReportContext(
                    currentShotPosition,
                    currentShotVelocity,
                    true,
                    "current-shot");
            }

            if (trackerSnapshot != null)
            {
                return new PhysicalLifecycleReportContext(
                    trackerSnapshot.LastKnownPosition,
                    trackerSnapshot.LastKnownVelocity,
                    false,
                    "tracker-snapshot");
            }

            return new PhysicalLifecycleReportContext(
                bindingCreationPosition,
                bindingCreationVelocity,
                false,
                "binding-creation");
        }
    }
}
