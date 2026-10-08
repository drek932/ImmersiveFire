using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace ImmersiveFire;

internal static class PerfectPlacementBridge
{
	private static bool looked;

	private static MethodInfo carried;

	private static MethodInfo letGo;

	private static bool Ready()
	{
		if (!looked)
		{
			looked = true;
			Type? obj = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault((Assembly a) => a.GetName().Name == "PerfectPlacement")?.GetType("PerfectPlacement.Api");
			carried = obj?.GetMethod("CarriedGroup", BindingFlags.Static | BindingFlags.Public);
			letGo = obj?.GetMethod("LetGo", BindingFlags.Static | BindingFlags.Public);
			Main.Log.Msg((carried != null && letGo != null) ? "PerfectPlacement found: groups carried over a fire burn too" : "PerfectPlacement (0.32.0+) not found: only the held item burns when moving");
		}
		if (carried != null)
		{
			return letGo != null;
		}
		return false;
	}

	public static List<GameObject> Carried()
	{
		if (!Ready())
		{
			return new List<GameObject>();
		}
		try
		{
			return ((GameObject[])carried.Invoke(null, null)).Where((GameObject g) => g != null).ToList();
		}
		catch (Exception ex)
		{
			Once.Warn("pp:carried", "PerfectPlacement group: " + ex);
			return new List<GameObject>();
		}
	}

	public static bool LetGo(GameObject item)
	{
		if (!Ready())
		{
			return false;
		}
		try
		{
			return (bool)letGo.Invoke(null, new object[1] { item });
		}
		catch (Exception ex)
		{
			Once.Warn("pp:letgo", "PerfectPlacement let go: " + ex);
			return false;
		}
	}
}
