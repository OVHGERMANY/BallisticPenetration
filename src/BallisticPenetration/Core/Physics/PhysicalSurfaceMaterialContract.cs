#nullable enable

using System;
using System.Collections.Generic;
using System.Reflection;

namespace BallisticPenetration.Core.Physics
{
    public enum PhysicalSurfaceMaterialMetadataStatus
    {
        Absent = 0,
        Resolved = 1,
        Invalid = 2
    }

    /// <summary>
    /// Optional reflection contract for a ballistic collider that can describe its physical
    /// material more precisely than EFT's broad MaterialType value.
    /// </summary>
    public static class PhysicalSurfaceMaterialContract
    {
        public const int SupportedSchema = 1;
        public const string SchemaPropertyName = "PhysicalBallisticsSurfaceSchema";
        public const string MaterialClassPropertyName = "PhysicalBallisticsMaterialClass";
        public const string SurfaceIdentityPropertyName = "PhysicalBallisticsSurfaceIdentity";

        private static readonly object AccessorGate = new object();
        private static readonly Dictionary<Type, SurfaceAccessor> Accessors =
            new Dictionary<Type, SurfaceAccessor>();

        public static PhysicalSurfaceMaterialMetadataStatus TryRead(
            object? surface,
            out PhysicalMaterialClass materialClass)
        {
            return TryRead(surface, out materialClass, out _);
        }

        public static PhysicalSurfaceMaterialMetadataStatus TryRead(
            object? surface,
            out PhysicalMaterialClass materialClass,
            out string surfaceIdentity)
        {
            materialClass = PhysicalMaterialClass.Unknown;
            surfaceIdentity = string.Empty;
            if (surface == null)
            {
                return PhysicalSurfaceMaterialMetadataStatus.Absent;
            }

            SurfaceAccessor accessor;
            try
            {
                accessor = GetAccessor(surface.GetType());
            }
            catch (AmbiguousMatchException)
            {
                return PhysicalSurfaceMaterialMetadataStatus.Invalid;
            }
            if (!accessor.HasAnyProperty)
            {
                return PhysicalSurfaceMaterialMetadataStatus.Absent;
            }
            if (!accessor.IsComplete)
            {
                return PhysicalSurfaceMaterialMetadataStatus.Invalid;
            }

            try
            {
                object? schemaValue = accessor.SchemaProperty!.GetValue(surface, null);
                object? materialValue = accessor.MaterialClassProperty!.GetValue(surface, null);
                object? identityValue = accessor.SurfaceIdentityProperty?.GetValue(surface, null);
                if (!(schemaValue is int schema)
                    || schema != SupportedSchema
                    || !(materialValue is string materialName)
                    || !TryParseCanonicalMaterialClass(materialName, out materialClass)
                    || (accessor.SurfaceIdentityProperty != null
                        && (!(identityValue is string identity)
                            || string.IsNullOrWhiteSpace(identity))))
                {
                    materialClass = PhysicalMaterialClass.Unknown;
                    return PhysicalSurfaceMaterialMetadataStatus.Invalid;
                }

                surfaceIdentity = identityValue as string ?? string.Empty;

                return PhysicalSurfaceMaterialMetadataStatus.Resolved;
            }
            catch (TargetInvocationException)
            {
                materialClass = PhysicalMaterialClass.Unknown;
                surfaceIdentity = string.Empty;
                return PhysicalSurfaceMaterialMetadataStatus.Invalid;
            }
            catch (MethodAccessException)
            {
                materialClass = PhysicalMaterialClass.Unknown;
                surfaceIdentity = string.Empty;
                return PhysicalSurfaceMaterialMetadataStatus.Invalid;
            }
        }

