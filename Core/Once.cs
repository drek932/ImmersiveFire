using System.Collections.Generic;
using ImmersiveShared;

namespace ImmersiveFire;

internal static class Once
{
	private static readonly HashSet<string> shown = new HashSet<string>();

	public static void Warn(string key, string message)
	{
		if (shown.Add(key))
		{
			Host.Log.Warning(message);
		}
	}
}
