using UnityEngine;

namespace BallisticPenetration.Runtime.State
{
    internal enum CollisionAdjustmentResult
    {
        None,
        PluginDisabled,
        NonForwardHit,
        MissingTemplate,
        InputInvalid,
        InvalidExponents,
        InvalidImpactSpeed,
        CalculationFailed,
        DuplicateNormalization,
        PhysicalCapabilityOwned,
        Applied,
        Unknown
    }

    /// <summary>
    /// Values carried from HandleCollision to the matching CreateFragments call.
    /// Local output is set only after BallisticPenetration writes both stats.
    /// </summary>
    internal sealed class CollisionContext
    {
        internal CollisionContext(
            float damage,
            float penetrationPower,
            float templateInitialSpeed,
            string? templateId,
            string? templateName,
            bool hasPreviousFramePosition,
            Vector3 previousFramePosition,
            CollisionAdjustmentResult result = CollisionAdjustmentResult.Unknown)
        {
            EntryDamage = damage;
            EntryPenetrationPower = penetrationPower;
            TemplateInitialSpeed = templateInitialSpeed;
            TemplateId = templateId;
            TemplateName = templateName;
            HasPreviousFramePosition = hasPreviousFramePosition;
            PreviousFramePosition = previousFramePosition;
            AdjustmentResult = result;
            PatchInputDamage = damage;
            PatchInputPenetrationPower = penetrationPower;
        }

        internal float EntryDamage { get; private set; }

        internal float EntryPenetrationPower { get; private set; }

        internal float TemplateInitialSpeed { get; private set; }

        internal string? TemplateId { get; private set; }

        internal string? TemplateName { get; private set; }

        internal float PatchInputDamage { get; set; }

        internal float PatchInputPenetrationPower { get; set; }

        internal float LocalOutputDamage { get; set; }

        internal float LocalOutputPenetrationPower { get; set; }

        internal string NormalizationComponentId { get; set; } = string.Empty;

        internal string NormalizationRootShotId { get; set; } = string.Empty;

        internal string NormalizationCollisionId { get; set; } = string.Empty;

        internal int NormalizationCollisionOrdinal { get; set; }

        internal double PreviousDamageFactor { get; set; } = 1d;

        internal double PreviousPenetrationFactor { get; set; } = 1d;

        internal double CurrentDamageFactor { get; set; } = 1d;

        internal double CurrentPenetrationFactor { get; set; } = 1d;

        internal double AppliedDamageRatio { get; set; } = 1d;

        internal double AppliedPenetrationRatio { get; set; } = 1d;

        /// <summary>
        /// Captured from HandleCollision's exact prevVector3 argument only when
        /// visual diagnostics were enabled at collision entry.
        /// </summary>
        internal bool HasPreviousFramePosition { get; private set; }

        internal Vector3 PreviousFramePosition { get; private set; }

        internal CollisionAdjustmentResult AdjustmentResult { get; set; }
    }
}
