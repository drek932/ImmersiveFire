using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace ImmersiveFire
{
    /// <summary>
    /// Optional link to PerfectPlacement (0.39.0+), found by reflection: the items it moves along with the held one as a
    /// group, and letting one of them go before it is burnt (PerfectPlacement keeps moving its group members every frame).
    /// Without PerfectPlacement everything here is empty.
    /// </summary>
    internal static class PerfectPlacementBridge
    {
        private static bool looked;
        private static MethodInfo carried, letGo;

        private static bool Ready()
        {
            if (!looked)
            {
                looked = true;
                var api = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "PerfectPlacement")?.GetType("PerfectPlacement.Api");
                carried = api?.GetMethod("CarriedGroup", BindingFlags.Public | BindingFlags.Static);
                letGo = api?.GetMethod("LetGo", BindingFlags.Public | BindingFlags.Static);
                Main.Log.Msg(carried != null && letGo != null
                    ? "PerfectPlacement found: groups carried over a fire burn too"
                    : "PerfectPlacement (0.39.0+) not found: only the held item burns when moving");
            }
            return carried != null && letGo != null;
        }

        /// <summary>Items moved along with the held one (not the held one).</summary>
        public static List<GameObject> Carried()
        {
            if (!Ready()) return new List<GameObject>();
            try
            {
                return ((GameObject[])carried.Invoke(null, null)).Where(g => g != null).ToList();
            }
            catch (Exception e)
            {
                Once.Warn("pp:carried", "PerfectPlacement group: " + e);
                return new List<GameObject>();
            }
        }

        /// <summary>Takes the item out of the group (it stays where it is). False if that failed.</summary>
        public static bool LetGo(GameObject item)
        {
            if (!Ready()) return false;
            try
            {
                return (bool)letGo.Invoke(null, new object[] { item });
            }
            catch (Exception e)
            {
                Once.Warn("pp:letgo", "PerfectPlacement let go: " + e);
                return false;
            }
        }
    }
}
