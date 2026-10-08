using System;
using System.Collections.Generic;
using System.Text;
using Il2Cpp;
using UnityEngine;

namespace ImmersiveFire;

internal static class Calibration
{
	public const KeyCode Key = KeyCode.F6;

	private const float Around = 10f;

	private static readonly List<GameObject> dots = new List<GameObject>();

	private static float nextHeldLog;

	public static void Toggle()
	{
		if (dots.Count > 0)
		{
			Clear();
			HUDMessage.AddMessage("Fire zones hidden", 2f, highPriority: false, ignoreOverlayActive: true);
			return;
		}
		Transform playerTransform = GameManager.GetPlayerTransform();
		if (playerTransform == null)
		{
			return;
		}
		int num = 0;
		foreach (var (fireplaceInteraction, kind, zone, fireZone, fireZone2, array) in FuelFeeder.Fires())
		{
			if ((fireplaceInteraction.transform.position - playerTransform.position).magnitude > 10f)
			{
				continue;
			}
			Main.Log.Msg(Describe(fireplaceInteraction, kind, zone) + ((fireZone != null) ? $"  door opener {fireZone}\n" : "") + ((fireZone2 != null) ? $"  funnel {fireZone2}\n" : ""));
			Show(zone, kind);
			if (fireZone != null)
			{
				ShowBox(fireZone, Color.white);
			}
			if (fireZone2 != null && fireZone2.IsCone)
			{
				ShowCone(fireZone2, new Color(1f, 0.5f, 0f));
			}
			if (fireZone2 != null && fireZone2.IsSphere)
			{
				ShowSphere(fireZone2, new Color(1f, 0.5f, 0f));
			}
			if (array != null)
			{
				FireZone[] array2 = array;
				for (int i = 0; i < array2.Length; i++)
				{
					ShowBox(array2[i], new Color(1f, 0.5f, 0f));
				}
			}
			num++;
		}
		HUDMessage.AddMessage($"Fire zones shown: {num} (F6 hides)", 2f, highPriority: false, ignoreOverlayActive: true);
	}

	public static void Clear()
	{
		foreach (GameObject dot in dots)
		{
			if (dot != null)
			{
				UnityEngine.Object.Destroy(dot);
			}
		}
		dots.Clear();
	}

	private static void Show(FireZone zone, FireKind kind)
	{
		Color colour = kind switch
		{
			FireKind.Campfire => Color.yellow, 
			FireKind.Fireplace => Color.cyan, 
			FireKind.Forge => Color.magenta, 
			_ => Color.green, 
		};
		if (zone.IsBox)
		{
			ShowBox(zone, colour);
			return;
		}
		Vector3 center = zone.Center;
		Quaternion quaternion = Quaternion.Euler(0f, zone.Frame.eulerAngles.y, 0f);
		for (int i = -1; i <= 1; i++)
		{
			for (int j = 0; j < 24; j++)
			{
				float f = (float)j * Mathf.PI * 2f / 24f;
				Vector3 vector = new Vector3(Mathf.Cos(f) * zone.Radius, (float)i * zone.HalfHeight, Mathf.Sin(f) * zone.Radius);
				Dot(center + quaternion * vector, colour);
			}
		}
		for (int k = -2; k <= 2; k++)
		{
			Dot(center + Vector3.up * zone.HalfHeight * k / 2f, Color.red);
		}
	}

	private static void ShowSphere(FireZone zone, Color colour)
	{
		Vector3 center = zone.Center;
		float radius = zone.Radius;
		for (int i = 0; i < 36; i++)
		{
			float f = (float)i * Mathf.PI * 2f / 36f;
			float num = Mathf.Cos(f) * radius;
			float num2 = Mathf.Sin(f) * radius;
			Dot(center + new Vector3(num, 0f, num2), colour);
			Dot(center + new Vector3(num, num2, 0f), colour);
			Dot(center + new Vector3(0f, num2, num), colour);
		}
	}

	private static void ShowCone(FireZone zone, Color colour)
	{
		Vector3 center = zone.Center;
		Vector3 vector = zone.Yaw * zone.Axis;
		Vector3 vector2 = zone.Yaw * Vector3.right;
		Vector3 normalized = Vector3.Cross(vector, vector2).normalized;
		float num = Mathf.Tan(zone.Angle * Mathf.Deg2Rad);
		float num2 = Mathf.Tan(zone.SideAngle * Mathf.Deg2Rad);
		for (float num3 = 0.2f; num3 <= zone.Length + 0.001f; num3 += 0.2f)
		{
			for (int i = 0; i < 24; i++)
			{
				float f = (float)i * Mathf.PI * 2f / 24f;
				float num4 = Mathf.Cos(f);
				float num5 = Mathf.Sin(f);
				Vector3 vector3 = vector2 * (num4 * num3 * num2 + Mathf.Sign(num4) * zone.Spread) + normalized * (num5 * num3 * num);
				Dot(center + vector * num3 + vector3, colour);
			}
		}
		for (float num6 = 0f; num6 <= zone.Length + 0.001f; num6 += 0.1f)
		{
			Dot(center + vector * num6, Color.red);
		}
		for (float num7 = 0f - zone.Spread; num7 <= zone.Spread + 0.001f; num7 += 0.08f)
		{
			Dot(center + vector2 * num7, Color.red);
		}
	}

