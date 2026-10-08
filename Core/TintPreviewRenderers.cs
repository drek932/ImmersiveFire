#define VERBOSE
using System;
using System.Collections.Generic;
using HarmonyLib;
using Il2Cpp;
using Il2CppSystem.Collections.Generic;
using ImmersiveShared;
using UnityEngine;

namespace ImmersiveFire;

[HarmonyPatch(typeof(PlayerManager), "TintPreviewRenderers")]
internal static class TintPreviewRenderers
{
	private static MaterialPropertyBlock empty;

	private static bool described;

	private static void Postfix(PlayerManager __instance)
	{
		try
		{
			switch (Main.Settings.outlineMode)
			{
			case 0:
				return;
			case 1:
				if (__instance.m_ObjectToPlaceGearItem == null || !FuelFeeder.IsFuel(__instance.m_ObjectToPlaceGearItem))
				{
					return;
				}
				break;
			}
			if (!FuelFeeder.FireNearPlayer(Main.Settings.outlineDistance))
			{
				return;
			}
			Il2CppSystem.Collections.Generic.List<Renderer> tintedRenderers = __instance.m_TintedRenderers;
			if (tintedRenderers == null || tintedRenderers.Count == 0)
			{
				return;
			}
			GameObject objectToPlace = __instance.m_ObjectToPlace;
			HashSet<IntPtr> hashSet = new HashSet<IntPtr>();
			if (objectToPlace != null)
			{
				foreach (Renderer componentsInChild in objectToPlace.GetComponentsInChildren<Renderer>(includeInactive: true))
				{
					if (componentsInChild != null)
					{
						hashSet.Add(componentsInChild.Pointer);
					}
				}
			}
			if (empty == null)
			{
				empty = new MaterialPropertyBlock();
			}
			int num = 0;
			int num2 = 0;
			for (int i = 0; i < tintedRenderers.Count; i++)
			{
				Renderer renderer = tintedRenderers[i];
				if (!(renderer == null))
				{
					if (hashSet.Contains(renderer.Pointer))
					{
						renderer.SetPropertyBlock(empty);
						num++;
					}
					else
					{
						renderer.enabled = false;
						num2++;
					}
				}
			}
			if (!described)
			{
				described = true;
				Verbose.Log($"Placement tint hidden near a fire: {num} of the item's own renderer(s) untinted, {num2} outline renderer(s) switched off (first: '{tintedRenderers[0]?.name}', material '{((tintedRenderers[0] != null && tintedRenderers[0].sharedMaterial != null) ? tintedRenderers[0].sharedMaterial.name : "-")}')");
			}
		}
		catch (Exception ex)
		{
			Once.Warn("tint", "Hiding the placement tint failed: " + ex);
		}
	}
}