        public static bool TryParseCanonicalMaterialClass(
            string? value,
            out PhysicalMaterialClass materialClass)
        {
            switch (value)
            {
                case nameof(PhysicalMaterialClass.SoftTissue):
                    materialClass = PhysicalMaterialClass.SoftTissue;
                    return true;
                case nameof(PhysicalMaterialClass.Bone):
                    materialClass = PhysicalMaterialClass.Bone;
                    return true;
                case nameof(PhysicalMaterialClass.Fabric):
                    materialClass = PhysicalMaterialClass.Fabric;
                    return true;
                case nameof(PhysicalMaterialClass.Polymer):
                    materialClass = PhysicalMaterialClass.Polymer;
                    return true;
                case nameof(PhysicalMaterialClass.Wood):
                    materialClass = PhysicalMaterialClass.Wood;
                    return true;
                case nameof(PhysicalMaterialClass.Glass):
                    materialClass = PhysicalMaterialClass.Glass;
                    return true;
                case nameof(PhysicalMaterialClass.Aluminum):
                    materialClass = PhysicalMaterialClass.Aluminum;
                    return true;
                case nameof(PhysicalMaterialClass.MildSteel):
                    materialClass = PhysicalMaterialClass.MildSteel;
                    return true;
                case nameof(PhysicalMaterialClass.ArmoredSteel):
                    materialClass = PhysicalMaterialClass.ArmoredSteel;
                    return true;
                case nameof(PhysicalMaterialClass.Ceramic):
                    materialClass = PhysicalMaterialClass.Ceramic;
                    return true;
                case nameof(PhysicalMaterialClass.CompositeArmor):
                    materialClass = PhysicalMaterialClass.CompositeArmor;
                    return true;
                case nameof(PhysicalMaterialClass.Concrete):
                    materialClass = PhysicalMaterialClass.Concrete;
                    return true;
                case nameof(PhysicalMaterialClass.Soil):
                    materialClass = PhysicalMaterialClass.Soil;
                    return true;
                case nameof(PhysicalMaterialClass.Other):
                    materialClass = PhysicalMaterialClass.Other;
                    return true;
                case nameof(PhysicalMaterialClass.Titanium):
                    materialClass = PhysicalMaterialClass.Titanium;
                    return true;
                default:
                    materialClass = PhysicalMaterialClass.Unknown;
                    return false;
            }
        }

        private static SurfaceAccessor GetAccessor(Type surfaceType)
        {
            lock (AccessorGate)
            {
                if (!Accessors.TryGetValue(surfaceType, out SurfaceAccessor? accessor))
                {
                    accessor = SurfaceAccessor.Create(surfaceType);
                    Accessors.Add(surfaceType, accessor);
                }

                return accessor;
            }
        }

        private sealed class SurfaceAccessor
        {
            private SurfaceAccessor(
                PropertyInfo? schemaProperty,
                PropertyInfo? materialClassProperty,
                PropertyInfo? surfaceIdentityProperty)
            {
                SchemaProperty = schemaProperty;
                MaterialClassProperty = materialClassProperty;
                SurfaceIdentityProperty = surfaceIdentityProperty;
            }

            internal PropertyInfo? SchemaProperty { get; }

            internal PropertyInfo? MaterialClassProperty { get; }

            internal PropertyInfo? SurfaceIdentityProperty { get; }

            internal bool HasAnyProperty => SchemaProperty != null
                || MaterialClassProperty != null
                || SurfaceIdentityProperty != null;

            internal bool IsComplete =>
                IsReadableScalar(SchemaProperty, typeof(int))
                && IsReadableScalar(MaterialClassProperty, typeof(string))
                && (SurfaceIdentityProperty == null
                    || IsReadableScalar(SurfaceIdentityProperty, typeof(string)));

            internal static SurfaceAccessor Create(Type surfaceType)
            {
                const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public;
                return new SurfaceAccessor(
                    surfaceType.GetProperty(SchemaPropertyName, Flags),
                    surfaceType.GetProperty(MaterialClassPropertyName, Flags),
                    surfaceType.GetProperty(SurfaceIdentityPropertyName, Flags));
            }

            private static bool IsReadableScalar(PropertyInfo? property, Type propertyType)
            {
                return property != null
                    && property.PropertyType == propertyType
                    && property.GetMethod != null
                    && property.GetIndexParameters().Length == 0;
            }
        }
    }
}
