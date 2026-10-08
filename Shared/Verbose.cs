using System.Diagnostics;

namespace ImmersiveShared;

internal static class Verbose
{
	[Conditional("VERBOSE")]
	public static void Log(string message)
	{
		Host.Log.Msg(message);
	}
}
