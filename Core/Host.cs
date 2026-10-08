using System.Collections.Generic;
using ImmersiveFire;
using MelonLoader;

namespace ImmersiveShared;

internal static class Host
{
	public const string KeyPrefix = "IMFI_";

	public static MelonLogger.Instance Log => ImmersiveFire.Main.Log;

	public static Dictionary<string, (string ru, string en)> Texts => ImmersiveFire.Texts.All;
}
