using System;
using HarmonyLib;
using Il2Cpp;

namespace ImmersiveShared;

internal static class LocalizationPatches
{
	[HarmonyPatch(typeof(Localization), "Get", new Type[] { typeof(string) })]
	private static class Get
	{
		private static bool Prefix(string key, ref string __result)
		{
			string text = Loc.Resolve(key);
			if (text == null)
			{
				return true;
			}
			__result = text;
			return false;
		}
	}

	[HarmonyPatch(typeof(Localization), "Exists")]
	private static class Exists
	{
		private static bool Prefix(string key, ref bool __result)
		{
			if (key == null || !key.StartsWith(Loc.Prefix, StringComparison.Ordinal))
			{
				return true;
			}
			__result = true;
			return false;
		}
	}
}
