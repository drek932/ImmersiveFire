using System;
using HarmonyLib;
using Il2Cpp;
using ImmersiveShared;

namespace ImmersiveFire
{
    /// <summary>
    /// A torch (or other fuel) thrown into a fire goes in on the fly, without waiting for it to come to rest:
    /// PlayerManager.ReleaseThrownObject lets the item in hands fly (ISIL: ~1300 lines, a real method, safe to patch).
    /// </summary>
    [HarmonyPatch(typeof(PlayerManager), nameof(PlayerManager.ReleaseThrownObject))]
    internal static class ReleaseThrownObject
    {
        private static void Prefix(PlayerManager __instance, out GearItem __state)
        {
            __state = null;
            try
            {
                __state = __instance.m_ItemInHands;
            }
            catch (Exception e)
            {
                Once.Warn("throw:prefix", "Throw (before): " + e);
            }
        }

        private static void Postfix(GearItem __state)
        {
            try
            {
                if (__state == null || !FuelFeeder.IsFuel(__state)) return;
                Verbose.Log($"Thrown {__state.name}: in inventory {__state.m_InPlayerInventory}, at {FireZone.V(__state.transform.position)}");
                FuelFeeder.Thrown(__state);
            }
            catch (Exception e)
            {
                Once.Warn("throw:postfix", "Throw (after): " + e);
            }
        }
    }
}
