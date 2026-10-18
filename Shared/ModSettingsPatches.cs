using HarmonyLib;
using Il2Cpp;

namespace ImmersiveShared
{
    /// <summary>
    /// Keeps setting descriptions in the current game language. ModSettings caches a translated description
    /// and may show it again after the language changes, so the description line is checked right after
    /// ModSettings updates it and every frame while the options menu is open.
    /// </summary>
    internal static class ModSettingsPatches
    {
        private static string lastText;
        private static bool lastRussian;

        public static void Apply(HarmonyLib.Harmony harmony)
        {
            // ModSettingsGUI is not public, so patch it by name.
            var method = AccessTools.Method("ModSettings.ModSettingsGUI:UpdateDescriptionLabel");
            if (method == null)
            {
                Host.Log.Warning("ModSettingsGUI.UpdateDescriptionLabel not found, descriptions are only checked every frame");
                return;
            }
            harmony.Patch(method, postfix: new HarmonyMethod(typeof(ModSettingsPatches), nameof(AfterDescriptionUpdated)));
        }

        public static void LateUpdate()
        {
            // TryGetPanel only returns an existing panel and never creates one.
            if (InterfaceManager.TryGetPanel<Panel_OptionsMenu>(out var panel) && panel != null && panel.IsEnabled())
                TranslateDescription(panel);
        }

        private static void AfterDescriptionUpdated()
        {
            if (InterfaceManager.TryGetPanel<Panel_OptionsMenu>(out var panel) && panel != null)
                TranslateDescription(panel);
        }

        private static void TranslateDescription(Panel_OptionsMenu panel)
        {
            var label = panel.m_OptionDescriptionLabel;
            if (label == null) return;

            var text = label.text;
            bool russian = Loc.IsRussian;
            if (text == lastText && russian == lastRussian) return;

            var translated = Loc.Relocalize(text);
            if (translated != null && translated != text) label.text = translated;
            lastText = translated ?? text;
            lastRussian = russian;
        }
    }
}
