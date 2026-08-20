#nullable enable

using System;
using System.Collections.Generic;
using BallisticPenetration.Core.Physics;

namespace BallisticPenetration.Core.Diagnostics
{
    internal sealed class FieldReportLifecycleEventSnapshot
    {
        internal FieldReportLifecycleEventSnapshot(
            string eventName,
            DateTimeOffset timestamp,
            string projectileIdentity,
            string rootIdentity,
            string projectileKind,
            int fragmentIndex,
            int fragmentGeneration,
            int recordSequence,
            int collisionOrdinal,
            string phase,
            bool resolutionKnown,
            double creationTimestamp,
            double terminalTimestamp,
            PhysicalVector3 creationPosition,
            PhysicalVector3 creationVelocity,
            PhysicalVector3 currentPosition,
            PhysicalVector3 lastVelocity,
            string collisionIdentity,
            string materialId,
            string materialClass,
            PhysicalVector3 incomingVelocity,
            PhysicalVector3 outgoingVelocity,
            string collisionOutcome,
            bool continued,
            bool replaced,
            bool ballisticTerminal,
            bool lifecycleTerminal,
            string lifecycleEndReason,
            bool targetWasAlreadyDead,
            string targetSurface,
            string terminalState,
            string shotState,
            string reason,
            bool? localPlayerShooter,
            string shooterAlias,
            string weaponTemplateId,
            string weaponDisplayName,
            string ammunitionTemplateId,
            string ammunitionName,
            string caliber,
            double? muzzleSpeed,
            PhysicalVector3? shooterPosition,
            string targetCategory,
            string targetBodyPart,
            string armorContext,
            string colliderDescriptor,
            double? distanceTravelled,
            double? approximateShooterToImpactDistance,
            string replacementRelationship,
            bool shotBindingMatched,
            string contextSource)
        {
            EventName = eventName ?? string.Empty;
            Timestamp = timestamp;
            ProjectileIdentity = projectileIdentity ?? string.Empty;
            RootIdentity = rootIdentity ?? string.Empty;
            ProjectileKind = projectileKind ?? string.Empty;
            FragmentIndex = fragmentIndex;
            FragmentGeneration = fragmentGeneration;
            RecordSequence = recordSequence;
            CollisionOrdinal = collisionOrdinal;
            Phase = phase ?? string.Empty;
            ResolutionKnown = resolutionKnown;
            CreationTimestamp = creationTimestamp;
            TerminalTimestamp = terminalTimestamp;
            CreationPosition = creationPosition;
            CreationVelocity = creationVelocity;
            CurrentPosition = currentPosition;
            LastVelocity = lastVelocity;
            CollisionIdentity = collisionIdentity ?? string.Empty;
            MaterialId = materialId ?? string.Empty;
            MaterialClass = materialClass ?? string.Empty;
            IncomingVelocity = incomingVelocity;
            OutgoingVelocity = outgoingVelocity;
            CollisionOutcome = collisionOutcome ?? string.Empty;
            Continued = continued;
            Replaced = replaced;
            BallisticTerminal = ballisticTerminal;
            LifecycleTerminal = lifecycleTerminal;
            LifecycleEndReason = lifecycleEndReason ?? string.Empty;
            TargetWasAlreadyDead = targetWasAlreadyDead;
            TargetSurface = targetSurface ?? string.Empty;
            TerminalState = terminalState ?? string.Empty;
            ShotState = shotState ?? string.Empty;
            Reason = reason ?? string.Empty;
            LocalPlayerShooter = localPlayerShooter;
            ShooterAlias = shooterAlias ?? string.Empty;
            WeaponTemplateId = weaponTemplateId ?? string.Empty;
            WeaponDisplayName = weaponDisplayName ?? string.Empty;
            AmmunitionTemplateId = ammunitionTemplateId ?? string.Empty;
            AmmunitionName = ammunitionName ?? string.Empty;
            Caliber = caliber ?? string.Empty;
            MuzzleSpeed = muzzleSpeed;
            ShooterPosition = shooterPosition;
            TargetCategory = targetCategory ?? string.Empty;
            TargetBodyPart = targetBodyPart ?? string.Empty;
            ArmorContext = armorContext ?? string.Empty;
            ColliderDescriptor = colliderDescriptor ?? string.Empty;
            DistanceTravelled = distanceTravelled;
            ApproximateShooterToImpactDistance = approximateShooterToImpactDistance;
            ReplacementRelationship = replacementRelationship ?? string.Empty;
            ShotBindingMatched = shotBindingMatched;
            ContextSource = contextSource ?? string.Empty;
        }

