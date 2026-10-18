using System.Collections.Generic;
using ImmersiveShared;

namespace ImmersiveFire
{
    /// <summary>Warnings shown once per session (a failing check would repeat every scan).</summary>
    internal static class Once
    {
        private static readonly HashSet<string> shown = new();

        public static void Warn(string key, string message)
        {
            if (shown.Add(key)) Host.Log.Warning(message);
        }
    }
}
