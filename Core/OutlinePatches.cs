using System;
using System.Collections.Generic;
using HarmonyLib;
using Il2Cpp;
using ImmersiveShared;
using UnityEngine;

namespace ImmersiveFire
{
    /// <summary>
    /// Near a fire (setting), the green / red tint of an item being moved is hidden, so putting fuel in looks natural.
    /// PlayerManager.TintPreviewRenderers (ISIL ~270 lines, a real method) runs every frame while placing: for each of
    /// m_TintedRenderers it enables the renderer and sets the valid / invalid MaterialPropertyBlock (disables it when the
    /// item is in the air). After it: the item's own renderers lose the tint (empty property block), extra outline
    /// renderers are switched off.
    /// </summary>
    [HarmonyPatch(typeof(PlayerManager), nameof(PlayerManager.TintPreviewRenderers))]
    internal static class TintPreviewRenderers
    {
        private static MaterialPropertyBlock empty;
        private static bool described;

        private static void Postfix(PlayerManager __instance)
        {
            try
            {
                int mode = Main.Settings.outlineMode;
                if (mode == 0) return;
                // "Fuel only": what could make a fire burn longer (the same items this mod puts into fires).
                if (mode == 1 && (__instance.m_ObjectToPlaceGearItem == null || !FuelFeeder.IsFuel(__instance.m_ObjectToPlaceGearItem))) return;
                if (!FuelFeeder.FireNearPlayer(Main.Settings.outlineDistance)) return;
                var tinted = __instance.m_TintedRenderers;
                if (tinted == null || tinted.Count == 0) return;

                var held = __instance.m_ObjectToPlace;
                var own = new HashSet<IntPtr>();
                if (held != null)
                    foreach (var r in held.GetComponentsInChildren<Renderer>(true))
                        if (r != null) own.Add(r.Pointer);

                empty ??= new MaterialPropertyBlock();
                int cleared = 0, hidden = 0;
                for (int i = 0; i < tinted.Count; i++)
                {
                    var r = tinted[i];
                    if (r == null) continue;
                    if (own.Contains(r.Pointer))
                    {
                        r.SetPropertyBlock(empty);
                        cleared++;
                    }
                    else
                    {
                        r.enabled = false;
                        hidden++;
                    }
                }
                if (!described)
                {
                    described = true;
                    Verbose.Log($"Placement tint hidden near a fire: {cleared} of the item's own renderer(s) untinted, " +
                                $"{hidden} outline renderer(s) switched off (first: '{tinted[0]?.name}', " +
                                $"material '{(tinted[0] != null && tinted[0].sharedMaterial != null ? tinted[0].sharedMaterial.name : "-")}')");
                }
            }
            catch (Exception e)
            {
                Once.Warn("tint", "Hiding the placement tint failed: " + e);
            }
        }
    }
}
