using System;
using System.Reflection;
using EFT;
using EFT.Ballistics;
using UnityEngine;

namespace BallisticPenetration.Runtime
{
    /// <summary>
    /// Resolves only the SPT 4.1.4 game methods this plugin supports. Keeping
    /// signature verification separate from Harmony patch construction means an
    /// incompatible client fails during plugin startup instead of patching an
    /// unintended overload.
    /// </summary>
    internal static class TargetMethodResolver
    {
        private static readonly Type[] HandleCollisionParameters =
        {
            typeof(float),
            typeof(Vector3),
            typeof(Vector3)
        };

        private static readonly Type[] ApplyHitParameters =
        {
            typeof(DamageInfo),
            typeof(ShotId)
        };

        internal static MethodInfo ResolveHandleCollision()
        {
            MethodInfo method = typeof(Shot).GetMethod(
                "HandleCollision",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly,
                null,
                HandleCollisionParameters,
                null);

            return RequireExactInstanceMethod(
                method,
                "Shot.HandleCollision(float, Vector3, Vector3)",
                typeof(Shot),
                typeof(void),
                HandleCollisionParameters);
        }

        internal static MethodInfo ResolveCreateFragments()
        {
            MethodInfo method = typeof(Shot).GetMethod(
                "CreateFragments",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly,
                null,
                Type.EmptyTypes,
                null);

            return RequireExactInstanceMethod(
                method,
                "Shot.CreateFragments()",
                typeof(Shot),
                typeof(void),
                Type.EmptyTypes);
        }

        internal static MethodInfo ResolveBodyPartColliderApplyHit()
        {
            MethodInfo method = typeof(BodyPartCollider).GetMethod(
                "ApplyHit",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly,
                null,
                ApplyHitParameters,
                null);

            return RequireExactInstanceMethod(
                method,
                "BodyPartCollider.ApplyHit(DamageInfo, ShotId)",
                typeof(BodyPartCollider),
                typeof(PlayerHitInfo),
                ApplyHitParameters);
        }

        internal static MethodInfo ResolveArmorPlateColliderApplyHit()
        {
            MethodInfo method = typeof(ArmorPlateCollider).GetMethod(
                "ApplyHit",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly,
                null,
                ApplyHitParameters,
                null);

            return RequireExactInstanceMethod(
                method,
                "ArmorPlateCollider.ApplyHit(DamageInfo, ShotId)",
                typeof(ArmorPlateCollider),
                typeof(PlayerHitInfo),
                ApplyHitParameters);
        }

        private static MethodInfo RequireExactInstanceMethod(
            MethodInfo method,
            string displayName,
            Type expectedDeclaringType,
            Type expectedReturnType,
            Type[] expectedParameters)
        {
            if (method == null
                || method.DeclaringType != expectedDeclaringType
                || method.IsStatic
                || method.ReturnType != expectedReturnType)
            {
                throw new MissingMethodException("SPT 4.1.4 target was not found with the required signature: " + displayName);
            }

            ParameterInfo[] parameters = method.GetParameters();
            if (parameters.Length != expectedParameters.Length)
            {
                throw new MissingMethodException("SPT target signature did not match: " + displayName);
            }

            for (int index = 0; index < expectedParameters.Length; index++)
            {
                if (parameters[index].ParameterType != expectedParameters[index])
                {
                    throw new MissingMethodException("SPT target signature did not match: " + displayName);
                }
            }

            return method;
        }
    }
}