	private static void ShowBox(FireZone zone, Color colour)
	{
		Vector3 center = zone.Center;
		Quaternion yaw = zone.Yaw;
		Vector3 half = zone.Half;
		for (int i = 0; i < 3; i++)
		{
			for (int j = -1; j <= 1; j += 2)
			{
				for (int k = -1; k <= 1; k += 2)
				{
					int num = Mathf.Max(2, Mathf.CeilToInt(i switch
					{
						1 => half.y, 
						0 => half.x, 
						_ => half.z, 
					} * 2f / 0.08f));
					for (int l = 0; l <= num; l++)
					{
						float num2 = -1f + 2f * (float)l / (float)num;
						Dot(center + yaw * i switch
						{
							1 => new Vector3((float)j * half.x, num2 * half.y, (float)k * half.z), 
							0 => new Vector3(num2 * half.x, (float)j * half.y, (float)k * half.z), 
							_ => new Vector3((float)j * half.x, (float)k * half.y, num2 * half.z), 
						}, colour);
					}
				}
			}
		}
	}

	public static void LogHeld()
	{
		if (dots.Count == 0 || Time.time < nextHeldLog)
		{
			return;
		}
		PlayerManager playerManagerComponent = GameManager.GetPlayerManagerComponent();
		GearItem gearItem = ((playerManagerComponent != null && playerManagerComponent.IsInPlacementMode()) ? playerManagerComponent.m_ObjectToPlaceGearItem : null);
		if (gearItem == null)
		{
			return;
		}
		nextHeldLog = Time.time + 0.5f;
		Vector3 point = gearItem.transform.position;
		foreach (Collider componentsInChild in gearItem.GetComponentsInChildren<Collider>(includeInactive: false))
		{
			if (componentsInChild != null && componentsInChild.enabled && !componentsInChild.isTrigger)
			{
				point = componentsInChild.bounds.center;
				break;
			}
		}
		foreach (var (fireplaceInteraction, _, fireZone, fireZone2, fireZone3, array) in FuelFeeder.Fires())
		{
			if ((fireZone2 != null || fireZone3 != null) && !((fireplaceInteraction.transform.position - point).magnitude > 2.5f))
			{
				Main.Log.Msg($"[Calibration] {gearItem.name} at local {FireZone.V(fireZone.Local(point))} of '{fireplaceInteraction.gameObject.name}': in the firebox zone {fireZone.Contains(point)}, in the funnel {fireZone3?.Contains(point) ?? false}, beside {array != null && Array.Exists(array, (FireZone s) => s.Contains(point))}, in the door opener {fireZone2?.Contains(point) ?? false}");
			}
		}
	}

	private static void Dot(Vector3 at, Color colour)
	{
		GameObject gameObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
		gameObject.name = "ImmersiveFire zone dot";
		Collider component = gameObject.GetComponent<Collider>();
		if (component != null)
		{
			UnityEngine.Object.Destroy(component);
		}
		gameObject.transform.position = at;
		gameObject.transform.localScale = Vector3.one * 0.025f;
		Renderer component2 = gameObject.GetComponent<Renderer>();
		if (component2 != null)
		{
			component2.material.color = colour;
		}
		dots.Add(gameObject);
	}

