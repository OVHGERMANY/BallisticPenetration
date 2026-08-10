using System;
using System.Reflection;
using EFT.Ballistics;
using UnityEngine;

namespace BallisticPenetration.Runtime
{
    /// <summary>
    /// Resolves only the SPT 4.1.2 Shot methods this plugin supports.  Keeping
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

        internal static MethodInfo ResolveHandleCollision()
        {
            MethodInfo method = typeof(Shot).GetMethod(
                "HandleCollision",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly,
                null,
                HandleCollisionParameters,
                null);

            return RequireExactInstanceVoidMethod(method, "Shot.HandleCollision(float, Vector3, Vector3)", HandleCollisionParameters);
        }

        internal static MethodInfo ResolveCreateFragments()
        {
            MethodInfo method = typeof(Shot).GetMethod(
                "CreateFragments",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly,
                null,
                Type.EmptyTypes,
                null);

            return RequireExactInstanceVoidMethod(method, "Shot.CreateFragments()", Type.EmptyTypes);
        }

        private static MethodInfo RequireExactInstanceVoidMethod(MethodInfo method, string displayName, Type[] expectedParameters)
        {
            if (method == null
                || method.DeclaringType != typeof(Shot)
                || method.IsStatic
                || method.ReturnType != typeof(void))
            {
                throw new MissingMethodException("SPT 4.1.2 target was not found with the required signature: " + displayName);
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
