using System.Reflection;
using HarmonyLib;
using Il2Cpp;

namespace ImmersiveShared;

internal static class ModSettingsPatches
{
	private static string lastText;

	private static bool lastRussian;

	public static void Apply(HarmonyLib.Harmony harmony)
	{
		MethodInfo methodInfo = AccessTools.Method("ModSettings.ModSettingsGUI:UpdateDescriptionLabel");
		if (methodInfo == null)
		{
			Host.Log.Warning("ModSettingsGUI.UpdateDescriptionLabel not found, descriptions are only checked every frame");
		}
		else
		{
			harmony.Patch(methodInfo, null, new HarmonyMethod(typeof(ModSettingsPatches), "AfterDescriptionUpdated"));
		}
	}

	public static void LateUpdate()
	{
		if (InterfaceManager.TryGetPanel<Panel_OptionsMenu>(out var panel) && panel != null && panel.IsEnabled())
		{
			TranslateDescription(panel);
		}
	}

	private static void AfterDescriptionUpdated()
	{
		if (InterfaceManager.TryGetPanel<Panel_OptionsMenu>(out var panel) && panel != null)
		{
			TranslateDescription(panel);
		}
	}

	private static void TranslateDescription(Panel_OptionsMenu panel)
	{
		UILabel optionDescriptionLabel = panel.m_OptionDescriptionLabel;
		if (optionDescriptionLabel == null)
		{
			return;
		}
		string text = optionDescriptionLabel.text;
		bool isRussian = Loc.IsRussian;
		if (!(text == lastText) || isRussian != lastRussian)
		{
			string text2 = Loc.Relocalize(text);
			if (text2 != null && text2 != text)
			{
				optionDescriptionLabel.text = text2;
			}
			lastText = text2 ?? text;
			lastRussian = isRussian;
		}
	}
}