	private static string Describe(FireplaceInteraction fi, FireKind kind, FireZone zone)
	{
		StringBuilder stringBuilder = new StringBuilder();
		Transform transform = fi.transform;
		StringBuilder stringBuilder2 = stringBuilder;
		StringBuilder stringBuilder3 = stringBuilder2;
		StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(30, 5, stringBuilder2);
		handler.AppendLiteral("[Calibration] ");
		handler.AppendFormatted(kind);
		handler.AppendLiteral(" '");
		handler.AppendFormatted(Path(transform));
		handler.AppendLiteral("' (");
		handler.AppendFormatted(fi.GetIl2CppType().Name);
		handler.AppendLiteral(") at ");
		handler.AppendFormatted(FireZone.V(transform.position));
		handler.AppendLiteral(", yaw ");
		handler.AppendFormatted(transform.eulerAngles.y, "0");
		stringBuilder3.AppendLine(ref handler);
		stringBuilder2 = stringBuilder;
		StringBuilder stringBuilder4 = stringBuilder2;
		handler = new StringBuilder.AppendInterpolatedStringHandler(7, 1, stringBuilder2);
		handler.AppendLiteral("  zone ");
		handler.AppendFormatted(zone);
		stringBuilder4.AppendLine(ref handler);
		Fire fire = fi.Fire;
		if (fire != null)
		{
			stringBuilder2 = stringBuilder;
			StringBuilder stringBuilder5 = stringBuilder2;
			handler = new StringBuilder.AppendInterpolatedStringHandler(60, 6, stringBuilder2);
			handler.AppendLiteral("  Fire on '");
			handler.AppendFormatted(fire.gameObject.name);
			handler.AppendLiteral("' at local ");
			handler.AppendFormatted(FireZone.V(transform.InverseTransformPoint(fire.transform.position)));
			handler.AppendLiteral(", ");
			handler.AppendLiteral("state ");
			handler.AppendFormatted(fire.GetFireState());
			handler.AppendLiteral(", burning ");
			handler.AppendFormatted(fire.m_ElapsedOnTODSeconds / 60f, "0");
			handler.AppendLiteral(" min (");
			handler.AppendFormatted(fire.GetMinutesBurning(), "0");
			handler.AppendLiteral(" min), ");
			handler.AppendLiteral("left ");
			handler.AppendFormatted(fire.GetRemainingLifeTimeHours(), "0.00");
			handler.AppendLiteral(" h");
			stringBuilder5.AppendLine(ref handler);
		}
		Campfire campfire = fi.TryCast<Campfire>();
		if (campfire != null && campfire.m_RockRingCollider != null)
		{
			stringBuilder2 = stringBuilder;
			StringBuilder stringBuilder6 = stringBuilder2;
			handler = new StringBuilder.AppendInterpolatedStringHandler(24, 2, stringBuilder2);
			handler.AppendLiteral("  stone ring collider ");
			handler.AppendFormatted(campfire.m_RockRingCollider.GetIl2CppType().Name);
			handler.AppendLiteral(": ");
			handler.AppendFormatted(Box(transform, campfire.m_RockRingCollider.bounds));
			stringBuilder6.AppendLine(ref handler);
		}
		if (fi.m_CookingSlots != null)
		{
			foreach (CookingSlot cookingSlot in fi.m_CookingSlots)
			{
				if (cookingSlot != null)
				{
					stringBuilder2 = stringBuilder;
					StringBuilder stringBuilder7 = stringBuilder2;
					handler = new StringBuilder.AppendInterpolatedStringHandler(27, 2, stringBuilder2);
					handler.AppendLiteral("  cooking slot '");
					handler.AppendFormatted(cookingSlot.name);
					handler.AppendLiteral("' at local ");
					handler.AppendFormatted(FireZone.V(transform.InverseTransformPoint(cookingSlot.transform.position)));
					stringBuilder7.AppendLine(ref handler);
				}
			}
		}
		foreach (Collider componentsInChild in fi.GetComponentsInChildren<Collider>(includeInactive: false))
		{
			if (componentsInChild != null && componentsInChild.enabled)
			{
				stringBuilder2 = stringBuilder;
				StringBuilder stringBuilder8 = stringBuilder2;
				handler = new StringBuilder.AppendInterpolatedStringHandler(23, 5, stringBuilder2);
				handler.AppendLiteral("  collider '");
				handler.AppendFormatted(componentsInChild.name);
				handler.AppendLiteral("' ");
				handler.AppendFormatted(componentsInChild.GetIl2CppType().Name);
				handler.AppendFormatted(componentsInChild.isTrigger ? " (trigger)" : "");
				handler.AppendLiteral(" layer ");
				handler.AppendFormatted(LayerMask.LayerToName(componentsInChild.gameObject.layer));
				handler.AppendLiteral(": ");
				handler.AppendFormatted(Box(transform, componentsInChild.bounds));
				stringBuilder8.AppendLine(ref handler);
			}
		}
		foreach (MeshFilter componentsInChild2 in fi.GetComponentsInChildren<MeshFilter>(includeInactive: false))
		{
			if (componentsInChild2 != null && componentsInChild2.sharedMesh != null)
			{
				stringBuilder2 = stringBuilder;
				StringBuilder stringBuilder9 = stringBuilder2;
				handler = new StringBuilder.AppendInterpolatedStringHandler(34, 4, stringBuilder2);
				handler.AppendLiteral("  mesh '");
				handler.AppendFormatted(componentsInChild2.name);
				handler.AppendLiteral("' (");
				handler.AppendFormatted(componentsInChild2.sharedMesh.name);
				handler.AppendLiteral("): local centre ");
				handler.AppendFormatted(FireZone.V(transform.InverseTransformPoint(componentsInChild2.transform.TransformPoint(componentsInChild2.sharedMesh.bounds.center))));
				handler.AppendLiteral(", size ");
				handler.AppendFormatted(FireZone.V(Vector3.Scale(componentsInChild2.sharedMesh.bounds.size, componentsInChild2.transform.lossyScale)));
				stringBuilder9.AppendLine(ref handler);
			}
		}
		return stringBuilder.ToString();
	}

	private static string Box(Transform root, Bounds b)
	{
		return "local centre " + FireZone.V(root.InverseTransformPoint(b.center)) + ", world size " + FireZone.V(b.size);
	}

	private static string Path(Transform t)
	{
		string text = t.name;
		Transform parent = t.parent;
		while (parent != null)
		{
			text = parent.name + "/" + text;
			parent = parent.parent;
		}
		return text;
	}
}
