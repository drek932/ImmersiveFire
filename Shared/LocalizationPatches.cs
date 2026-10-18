using System;
using HarmonyLib;
using Il2Cpp;

namespace ImmersiveShared
{
    /// <summary>
    /// Serves the mod's texts to the game by its keys (see Loc), so labels in the ModSettings menu
    /// go through the game's localization system and follow its language.
    /// </summary>
    internal static class LocalizationPatches
    {
        [HarmonyPatch(typeof(Localization), nameof(Localization.Get), new[] { typeof(string) })]
        private static class Get
        {
            private static bool Prefix(string key, ref string __result)
            {
                var text = Loc.Resolve(key);
                if (text == null) return true; // not our key
                __result = text;
                return false;
            }
        }

        [HarmonyPatch(typeof(Localization), nameof(Localization.Exists))]
        private static class Exists
        {
            private static bool Prefix(string key, ref bool __result)
            {
                if (key == null || !key.StartsWith(Loc.Prefix, StringComparison.Ordinal)) return true;
                __result = true;
                return false;
            }
        }
    }
}
