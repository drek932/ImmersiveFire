using Il2Cpp;

namespace ImmersiveFire;

internal static class NoBurnCap
{
	private const float NoLimit = 1000f;

	private static float original = -1f;

	public static void Update()
	{
		FireManager fireManagerComponent = GameManager.GetFireManagerComponent();
		if (fireManagerComponent == null)
		{
			return;
		}
		float maxDurationHoursOfFire = fireManagerComponent.m_MaxDurationHoursOfFire;
		if (Main.Settings.testNoBurnCap)
		{
			if (maxDurationHoursOfFire != 1000f)
			{
				original = maxDurationHoursOfFire;
				fireManagerComponent.m_MaxDurationHoursOfFire = 1000f;
				Main.Log.Msg($"[Test] Fire burn time limit lifted ({original:0.#} h -> {1000f:0} h)");
			}
		}
		else if (maxDurationHoursOfFire == 1000f && original > 0f)
		{
			fireManagerComponent.m_MaxDurationHoursOfFire = original;
			Main.Log.Msg($"[Test] Fire burn time limit back to {original:0.#} h");
		}
	}
}
