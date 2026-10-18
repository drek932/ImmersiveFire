using System;
using System.Linq;
using System.Reflection;

namespace ImmersiveFire
{
    /// <summary>
    /// What other installed mods count as fuel, found by reflection (no reference to them needed).
    /// More_Fuels_Accelerants (AnimalFatFuel): animal fat gets a FuelSourceItem for good (nothing to do here); tinder is
    /// fuel only while the game's "Add fuel" menu is open (it clears FuelSourceItem.m_IsTinder there and puts it back after),
    /// so its setting "tinderAsFuel" is read here and followed.
    /// </summary>
    internal static class OtherMods
    {
        private static bool looked;
        private static FieldInfo options, tinderAsFuel;

        /// <summary>Tinder (paper, bark...) may go into a burning fire as fuel.</summary>
        public static bool TinderIsFuel
        {
            get
            {
                if (!looked) Look();
                if (options == null || tinderAsFuel == null) return false;
                try
                {
                    var settings = options.GetValue(null);
                    return settings != null && (bool)tinderAsFuel.GetValue(settings);
                }
                catch (Exception e)
                {
                    Once.Warn("mfa", "Reading More_Fuels_Accelerants settings: " + e);
                    return false;
                }
            }
        }

        private static void Look()
        {
            looked = true;
            var mod = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "More_Fuels_Accelerants");
            var settingsType = mod?.GetType("AnimalFatFuel.Settings");
            options = settingsType?.GetField("options", BindingFlags.Public | BindingFlags.Static);
            tinderAsFuel = options?.FieldType.GetField("tinderAsFuel", BindingFlags.Public | BindingFlags.Instance);
            Main.Log.Msg(tinderAsFuel != null
                ? "More_Fuels_Accelerants found: tinder goes into a fire when its \"tinder as fuel\" setting is on"
                : "More_Fuels_Accelerants not found: tinder doesn't go into a burning fire (as in the game)");
        }
    }
}
