#define VERBOSE
using System;
using HarmonyLib;
using Il2Cpp;
using ImmersiveShared;

namespace ImmersiveFire;

[HarmonyPatch(typeof(PlayerManager), "ReleaseThrownObject")]
internal static class ReleaseThrownObject
{
	private static void Prefix(PlayerManager __instance, out GearItem __state)
	{
		__state = null;
		try
		{
			__state = __instance.m_ItemInHands;
		}
		catch (Exception ex)
		{
			Once.Warn("throw:prefix", "Throw (before): " + ex);
		}
	}

	private static void Postfix(GearItem __state)
	{
		try
		{
			if (!(__state == null) && FuelFeeder.IsFuel(__state))
			{
				Verbose.Log($"Thrown {__state.name}: in inventory {__state.m_InPlayerInventory}, at {FireZone.V(__state.transform.position)}");
				FuelFeeder.Thrown(__state);
			}
		}
		catch (Exception ex)
		{
			Once.Warn("throw:postfix", "Throw (after): " + ex);
		}
	}
}