        internal string EventName { get; }
        internal DateTimeOffset Timestamp { get; }
        internal string ProjectileIdentity { get; }
        internal string RootIdentity { get; }
        internal string ProjectileKind { get; }
        internal int FragmentIndex { get; }
        internal int FragmentGeneration { get; }
        internal int RecordSequence { get; }
        internal int CollisionOrdinal { get; }
        internal string Phase { get; }
        internal bool ResolutionKnown { get; }
        internal double CreationTimestamp { get; }
        internal double TerminalTimestamp { get; }
        internal PhysicalVector3 CreationPosition { get; }
        internal PhysicalVector3 CreationVelocity { get; }
        internal PhysicalVector3 CurrentPosition { get; }
        internal PhysicalVector3 LastVelocity { get; }
        internal double LastSpeed => LastVelocity.Magnitude;
        internal string CollisionIdentity { get; }
        internal string MaterialId { get; }
        internal string MaterialClass { get; }
        internal PhysicalVector3 IncomingVelocity { get; }
        internal double IncomingSpeed => IncomingVelocity.Magnitude;
        internal PhysicalVector3 OutgoingVelocity { get; }
        internal double OutgoingSpeed => OutgoingVelocity.Magnitude;
        internal string CollisionOutcome { get; }
        internal bool Continued { get; }
        internal bool Replaced { get; }
        internal bool BallisticTerminal { get; }
        internal bool LifecycleTerminal { get; }
        internal string LifecycleEndReason { get; }
        internal bool TargetWasAlreadyDead { get; }
        internal string TargetSurface { get; }
        internal string TerminalState { get; }
        internal string ShotState { get; }
        internal string Reason { get; }
        internal bool? LocalPlayerShooter { get; }
        internal string ShooterAlias { get; }
        internal string WeaponTemplateId { get; }
        internal string WeaponDisplayName { get; }
        internal string AmmunitionTemplateId { get; }
        internal string AmmunitionName { get; }
        internal string Caliber { get; }
        internal double? MuzzleSpeed { get; }
        internal PhysicalVector3? ShooterPosition { get; }
        internal string TargetCategory { get; }
        internal string TargetBodyPart { get; }
        internal string ArmorContext { get; }
        internal string ColliderDescriptor { get; }
        internal double? DistanceTravelled { get; }
        internal double? ApproximateShooterToImpactDistance { get; }
        internal string ReplacementRelationship { get; }
        internal bool ShotBindingMatched { get; }
        internal string ContextSource { get; }

        internal FieldReportRecord ToRecord(bool critical = false)
        {
            return new FieldReportRecord(
                EventName,
                critical,
                new[]
                {
                    Field("utcTimestamp", Timestamp.UtcDateTime.ToString("O")),
                    Field("localTimestamp", Timestamp.ToString("O")),
                    Field("projectileIdentity", ProjectileIdentity),
                    Field("rootIdentity", RootIdentity),
                    Field("projectileKind", ProjectileKind),
                    Field("fragmentIndex", FragmentIndex),
                    Field("fragmentGeneration", FragmentGeneration),
                    Field("recordSequence", RecordSequence),
                    Field("collisionOrdinal", CollisionOrdinal),
                    Field("phase", EmptyToNull(Phase)),
                    Field("resolutionKnown", ResolutionKnown),
                    Field("creationTimestamp", creationTimestamp: CreationTimestamp),
                    Field("terminalTimestamp", nullableNumber: TerminalTimestamp > 0d ? TerminalTimestamp : (double?)null),
                    Field("creationPosition", CreationPosition),
                    Field("creationVelocity", CreationVelocity),
                    Field("position", CurrentPosition),
                    Field("lastVelocity", LastVelocity),
                    Field("lastSpeed", LastSpeed),
                    Field("collisionIdentity", EmptyToNull(CollisionIdentity)),
                    Field("materialId", EmptyToNull(MaterialId)),
                    Field("materialClass", EmptyToNull(MaterialClass)),
                    Field("incomingVelocity", IncomingVelocity),
                    Field("incomingSpeed", IncomingSpeed),
                    Field("outgoingVelocity", OutgoingVelocity),
                    Field("outgoingSpeed", OutgoingSpeed),
                    Field("collisionOutcome", EmptyToNull(CollisionOutcome)),
                    Field("continued", Continued),
                    Field("replaced", Replaced),
                    Field("ballisticTerminal", BallisticTerminal),
                    Field("lifecycleTerminal", LifecycleTerminal),
                    Field("lifecycleEndReason", LifecycleEndReason),
                    Field("targetWasAlreadyDead", TargetWasAlreadyDead),
                    Field("targetSurface", EmptyToNull(TargetSurface)),
                    Field("terminalState", EmptyToNull(TerminalState)),
                    Field("shotState", EmptyToNull(ShotState)),
                    Field("reason", Reason),
                    Field("localPlayerShooter", LocalPlayerShooter),
                    Field("shooterAlias", EmptyToNull(ShooterAlias)),
                    Field("weaponTemplateId", EmptyToNull(WeaponTemplateId)),
                    Field("weaponDisplayName", EmptyToNull(WeaponDisplayName)),
                    Field("ammunitionTemplateId", EmptyToNull(AmmunitionTemplateId)),
                    Field("ammunitionName", EmptyToNull(AmmunitionName)),
                    Field("caliber", EmptyToNull(Caliber)),
                    Field("muzzleSpeed", MuzzleSpeed),
                    Field("shooterPosition", ShooterPosition),
                    Field("targetCategory", EmptyToNull(TargetCategory)),
                    Field("targetBodyPart", EmptyToNull(TargetBodyPart)),
                    Field("armorContext", EmptyToNull(ArmorContext)),
                    Field("colliderDescriptor", EmptyToNull(ColliderDescriptor)),
                    Field("distanceTravelled", DistanceTravelled),
                    Field("approximateShooterToImpactDistance", ApproximateShooterToImpactDistance),
                    Field("replacementRelationship", EmptyToNull(ReplacementRelationship)),
                    Field("shotBindingMatched", ShotBindingMatched),
                    Field("contextSource", ContextSource)
                });
        }

        private static string? EmptyToNull(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }

        private static KeyValuePair<string, object?> Field(string name, object? value)
        {
            return new KeyValuePair<string, object?>(name, value);
        }

        private static KeyValuePair<string, object?> Field(string name, double creationTimestamp)
        {
            return new KeyValuePair<string, object?>(name, creationTimestamp);
        }

        private static KeyValuePair<string, object?> Field(string name, double? nullableNumber)
        {
            return new KeyValuePair<string, object?>(name, nullableNumber);
        }
    }
}
