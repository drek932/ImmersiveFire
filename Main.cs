using System;
using Il2Cpp;
using MelonLoader;
using ImmersiveShared;

[assembly: MelonInfo(typeof(ImmersiveFire.Main), "ImmersiveFire", "1.1.0", "drek932")]
[assembly: MelonGame("Hinterland", "TheLongDark")]

namespace ImmersiveFire
{
    /// <summary>
    /// Fuel put into a burning fire goes into it, as if added from the game's "Add fuel" menu: inside a campfire's stone
    /// ring, into a fireplace, to the flames of a stove or barrel, into a forge. The game's rules apply (wind, longest
    /// burn time, coal only into an old enough fire, unread skill books per setting).
    ///
    ///  Core/    — settings, texts, fire zones, the feeding itself;
    ///  Shared/  — texts in the game's language, ModSettings descriptions (same files as the other Immersive mods);
    ///  Debug/   — test tools (F6 zone calibration, F8 spawner), compiled only in Verbose builds.
    /// </summary>
    public sealed class Main : MelonMod
    {
        internal static MelonLogger.Instance Log;
        internal static Settings Settings;

        public override void OnInitializeMelon()
        {
            Log = LoggerInstance;
            Settings = new Settings();
            Settings.AddToModSettings("Immersive Fire");
            ModSettingsPatches.Apply(HarmonyInstance);
            Log.Msg("Settings: " + Settings.Describe());
        }

        public override void OnSceneWasInitialized(int buildIndex, string sceneName)
        {
            FuelFeeder.Reset();
#if VERBOSE
            Calibration.Clear();
            Spawner.Close();
#endif
            FirewoodBins.Reset();
        }

        public override void OnUpdate()
        {
            try
            {
                if (GameManager.IsMainMenuActive() || GameManager.GetPlayerManagerComponent() == null) return;
                FuelFeeder.Update();
#if VERBOSE
                if (UnityEngine.Input.GetKeyDown(Calibration.Key) && !InterfaceManager.IsOverlayActiveImmediate()) Calibration.Toggle();
                Calibration.LogHeld();
                Spawner.Update();
#endif
                FireMenu.Update();
                FirewoodBins.Update();
            }
            catch (Exception e)
            {
                Once.Warn("update:" + e.GetType().Name, "Update failed: " + e);
            }
        }

#if VERBOSE
        public override void OnGUI()
        {
            try
            {
                Spawner.OnGUI();
            }
            catch (Exception e)
            {
                Once.Warn("gui", "Spawner menu: " + e);
            }
        }
#endif

        public override void OnLateUpdate()
        {
            // Stove doors: after the game's own door tweens moved them this frame.
            StoveDoors.LateUpdate();
            FirewoodBins.LateUpdate();
            try
            {
                FireMenu.LateUpdate();
            }
            catch (Exception e)
            {
                Once.Warn("menu:late", "Fire menu frame: " + e);
            }
            try
            {
                ModSettingsPatches.LateUpdate();
            }
            catch (Exception)
            {
                // Interface not ready yet (loading screens).
            }
        }
    }
}
