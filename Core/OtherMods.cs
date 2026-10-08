using System;
using System.Linq;
using System.Reflection;

namespace ImmersiveFire;

internal static class OtherMods
{
	private static bool looked;

	private static FieldInfo options;

	private static FieldInfo tinderAsFuel;

	public static bool TinderIsFuel
	{
		get
		{
			if (!looked)
			{
				Look();
			}
			if (options == null || tinderAsFuel == null)
			{
				return false;
			}
			try
			{
				object value = options.GetValue(null);
				return value != null && (bool)tinderAsFuel.GetValue(value);
			}
			catch (Exception ex)
			{
				Once.Warn("mfa", "Reading More_Fuels_Accelerants settings: " + ex);
				return false;
			}
		}
	}

	private static void Look()
	{
		looked = true;
		options = (AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault((Assembly a) => a.GetName().Name == "More_Fuels_Accelerants")?.GetType("AnimalFatFuel.Settings"))?.GetField("options", BindingFlags.Static | BindingFlags.Public);
		tinderAsFuel = options?.FieldType.GetField("tinderAsFuel", BindingFlags.Instance | BindingFlags.Public);
		Main.Log.Msg((tinderAsFuel != null) ? "More_Fuels_Accelerants found: tinder goes into a fire when its \"tinder as fuel\" setting is on" : "More_Fuels_Accelerants not found: tinder doesn't go into a burning fire (as in the game)");
	}
}
