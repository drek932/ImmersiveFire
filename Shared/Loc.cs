using System;
using System.Collections.Generic;

namespace ImmersiveShared
{
    /// <summary>
    /// Text lookup: Russian if the game language is Russian, otherwise English (the texts are in Texts.cs).
    /// Keys "<prefix>X" (Host.KeyPrefix) are served through the Localization.Get patch, so ModSettings labels follow the game language.
    /// </summary>
    internal static class Loc
    {
        public static string Prefix => Host.KeyPrefix;

        public static bool IsRussian =>
            CurrentGameLanguage() is { } lang && lang.IndexOf("russian", StringComparison.OrdinalIgnoreCase) >= 0;

        private static string CurrentGameLanguage()
        {
            try
            {
                var lang = Il2Cpp.Localization.s_Language;
                if (!string.IsNullOrEmpty(lang)) return lang;
            }
            catch
            {
                // ignored, try the property below
            }

            try
            {
                return Il2Cpp.Localization.Language;
            }
            catch
            {
                return null;
            }
        }

        public static string T(string key)
        {
            if (!Host.Texts.TryGetValue(key, out var t)) return key;
            return IsRussian ? t.ru : t.en;
        }

        // Every text handed out, with the key it came from: lets a cached text be rebuilt in another language.
        private static readonly Dictionary<string, string> KeyByText = new();

        /// <summary>Text for one of this mod's keys; null if the key is not ours.</summary>
        public static string Resolve(string fullKey)
        {
            if (fullKey == null || !fullKey.StartsWith(Prefix, StringComparison.Ordinal)) return null;
            var text = T(fullKey.Substring(Prefix.Length));
            KeyByText[text] = fullKey;
            return text;
        }

        /// <summary>
        /// Brings a text of this mod to the current game language: a key, or a text handed out earlier in any language.
        /// Null if the text is not ours.
        /// </summary>
        public static string Relocalize(string text)
        {
            if (string.IsNullOrEmpty(text)) return null;
            if (text.StartsWith(Prefix, StringComparison.Ordinal)) return Resolve(text);
            if (KeyByText.TryGetValue(text, out var key)) return Resolve(key);

            // A text in the other language that was never handed out (the language changed after ModSettings cached it).
            foreach (var pair in Host.Texts)
                if (pair.Value.ru == text || pair.Value.en == text) return Resolve(Prefix + pair.Key);
            return null;
        }
    }
}
