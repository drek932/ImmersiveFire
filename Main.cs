using System;
using Il2Cpp;
using ImmersiveShared;
using MelonLoader;
using UnityEngine;

[assembly: MelonInfo(typeof(ImmersiveFire.Main), "ImmersiveFire", "1.0.0", "drek932")]
[assembly: MelonGame("Hinterland", "TheLongDark")]

namespace ImmersiveFire;

public sealed class Main : MelonMod
{
	internal static MelonLogger.Instance Log;

	internal static Settings Settings;

	public override void OnInitializeMelon()
	{
		Log = base.LoggerInstance;
		Settings = new Settings();
		Settings.AddToModSettings("Immersive Fire");
		ModSettingsPatches.Apply(base.HarmonyInstance);
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
			if (!GameManager.IsMainMenuActive() && !(GameManager.GetPlayerManagerComponent() == null))
			{
				FuelFeeder.Update();
#if VERBOSE
				if (Input.GetKeyDown(KeyCode.F6) && !InterfaceManager.IsOverlayActiveImmediate())
				{
					Calibration.Toggle();
				}
				Calibration.LogHeld();
				Spawner.Update();
#endif
				FireMenu.Update();
				FirewoodBins.Update();
			}
		}
		catch (Exception ex)
		{
			Once.Warn("update:" + ex.GetType().Name, "Update failed: " + ex);
		}
	}

#if VERBOSE
	public override void OnGUI()
	{
		try
		{
			Spawner.OnGUI();
		}
		catch (Exception ex)
		{
			Once.Warn("gui", "Spawner menu: " + ex);
		}
	}
#endif

	public override void OnLateUpdate()
	{
		StoveDoors.LateUpdate();
		FirewoodBins.LateUpdate();
		try
		{
			FireMenu.LateUpdate();
		}
		catch (Exception ex)
		{
			Once.Warn("menu:late", "Fire menu frame: " + ex);
		}
		try
		{
			ModSettingsPatches.LateUpdate();
		}
		catch (Exception)
		{
		}
	}
}
