using System;
using System.Collections.Generic;
using Il2Cpp;

namespace ImmersiveShared;

internal static class Loc
{
	private static readonly Dictionary<string, string> KeyByText = new Dictionary<string, string>();

	public static string Prefix => "IMFI_";

	public static bool IsRussian
	{
		get
		{
			string text = CurrentGameLanguage();
			if (text != null)
			{
				return text.IndexOf("russian", StringComparison.OrdinalIgnoreCase) >= 0;
			}
			return false;
		}
	}

	private static string CurrentGameLanguage()
	{
		try
		{
			string s_Language = Localization.s_Language;
			if (!string.IsNullOrEmpty(s_Language))
			{
				return s_Language;
			}
		}
		catch
		{
		}
		try
		{
			return Localization.Language;
		}
		catch
		{
			return null;
		}
	}

	public static string T(string key)
	{
		if (!Host.Texts.TryGetValue(key, out (string, string) value))
		{
			return key;
		}
		if (!IsRussian)
		{
			return value.Item2;
		}
		return value.Item1;
	}

	public static string Resolve(string fullKey)
	{
		if (fullKey == null || !fullKey.StartsWith(Prefix, StringComparison.Ordinal))
		{
			return null;
		}
		string text = T(fullKey.Substring(Prefix.Length));
		KeyByText[text] = fullKey;
		return text;
	}

	public static string Relocalize(string text)
	{
		if (string.IsNullOrEmpty(text))
		{
			return null;
		}
		if (text.StartsWith(Prefix, StringComparison.Ordinal))
		{
			return Resolve(text);
		}
		if (KeyByText.TryGetValue(text, out var value))
		{
			return Resolve(value);
		}
		foreach (KeyValuePair<string, (string, string)> text2 in Host.Texts)
		{
			if (text2.Value.Item1 == text || text2.Value.Item2 == text)
			{
				return Resolve(Prefix + text2.Key);
			}
		}
		return null;
	}
}
