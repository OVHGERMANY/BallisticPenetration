#nullable enable

using System;

namespace BallisticPenetration.Core
{
    /// <summary>
    /// Defines the one SPT core plugin version whose collision behavior this
    /// build has verified. The BepInEx dependency attribute supplies load order
    /// and a minimum; runtime initialization separately enforces this equality.
    /// </summary>
    internal static class SptVersionCompatibility
    {
        internal const string CorePluginGuid = "com.SPT.core";
        internal const string SupportedCoreVersionText = "4.1.3";

        private static readonly Version SupportedCoreVersion =
            new Version(SupportedCoreVersionText);

        internal static bool IsExactSupportedCoreVersion(Version? actualVersion)
        {
            return actualVersion != null && actualVersion.Equals(SupportedCoreVersion);
        }
    }
}
